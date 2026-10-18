using DubUrl.Cli;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;
using System.Text.Json;

namespace DubUrl.Cli.Testing;

public class ProviderCommandTests
{
    [Test]
    public async Task List_Json_ReportsStableProviderFields()
    {
        var console = new TestConsole();
        var service = new StubProviderPackService
        {
            Packs =
            [
                new("SQLite", "bundled", "bundled", "active", "available", ["sqlite"], []),
                new("duckdb", "1.2.3", "user-store", "active", "active", ["duckdb"], []),
            ],
        };
        await using var services = CliComposition.CreateDefault(console, providerPackService: service);

        var exitCode = await CliHost.InvokeAsync(["provider", "list", "--json"], services);

        using var json = JsonDocument.Parse(console.StandardOutput);
        var optional = json.RootElement.GetProperty("providers")[1];
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(json.RootElement.GetProperty("command").GetString(), Is.EqualTo("provider list"));
            Assert.That(optional.GetProperty("name").GetString(), Is.EqualTo("duckdb"));
            Assert.That(optional.GetProperty("version").GetString(), Is.EqualTo("1.2.3"));
            Assert.That(optional.GetProperty("source").GetString(), Is.EqualTo("user-store"));
            Assert.That(optional.GetProperty("activation").GetString(), Is.EqualTo("active"));
            Assert.That(optional.GetProperty("status").GetString(), Is.EqualTo("active"));
        });
    }

    [Test]
    public async Task Add_CuratedProvider_ReportsNewProcessActivation()
    {
        var console = new TestConsole();
        var service = new StubProviderPackService();
        await using var services = CliComposition.CreateDefault(console, providerPackService: service);

        var exitCode = await CliHost.InvokeAsync(["provider", "add", "duckdb", "--json"], services);

        using var json = JsonDocument.Parse(console.StandardOutput);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(service.Added, Is.EqualTo("duckdb"));
            Assert.That(json.RootElement.GetProperty("activationAppliesTo").GetString(), Is.EqualTo("new-processes"));
        });
    }

    [Test]
    public async Task Remove_MissingProvider_ReturnsDeterministicExitCode()
    {
        var console = new TestConsole();
        var service = new StubProviderPackService { FailRemove = true };
        await using var services = CliComposition.CreateDefault(console, providerPackService: service);

        var exitCode = await CliHost.InvokeAsync(["provider", "remove", "secret-provider"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.ProviderNotFound));
            Assert.That(console.StandardOutput, Is.Empty);
            Assert.That(console.StandardError, Does.Contain("provider-not-installed"));
        });
    }

    private sealed class StubProviderPackService : IProviderPackService
    {
        public IReadOnlyList<CliProviderPack> Packs { get; init; } = [];
        public string? Added { get; private set; }
        public bool FailRemove { get; init; }

        public IReadOnlyList<CliProviderPack> List() => Packs;

        public Task<CliProviderPack> AddAsync(string provider, CancellationToken cancellationToken)
        {
            Added = provider;
            return Task.FromResult(new CliProviderPack(
                provider, "1.0.0", "user-store", "active", "active", [provider], []));
        }

        public bool Remove(string provider)
        {
            if (FailRemove)
                throw new ProviderPackCommandException(
                    "provider-not-installed",
                    $"Optional provider pack '{provider}' is not installed.",
                    CliExitCodes.ProviderNotFound);
            return true;
        }
    }

    private sealed class TestConsole : ICliConsole
    {
        private readonly StringWriter _output = new();
        private readonly StringWriter _error = new();

        public TextReader In { get; } = new StringReader(string.Empty);
        public TextWriter Out => _output;
        public TextWriter Error => _error;
        public bool IsInputRedirected => true;
        public bool IsInteractive => false;
        public string StandardOutput => _output.ToString();
        public string StandardError => _error.ToString();
    }
}
