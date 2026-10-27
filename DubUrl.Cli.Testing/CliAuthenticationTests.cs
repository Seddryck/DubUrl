using DubUrl.Authentication;
using DubUrl.Cli;
using DubUrl.Mapping;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class CliAuthenticationTests
{
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
