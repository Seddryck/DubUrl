using DubUrl.Cli;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DubUrl.Cli.Testing;

public class ProviderPackServiceTests
{
    [Test]
    public void List_CuratedConnector_ReportsCanonicalIdAliasesAndSchemesAsListed()
    {
        var root = CreateStorePath();
        try
        {
            var service = CreateService(root);

            var connector = service.List().Single();

            Assert.Multiple(() =>
            {
                Assert.That(connector.Id, Is.EqualTo("postgresql-npgsql"));
                Assert.That(connector.Source, Is.EqualTo("catalog"));
                Assert.That(connector.State, Is.EqualTo("listed"));
                Assert.That(connector.Listed, Is.True);
                Assert.That(connector.Installed, Is.False);
                Assert.That(connector.Active, Is.False);
                Assert.That(connector.Unhealthy, Is.False);
                Assert.That(connector.Aliases, Is.EquivalentTo(new[] { "postgresql", "pg", "npgsql" }));
                Assert.That(connector.Schemes, Is.EquivalentTo(new[] { "postgresql", "postgres", "pg" }));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestCase(ProviderPackStatus.Installed, "installed", false, false)]
    [TestCase(ProviderPackStatus.Active, "active", true, false)]
    [TestCase(ProviderPackStatus.Incompatible, "unhealthy", false, true)]
    [TestCase(ProviderPackStatus.Incomplete, "unhealthy", false, true)]
    [TestCase(ProviderPackStatus.Corrupt, "unhealthy", false, true)]
    public void ToCliPack_ReportsIndependentInstallationAndHealthFacts(
        ProviderPackStatus status,
        string state,
        bool active,
        bool unhealthy)
    {
        var entry = CreateEntry();
        var pack = new InstalledProviderPack(CreateManifest(), "unused", status, []);

        var connector = ProviderPackService.ToCliPack(pack, entry);

        Assert.Multiple(() =>
        {
            Assert.That(connector.State, Is.EqualTo(state));
            Assert.That(connector.Listed, Is.True);
            Assert.That(connector.Installed, Is.True);
            Assert.That(connector.Active, Is.EqualTo(active));
            Assert.That(connector.Unhealthy, Is.EqualTo(unhealthy));
        });
    }

    [Test]
    public void Uninstall_AliasForMissingConnector_ReportsCanonicalId()
    {
        var root = CreateStorePath();
        try
        {
            var service = CreateService(root);

            var exception = Assert.Throws<ProviderPackCommandException>(() => service.Uninstall("postgresql"));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo("connector-not-installed"));
                Assert.That(exception.Message, Does.Contain("postgresql-npgsql"));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task List_CatalogUpgrade_ReportsStaleInstalledVersionSeparatelyFromListedVersion()
    {
        var root = CreateStorePath();
        try
        {
            var store = new ProviderPackStore(root);
            var package = CreatePackage("postgresql-npgsql", "1.0.0");
            await store.InstallAsync(package.Entry, package.Archive);
            var current = CreateEntry() with { Version = "2.0.0" };
            var service = new ProviderPackService(store, new ProviderPackCatalog([current]), new HttpClient());

            var connectors = service.List();

            Assert.Multiple(() =>
            {
                Assert.That(connectors, Has.Count.EqualTo(2));
                Assert.That(connectors.Single(item => item.Version == "1.0.0").Listed, Is.False);
                Assert.That(connectors.Single(item => item.Version == "1.0.0").Installed, Is.True);
                Assert.That(connectors.Single(item => item.Version == "2.0.0").Listed, Is.True);
                Assert.That(connectors.Single(item => item.Version == "2.0.0").Installed, Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Install_UnavailableCatalogArtifact_ReportsDownloadDiagnostic()
    {
        var root = CreateStorePath();
        try
        {
            var service = new ProviderPackService(
                new ProviderPackStore(root),
                new ProviderPackCatalog([CreateEntry()]),
                new HttpClient(new UnavailableHandler()));

            var exception = await Assert.ThrowsAsync<ProviderPackCommandException>(
                async () => await service.InstallAsync("postgresql", CancellationToken.None));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo("connector-download-failed"));
                Assert.That(exception.Message, Does.Contain("could not be downloaded"));
                Assert.That(Directory.Exists(root), Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static ProviderPackService CreateService(string root)
    {
        var entry = CreateEntry();
        return new(new ProviderPackStore(root), new ProviderPackCatalog([entry]), new HttpClient());
    }

    private static ProviderPackCatalogEntry CreateEntry()
        => new(
            "postgresql-npgsql",
            "1.0.0",
            new Uri("https://example.test/postgresql-npgsql.zip"),
            new string('0', 64),
            ["postgresql", "pg", "npgsql"],
            ["postgresql", "postgres", "pg"]);

    private static ProviderPackManifest CreateManifest()
        => new()
        {
            Provider = "postgresql-npgsql",
            Version = "1.0.0",
            DubUrlApiRange = "*",
            Module = new ProviderPackModuleEntry { Assembly = "connector.dll", Type = "Connector.Module" },
            Schemes = ["postgresql", "postgres", "pg"],
            TargetFrameworks = ["net10.0"],
            RuntimeIdentifiers = [],
            Assets = [],
        };

    private static string CreateStorePath()
        => Path.Combine(TestContext.CurrentContext.WorkDirectory, $"connector-store-{Guid.NewGuid():N}");

    private static Package CreatePackage(string provider, string version)
    {
        var assetBytes = Encoding.UTF8.GetBytes("fake connector module");
        var manifest = new ProviderPackManifest
        {
            Provider = provider,
            Version = version,
            DubUrlApiRange = "[0.0.0,999.0.0)",
            Module = new() { Assembly = "module.dll", Type = "Connector.Module" },
            Schemes = ["postgresql"],
            TargetFrameworks = [CurrentTargetFramework()],
            RuntimeIdentifiers = [],
            Assets =
            [
                new()
                {
                    Path = "module.dll",
                    Sha256 = Convert.ToHexString(SHA256.HashData(assetBytes)),
                    Kind = ProviderPackAssetKind.Managed,
                },
            ],
        };
        var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = zip.CreateEntry(ProviderPackManifest.FileName);
            using (var stream = manifestEntry.Open())
                JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var moduleEntry = zip.CreateEntry("module.dll");
            using var module = moduleEntry.Open();
            module.Write(assetBytes);
        }
        archive.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(archive));
        archive.Position = 0;
        return new(new(provider, version, new Uri($"https://example.test/{provider}-{version}.zip"), hash), archive);
    }

    private static string CurrentTargetFramework()
    {
#if NET10_0
        return "net10.0";
#elif NET9_0
        return "net9.0";
#else
        return "net8.0";
#endif
    }

    private sealed record Package(ProviderPackCatalogEntry Entry, MemoryStream Archive);

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("Catalog artifact is unavailable.");
    }
}
