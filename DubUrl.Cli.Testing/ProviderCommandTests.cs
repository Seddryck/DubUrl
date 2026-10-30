using DubUrl.Cli;
using DubUrl.Application;
using DubUrl.Core.ProviderPacks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace DubUrl.Cli.Testing;

public class ConnectorCommandTests
{
    private static IEnumerable<TestCaseData> NamedCatalogCases()
    {
        foreach (var mode in new[] { "sha256", "unsigned", "signed" })
            foreach (var official in new[] { "healthy", "disabled", "missing" })
                foreach (var json in new[] { true, false })
                    yield return new TestCaseData(json, official, mode);
    }

    [TestCaseSource(nameof(NamedCatalogCases))]
    public async Task List_NamedSources_ReportsSourceAndSafeFailuresInTextAndJson(bool jsonOutput, string officialState, string trustMode)
    {
        var path = Path.GetTempFileName();
        var entry = new ConnectorDefinition
        {
            Provider = "enterprise-pack", Version = "1.0.0", Aliases = ["corp"],
            DownloadUri = new Uri("https://private.test/archive?token=secret"), Sha256 = new string('0', 64),
            DubUrlApiRange = "*", Assembly = "fixture.dll", Driver = "Fixture", ImplementationAssemblies = ["fixture.dll"],
            Databases = [new("Fixture", ["corp"])], TargetFrameworks = ["net10.0"], RuntimeIdentifiers = [],
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new ProviderPackCatalogDocument(2, [entry]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await File.WriteAllBytesAsync(path, bytes);
        using var key = System.Security.Cryptography.RSA.Create(2048);
        await File.WriteAllTextAsync(path + ".public.pem", key.ExportSubjectPublicKeyInfoPem());
        await File.WriteAllBytesAsync(path + ".sig", ConnectorCatalogSignature.Sign(bytes, key, "enterprise", "key-1", 1, DateTimeOffset.UtcNow.AddHours(1)));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProviderPacks:StorePath"] = path + ".store",
            ["ProviderPacks:OfficialEnabled"] = officialState == "disabled" ? "false" : "true",
            ["ProviderPacks:CatalogPath"] = officialState == "missing" ? path + ".missing" : null,
            ["ProviderPacks:Sources:0:Id"] = "enterprise",
            ["ProviderPacks:Sources:0:Uri"] = new Uri(path).AbsoluteUri,
            ["ProviderPacks:Sources:0:TrustMode"] = trustMode,
            ["ProviderPacks:Sources:0:Sha256"] = trustMode == "sha256" ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) : null,
            ["ProviderPacks:Sources:0:SignatureUri"] = trustMode == "signed" ? new Uri(path + ".sig").AbsoluteUri : null,
            ["ProviderPacks:Sources:0:TrustedSigningKeys:0:KeyId"] = trustMode == "signed" ? "key-1" : null,
            ["ProviderPacks:Sources:0:TrustedSigningKeys:0:PublicKeyPath"] = trustMode == "signed" ? path + ".public.pem" : null,
            ["ProviderPacks:Sources:0:MinimumRevision"] = "1",
            ["ProviderPacks:Sources:1:Id"] = "unavailable",
            ["ProviderPacks:Sources:1:Uri"] = "https://user:password@private.test/catalog?token=secret",
            ["ProviderPacks:Sources:1:Sha256"] = new string('0', 64),
        }.Where(pair => pair.Value is not null)).Build();
        var console = new TestConsole();
        try
        {
            await using var services = CliComposition.CreateDefault(console, configuration);
            Assert.That(await CliHost.InvokeAsync(jsonOutput ? ["connector", "list", "--json"] : ["connector", "list"], services), Is.Zero);
            var output = console.StandardOutput + console.StandardError;
            Assert.Multiple(() =>
            {
                Assert.That(output, Does.Contain("enterprise-pack").And.Contain("enterprise").And.Contain("unavailable"));
                Assert.That(output, Does.Not.Contain("private.test").And.Not.Contain("secret").And.Not.Contain("password"));
                if (officialState == "healthy") Assert.That(output, Does.Contain("postgresql-npgsql").And.Contain("official"));
                else Assert.That(output, Does.Not.Contain("postgresql-npgsql"));
            });
            if (jsonOutput)
            {
                using var document = JsonDocument.Parse(console.StandardOutput);
                var connector = document.RootElement.GetProperty("connectors").EnumerateArray().Single(item => item.GetProperty("id").GetString() == "enterprise-pack");
                Assert.That(connector.GetProperty("catalogSource").GetString(), Is.EqualTo("enterprise"));
                Assert.That(connector.GetProperty("catalogProvenance").GetProperty("trustMode").GetString(), Is.EqualTo(trustMode));
            }
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".sig");
            File.Delete(path + ".public.pem");
            if (Directory.Exists(path + ".store")) Directory.Delete(path + ".store", true);
        }
    }

    [Test]
    public async Task Mirror_PinnedSelection_WritesValidatedCatalogAndImmutableArchive()
    {
        var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "mirror-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var bytes = new byte[] { 1, 2, 3 };
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        var entry = new ConnectorDefinition
        {
            Provider = "fixture", Version = "1.0.0", DownloadUri = new Uri("https://upstream.test/fixture.zip"), Sha256 = hash,
            DubUrlApiRange = "[0.0.0,2.0.0)", Assembly = "fixture.dll", Driver = "Fixture", ImplementationAssemblies = ["fixture.dll"],
            Databases = [new("Fixture", ["fixture"])], TargetFrameworks = ["net10.0"], RuntimeIdentifiers = [],
        };
        var catalogPath = Path.Combine(root, "approved.json");
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new ProviderPackCatalogDocument(2, [entry]), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var console = new TestConsole();
        using var http = new HttpClient(new MirrorArchiveHandler(bytes));
        await using var services = new ServiceCollection().AddSingleton<ICliConsole>(console).AddSingleton(http).BuildServiceProvider();
        try
        {
            var output = Path.Combine(root, "snapshot");
            var exit = await CliHost.InvokeAsync(["connector", "mirror", catalogPath, output, "https://internal.test/packs/", "fixture@1.0.0"], services);
            Assert.That(exit, Is.Zero, console.StandardError);
            using var stream = File.OpenRead(Path.Combine(output, "connector-catalog.json"));
            var mirrored = ProviderPackCatalog.Read(stream).Entries.Single();
            Assert.Multiple(() =>
            {
                Assert.That(mirrored.DownloadUri, Is.EqualTo(new Uri("https://internal.test/packs/" + hash.ToLowerInvariant() + ".zip")));
                Assert.That(File.ReadAllBytes(Path.Combine(output, hash.ToLowerInvariant() + ".zip")), Is.EqualTo(bytes));
            });
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class MirrorArchiveHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
    }

    [Test]
    public async Task List_NoCatalogConfiguration_UsesApplicationBundledCatalog()
    {
        var store = Path.Combine(TestContext.CurrentContext.WorkDirectory, "bundled-catalog-" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProviderPacks:StorePath"] = store,
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
    public async Task List_ExplicitMissingCatalog_ReportsSourceFailureWithoutBundledFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProviderPacks:StorePath"] = root,
            ["ProviderPacks:CatalogPath"] = root + ".json",
        }).Build();
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, configuration);
        Assert.That(await CliHost.InvokeAsync(["connector", "list", "--json"], services), Is.Zero);
        using var document = JsonDocument.Parse(console.StandardOutput);
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("connectors").GetArrayLength(), Is.Zero);
            Assert.That(document.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("connector-source-unavailable"));
        });
    }

    [Test]
    public async Task Install_LocalArchive_ForwardsExplicitOfflineRequest()
    {
        var service = new StubProviderPackService();
        await using var services = CliComposition.CreateDefault(new TestConsole(), providerPackService: service);
        var exit = await CliHost.InvokeAsync(["connector", "install", "pg", "--archive", "approved.zip", "--json"], services);
        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.Zero);
            Assert.That(service.Installed, Is.EqualTo("pg"));
            Assert.That(service.ArchivePath, Is.EqualTo("approved.zip"));
        });
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

        public string? ArchivePath { get; private set; }
        public Task<CliProviderPack> InstallOfflineAsync(string connector, string archivePath, CancellationToken cancellationToken)
        {
            ArchivePath = archivePath;
            return InstallAsync(connector, cancellationToken);
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
