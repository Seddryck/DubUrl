using System.Data;
using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using Microsoft.Data.SqlClient;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

public class OperatingSystemIdentityMappingTests
{
    [TestCase("odbc+mssql://db/app?Driver=ODBC%20Driver%2018%20for%20SQL%20Server", "System.Data.Odbc", "Trusted_Connection", "yes")]
    [TestCase("odbc+postgres://alice@db/app?Driver=PostgreSQL%20Unicode&SSLmode=verify-full", "System.Data.Odbc", "Pqopt", "{require_auth=gss,sspi}")]
    [TestCase("odbc+drill://db/dfs?Driver=MapR%20Drill%20ODBC%20Driver&KrbServiceHost=db.example", "System.Data.Odbc", "AuthenticationType", "Kerberos")]
    [TestCase("odbc+trino://db/catalog/schema?Driver=Simba%20Trino%20ODBC%20Driver", "System.Data.Odbc", "UseExistingKrbCreds", "1")]
    [TestCase("db2://db/app?TargetPrincipal=db2%2Fdb.example%40REALM", "IBM.Data.Db2", "Authentication", "KERBEROS")]
    [TestCase("oracle://db/app", "Oracle.ManagedDataAccess", "User ID", "/")]
    [TestCase("teradata://db/app", "Teradata.Client", "Authentication Mechanism", "SPNEGO")]
    public async Task MappingsUseTheNormalProviderPipeline(string url, string provider, string key, string expected)
    {
        using var probe = CreateNative(provider);
        DbProviderFactories.RegisterFactory(provider, DbProviderFactories.GetFactory(probe) ?? throw new InvalidOperationException());
        var registry = new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
        var original = new ConnectionUrl(url, registry);
        var target = original.WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var sync = (DbConnection)target.Connect();
        await using var asyncConnection = await target.ConnectAsync();
        var settings = new DbConnectionStringBuilder(provider == "System.Data.Odbc") { ConnectionString = sync.ConnectionString };
        Assert.That(settings[key].ToString(), Is.EqualTo(expected).IgnoreCase);
        Assert.That(settings.ContainsKey("Password") || settings.ContainsKey("Pwd"), Is.False);
        Assert.That(asyncConnection.ConnectionString, Is.EqualTo(sync.ConnectionString));
        Assert.That(original.Url, Is.EqualTo(url));
        if (provider == "IBM.Data.Db2") Assert.That(settings["TargetPrincipal"], Is.EqualTo("db2/db.example@REALM"));
    }

    [TestCase("postgres"), TestCase("pg"), TestCase("pgx"), TestCase("timescale"), TestCase("cockroach"), TestCase("cr"), TestCase("cdb")]
    public async Task NetworkPostgresRequiresGssOrSspiAndPreservesTlsAndRole(string scheme)
    {
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        var registry = new ProviderCatalogBuilder().AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)).Build();
        var original = new ConnectionUrl(scheme + "://alice@db.example/app?SSL%20Mode=VerifyFull&Root%20Certificate=ca.pem", registry);
        var before = original.Parse();
        var target = original.WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var sync = (NpgsqlConnection)target.Connect();
        await using var asyncConnection = (NpgsqlConnection)await target.ConnectAsync();
        var settings = new NpgsqlConnectionStringBuilder(sync.ConnectionString);
        Assert.That(settings.RequireAuth, Is.EqualTo("GSS, SSPI"));
        Assert.That(settings.SslMode, Is.EqualTo(SslMode.VerifyFull));
        Assert.That(settings.RootCertificate, Is.EqualTo("ca.pem"));
        Assert.That(settings.Username, Is.EqualTo("alice"));
        Assert.That(settings.Password, Is.Null.Or.Empty);
        Assert.That(sync.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(asyncConnection.ConnectionString, Is.EqualTo(sync.ConnectionString));
        Assert.That(original.Parse(), Is.EqualTo(before));
        Assert.That(target.Parse(), Is.EqualTo(before));
    }

    [TestCase("System.Data.Odbc", "odbc+mssql", "Driver={ODBC Driver 18 for SQL Server};Server=db;Encrypt=yes", "Trusted_Connection", "yes")]
    [TestCase("System.Data.Odbc", "mssql+odbc", "Driver={ODBC Driver 17 for SQL Server};Server=db", "Trusted_Connection", "yes")]
    [TestCase("System.Data.Odbc", "postgres+odbc", "Driver={PostgreSQL Unicode(x64)};Server=db;Uid=alice;SSLmode=verify-full", "Pqopt", "{require_auth=gss,sspi}")]
    [TestCase("System.Data.Odbc", "timescale+odbc", "Driver={PostgreSQL Unicode};Server=db", "Pqopt", "{require_auth=gss,sspi}")]
    [TestCase("System.Data.Odbc", "trino+odbc", "Driver={Simba Trino ODBC Driver};Host=db;KrbServiceName=HTTP", "AuthenticationType", "Kerberos Authentication")]
    [TestCase("System.Data.Odbc", "drill+odbc", "Driver={MapR Drill ODBC Driver};Host=db;KrbServiceHost=db.example", "AuthenticationType", "Kerberos")]
    [TestCase("IBM.Data.Db2", "db2", "Server=db;Database=app;TargetPrincipal=db2/db.example@REALM", "Authentication", "KERBEROS")]
    [TestCase("Oracle.ManagedDataAccess", "oracle", "Data Source=db/app", "User Id", "/")]
    [TestCase("Teradata.Client", "teradata", "Data Source=db", "Authentication Mechanism", "SPNEGO")]
    public async Task MappingsConfigureClosedNativeConnections(string provider, string scheme, string input, string key, string expected)
    {
        using var connection = CreateNative(provider);
        connection.ConnectionString = input;
        var strategy = ConnectionAuthentication.OperatingSystemIdentity();
        var context = new AuthenticationContext(provider, scheme, "db", null, "app", null);
        Assert.That(strategy.Resolve(context), Is.Null);
        Assert.That(await strategy.ResolveAsync(context), Is.Null);
        strategy.Configure(connection, context);
        var first = connection.ConnectionString;
        await strategy.ConfigureAsync(connection, context);
        Assert.That(new DbConnectionStringBuilder(provider == "System.Data.Odbc") { ConnectionString = connection.ConnectionString }
            .EquivalentTo(new DbConnectionStringBuilder(provider == "System.Data.Odbc") { ConnectionString = first }), Is.True);
        var settings = new DbConnectionStringBuilder(provider == "System.Data.Odbc") { ConnectionString = first };
        Assert.That(settings[key].ToString(), Is.EqualTo(expected).IgnoreCase);
        Assert.That(settings.ContainsKey("Password") || settings.ContainsKey("Pwd"), Is.False);
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        if (provider == "System.Data.Odbc") Assert.That(first, Does.Not.Contain("\""), "ODBC values require ODBC brace rules, not ADO.NET quoting.");
        if (provider == "Npgsql" || scheme.StartsWith("postgres", StringComparison.Ordinal))
        {
            Assert.That(settings["Uid"], Is.EqualTo("alice"));
            Assert.That(settings["SSLmode"], Is.EqualTo("verify-full"));
        }
        if (scheme.StartsWith("trino", StringComparison.Ordinal))
        {
            Assert.That(settings["UseExistingKrbCreds"].ToString(), Is.EqualTo("1"));
            Assert.That(settings["DelegateKrbCreds"].ToString(), Is.EqualTo("0"));
            Assert.That(settings["SSL"].ToString(), Is.EqualTo("1"));
        }
    }

    [TestCase("System.Data.OleDb", "mssql+oledb", "Provider=MSOLEDBSQL19;Data Source=db", "Integrated Security", "SSPI")]
    [TestCase("System.Data.OleDb", "oracle+oledb", "Provider=OraOLEDB.Oracle;Data Source=db", "OSAuthent", "1")]
    [TestCase("FirebirdSql.Data.FirebirdClient", "firebird", "DataSource=db;Database=app", "Integrated Security", "True")]
    [Platform("Win")]
    public Task WindowsMappingsConfigureClosedNativeConnections(string provider, string scheme, string input, string key, string expected)
        => MappingsConfigureClosedNativeConnections(provider, scheme, input, key, expected);

    [TestCase("System.Data.Odbc", "mssql+odbc", "Driver={FreeTDS};Server=db", "UnsupportedAuthentication")]
    [TestCase("System.Data.Odbc", "postgres+odbc", "Driver={unverified};Server=db", "UnsupportedAuthentication")]
    [TestCase("System.Data.Odbc", "mssql+odbc", "DSN=test;Driver={ODBC Driver 18 for SQL Server}", "UnsupportedAuthentication")]
    [TestCase("System.Data.Odbc", "drill+odbc", "Driver={MapR Drill ODBC Driver};Host=db", "MissingCredentials")]
    [TestCase("IBM.Data.Db2", "db2", "Server=db;Database=app", "MissingCredentials")]
    public void MissingConfigurationOrUnknownDriverRejectsSafely(string provider, string scheme, string input, string code)
    {
        using var connection = CreateNative(provider);
        connection.ConnectionString = input;
        Assert.That(() => ConnectionAuthentication.OperatingSystemIdentity().Configure(connection, new(provider, scheme, "db", null, "app", null)),
            Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo(code));
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [TestCase("MySqlConnector", "mysql"), TestCase("MySqlConnector", "maria")]
    [TestCase("MySql.Data.MySqlClient", "mysql"), TestCase("SingleStoreConnector", "singlestore")]
    [TestCase("NReco.PrestoAdo", "trino"), TestCase("Npgsql", "questdb")]
    public async Task UnverifiedOrUnsupportedProviderNeverFallsBack(string provider, string scheme)
    {
        var strategy = ConnectionAuthentication.OperatingSystemIdentity();
        var context = new AuthenticationContext(provider, scheme, "db", null, "app", null);
        Assert.That(() => strategy.Resolve(context), Throws.TypeOf<ConnectionAuthenticationException>()
            .With.Property("Code").EqualTo("UnsupportedAuthentication"));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await strategy.ResolveAsync(context));
        Assert.That(error!.Code, Is.EqualTo("UnsupportedAuthentication"));
    }

    [TestCase("Password"), TestCase("Password2"), TestCase("PWD3"), TestCase("Authentication")]
    [TestCase("Pqopt"), TestCase("D5"), TestCase("Extended Properties"), TestCase("AuthenticationString")]
    [TestCase("OSAuthent"), TestCase("KerberosUsername"), TestCase("KrbKeytab"), TestCase("UseExistingKrbCreds")]
    [TestCase("OAuthClientId"), TestCase("AccessToken"), TestCase("Passfile"), TestCase("DefaultAuthenticationPlugin")]
    [TestCase("ExtraCredentials"), TestCase("KrbKeytabFilePath"), TestCase("KerberosPrincipal")]
    public void ConflictingOptionsRejectWithoutEchoingValues(string key)
    {
        var target = new ConnectionUrl("postgres://alice@db/app?" + Uri.EscapeDataString(key) + "=sensitive-sentinel")
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        var error = Assert.Throws<ConnectionAuthenticationException>(target.ValidateAuthentication);
        Assert.That(error!.Code, Is.EqualTo("CredentialConflict"));
        Assert.That(error.ToString(), Does.Not.Contain("sensitive-sentinel"));
    }

    private static DbConnection CreateNative(string provider) => provider switch
    {
        "System.Data.Odbc" => new System.Data.Odbc.OdbcConnection(),
        "System.Data.OleDb" when OperatingSystem.IsWindows() => new System.Data.OleDb.OleDbConnection(),
        "IBM.Data.Db2" => new IBM.Data.Db2.DB2Connection(),
        "Oracle.ManagedDataAccess" => new Oracle.ManagedDataAccess.Client.OracleConnection(),
        "Teradata.Client" => new Teradata.Client.Provider.TdConnection(),
        "FirebirdSql.Data.FirebirdClient" => new FirebirdSql.Data.FirebirdClient.FbConnection(),
        _ => throw new ArgumentException(nameof(provider))
    };
}

