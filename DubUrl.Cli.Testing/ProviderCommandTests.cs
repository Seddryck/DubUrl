using DubUrl.Cli;
using DubUrl.Application;
using DubUrl.Core.ProviderPacks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.Text.Json;

namespace DubUrl.Cli.Testing;

public class ConnectorCommandTests
{
    [Test]
    public async Task List_NoCatalogConfiguration_UsesApplicationBundledCatalog()
    {
        var store = Path.Combine(TestContext.CurrentContext.WorkDirectory, "bundled-catalog-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProviderPacks:StorePath"] = store,
            ["ProviderPacks:CatalogPath"] = Path.Combine(store, "absent.json"),
        }).Build();
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, configuration);
        Assert.That(await CliHost.InvokeAsync(["connector", "list", "--json"], services), Is.Zero);
        using var json = JsonDocument.Parse(console.StandardOutput);
        Assert.That(json.RootElement.GetProperty("connectors").EnumerateArray()
            .Select(entry => entry.GetProperty("id").GetString()), Does.Contain("postgresql-npgsql"));
        Assert.That(Directory.Exists(store), Is.False);
    }

    [Test]
    public async Task List_FreshStore_LoadsVersionedOfficialCatalogWithoutInstallingConnector()
    {
        var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"connector-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var catalogPath = Path.Combine(root, "connector-catalog.json");
            var storePath = Path.Combine(root, "store");
            await File.WriteAllTextAsync(catalogPath, """
                {
                  "schemaVersion": 2,
                  "entries": [{
                    "provider": "postgresql-npgsql",
                    "version": "1.2.3",
                    "downloadUri": "https://example.test/postgresql-npgsql.zip",
                    "sha256": "0000000000000000000000000000000000000000000000000000000000000000",
                    "assembly": "PostgreSql.dll", "databases": [{"name": "Fixture", "schemes": ["pg", "pgx", "pgsql", "postgres", "postgresql"]}], "driver": "Fixture", "implementationAssemblies": ["PostgreSql.dll"], "driverAssembly": "PostgreSql.dll",
                    "aliases": ["postgresql", "postgres", "pg", "npgsql"],

                    "dubUrlApiRange": "[1.0.0,2.0.0)",
                    "targetFrameworks": ["net8.0", "net9.0", "net10.0"],
                    "runtimeIdentifiers": []
                  }]
                }
                """);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProviderPacks:CatalogPath"] = catalogPath,
                ["ProviderPacks:StorePath"] = storePath,
            }).Build();
            var console = new TestConsole();
            await using var services = CliComposition.CreateDefault(console, configuration);

            var exitCode = await CliHost.InvokeAsync(["connector", "list", "--json"], services);

            using var json = JsonDocument.Parse(console.StandardOutput);
            var connector = json.RootElement.GetProperty("connectors")[0];
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(connector.GetProperty("id").GetString(), Is.EqualTo("postgresql-npgsql"));
                Assert.That(connector.GetProperty("listed").GetBoolean(), Is.True);
                Assert.That(connector.GetProperty("installed").GetBoolean(), Is.False);
                Assert.That(connector.GetProperty("active").GetBoolean(), Is.False);
                Assert.That(connector.GetProperty("unhealthy").GetBoolean(), Is.False);
                Assert.That(Directory.Exists(storePath), Is.False);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task List_Json_ReportsStableConnectorStateFields()
    {
        var console = new TestConsole();
        var service = new StubProviderPackService
        {
            Packs =
            [
                new("postgresql-npgsql", "1.2.3", "user-store", "active", true, true, true, false, ["postgresql", "pg"], ["postgresql"], []),
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
            Assert.That(installed.GetProperty("state").GetString(), Is.EqualTo("active"));
            Assert.That(installed.GetProperty("listed").GetBoolean(), Is.True);
            Assert.That(installed.GetProperty("installed").GetBoolean(), Is.True);
            Assert.That(installed.GetProperty("active").GetBoolean(), Is.True);
            Assert.That(installed.GetProperty("unhealthy").GetBoolean(), Is.False);
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

    [TestCase("install", "unknown", "connector-not-curated", CliExitCodes.ProviderNotFound)]
    [TestCase("install", "pg", "connector-download-failed", CliExitCodes.ProviderOperationFailed)]
    [TestCase("uninstall", "pg", "connector-not-installed", CliExitCodes.ProviderNotFound)]
    public async Task SharedServiceFailure_ReportsDiagnosticAndExitCode(string command, string requested, string code, int expectedExit)
    {
        var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"connector-errors-{Guid.NewGuid():N}");
        using var http = new HttpClient(new UnavailableHandler());
        var catalog = new ProviderPackCatalog([new()
        {
            Provider = "postgresql-npgsql", Version = "1.0.0", DownloadUri = new Uri("https://example.test/pack.zip"),
            Sha256 = new string('0', 64), Aliases = ["pg"], Databases = [new("Fixture", ["pg"])],
            DubUrlApiRange = "[1.0.0,2.0.0)", TargetFrameworks = ["net8.0"], RuntimeIdentifiers = [],
            Assembly = "PostgreSql.dll", Driver = "Fixture",
            ImplementationAssemblies = ["PostgreSql.dll"], DriverAssembly = "PostgreSql.dll",
        }]);
        var application = new ConnectorService(new ProviderPackStore(root), catalog, http);
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, providerPackService: new ProviderPackService(application));
        var exit = await CliHost.InvokeAsync(["connector", command, requested], services);
        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.EqualTo(expectedExit));
            Assert.That(console.StandardError, Does.Contain(code));
            Assert.That(console.StandardError, Does.Not.Contain("password"));
            Assert.That(console.StandardOutput, Is.Empty);
            Assert.That(Directory.Exists(root), Is.False);
        });
    }

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("https://user:password@example.test/");
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
                "postgresql-npgsql", "1.0.0", "user-store", "active", true, true, true, false, ["postgresql", "pg"], ["postgresql"], []));
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
