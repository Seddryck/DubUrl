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
    [TestCase("connection"), TestCase("schema")]
    public async Task CommandCancellationWithoutCallerTokenReturns130(string command)
    {
        var console = new RecordingConsole();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:test"] = "mssql://localhost/app" }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration);
        string[] arguments = command == "connection"
            ? ["connection", "check", "mssql://localhost/app", "--output-format", "json"]
            : ["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--output-format", "json"];
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
    [TestCase("--credential-store", "development")]
    public async Task DryRunNeverReadsSecrets(params string[] arguments)
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
        var prompt = new CountingPrompt();
        var store = new CountingStore();
        var console = new RecordingConsole();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:test"] = "sqlite:///:memory:" }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], passwordPrompt: prompt, credentialStore: store);
        var exit = await CliHost.InvokeAsync(["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--dry-run", .. arguments], services);
        Assert.That(exit, Is.Zero, console.Errors.ToString());
        Assert.That(prompt.Calls, Is.Zero);
        Assert.That(store.Calls, Is.Zero);
        Assert.That(console.Output.ToString() + console.Errors.ToString(), Does.Not.Contain("sentinel"));
    }

    [TestCase("--password-prompt", "--credential-store", "development")]
    [TestCase("--username", "alice")]
    public async Task ConflictsFailWithoutAcquisition(params string[] arguments)
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
        var prompt = new CountingPrompt();
        var store = new CountingStore();
        var console = new RecordingConsole();
        await using var services = CliComposition.CreateDefault(console, passwordPrompt: prompt, credentialStore: store);
        var exit = await CliHost.InvokeAsync(["connection", "check", "postgres://localhost/app", "--output-format", "json", .. arguments], services);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(prompt.Calls + store.Calls, Is.Zero);
        Assert.That(console.Output.ToString(), Does.Not.Contain("sentinel"));
    }

    [Test]
    public async Task UnsupportedAuthenticationIsInvalidInputAndDoesNotReadStore()
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
        var store = new CountingStore();
        var console = new RecordingConsole();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], credentialStore: store);
        var exit = await CliHost.InvokeAsync(["connection", "check", "sqlite:///:memory:", "--credential-store", "fixture", "--output-format", "json"], services);
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(store.Calls, Is.Zero);
        Assert.That(console.Output.ToString(), Does.Not.Contain("sentinel"));
    }

    private sealed class CountingPrompt : IHiddenPasswordPrompt
    {
        public int Calls { get; private set; }
        public ValueTask<string> ReadAsync(CancellationToken cancellationToken = default) { Calls++; return new("sentinel"); }
    }
    private sealed class CountingStore : IOsCredentialStore
    {
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
