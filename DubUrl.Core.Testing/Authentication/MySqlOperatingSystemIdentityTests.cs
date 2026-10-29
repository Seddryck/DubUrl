using System.Data;
using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Mapping;
using DubUrl.Provider.MySql.Mapping;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Authentication;

public class MySqlOperatingSystemIdentityTests
{
    [TestCase("maria"), TestCase("mariadb")]
    [TestCase("sg"), TestCase("sgs"), TestCase("single"), TestCase("singlestore"), TestCase("singlestoredb")]
    public async Task NativeGssapiKeepsDatabaseRoleSpnAndTlsWithoutInjectingPasswords(string scheme)
    {
        var provider = scheme is "maria" or "mariadb" ? "MySqlConnector" : "SingleStoreConnector";
        var registry = Registry(provider);
        var url = scheme + "://mapped_role@db.example/app?ServerSPN=mariadb%2Fdb.example&SSL%20Mode=VerifyFull&Pooling=false";
        var original = new ConnectionUrl(url, registry);
        var originalSettings = original.Parse();
        var target = original.WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var sync = (DbConnection)target.Connect();
        await using var asyncConnection = await target.ConnectAsync();
        Assert.That(asyncConnection.ConnectionString, Is.EqualTo(sync.ConnectionString));
        var settings = new DbConnectionStringBuilder { ConnectionString = sync.ConnectionString };
        Assert.That(settings["User ID"], Is.EqualTo("mapped_role"));
        var serverSpn = provider == "MySqlConnector" ? new MySqlConnector.MySqlConnectionStringBuilder(sync.ConnectionString).ServerSPN
            : new SingleStoreConnector.SingleStoreConnectionStringBuilder(sync.ConnectionString).ServerSPN;
        Assert.That(serverSpn, Is.EqualTo("mariadb/db.example"));
        Assert.That(settings["SSL Mode"], Is.EqualTo("VerifyFull"));
        Assert.That(settings["Pooling"].ToString(), Is.EqualTo("False").IgnoreCase);
        AssertNoPassword(settings);
        Assert.That(sync.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(original.Parse(), Is.EqualTo(originalSettings));
        Assert.That(target.Parse(), Is.EqualTo(originalSettings));
        Assert.That(original.Url, Is.EqualTo(url));
    }

    [TestCase("mysql", false), TestCase("my", true)]
    public async Task ConnectorNetExplicitlySelectedUsesKerberosPlugin(string scheme, bool withRole)
    {
        if (OperatingSystem.IsMacOS()) Assert.Ignore("Connector.NET does not support Kerberos on macOS.");
        var registry = Registry("MySql.Data");
        var url = scheme + "://" + (withRole ? "mapped_role@" : "") + "db.example/app?SSL%20Mode=VerifyFull";
        var original = new ConnectionUrl(url, registry);
        var target = original.WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var sync = (DbConnection)target.Connect();
        await using var asyncConnection = await target.ConnectAsync();
        Assert.That(sync, Is.TypeOf<MySql.Data.MySqlClient.MySqlConnection>());
        var settings = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(sync.ConnectionString);
        Assert.That(settings.DefaultAuthenticationPlugin, Is.EqualTo("authentication_kerberos_client"));
        Assert.That(settings.UserID, Is.EqualTo(withRole ? "mapped_role" : ""));
        Assert.That(settings.SslMode.ToString(), Is.EqualTo("VerifyFull"));
        Assert.That(settings.IntegratedSecurity, Is.False);
        if (OperatingSystem.IsWindows()) Assert.That(settings.KerberosAuthMode.ToString(), Is.EqualTo("SSPI"));
        AssertNoPassword(new() { ConnectionString = sync.ConnectionString });
        Assert.That(asyncConnection.ConnectionString, Is.EqualTo(sync.ConnectionString));
        Assert.That(original.Url, Is.EqualTo(url));
    }

    [TestCase("maria", "MariaDB ODBC 3.1 Driver"), TestCase("mariadb", "MariaDB ODBC 3.2 Driver")]
    [TestCase("mysql", "MySQL ODBC 8.0 Unicode Driver"), TestCase("my", "MySQL ODBC 9.4 ANSI Driver")]
    public async Task OdbcUsesSelectedVendorAndPreservesDeploymentOptions(string scheme, string driver)
    {
        var mysql = scheme is "mysql" or "my";
        if (mysql && OperatingSystem.IsMacOS()) Assert.Ignore("MySQL Kerberos route is limited to documented Windows/Linux clients.");
        var url = "odbc+" + scheme + "://mapped_role@db.example/app?Driver=" + Uri.EscapeDataString(driver)
            + "&PLUGIN_DIR=%2Fdeployment%2Fplugins&SSLMODE=VERIFY_IDENTITY";
        var target = new ConnectionUrl(url, Registry("System.Data.Odbc")).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        using var sync = (DbConnection)target.Connect();
        await using var asyncConnection = await target.ConnectAsync();
        var settings = new System.Data.Odbc.OdbcConnectionStringBuilder(sync.ConnectionString);
        Assert.That(settings.Driver.Trim('{', '}'), Is.EqualTo(driver));
        Assert.That(settings["Uid"], Is.EqualTo("mapped_role"));
        Assert.That(settings["PLUGIN_DIR"], Is.EqualTo("/deployment/plugins"));
        Assert.That(settings["SSLMODE"], Is.EqualTo("VERIFY_IDENTITY"));
        if (mysql)
        {
            Assert.That(settings["DEFAULT_AUTH"], Is.EqualTo("authentication_kerberos_client"));
            Assert.That(settings["authentication-kerberos-mode"], Is.EqualTo(OperatingSystem.IsWindows() ? "SSPI" : "GSSAPI"));
        }
        else Assert.That(settings.ContainsKey("DEFAULT_AUTH"), Is.False, "MariaDB selects its server-account plugin; do not invent a driver option.");
        AssertNoPassword(settings);
        Assert.That(asyncConnection.ConnectionString, Is.EqualTo(sync.ConnectionString));
        Assert.That(target.Url, Is.EqualTo(url));
    }

    [TestCase("KerberosAuthMode=GSSAPI"), TestCase("Kerberos%20Auth%20Mode=GSSAPI")]
    [TestCase("KerberosAuthMode=AUTO")]
    [TestCase("DefaultAuthenticationPlugin=authentication_kerberos_client")]
    public async Task ConnectorNetAcceptsCompatibleExplicitOptions(string option)
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("This case exercises the Windows-only Connector.NET KerberosAuthMode option.");
        var target = new ConnectionUrl("mysql://db.example/app?" + option, Registry("MySql.Data"))
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        await using var connection = await target.ConnectAsync();
        var settings = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(connection.ConnectionString);
        Assert.That(settings.DefaultAuthenticationPlugin, Is.EqualTo("authentication_kerberos_client"));
        var expected = option.EndsWith("=AUTO", StringComparison.Ordinal) ? "AUTO" : option.StartsWith("Kerberos", StringComparison.Ordinal) ? "GSSAPI" : "SSPI";
        Assert.That(settings.KerberosAuthMode.ToString(), Is.EqualTo(expected));
    }

    [TestCase("maria", "MySqlConnector"), TestCase("singlestore", "SingleStoreConnector")]
    public async Task RequiredDatabaseRoleRejectsBeforeNativeCreation(string scheme, string provider)
    {
        var target = new ConnectionUrl(scheme + "://db.example/app", Registry(provider)).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("MissingCredentials"));
        var error = await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await target.ConnectAsync());
        Assert.That(error!.Code, Is.EqualTo("MissingCredentials"));
    }

    [TestCase("MySQL ODBC 5.3 Unicode Driver", "mysql")]
    [TestCase("MySQL ODBC 8.0.31 Unicode Driver", "mysql")]
    [TestCase("MariaDB ODBC 2.0 Driver", "maria")]
    [TestCase("MariaDB ODBC 3.2 Driver", "mysql")]
    [TestCase("MySQL ODBC 9.4 Unicode Driver", "maria")]
    [TestCase("Other ODBC 9.4 Driver", "mysql")]
    public void UnsupportedDriverDoesNotGetSubstituted(string driver, string scheme)
    {
        var url = "odbc+" + scheme + "://mapped_role@db.example/app?Driver=" + Uri.EscapeDataString(driver);
        var target = new ConnectionUrl(url, Registry("System.Data.Odbc")).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        Assert.That(() => target.Connect(), Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("UnsupportedAuthentication"));
    }

    [TestCase("Password"), TestCase("PWD2"), TestCase("Password3"), TestCase("AccessToken")]
    [TestCase("DefaultAuthenticationPlugin"), TestCase("DEFAULT_AUTH"), TestCase("KerberosUsername")]
    [TestCase("OPENID_TOKEN_FILE"), TestCase("OCIConfigFile"), TestCase("ENABLE_CLEARTEXT_PLUGIN")]
    public void CompetingAuthenticationRejectsBeforeMapping(string key)
    {
        var target = new ConnectionUrl("mysql://db.example/app?" + key + "=sensitive-sentinel", Registry("MySql.Data"))
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        var error = Assert.Throws<ConnectionAuthenticationException>(target.ValidateAuthentication);
        Assert.That(error!.Code, Is.EqualTo("CredentialConflict"));
        Assert.That(error.ToString(), Does.Not.Contain("sensitive-sentinel"));
    }

    [TestCase("maria://mapped_role:@db.example/app", "MySqlConnector")]
    [TestCase("singlestore://mapped_role:@db.example/app", "SingleStoreConnector")]
    [TestCase("mysql://mapped_role:@db.example/app", "MySql.Data")]
    public void ExplicitEmptyPasswordIsStillAConflict(string url, string provider)
        => Assert.That(() => new ConnectionUrl(url, Registry(provider)).WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity()).Connect(),
            Throws.TypeOf<ConnectionAuthenticationException>().With.Property("Code").EqualTo("CredentialConflict"));

    [TestCase("maria", "MySqlConnector"), TestCase("singlestore", "SingleStoreConnector")]
    public async Task OmittedSpnUsesTheServersChallengeAndPreCancellationIsHonored(string scheme, string provider)
    {
        var target = new ConnectionUrl(scheme + "://mapped_role@db.example/app", Registry(provider))
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        await using var connection = await target.ConnectAsync();
        var settings = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        Assert.That(settings.Keys.Cast<string>().Any(key => key.Replace(" ", "").Equals("ServerSPN", StringComparison.OrdinalIgnoreCase)), Is.False);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(), async () => await target.ConnectAsync(cancelled.Token));
    }

    [TestCase("KerberosAuthMode=PASSWORD")]
    [TestCase("KerberosAuthMode=LDAP")]
    [TestCase("KerberosAuthMode=SSPI&Kerberos%20Auth%20Mode=GSSAPI")]
    public void InvalidOrAmbiguousModeFailsSafely(string option)
    {
        var target = new ConnectionUrl("mysql://mapped_role@db.example/app?" + option, Registry("MySql.Data"))
            .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        var error = Assert.Throws<ConnectionAuthenticationException>(() => target.Connect());
        Assert.That(error!.Code, Is.EqualTo("CredentialConflict"));
        Assert.That(error.InnerException, Is.Null);
    }

    private static void AssertNoPassword(DbConnectionStringBuilder settings)
    {
        foreach (var key in new[] { "Password", "Pwd", "Password1", "Password2", "Password3", "PWD1", "PWD2", "PWD3" })
            Assert.That(settings.ContainsKey(key), Is.False, key);
    }

    internal static ISchemeRegistry Registry(string provider)
    {
        var catalog = new ProviderCatalogBuilder();
        if (provider == "MySql.Data")
        {
            var dialects = new DialectRegistryBuilder();
            dialects.AddDialect<MySqlDialect>(["mysql", "my"]);
            // Connector.NET is an alternative mapper: select it explicitly rather than changing
            // the default mysql:// MySqlConnector registration or substituting a driver at runtime.
            catalog.AddProvider(new("MySQL Connector.NET", ["mysql", "my"], provider,
                () => MySql.Data.MySqlClient.MySqlClientFactory.Instance, dialects.Build().Get<MySqlDialect>(), () => new NamedParametrizer(),
                context => new MySqlDataMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer)));
        }
        else
        {
            DbProviderFactories.RegisterFactory(provider, provider switch
            {
                "MySqlConnector" => MySqlConnector.MySqlConnectorFactory.Instance,
                "SingleStoreConnector" => SingleStoreConnector.SingleStoreConnectorFactory.Instance,
                "System.Data.Odbc" => System.Data.Odbc.OdbcFactory.Instance,
                _ => throw new ArgumentException(nameof(provider))
            });
            catalog.AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies));
        }
        return catalog.Build();
    }
}
