using System.Data.Common;
using DubUrl.Application;
using DubUrl.Authentication;
using DubUrl.Cli;
using DubUrl.Mapping;
using DubUrl.Provider.MySql.Mapping;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class MySqlOperatingSystemIdentityCliTests
{
    [TestCase("maria", "human"), TestCase("maria", "json")]
    [TestCase("mysql", "human"), TestCase("mysql", "json")]
    [TestCase("singlestore", "human"), TestCase("singlestore", "json")]
    public async Task CliUsesTheSelectedNativeMappingWithoutAcquiringSecrets(string scheme, string format)
    {
        if (scheme == "mysql" && OperatingSystem.IsMacOS()) Assert.Ignore("Connector.NET Kerberos is unsupported on macOS.");
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnector.MySqlConnectorFactory.Instance);
        DbProviderFactories.RegisterFactory("SingleStoreConnector", SingleStoreConnector.SingleStoreConnectorFactory.Instance);
        var console = new RecordingConsole();
        var secrets = new ForbiddenSecrets();
        IDubUrlProviderModule module = scheme == "mysql" ? new ConnectorNetModule()
            : new AttributeProviderModule(typeof(MariaDbConnectorMapper).Assembly);
        await using var services = CliComposition.CreateDefault(console, providerModules: [module], passwordPrompt: secrets, credentialStore: secrets);
        var check = new MappingCheck(scheme);
        var exit = await CliHost.InvokeAsync(["connection", "check", scheme + "://mapped_role@db.example/app", "--os-identity", "--engine", "--output-format", format],
            new CheckServices(services, check));
        Assert.That(exit, Is.Zero, console.Errors.ToString());
        Assert.That(check.Calls, Is.EqualTo(1));
        Assert.That(secrets.Calls, Is.Zero);
        if (format == "json")
        {
            using var result = System.Text.Json.JsonDocument.Parse(console.Output.ToString());
            Assert.That(result.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }
        else Assert.That(console.Output.ToString(), Does.Contain("succeeded"));
    }

    private sealed class MappingCheck(string scheme) : IConnectionCheckService
    {
        public int Calls { get; private set; }
        public async Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default)
        {
            Calls++;
            await using var native = await target.ConnectAsync(cancellationToken);
            var settings = new DbConnectionStringBuilder { ConnectionString = native.ConnectionString };
            Assert.That(settings.ContainsKey("Password"), Is.False);
            Assert.That(settings["User ID"], Is.EqualTo("mapped_role"));
            if (scheme == "mysql")
            {
                Assert.That(native, Is.TypeOf<MySql.Data.MySqlClient.MySqlConnection>());
                Assert.That(new MySql.Data.MySqlClient.MySqlConnectionStringBuilder(native.ConnectionString).DefaultAuthenticationPlugin,
                    Is.EqualTo("authentication_kerberos_client"));
            }
            else Assert.That(native.GetType().FullName, Is.EqualTo(scheme == "maria" ? "MySqlConnector.MySqlConnection" : "SingleStoreConnector.SingleStoreConnection"));
            return new(true, "engine", scheme, 0, []); // Mapping proof only; no database opening is simulated.
        }
    }
    private sealed class CheckServices(IServiceProvider inner, IConnectionCheckService check) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(IConnectionCheckService) ? check : inner.GetService(type);
    }
    private sealed class ConnectorNetModule : IDubUrlProviderModule
    {
        public void Register(IProviderCatalogBuilder catalog)
        {
            var dialects = new DialectRegistryBuilder();
            dialects.AddDialect<MySqlDialect>(["mysql", "my"]);
            catalog.AddProvider(new("MySQL Connector.NET", ["mysql", "my"], "MySql.Data",
                () => MySql.Data.MySqlClient.MySqlClientFactory.Instance, dialects.Build().Get<MySqlDialect>(), () => new NamedParametrizer(),
                context => new MySqlDataMapper(context.ConnectionStringBuilder, context.Dialect, context.Parametrizer)));
        }
    }
    private sealed class ForbiddenSecrets : IHiddenPasswordPrompt, IOsCredentialStore
    {
        public int Calls { get; private set; }
        public ValueTask<string> ReadAsync(CancellationToken cancellationToken = default) { Calls++; throw new AssertionException("OS identity must not prompt."); }
        public ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default)
        { Calls++; throw new AssertionException("OS identity must not access credential storage."); }
    }
    private sealed class RecordingConsole : ICliConsole
    {
        public StringWriter Output { get; } = new();
        public StringWriter Errors { get; } = new();
        public TextReader In => TextReader.Null;
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInteractive => false;
        public bool IsInputRedirected => true;
    }
}
