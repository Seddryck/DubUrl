using DubUrl.Authentication;
using DubUrl.Application;
using DubUrl.Cli;
using DubUrl.Mapping;
using DubUrl.Schema.Documents;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class CliAuthenticationTests
{
    [TestCase("connection", false), TestCase("schema", false)]
    [TestCase("connection", true), TestCase("schema", true)]
    public async Task CommandCancellationWithoutCallerTokenReturns130(string command, bool osIdentity)
    {
        var console = new RecordingConsole();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:test"] = "mssql://localhost/app" }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration);
        string[] arguments = command == "connection"
            ? ["connection", "check", "mssql://localhost/app", "--output-format", "json"]
            : ["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--output-format", "json"];
        if (osIdentity) arguments = [.. arguments, "--os-identity"];
        var exit = await CliHost.InvokeAsync(arguments, new AbortingServices(services));
        Assert.That(exit, Is.EqualTo(130));
        Assert.That(console.Output.ToString(), Is.Empty);
        Assert.That(console.Errors.ToString(), Does.Contain("cancelled"));
    }

    private sealed class AbortingServices(IServiceProvider inner) : IServiceProvider
    {
        public object? GetService(Type type)
            => type == typeof(IConnectionCheckService) ? new AbortingCheck()
                : type == typeof(ISchemaDeploymentService) ? new AbortingDeployment() : inner.GetService(type);
    }
    private sealed class AbortingCheck : IConnectionCheckService
    {
        public Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }
    private sealed class AbortingDeployment : ISchemaDeploymentService
    {
        public Task<SchemaDeploymentResult> DeployAsync(ISchemaDocument document, ConnectionUrl target, SchemaDeploymentOptions? options = null, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }
    [TestCase("--password-prompt")]
    [TestCase("--credential", "development")]
    [TestCase("--os-identity")]
    public async Task DryRunNeverReadsSecrets(params string[] arguments)
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
        var prompt = new CountingPrompt();
        var store = new CountingStore();
        var console = new RecordingConsole();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:test"] = "sqlite:///:memory:" }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: prompt, namedCredentialStore: store);
        var exit = await CliHost.InvokeAsync(["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--dry-run", .. arguments], services);
        Assert.That(exit, Is.Zero, console.Errors.ToString());
        Assert.That(prompt.Calls, Is.Zero);
        Assert.That(store.Calls, Is.Zero);
        Assert.That(console.Output.ToString() + console.Errors.ToString(), Does.Not.Contain("sentinel"));
    }

    [TestCase("--password-prompt", "--credential", "development")]
    [TestCase("--username", "alice")]
    [TestCase("--os-identity", "--password-prompt")]
    [TestCase("--os-identity", "--credential", "development")]
    [TestCase("--os-identity", "--username", "alice")]
    [TestCase("--oauth", "browser", "--credential", "development")]
    [TestCase("--oauth", "device-code", "--os-identity")]
    public async Task ConflictsFailWithoutAcquisition(params string[] arguments)
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
        var prompt = new CountingPrompt();
        var store = new CountingStore();
        var console = new RecordingConsole();
        await using var services = CliComposition.CreateDefault(console, passwordPrompt: prompt, namedCredentialStore: store);
        var exit = await CliHost.InvokeAsync(["connection", "check", "postgres://localhost/app", "--output-format", "json", .. arguments], services);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(prompt.Calls + store.Calls, Is.Zero);
        Assert.That(console.Output.ToString(), Does.Not.Contain("sentinel"));
    }

    [Test]
    public async Task CredentialStoreUsesProviderMappingWithoutAnAllowlist()
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("NReco.PrestoAdo", NReco.PrestoAdo.PrestoDbFactory.Instance);
        var store = new CountingStore();
        var console = new RecordingConsole();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], namedCredentialStore: store);
        // NReco's Open only changes state; actual server authentication occurs when executing a query.
        var exit = await CliHost.InvokeAsync(["connection", "check", "trino://localhost/pg/public?UseSsl=true", "--credential", "fixture", "--output-format", "json"], services);
        Assert.That(exit, Is.Zero);
        Assert.That(store.Calls, Is.EqualTo(1));
        Assert.That(console.Output.ToString(), Does.Not.Contain("sentinel"));
    }

    [TestCase("json"), TestCase("human")]
    public async Task OsIdentityUsesTheSharedStrategyWithoutReadingSecrets(string format)
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
        var console = new RecordingConsole();
        var prompt = new CountingPrompt();
        var store = new CountingStore();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: prompt, namedCredentialStore: store);
        var capture = new IdentityCheck();
        var exit = await CliHost.InvokeAsync(["connection", "check", "mssql://db.example/app", "--os-identity", "--engine", "--output-format", format],
            new CheckServices(services, capture));
        Assert.That(exit, Is.Zero, console.Errors.ToString());
        Assert.That(capture.Calls, Is.EqualTo(1));
        Assert.That(prompt.Calls + store.Calls, Is.Zero);
        if (format == "json")
        {
            using var json = System.Text.Json.JsonDocument.Parse(console.Output.ToString());
            Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.True);
        }
        else Assert.That(console.Output.ToString(), Does.Contain("succeeded"));
    }

    [TestCase("mssql://alice:sensitive-sentinel@db/app")]
    [TestCase("mssql://db/app?Password=sensitive-sentinel")]
    [TestCase("mssql://db/app?AccessToken=sensitive-sentinel")]
    [TestCase("mssql://db/app?OAuthClientId=sensitive-sentinel")]
    public async Task OsIdentityRejectsUrlConflictsBeforeCheckOrAcquisition(string url)
    {
        var console = new RecordingConsole();
        var prompt = new CountingPrompt();
        var store = new CountingStore();
        await using var services = CliComposition.CreateDefault(console, passwordPrompt: prompt, namedCredentialStore: store);
        var capture = new IdentityCheck();
        var exit = await CliHost.InvokeAsync(["connection", "check", url, "--os-identity", "--output-format", "json"], new CheckServices(services, capture));
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(capture.Calls + prompt.Calls + store.Calls, Is.Zero);
        Assert.That(console.Output.ToString() + console.Errors.ToString(), Does.Not.Contain("sensitive-sentinel"));
    }

    [Test]
    public async Task OsIdentityUnsupportedProviderHasSafeDiagnostic()
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
        var console = new RecordingConsole();
        var prompt = new CountingPrompt();
        var store = new CountingStore();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: prompt, namedCredentialStore: store);
        var exit = await CliHost.InvokeAsync(["connection", "check", "sqlite:///:memory:", "--os-identity", "--output-format", "json"], services);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(console.Output.ToString(), Does.Contain("UnsupportedAuthentication"));
        Assert.That(prompt.Calls + store.Calls, Is.Zero);
    }

    private sealed class CheckServices(IServiceProvider inner, IConnectionCheckService check) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(IConnectionCheckService) ? check : inner.GetService(type);
    }
    private sealed class IdentityCheck : IConnectionCheckService
    {
        public int Calls { get; private set; }
        public async Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.That(options.Engine, Is.True);
            // Exercise the actual Library pipeline, without claiming a database login.
            await using var actual = await target.ConnectAsync(cancellationToken);
            await using var expected = await new ConnectionUrl("mssql://db.example/app")
                .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity()).ConnectAsync(cancellationToken);
            Assert.That(actual.ConnectionString, Is.EqualTo(expected.ConnectionString));
            Assert.That(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(actual.ConnectionString).IntegratedSecurity, Is.True);
            return new(true, "engine", "Microsoft.Data.SqlClient", 0, []);
        }
    }

    private sealed class CountingPrompt : IHiddenPasswordPrompt
    {
        public int Calls { get; private set; }
        public ValueTask<string> ReadAsync(CancellationToken cancellationToken = default) { Calls++; return new("sentinel"); }
    }
    private sealed class CountingStore : INamedCredentialStore
    {
        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask AddAsync(string name, UsernamePasswordCredentials credentials, bool replace, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public int Calls { get; private set; }
        public ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default)
        { Calls++; return new(new UsernamePasswordCredentials("alice", "sentinel")); }
    }
    private sealed class RecordingConsole : ICliConsole
    {
        public StringWriter Output { get; } = new();
        public StringWriter Errors { get; } = new();
        public TextReader In => new StringReader("{\"version\":1,\"tables\":[{\"identity\":{\"name\":\"customer\"},\"columns\":[{\"name\":\"id\",\"type\":\"int32\"}]}]}");
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInteractive => false;
        public bool IsInputRedirected => true;
    }
}
