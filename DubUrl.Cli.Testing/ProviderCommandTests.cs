using DubUrl.Cli;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;
using System.Text.Json;

namespace DubUrl.Cli.Testing;

public class ConnectorCommandTests
{
    [Test]
    public async Task List_Json_ReportsStableProviderFields()
    {
        var console = new TestConsole();
        var service = new StubProviderPackService
        {
            Packs =
            [
                new("postgresql-npgsql", "1.2.3", "user-store", "active", "active", ["postgresql", "pg"], ["postgresql"], []),
            ],
        };
        await using var services = CliComposition.CreateDefault(console, providerPackService: service);

        var exitCode = await CliHost.InvokeAsync(["connector", "list", "--json"], services);

        using var json = JsonDocument.Parse(console.StandardOutput);
        var installed = json.RootElement.GetProperty("connectors")[0];
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(json.RootElement.GetProperty("command").GetString(), Is.EqualTo("connector list"));
            Assert.That(installed.GetProperty("id").GetString(), Is.EqualTo("postgresql-npgsql"));
            Assert.That(installed.GetProperty("aliases")[0].GetString(), Is.EqualTo("postgresql"));
            Assert.That(installed.GetProperty("version").GetString(), Is.EqualTo("1.2.3"));
            Assert.That(installed.GetProperty("source").GetString(), Is.EqualTo("user-store"));
            Assert.That(installed.GetProperty("activation").GetString(), Is.EqualTo("active"));
            Assert.That(installed.GetProperty("status").GetString(), Is.EqualTo("active"));
        });
    }

    [Test]
    public async Task Install_Alias_ReportsCanonicalConnectorAndNewProcessActivation()
    {
        var console = new TestConsole();
        var service = new StubProviderPackService();
        await using var services = CliComposition.CreateDefault(console, providerPackService: service);

        var exitCode = await CliHost.InvokeAsync(["connector", "install", "postgresql", "--json"], services);

        using var json = JsonDocument.Parse(console.StandardOutput);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.Success));
            Assert.That(service.Installed, Is.EqualTo("postgresql"));
            Assert.That(json.RootElement.GetProperty("connector").GetProperty("id").GetString(), Is.EqualTo("postgresql-npgsql"));
            Assert.That(json.RootElement.GetProperty("activationAppliesTo").GetString(), Is.EqualTo("new-processes"));
        });
    }

    [Test]
    public async Task Uninstall_MissingConnector_ReturnsDeterministicExitCode()
    {
        var console = new TestConsole();
        var service = new StubProviderPackService { FailUninstall = true };
        await using var services = CliComposition.CreateDefault(console, providerPackService: service);

        var exitCode = await CliHost.InvokeAsync(["connector", "uninstall", "secret-connector"], services);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(CliExitCodes.ProviderNotFound));
            Assert.That(console.StandardOutput, Is.Empty);
            Assert.That(console.StandardError, Does.Contain("connector-not-installed"));
        });
    }

    private sealed class StubProviderPackService : IProviderPackService
    {
        public IReadOnlyList<CliProviderPack> Packs { get; init; } = [];
        public string? Installed { get; private set; }
        public bool FailUninstall { get; init; }

        public IReadOnlyList<CliProviderPack> List() => Packs;

        public Task<CliProviderPack> InstallAsync(string connector, CancellationToken cancellationToken)
        {
            Installed = connector;
            return Task.FromResult(new CliProviderPack(
                "postgresql-npgsql", "1.0.0", "user-store", "active", "active", ["postgresql", "pg"], ["postgresql"], []));
        }

        public string Uninstall(string connector)
        {
            if (FailUninstall)
                throw new ProviderPackCommandException(
                    "connector-not-installed",
                    $"Connector '{connector}' is not installed.",
                    CliExitCodes.ProviderNotFound);
            return connector;
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
