using System.Data.Common;
using System.Reflection;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Provider.Sqlite.Mapping;
using DubUrl.Provider.DuckDb.Mapping;
using DubUrl.Provider.Odbc.Mapping;
using Microsoft.Data.Sqlite;
using DuckDB.NET.Data;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace DubUrl.Core.Testing;

public class ReadOnlyConnectionTest
{
    private static ISchemeRegistry Registry(Type mapper, DbProviderFactory factory)
    {
        var builder = new ProviderCatalogBuilder();
        var assemblies = new[] { typeof(SqliteMapper).Assembly, typeof(DuckdbMapper).Assembly, typeof(OdbcMapper).Assembly };
        ProviderCatalogBuilder.RegisterMappings(builder,
            AttributeProviderModule.Discover(assemblies).Where(info => info.MapperType == mapper), _ => factory);
        return builder.Build();
    }

    [TestCase("sqlite:///example.db?Mode=ReadWriteCreate", typeof(SqliteMapper), "Mode", "ReadOnly")]
    [TestCase("duckdb:///example.db?access_mode=READ_WRITE", typeof(DuckdbMapper), "access_mode", "READ_ONLY")]
    [TestCase("odbc+duckdb:///example.db?Driver=DuckDB%20Driver&access_mode=READ_WRITE", typeof(DuckdbOdbcMapper), "access_mode", "READ_ONLY")]
    public void NativeReadOnly_OverridesNativeWriteModeAndSurvivesOptionalCalls(string url, Type mapper, string keyword, string value)
    {
        DbProviderFactory factory = mapper == typeof(SqliteMapper) ? SqliteFactory.Instance
            : mapper == typeof(DuckdbMapper) ? DuckDBClientFactory.Instance : System.Data.Odbc.OdbcFactory.Instance;
        var original = new ConnectionUrl(url, Registry(mapper, factory));
        var originalValue = original.Parse();
        var target = original.WithReadOnly().WithOptional(options => options.WithApplicationName("Optional"))
            .WithReadOnly().WithOptional(_ => { });
        var mapped = new DbConnectionStringBuilder { ConnectionString = target.Parse() };
        Assert.That(mapped[keyword], Is.EqualTo(value));
        Assert.That(original.Parse(), Is.EqualTo(originalValue));
        Assert.That(original.Url, Is.EqualTo(target.Url));
        Assert.That(target.SkippedOptions.Single().Option, Is.EqualTo(ConnectionConfigurationOption.ApplicationName));
    }

    [TestCase("sqlite://", typeof(SqliteMapper))]
    [TestCase("duckdb://memory", typeof(DuckdbMapper))]
    [TestCase("duckdb:///md:remote", typeof(DuckdbMapper))]
    public void UnsupportedDatabaseMode_FailsBeforeCreatingConnection(string url, Type mapper)
    {
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnectionStringBuilder()).Returns(() => mapper == typeof(SqliteMapper)
            ? new SqliteConnectionStringBuilder() : new DuckDBConnectionStringBuilder());
        var registry = Registry(mapper, factory.Object);
        Assert.Throws<NotSupportedException>(() => new ConnectionUrl(url, registry).WithReadOnly().Connect());
        factory.Verify(x => x.CreateConnection(), Times.Never);
    }

    [TestCase("mssql://alice:marker@localhost/app?Application%20Intent=ReadOnly")]
    [TestCase("postgres://alice:marker@localhost/app")]
    [TestCase("mysql://alice:marker@localhost/app")]
    [TestCase("oledb+mssql://alice:marker@localhost/app")]
    public void UndeclaredSupport_ThrowsBeforeCreatingConnectionAndNeverLeaksCredentials(string url)
    {
        var factory = new Mock<DbProviderFactory>();
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(new ResolvedConnection("",
            Mock.Of<IDialect>(), Mock.Of<IConnectivity>(), Mock.Of<IParametrizer>(), factory.Object));
        var target = new ConnectionUrl(url, registry.Object).WithReadOnly().WithOptional(options => options.WithApplicationName("Optional"));
        var error = Assert.Throws<NotSupportedException>(() => target.Open());
        Assert.That(error!.Message, Does.Contain("ReadOnly").And.Contain("provider"));
        Assert.That(error.Message, Does.Not.Contain("marker"));
        factory.Verify(x => x.CreateConnection(), Times.Never);
    }

    [Test]
    public void ReadOnly_IsNotExposedByOptionalBuilderOrOptionalSettingEnum()
    {
        Assert.That(typeof(OptionalConnectionOptions).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name), Does.Not.Contain("WithReadOnly"));
        Assert.That(Enum.GetNames<ConnectionConfigurationOption>(), Does.Not.Contain("ReadOnly"));
    }

    [Test]
    public void ReadOnlyCapabilities_AreOnlyDeclaredOnVerifiedRoutes()
    {
        var mappings = AttributeProviderModule.Discover(new AssemblyTypesProbe());
        Assert.That(mappings.Where(info => info.ReadOnlyConnection is not null).Select(info => info.MapperType).Distinct(),
            Is.EquivalentTo(new[] { typeof(SqliteMapper), typeof(DuckdbMapper), typeof(DuckdbOdbcMapper) }));
    }

    [TestCase(false)]
    [TestCase(null)]
    public void EffectiveModeGuard_FailsClosedForWritableOrUnknownMode(object? result)
    {
        var command = new Mock<DbCommand>();
        command.Setup(x => x.ExecuteScalar()).Returns(result);
        var connection = new Mock<DbConnection>();
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        new ReadOnlyConnectionCapability("access_mode", "READ_ONLY", "DataSource", "SELECT readonly").Configure(connection.Object);
        Assert.Throws<InvalidOperationException>(() => connection.Raise(x => x.StateChange += null,
            new System.Data.StateChangeEventArgs(System.Data.ConnectionState.Closed, System.Data.ConnectionState.Open)));
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.Is<bool>(disposing => disposing));
    }

    [TestCase(typeof(OperationCanceledException))]
    [TestCase(typeof(TimeoutException))]
    [TestCase(typeof(NotSupportedException))]
    public void EffectiveModeGuard_DoesNotSwallowProviderFailuresOrCancellation(Type failureType)
    {
        var failure = (Exception)Activator.CreateInstance(failureType)!;
        var command = new Mock<DbCommand>();
        command.Setup(x => x.ExecuteScalar()).Throws(failure);
        var connection = new Mock<DbConnection>();
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        new ReadOnlyConnectionCapability("access_mode", "READ_ONLY", "DataSource", "SELECT readonly").Configure(connection.Object);
        Assert.That(Assert.Throws(failureType, () => connection.Raise(x => x.StateChange += null,
            new System.Data.StateChangeEventArgs(System.Data.ConnectionState.Closed, System.Data.ConnectionState.Open))), Is.SameAs(failure));
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.Is<bool>(disposing => disposing));
    }
}
