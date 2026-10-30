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
    [TestCase("sha256", false)]
    [TestCase("unsigned", false)]
    [TestCase("signed", false)]
    [TestCase("unsigned", true)]
    [TestCase("signed", true)]
    public async Task NonDefaultCatalog_EnforcesArchiveIntegrityAndRetainsProvenance(string mode, bool wrongArchiveHash)
    {
        var root = CreateStorePath();
        var path = root + ".json";
        var package = CreatePackage("private-provider", "1.0.0");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new ProviderPackCatalogDocument(2,
            [package.Entry with { Aliases = ["corp"], Sha256 = wrongArchiveHash ? new string('0', 64) : package.Entry.Sha256,
                CatalogProvenance = new("forged", "forged", "0", null, null) }]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await File.WriteAllBytesAsync(path, bytes);
        var digest = Convert.ToHexString(SHA256.HashData(bytes));
        using var http = new HttpClient(new ArchiveHandler(package.Archive.ToArray(), package.Entry.DownloadUri!));
        var statePath = root + ".revisions";
        var loader = new TrustedConnectorCatalogs(http, statePath);
        using var signingKey = RSA.Create(2048);
        var publicKeyPath = root + ".public.pem";
        await File.WriteAllTextAsync(publicKeyPath, signingKey.ExportSubjectPublicKeyInfoPem());
        await File.WriteAllBytesAsync(path + ".sig", ConnectorCatalogSignature.Sign(bytes, signingKey, "enterprise", "key-1", 1, DateTimeOffset.UtcNow.AddHours(1)));
        var source = new ConnectorCatalogSource
        {
            Id = "enterprise", Uri = new Uri(path), TrustMode = mode, Sha256 = mode == "sha256" ? digest : null,
            SignatureUri = mode == "signed" ? new Uri(path + ".sig") : null,
            TrustedSigningKeys = mode == "signed" ? [new() { KeyId = "key-1", PublicKeyPath = publicKeyPath }] : [],
        };
        var store = new ProviderPackStore(root);
        try
        {
            var catalog = await loader.LoadAsync([source]);
            var service = new ConnectorService(store, catalog, http);
            if (wrongArchiveHash)
            {
                var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await service.InstallAsync("corp"));
                Assert.That(exception!.Code, Is.EqualTo("pack-integrity"));
                Assert.That(store.Inspect(), Is.Empty);
                return;
            }
            var installed = await service.InstallAsync("CORP");
            Assert.Multiple(() =>
            {
                Assert.That(installed.CatalogSource, Is.EqualTo("enterprise"));
                Assert.That(installed.CatalogProvenance!.EntryId, Is.EqualTo("private-provider"));
                Assert.That(installed.CatalogProvenance.CatalogSha256, Is.EqualTo(digest.ToLowerInvariant()));
                Assert.That(installed.CatalogProvenance.ArchiveSha256, Is.EqualTo(package.Entry.Sha256));
                Assert.That(installed.CatalogProvenance.TrustMode, Is.EqualTo(mode));
                Assert.That(installed.CatalogProvenance.SigningKeyId, Is.EqualTo(mode == "signed" ? "key-1" : null));
            });
            File.Delete(path);
            foreach (var unavailable in new[] { await loader.LoadAsync([source]), await loader.LoadAsync([]) })
            {
                var listed = new ConnectorService(new ProviderPackStore(root), unavailable, http).List().Single();
                Assert.Multiple(() =>
                {
                    Assert.That(listed.Active, Is.True);
                    Assert.That(listed.Listed, Is.False);
                    Assert.That(listed.CatalogProvenance, Is.EqualTo(installed.CatalogProvenance));
                });
            }
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".sig");
            File.Delete(publicKeyPath);
            if (Directory.Exists(statePath)) Directory.Delete(statePath, true);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Offline_ApprovedArchive_InstallsAndReinstallsWithoutHttp()
    {
        var root = CreateStorePath();
        var path = root + ".zip";
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        await File.WriteAllBytesAsync(path, package.Archive.ToArray());
        using var http = new HttpClient(new NoHttpHandler());
        IConnectorService service = new ConnectorService(new ProviderPackStore(root), new ProviderPackCatalog([package.Entry with { Aliases = ["pg"] }]), http);
        try
        {
            var first = await service.InstallOfflineAsync("pg", path);
            var second = await service.InstallOfflineAsync("postgresql-npgsql", path);
            Assert.Multiple(() =>
            {
                Assert.That(first.Active, Is.True);
                Assert.That(second.Active, Is.True);
                Assert.That(service.List(), Has.Count.EqualTo(1));
                Assert.That(Directory.GetFileSystemEntries(Path.Combine(root, ".staging")), Is.Empty);
            });
        }
        finally
        {
            File.Delete(path);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestCase("missing", "connector-archive-missing")]
    [TestCase("digest", "pack-integrity")]
    [TestCase("archive", "connector-archive-invalid")]
    [TestCase("metadata", "catalog-mismatch")]
    [TestCase("api", "api-incompatible")]
    [TestCase("tfm", "tfm-incompatible")]
    [TestCase("rid", "rid-incompatible")]
    [TestCase("unknown", "connector-not-curated")]
    public async Task Offline_Failure_PreservesActiveInstallation(string failure, string code)
    {
        var root = CreateStorePath();
        var path = root + ".zip";
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        var store = new ProviderPackStore(root);
        await store.InstallAsync(package.Entry, package.Archive);
        var originalActivation = await File.ReadAllTextAsync(Path.Combine(root, "active", "postgresql-npgsql.json"));
        var definition = package.Entry with
        {
            Version = "2.0.0",
            DubUrlApiRange = failure == "api" ? "[999.0.0,1000.0.0)" : package.Entry.DubUrlApiRange,
            TargetFrameworks = failure == "tfm" ? ["net99.0"] : package.Entry.TargetFrameworks,
            RuntimeIdentifiers = failure == "rid" ? ["unsupported-rid"] : [],
        };
        var upgrade = CreatePackage("postgresql-npgsql", "2.0.0", definition);
        var bytes = failure == "archive" ? Encoding.UTF8.GetBytes("invalid zip") : upgrade.Archive.ToArray();
        var entry = upgrade.Entry with
        {
            Sha256 = failure == "digest" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes)),
            Driver = failure == "metadata" ? "UnapprovedDriver" : definition.Driver,
        };
        if (failure != "missing") await File.WriteAllBytesAsync(path, bytes);
        using var http = new HttpClient(new NoHttpHandler());
        var service = new ConnectorService(store, new ProviderPackCatalog([entry]), http);
        try
        {
            var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () =>
                await service.InstallOfflineAsync(failure == "unknown" ? "unapproved" : entry.Provider, path));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo(code));
                Assert.That(store.Inspect().Single().Status, Is.EqualTo(ProviderPackStatus.Active));
                Assert.That(store.Inspect().Single().Manifest.Version, Is.EqualTo("1.0.0"));
                Assert.That(File.ReadAllText(Path.Combine(root, "active", "postgresql-npgsql.json")), Is.EqualTo(originalActivation));
                Assert.That(Directory.GetFileSystemEntries(Path.Combine(root, ".staging")), Is.Empty);
            });
        }
        finally
        {
            File.Delete(path);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task Mirror_ExactSelection_PreservesBytesAndMetadataAndInstallsWithPublicHttpBlocked()
    {
        var root = CreateStorePath();
        var output = root + "-mirror";
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        var bytes = package.Archive.ToArray();
        using var upstream = new HttpClient(new ArchiveHandler(bytes, package.Entry.DownloadUri!));
        try
        {
            await new ConnectorMirror(upstream).PrepareAsync(new ProviderPackCatalog([package.Entry]),
                [new(package.Entry.Provider, package.Entry.Version)], new Uri("https://internal.test/approved/"), output + Path.DirectorySeparatorChar);
            using var stream = File.OpenRead(Path.Combine(output, "connector-catalog.json"));
            var catalog = ProviderPackCatalog.Read(stream);
            var mirrored = catalog.Entries.Single();
            var archivePath = Path.Combine(output, mirrored.DownloadUri!.Segments.Last());
            Assert.Multiple(() =>
            {
                Assert.That(File.ReadAllBytes(archivePath), Is.EqualTo(bytes));
                Assert.That(JsonSerializer.Serialize(mirrored with { DownloadUri = package.Entry.DownloadUri }), Is.EqualTo(JsonSerializer.Serialize(package.Entry)));
                Assert.That(mirrored.DownloadUri.Host, Is.EqualTo("internal.test"));
            });
            using var internalHttp = new HttpClient(new ArchiveHandler(File.ReadAllBytes(archivePath), mirrored.DownloadUri));
            var service = new ConnectorService(new ProviderPackStore(root), catalog, internalHttp);
            Assert.That(service.List().Single().Listed, Is.True);
            Assert.That((await service.InstallAsync(package.Entry.Provider)).Active, Is.True);
            using var noHttp = new HttpClient(new NoHttpHandler());
            var offline = new ConnectorService(new ProviderPackStore(root), catalog, noHttp);
            Assert.That((await offline.InstallOfflineAsync(package.Entry.Provider, archivePath)).Active, Is.True);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }

    [TestCase("digest", "pack-integrity")]
    [TestCase("version", "mirror-selection-invalid")]
    [TestCase("url", "mirror-url-invalid")]
    [TestCase("credentials", "mirror-url-invalid")]
    [TestCase("metadata", "connector-catalog-invalid")]
    public async Task Mirror_InvalidInput_DoesNotPublish(string failure, string code)
    {
        var output = CreateStorePath();
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        var entry = package.Entry with
        {
            Sha256 = failure == "digest" ? new string('0', 64) : package.Entry.Sha256,
            TargetFrameworks = failure == "metadata" ? [] : package.Entry.TargetFrameworks,
        };
        using var upstream = new HttpClient(new ArchiveHandler(package.Archive.ToArray(), entry.DownloadUri!));
        var uri = failure switch
        {
            "url" => new Uri("http://internal.test/"),
            "credentials" => new Uri("https://user:secret@internal.test/"),
            _ => new Uri("https://internal.test/"),
        };
        var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await new ConnectorMirror(upstream).PrepareAsync(
            new ProviderPackCatalog([entry]), [new(entry.Provider, failure == "version" ? "9.0.0" : entry.Version)], uri, output));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Code, Is.EqualTo(code));
            Assert.That(Directory.Exists(output), Is.False);
            Assert.That(Directory.GetDirectories(Path.GetDirectoryName(output)!, Path.GetFileName(output) + ".*.staging"), Is.Empty);
            Assert.That(exception.ToString(), Does.Not.Contain("secret"));
        });
    }

    [Test]
    public async Task Offline_Cancellation_DoesNotOpenArchiveOrContactHttp()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var http = new HttpClient(new NoHttpHandler());
        var root = CreateStorePath();
        var service = new ConnectorService(new ProviderPackStore(root), new ProviderPackCatalog([CreateEntry()]), http);
        var exception = await Assert.ThrowsAsync<ConnectorOperationCanceledException>(async () =>
            await service.InstallOfflineAsync("pg", "missing.zip", cancellation.Token));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
            Assert.That(Directory.Exists(root), Is.False);
        });
    }

    [Test]
    public async Task Mirror_ExistingSnapshot_IsNeverOverwritten()
    {
        var output = CreateStorePath();
        Directory.CreateDirectory(output);
        var marker = Path.Combine(output, "connector-catalog.json");
        await File.WriteAllTextAsync(marker, "previous approved snapshot");
        using var http = new HttpClient(new NoHttpHandler());
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        try
        {
            var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () =>
                await new ConnectorMirror(http).PrepareAsync(new ProviderPackCatalog([package.Entry]),
                    [new(package.Entry.Provider, package.Entry.Version)], new Uri("https://internal.test/"), output));
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo("mirror-output-exists"));
                Assert.That(File.ReadAllText(marker), Is.EqualTo("previous approved snapshot"));
                Assert.That(Directory.GetFiles(output), Has.Length.EqualTo(1));
            });
        }
        finally { Directory.Delete(output, true); }
    }

    private sealed class NoHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new AssertionException("Offline installation attempted HTTP access.");
    }

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
        using var http = new HttpClient(new ArchiveHandler(package.Archive.ToArray(), package.Entry.DownloadUri!));
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

    [TestCase("postgresql-npgsql", "cockroachdb-npgsql", "timescale-npgsql")]
    [TestCase("postgresql-odbc", "questdb-odbc", "timescale-odbc")]
    [TestCase("mysql-mysqlconnector", "mariadb-mysqlconnector", "mysql")]
    [TestCase("mysql-odbc", "mariadb-odbc", "mysql-odbc")]
    public async Task ConsolidatedAliases_InstallOnceAndUninstallTheCanonicalArchive(string canonical, string first, string second)
    {
        var root = CreateStorePath();
        BundledProviderCatalog.Create().TryGet(canonical, out var definition);
        var package = CreatePackage(canonical, "0.0.0", definition);
        using var http = new HttpClient(new ArchiveHandler(package.Archive.ToArray(), package.Entry.DownloadUri!));
        var service = new ConnectorService(new ProviderPackStore(root), new ProviderPackCatalog([package.Entry]), http);
        try
        {
            var installed = await service.InstallAsync(first);
            var reinstalled = await service.InstallAsync(second);
            Assert.Multiple(() =>
            {
                Assert.That(installed.Id, Is.EqualTo(canonical));
                Assert.That(reinstalled.Id, Is.EqualTo(canonical));
                Assert.That(service.List(), Has.Count.EqualTo(1));
                Assert.That(Directory.GetDirectories(Path.Combine(root, "packs")), Has.Length.EqualTo(1));
            });
            Assert.That(service.Uninstall(first), Is.EqualTo(canonical));
            Assert.That(service.List().Single().Installed, Is.False);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [TestCase("archive", "connector-archive-invalid")]
    [TestCase("manifest", "connector-archive-invalid")]
    [TestCase("digest", "pack-integrity")]
    [TestCase("module", "assembly-missing")]
    [TestCase("api", "api-incompatible")]
    [TestCase("tfm", "tfm-incompatible")]
    [TestCase("rid", "rid-incompatible")]
    public async Task Install_InvalidOrIncompatibleArtifact_ReturnsStableDiagnostic(string failure, string code)
    {
        var root = CreateStorePath();
        var package = CreatePackage("postgresql-npgsql", "1.0.0");
        var bytes = package.Archive.ToArray();
        if (failure == "archive") bytes = Encoding.UTF8.GetBytes("not a zip");
        if (failure is "manifest" or "module")
        {
            using var modified = new MemoryStream();
            modified.Write(bytes);
            using (var zip = new ZipArchive(modified, ZipArchiveMode.Update, true))
            {
                var name = failure == "manifest" ? ConnectorDefinition.FileName : "module.dll";
                zip.GetEntry(name)!.Delete();
                if (failure == "manifest")
                {
                    using var stream = zip.CreateEntry(name).Open();
                    stream.Write(Encoding.UTF8.GetBytes("invalid"));
                }
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
                var manifestEntry = zip.GetEntry(ConnectorDefinition.FileName)!;
                ConnectorDefinition manifest;
                using (var stream = manifestEntry.Open())
                    manifest = JsonSerializer.Deserialize<ConnectorDefinition>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                manifestEntry.Delete();
                using var destination = zip.CreateEntry(ConnectorDefinition.FileName).Open();
                JsonSerializer.Serialize(destination, manifest with { RuntimeIdentifiers = ["unsupported-rid"] }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            bytes = modified.ToArray();
            entry = entry with { Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        }
        using var http = new HttpClient(new ArchiveHandler(bytes, entry.DownloadUri!));
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

    [TestCase("unknown")]
    [TestCase("https://user:password@example.test/package.zip?token=secret")]
    [TestCase("arbitrary-package-id")]
    public async Task Install_UnknownConnector_DoesNotDownload(string requested)
    {
        var service = CreateService(CreateStorePath());
        var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await service.InstallAsync(requested));
        Assert.That(exception!.Code, Is.EqualTo("connector-not-curated"));
        Assert.That(exception.ToString(), Does.Not.Contain("password").And.Not.Contain("secret"));
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
        using var http = new HttpClient(new ArchiveHandler(package.Archive.ToArray(), package.Entry.DownloadUri!));
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

    private static ConnectorDefinition CreateEntry()
        => CreateManifest() with
        {
            DownloadUri = new Uri("https://example.test/postgresql-npgsql.zip"),
            Sha256 = new string('0', 64), Aliases = ["postgresql", "pg", "npgsql"],
        };

    private static ConnectorDefinition CreateManifest()
        => new()
        {
            Provider = "postgresql-npgsql",
            Version = "1.0.0",
            DubUrlApiRange = "*",
            Assembly = "connector.dll", Driver = "Fixture",
            ImplementationAssemblies = ["connector.dll"], DriverAssembly = "connector.dll",
            Databases = [new("Fixture", ["postgresql", "postgres", "pg"])],
            TargetFrameworks = ["net10.0"],
            RuntimeIdentifiers = [],
        };

    private static string CreateStorePath()
        => Path.Combine(TestContext.CurrentContext.WorkDirectory, $"connector-store-{Guid.NewGuid():N}");

    private static Package CreatePackage(string provider, string version, ConnectorDefinition? definition = null)
    {
        var assetBytes = Encoding.UTF8.GetBytes("fake connector module");
        var manifest = definition ?? new ConnectorDefinition
        {
            Provider = provider,
            Version = version,
            DubUrlApiRange = "[0.0.0,999.0.0)",
            Assembly = "module.dll", Driver = "Fixture",
            ImplementationAssemblies = ["module.dll"], DriverAssembly = "module.dll",
            Databases = [new("Fixture", ["postgresql"])],
            TargetFrameworks = [CurrentTargetFramework()],
            RuntimeIdentifiers = [],
        };
        var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = zip.CreateEntry(ConnectorDefinition.FileName);
            using (var stream = manifestEntry.Open())
                JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            foreach (var name in manifest.ImplementationAssemblies.Append(manifest.Assembly)
                .Append(manifest.DriverAssembly ?? manifest.Driver + ".dll").Distinct())
            {
                using var module = zip.CreateEntry(name).Open();
                module.Write(assetBytes);
            }
        }
        archive.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(archive));
        archive.Position = 0;
        return new(manifest with { DownloadUri = new Uri($"https://example.test/{provider}-{version}.zip"), Sha256 = hash }, archive);
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

    private sealed record Package(ConnectorDefinition Entry, MemoryStream Archive);

    private sealed class UnavailableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("https://user:password@example.test/?token=secret");
    }
}
