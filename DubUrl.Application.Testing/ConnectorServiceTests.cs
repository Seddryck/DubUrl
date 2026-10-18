using DubUrl.Application;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DubUrl.Application.Testing;

public class ConnectorServiceTests
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
    public void ToConnectorInfo_ReportsIndependentInstallationAndHealthFacts(
        ProviderPackStatus status,
        string state,
        bool active,
        bool unhealthy)
    {
        var entry = CreateEntry();
        var pack = new InstalledProviderPack(CreateManifest(), "unused", status, []);

        var connector = ConnectorService.ToConnectorInfo(pack, entry);

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

            var exception = Assert.Throws<ConnectorOperationException>(() => service.Uninstall("postgresql"));

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
            var service = new ConnectorService(store, new ProviderPackCatalog([current]), new HttpClient());

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
            var service = new ConnectorService(
                new ProviderPackStore(root),
                new ProviderPackCatalog([CreateEntry()]),
                new HttpClient(new UnavailableHandler()));

            var exception = await Assert.ThrowsAsync<ConnectorOperationException>(
                async () => await service.InstallAsync("postgresql", CancellationToken.None));

            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo("connector-download-failed"));
                Assert.That(exception.Message, Does.Contain("could not be downloaded"));
                Assert.That(exception.ToString(), Does.Not.Contain("password").And.Not.Contain("token=secret"));
                Assert.That(Directory.Exists(root), Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Install_Alias_ReinstallListAndUninstall_UsesPinnedCatalogArtifact()
    {
        var root = CreateStorePath();
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        using var http = new HttpClient(new ArchiveHandler(package.Archive.ToArray(), package.Entry.DownloadUri));
        var entry = package.Entry with { Aliases = ["pg"] };
        IConnectorService service = new ConnectorService(new ProviderPackStore(root), new ProviderPackCatalog([entry]), http);
        try
        {
            var installed = await service.InstallAsync("PG");
            var reinstalled = await service.InstallAsync(entry.Provider);
            Assert.Multiple(() =>
            {
                Assert.That(installed.Id, Is.EqualTo(entry.Provider));
                Assert.That(installed.Version, Is.EqualTo(entry.Version));
                Assert.That(installed.Active, Is.True);
                Assert.That(reinstalled.Active, Is.True);
                Assert.That(service.List().Single().Active, Is.True);
                Assert.That(Directory.GetDirectories(Path.Combine(root, "packs", entry.Provider)), Has.Length.EqualTo(1));
            });
            Assert.That(service.Uninstall("pg"), Is.EqualTo(entry.Provider));
            Assert.That(service.List().Single().Installed, Is.False);
            Assert.That(Directory.GetFileSystemEntries(Path.Combine(root, ".staging")), Is.Empty);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestCase("archive", "connector-archive-invalid")]
    [TestCase("manifest", "connector-archive-invalid")]
    [TestCase("digest", "pack-integrity")]
    [TestCase("asset", "asset-integrity")]
    [TestCase("api", "api-incompatible")]
    [TestCase("tfm", "tfm-incompatible")]
    [TestCase("rid", "rid-incompatible")]
    public async Task Install_InvalidOrIncompatibleArtifact_ReturnsStableDiagnostic(string failure, string code)
    {
        var root = CreateStorePath();
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        var bytes = package.Archive.ToArray();
        if (failure == "archive") bytes = Encoding.UTF8.GetBytes("not a zip");
        if (failure is "manifest" or "asset")
        {
            using var modified = new MemoryStream();
            modified.Write(bytes);
            using (var zip = new ZipArchive(modified, ZipArchiveMode.Update, true))
            {
                var name = failure == "manifest" ? ProviderPackManifest.FileName : "module.dll";
                zip.GetEntry(name)!.Delete();
                using var stream = zip.CreateEntry(name).Open();
                stream.Write(Encoding.UTF8.GetBytes("invalid"));
            }
            bytes = modified.ToArray();
        }
        var entry = package.Entry with { Sha256 = failure == "digest" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes)) };
        var store = new ProviderPackStore(root,
            apiVersion: failure == "api" ? new Version(999, 0, 0) : null,
            targetFramework: failure == "tfm" ? "net99.0" : null);
        if (failure == "rid")
        {
            using var modified = new MemoryStream();
            modified.Write(bytes);
            using (var zip = new ZipArchive(modified, ZipArchiveMode.Update, true))
            {
                var manifestEntry = zip.GetEntry(ProviderPackManifest.FileName)!;
                ProviderPackManifest manifest;
                using (var stream = manifestEntry.Open())
                    manifest = JsonSerializer.Deserialize<ProviderPackManifest>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                manifestEntry.Delete();
                using var destination = zip.CreateEntry(ProviderPackManifest.FileName).Open();
                JsonSerializer.Serialize(destination, manifest with { RuntimeIdentifiers = ["unsupported-rid"] }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            bytes = modified.ToArray();
            entry = entry with { Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        }
        using var http = new HttpClient(new ArchiveHandler(bytes, entry.DownloadUri));
        var service = new ConnectorService(store, new ProviderPackCatalog([entry]), http);
        try
        {
            var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await service.InstallAsync(entry.Provider));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo(code));
                Assert.That(store.Inspect(), Is.Empty);
                Assert.That(Directory.GetFileSystemEntries(Path.Combine(root, ".staging")), Is.Empty);
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Test]
    public async Task Install_UnknownConnector_DoesNotDownload()
    {
        var service = CreateService(CreateStorePath());
        var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await service.InstallAsync("unknown"));
        Assert.That(exception!.Code, Is.EqualTo("connector-not-curated"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Install_Cancellation_PreservesToken(bool duringDownload)
    {
        using var cancellation = new CancellationTokenSource();
        using var http = new HttpClient(new CancellingHandler(cancellation));
        var root = CreateStorePath();
        var service = new ConnectorService(new ProviderPackStore(root), new ProviderPackCatalog([CreateEntry()]), http);
        if (!duringDownload) cancellation.Cancel();
        var exception = await Assert.CatchAsync<OperationCanceledException>(async () => await service.InstallAsync("pg", cancellation.Token));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
            Assert.That(exception, Is.TypeOf<ConnectorOperationCanceledException>());
            Assert.That(Directory.Exists(root), Is.False);
        });
    }

    [Test]
    public async Task Install_StoreIsFile_ReturnsSafeStoreDiagnostic()
    {
        var root = CreateStorePath();
        await File.WriteAllTextAsync(root, "occupied");
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        using var http = new HttpClient(new ArchiveHandler(package.Archive.ToArray(), package.Entry.DownloadUri));
        var service = new ConnectorService(new ProviderPackStore(root), new ProviderPackCatalog([package.Entry]), http);
        try
        {
            var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await service.InstallAsync(package.Entry.Provider));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo("connector-install-failed"));
                Assert.That(exception.InnerException, Is.Null);
                Assert.That(exception.ToString(), Does.Not.Contain(root));
            });
        }
        finally { File.Delete(root); }
    }

    [Test]
    public async Task ListAndUninstall_StoreAccessFailures_ReturnStableDiagnostics()
    {
        var root = CreateStorePath();
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        var store = new ProviderPackStore(root);
        await store.InstallAsync(package.Entry, package.Archive);
        using var http = new HttpClient();
        var service = new ConnectorService(store, new ProviderPackCatalog([package.Entry]), http);
        try
        {
            using (var locked = new FileStream(Path.Combine(root, "active", "postgresql-npgsql.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var exception = Assert.Throws<ConnectorOperationException>(() => service.List());
                Assert.That(exception!.Code, Is.EqualTo("connector-list-failed"));
            }
            Directory.Delete(Path.Combine(root, ".staging"));
            await File.WriteAllTextAsync(Path.Combine(root, ".staging"), "occupied");
            var removal = Assert.Throws<ConnectorOperationException>(() => service.Uninstall(package.Entry.Provider));
            Assert.That(removal!.Code, Is.EqualTo("connector-uninstall-failed"));
            Assert.That(service.List().Single().Active, Is.True);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class ArchiveHandler(byte[] bytes, Uri expected) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.That(request.RequestUri, Is.EqualTo(expected));
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }

    private sealed class CancellingHandler(CancellationTokenSource source) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            source.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation was not forwarded.");
        }
    }

    private static ConnectorService CreateService(string root)
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
            => throw new HttpRequestException("https://user:password@example.test/?token=secret");
    }
}
