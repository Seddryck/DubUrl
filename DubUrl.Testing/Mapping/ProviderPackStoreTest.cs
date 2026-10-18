using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
using DubUrl.ProviderPack.TestingModule;
using NUnit.Framework;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DubUrl.Testing.Mapping;

public class ProviderPackStoreTest
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "provider-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public async Task Install_ActivatesVerifiedPack_AndIsIdempotent()
    {
        var package = CreatePackage("duckdb", "1.0.0");
        var store = CreateStore();

        var first = await store.InstallAsync(package.Entry, package.Archive);
        package.Archive.Position = 0;
        var second = await store.InstallAsync(package.Entry, package.Archive);

        Assert.Multiple(() =>
        {
            Assert.That(first.Status, Is.EqualTo(ProviderPackStatus.Active));
            Assert.That(second.Status, Is.EqualTo(ProviderPackStatus.Active));
            Assert.That(store.Inspect(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Install_NewVersion_PreservesVersionsAndActivatesOnlyNewest()
    {
        var first = CreatePackage("duckdb", "1.0.0");
        var second = CreatePackage("duckdb", "2.0.0");
        var store = CreateStore();

        await store.InstallAsync(first.Entry, first.Archive);
        await store.InstallAsync(second.Entry, second.Archive);

        var packs = store.Inspect();
        Assert.Multiple(() =>
        {
            Assert.That(packs, Has.Count.EqualTo(2));
            Assert.That(packs.Count(pack => pack.Status == ProviderPackStatus.Active), Is.EqualTo(1));
            Assert.That(packs.Single(pack => pack.Status == ProviderPackStatus.Active).Manifest.Version, Is.EqualTo("2.0.0"));
        });
    }

    [Test]
    public async Task Inspect_CorruptPack_ReportsDiagnosticWithoutHidingValidPack()
    {
        var valid = CreatePackage("valid", "1.0.0");
        var corrupt = CreatePackage("corrupt", "1.0.0");
        var store = CreateStore();
        await store.InstallAsync(valid.Entry, valid.Archive);
        var installed = await store.InstallAsync(corrupt.Entry, corrupt.Archive);
        await File.AppendAllTextAsync(Path.Combine(installed.InstallPath, "lib", "module.dll"), "tampered");

        var packs = store.Inspect();
        Assert.Multiple(() =>
        {
            Assert.That(packs.Single(pack => pack.Manifest.Provider == "valid").Status, Is.EqualTo(ProviderPackStatus.Active));
            Assert.That(packs.Single(pack => pack.Manifest.Provider == "corrupt").Status, Is.EqualTo(ProviderPackStatus.Corrupt));
            Assert.That(packs.Single(pack => pack.Manifest.Provider == "corrupt").Diagnostics[0].Code, Is.EqualTo("asset-integrity"));
        });
    }

    [Test]
    public async Task Install_IncompatibleApi_LeavesStoreUnchanged()
    {
        var package = CreatePackage("future", "1.0.0", "[3.0,4.0)");
        var store = CreateStore();

        var exception = await Assert.ThrowsAsync<ProviderPackException>(
            async () => await store.InstallAsync(package.Entry, package.Archive));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Code, Is.EqualTo("api-incompatible"));
            Assert.That(store.Inspect(), Is.Empty);
        });
    }

    [Test]
    public async Task Remove_InstalledProvider_IsDeterministic()
    {
        var package = CreatePackage("duckdb", "1.0.0");
        var store = CreateStore();
        await store.InstallAsync(package.Entry, package.Archive);

        Assert.Multiple(() =>
        {
            Assert.That(store.Remove("duckdb"), Is.True);
            Assert.That(store.Remove("duckdb"), Is.False);
            Assert.That(store.Inspect(), Is.Empty);
        });
    }

    [Test]
    public async Task Install_ArchiveDigestMismatch_IsRejected()
    {
        var package = CreatePackage("duckdb", "1.0.0");
        var invalidEntry = package.Entry with { Sha256 = new string('0', 64) };

        var exception = await Assert.ThrowsAsync<ProviderPackException>(
            async () => await CreateStore().InstallAsync(invalidEntry, package.Archive));

        Assert.That(exception!.Code, Is.EqualTo("pack-integrity"));
    }

    [Test]
    public async Task InstalledModuleLoader_LoadsActivePackThroughModuleContract()
    {
        var assemblyPath = typeof(TestProviderModule).Assembly.Location;
        var package = CreatePackage(
            "testing",
            "1.0.0",
            assetBytes: await File.ReadAllBytesAsync(assemblyPath),
            moduleType: typeof(TestProviderModule).FullName!);
        var store = CreateStore();
        await store.InstallAsync(package.Entry, package.Archive);

        var diagnostics = new List<ProviderPackDiagnostic>();
        using var loader = new InstalledProviderModuleLoader();
        var modules = loader.LoadActive(store, diagnostics);

        Assert.Multiple(() =>
        {
            Assert.That(modules, Has.Count.EqualTo(1));
            Assert.That(modules[0].GetType().FullName, Is.EqualTo(typeof(TestProviderModule).FullName));
            Assert.That(diagnostics, Is.Empty);
        });
        modules = [];
        loader.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private ProviderPackStore CreateStore()
        => new(_root, new Version(1, 5), "net10.0", "win-x64");

    private static Package CreatePackage(
        string provider,
        string version,
        string apiRange = "[1.0,2.0)",
        byte[]? assetBytes = null,
        string moduleType = "Example.ProviderModule")
    {
        assetBytes ??= Encoding.UTF8.GetBytes($"fake assembly for {provider} {version}");
        var assetHash = Convert.ToHexString(SHA256.HashData(assetBytes));
        var manifest = new ProviderPackManifest
        {
            Provider = provider,
            Version = version,
            DubUrlApiRange = apiRange,
            Module = new() { Assembly = "lib/module.dll", Type = moduleType },
            Schemes = [provider],
            TargetFrameworks = ["net10.0"],
            RuntimeIdentifiers = ["win-x64"],
            Assets =
            [
                new()
                {
                    Path = "lib/module.dll",
                    Sha256 = assetHash,
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
            var assetEntry = zip.CreateEntry("lib/module.dll");
            using var assetStream = assetEntry.Open();
            assetStream.Write(assetBytes);
        }
        archive.Position = 0;
        var archiveHash = Convert.ToHexString(SHA256.HashData(archive));
        archive.Position = 0;
        return new(new(provider, version, new Uri($"https://packages.example/{provider}/{version}.zip"), archiveHash), archive);
    }

    private sealed record Package(ProviderPackCatalogEntry Entry, MemoryStream Archive);
}
