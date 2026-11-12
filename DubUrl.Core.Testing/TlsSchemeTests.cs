using System.Data.Common;
using System.Data.Odbc;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Provider.Db2.Mapping;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Provider.Sqlite.Mapping;
using DubUrl.Provider.Trino.Mapping;
using IBM.Data.Db2;
using Microsoft.Data.Sqlite;
using Npgsql;
using NReco.PrestoAdo;
using NUnit.Framework;

namespace DubUrl.Core.Testing;

public class TlsSchemeTests
{
    [Test]
    public void NativeAliasRetainsOptionsAndComposesWithExplicitCa()
    {
        var registry = Registry(typeof(PostgresqlMapper), NpgsqlFactory.Instance);
        const string url = "PGSQL+TLS://db.example/app?SSL%20Mode=VerifyFull&Timeout=9";
        var original = new ConnectionUrl(url, registry);
        var target = original.WithEncryption(ConnectionEncryption.Tls("server-ca.pem"));

        using var connection = target.Connect();
        var settings = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        Assert.Multiple(() =>
        {
            Assert.That(registry.CanHandle("PGSQL+TLS"), Is.True);
            Assert.That(settings.SslMode, Is.EqualTo(SslMode.VerifyFull));
            Assert.That(settings.Timeout, Is.EqualTo(9));
            Assert.That(settings.RootCertificate, Is.EqualTo(Path.GetFullPath("server-ca.pem")));
            Assert.That(target.Url, Is.EqualTo(url));
            Assert.That(new NpgsqlConnectionStringBuilder(original.Parse()).RootCertificate, Is.Null);
        });
    }

    [TestCase("")]
    [TestCase(":8443")]
    public void TrinoHttpsAndHttpPlusTlsMatchTheVerifiedNativeSettings(string port)
    {
        var registry = Registry(typeof(TrinoMapper), PrestoDbFactory.Instance);
        var endpoint = $"://trino.example{port}/catalog/schema";
        var expected = new ConnectionUrl("trino" + endpoint + "?UseSsl=true", registry).Parse();

        Assert.Multiple(() =>
        {
            Assert.That(registry.CanHandle("trino+https"), Is.True);
            Assert.That(new ConnectionUrl("trino+tls" + endpoint, registry).Parse(), Is.EqualTo(expected).IgnoreCase);
            Assert.That(new ConnectionUrl("trino+http+tls" + endpoint, registry).Parse(), Is.EqualTo(expected).IgnoreCase);
            Assert.That(new ConnectionUrl("trino+https" + endpoint, registry).Parse(), Is.EqualTo(expected).IgnoreCase);
            Assert.That(bool.Parse((string)new DbConnectionStringBuilder { ConnectionString = expected }["UseSsl"]), Is.True);
        });
    }

    [TestCase("trino+tls")]
    [TestCase("trino+http+tls")]
    [TestCase("trino+https")]
    public void TrinoRejectsPlaintextOverrideOnlyWhenTlsIsRequested(string scheme)
    {
        var registry = Registry(typeof(TrinoMapper), PrestoDbFactory.Instance);
        const string endpoint = "://trino.example/catalog/schema?UseSsl=false";
        var original = new DbConnectionStringBuilder { ConnectionString = new ConnectionUrl("trino" + endpoint, registry).Parse() };
        Assert.That(bool.Parse((string)original["UseSsl"]), Is.False);
        Assert.Throws<ArgumentException>(() => new ConnectionUrl(scheme + endpoint, registry).ValidateConfiguration());
    }

    [TestCase("odbc+postgres+tls")]
    [TestCase("postgres+odbc+TLS")]
    public void CompositeOdbcSchemeUsesSelectedDriverAndSingleTlsConfiguration(string scheme)
    {
        var registry = Registry(typeof(PostgresqlOdbcMapper), OdbcFactory.Instance);
        var url = scheme + "://db.example/app?Driver=PostgreSQL%20Unicode";
        var uri = ConnectionUri.Parse(url);
        var expected = registry.Resolve(uri).ConnectionString;
        var settings = new OdbcConnectionStringBuilder(expected);
        var explicitCa = new OdbcConnectionStringBuilder(new ConnectionUrl(url, registry)
            .WithEncryption(ConnectionEncryption.Tls("server-ca.pem")).Parse());
        using var source = new ConnectionSourceUrl(url, registry).Create();

        Assert.Multiple(() =>
        {
            Assert.That(uri.Scheme.Canonical, Is.EqualTo(scheme.ToLowerInvariant()));
            Assert.That(settings["SSLmode"], Is.EqualTo("verify-full"));
            Assert.That(settings["Pqopt"].ToString(), Does.Contain("sslrootcert='system'"));
            Assert.That(explicitCa["Pqopt"].ToString(), Does.Contain("server-ca.pem"));
            Assert.That(new DataSourceUrl(url, registry).Parse(), Is.EqualTo(expected));
            Assert.That(source.QueryContext.Parse(), Is.EqualTo(expected));
        });
    }

    [TestCase("Simba Spark ODBC Driver", "CAIssuedCertNamesMismatch")]
    [TestCase("Cloudera ODBC Driver for Apache Hive", "CAIssuedCertNamesMismatch")]
    [TestCase("Databricks ODBC Driver", "AllowHostNameCNMismatch")]
    [TestCase("Simba Trino ODBC Driver", "AllowHostNameCNMismatch")]
    public void NamedOdbcDriverRequiresCertificateAndHostVerification(string driver, string hostnameOption)
    {
        var settings = new OdbcConnectionStringBuilder { Driver = driver };
        var tls = ConnectionEncryption.Tls("server-ca.pem");
        TransportEncryption.Apply(settings, tls, odbc: true);
        Assert.Multiple(() =>
        {
            Assert.That(settings["SSL"], Is.EqualTo("1"));
            Assert.That(settings[hostnameOption], Is.EqualTo("0"));
            Assert.That(settings["TrustedCerts"], Is.EqualTo(Path.GetFullPath("server-ca.pem")));
            settings["SSL"] = "0";
            Assert.Throws<ArgumentException>(() => TransportEncryption.Apply(settings, tls, odbc: true));
            settings["SSL"] = "1";
            settings[hostnameOption] = "1";
            Assert.Throws<ArgumentException>(() => TransportEncryption.Apply(settings, tls, odbc: true));
        });
    }

    [TestCase("spark", "Simba Spark ODBC Driver")]
    [TestCase("hive", "Cloudera ODBC Driver for Apache Hive")]
    public void PlaintextOnlyOdbcHttpSuffixConflictsWithTls(string alias, string driver)
    {
        var catalog = new ProviderCatalogBuilder();
        var mappings = AttributeProviderModule.Discover(new AssemblyTypesProbe())
            .Where(mapping => mapping.ProviderInvariantName == "System.Data.Odbc");
        ProviderCatalogBuilder.RegisterMappings(catalog, mappings, _ => OdbcFactory.Instance);
        var registry = catalog.Build();
        if (!registry.CanHandle($"odbc+{alias}"))
            Assert.Ignore($"This ODBC provider package does not include the {alias} route.");

        var endpoint = $"://db.example/schema/app?Driver={Uri.EscapeDataString(driver)}";
        var plaintext = new ConnectionUrl($"odbc+{alias}+http{endpoint}", registry);
        var verifiedTls = new ConnectionUrl($"odbc+{alias}+https+tls{endpoint}", registry);

        Assert.Multiple(() =>
        {
            Assert.That(new OdbcConnectionStringBuilder(plaintext.Parse())["SSL"], Is.EqualTo("0"));
            Assert.That(new OdbcConnectionStringBuilder(verifiedTls.Parse())["SSL"], Is.EqualTo("1"));
            Assert.Throws<ArgumentException>(() => new ConnectionUrl($"odbc+{alias}+http+tls{endpoint}", registry).Parse());
        });
    }

    [Test]
    public void Db2RejectsTlsWithTheCurrentManagedDriver()
    {
        var registry = Registry(typeof(Db2Mapper), DB2Factory.Instance);
        Assert.That(new DB2ConnectionStringBuilder().ContainsKey("SSLClientHostnameValidation"), Is.False);
        Assert.Throws<NotSupportedException>(() => new ConnectionUrl("db2+tls://db.example:50001/app", registry).Parse());
    }

    [Test]
    public void UnsupportedTlsRouteFailsWithAResolutionDiagnostic()
    {
        var registry = Registry(typeof(SqliteMapper), SqliteFactory.Instance);
        const string url = "sqlite+tls:///test.db";
        Assert.Throws<NotSupportedException>(() => new ConnectionUrl(url, registry).ValidateConfiguration());
        Assert.That(registry.TryResolve(ConnectionUri.Parse(url), out var resolved, out var diagnostic), Is.False);
        Assert.That(resolved, Is.Null);
        Assert.That(diagnostic?.Code, Is.EqualTo(ConnectionResolutionDiagnosticCode.ResolutionFailed));
    }

    [Test]
    public void DuplicateTlsModifierIsInvalid()
        => Assert.That(ConnectionUri.TryParse("postgres+tls+TLS://db.example/app", out _), Is.False);

    private static SchemeRegistry Registry(Type mapper, DbProviderFactory factory)
    {
        var catalog = new ProviderCatalogBuilder();
        var mappings = factory == OdbcFactory.Instance
            ? AttributeProviderModule.Discover(new AssemblyTypesProbe())
            : AttributeProviderModule.Discover(mapper.Assembly);
        ProviderCatalogBuilder.RegisterMappings(catalog, mappings.Where(mapping => mapping.MapperType == mapper), _ => factory);
        return catalog.Build();
    }
}
