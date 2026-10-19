using DubUrl.Provider.Snowflake.Mapping;
using DubUrl.Provider.SqlServer.Mapping;
using DubUrl.Conversion;
using DubUrl.Mapping;
using DubUrl.Parsing;
using Microsoft.Data.SqlClient;
using Moq;
using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.Testing.Conversion;

public class ConnectionStringConverterTest
{
    private static SchemeRegistry Registry()
        => ConversionTestSupport.Registry("mssql", SqlClientFactory.Instance,
            csb => new MsSqlServerMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));

    [TestCase("", "Data Source=localhost")]
    [TestCase("mssql", "")]
    [TestCase("mssql", "Password=\"unfinished")]
    [TestCase("mssql", "UnknownSetting=example")]
    public void TryConvert_InvalidInput_ReturnsSanitizedDiagnostic(string scheme, string input)
    {
        var converter = new ConnectionStringConverter(Registry());
        Assert.That(converter.TryConvert(scheme, input, out var uri, out var diagnostic), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(uri, Is.Null);
            Assert.That(diagnostic!.Code, Is.EqualTo(ConnectionStringConversionDiagnosticCode.InvalidInput));
            Assert.That(diagnostic.Message, Does.Not.Contain("unfinished").And.Not.Contain("UnknownSetting"));
        });
    }

    [Test]
    public void TryConvert_UnknownScheme_ReturnsSchemeDiagnostic()
    {
        var converter = new ConnectionStringConverter(Registry());
        Assert.That(converter.TryConvert("unknown", "Password=example", out _, out var diagnostic), Is.False);
        Assert.That(diagnostic!.Code, Is.EqualTo(ConnectionStringConversionDiagnosticCode.SchemeNotFound));
    }

    [Test]
    public void TryConvert_CustomForwardOnlyMapper_RemainsSupportedForForwardUse()
    {
        var mapper = Mock.Of<IMapper>();
        var registry = new SchemeRegistry(new() { ["custom"] = () => mapper });
        var converter = new ConnectionStringConverter(registry);
        Assert.That(converter.TryConvert("custom", "Password=example", out _, out var diagnostic), Is.False);
        Assert.That(diagnostic!.Code, Is.EqualTo(ConnectionStringConversionDiagnosticCode.ReverseMappingNotSupported));
    }

    [Test]
    public void TryConvert_MissingBuilder_ReturnsProviderDiagnostic()
    {
        var factory = Mock.Of<DbProviderFactory>();
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(r => r.GetProviderFactory(It.IsAny<SchemeKey>())).Returns(factory);
        var reverse = new ReverseMapper();
        registry.Setup(r => r.GetMapper(It.IsAny<SchemeKey>())).Returns(reverse);
        var converter = new ConnectionStringConverter(registry.Object);
        Assert.That(converter.TryConvert("custom", "Password=example", out _, out var diagnostic), Is.False);
        Assert.That(diagnostic!.Code, Is.EqualTo(ConnectionStringConversionDiagnosticCode.ProviderNotFound));
    }

    [Test]
    public void Convert_ProviderFailure_DoesNotExposeInputOrInnerException()
    {
        var exception = Assert.Throws<ConnectionStringConversionException>(
            () => new ConnectionStringConverter(Registry()).Convert("mssql", "Password=\"private-example"));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.ToString(), Does.Not.Contain("private-example"));
            Assert.That(exception.InnerException, Is.Null);
        });
    }

    [Test]
    public void Convert_SqlServer_ExtractsComponentsAndEncodesValues()
    {
        var uri = new ConnectionStringConverter(Registry()).Convert("mssql",
            "Server=localhost\\instance,1435;Database=\"db /é\";UID=example;PWD=\"p;a@ss+%&\";Application Name=example");
        Assert.Multiple(() =>
        {
            Assert.That(uri.Host, Is.EqualTo("localhost"));
            Assert.That(uri.Port, Is.EqualTo(1435));
            Assert.That(uri.Segments, Is.EqualTo(new[] { "instance", "db /é" }));
            Assert.That(uri.Username, Is.EqualTo("example"));
            Assert.That(uri.Password, Is.EqualTo("p;a@ss+%&"));
            Assert.That(uri.Options["Application Name"], Is.EqualTo("example"));
            Assert.That(uri.ToString(false), Does.Contain("%2B%25%26"));
        });
    }

    [Test]
    public void Convert_SqlServer_AbsentAuthenticationFlag_PreservesProviderDefault()
    {
        var uri = new ConnectionStringConverter(Registry()).Convert("mssql",
            "Server=localhost;Database=db");
        Assert.That(bool.Parse(uri.Options["Integrated Security"]), Is.False);
    }

    [Test]
    public void Convert_ExplicitConnectorModule_RequiresNoGlobalFactoryRegistration()
    {
        var registry = new ProviderCatalogBuilder()
            .AddModule(new DubUrl.Connector.PostgreSql.Npgsql.PostgreSqlNpgsqlProviderModule()).Build();
        var uri = new ConnectionStringConverter(registry).Convert("pg", "Host=localhost;Database=db;Username=example");
        Assert.Multiple(() =>
        {
            Assert.That(uri.Scheme, Is.EqualTo(SchemeKey.Parse("pg")));
            Assert.That(uri.Host, Is.EqualTo("localhost"));
            Assert.That(uri.Segments, Is.EqualTo(new[] { "db" }));
        });
    }

    [Test]
    public void TryConvert_OptionIgnoredByForwardMapper_RejectsLossyResult()
    {
        var registry = ConversionTestSupport.Registry("snowflake", global::Snowflake.Data.Client.SnowflakeDbFactory.Instance,
            csb => new SnowflakeMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
        var converter = new ConnectionStringConverter(registry);
        Assert.That(converter.TryConvert("snowflake",
            "ACCOUNT=example;DB=db;SCHEMA=public;USER=example;PASSWORD=example;WAREHOUSE=compute",
            out _, out var diagnostic), Is.False);
        Assert.That(diagnostic!.Code, Is.EqualTo(ConnectionStringConversionDiagnosticCode.SettingsNotRepresentable));
    }

    private sealed class ReverseMapper : IMapper, IReverseConnectionMapper
    {
        public bool SupportsReverseConversion => true;
        public ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source) => new() { Scheme = scheme };
        public IReadOnlyDictionary<string, object> Rewrite(ConnectionUri uri) => throw new NotImplementedException();
        public string GetConnectionString() => throw new NotImplementedException();
        public DubUrl.Querying.Dialects.IDialect GetDialect() => throw new NotImplementedException();
        public IConnectivity GetConnectivity() => throw new NotImplementedException();
        public string GetProviderName() => "custom";
        public DubUrl.Querying.Parametrizing.IParametrizer GetParametrizer() => throw new NotImplementedException();
    }
}
