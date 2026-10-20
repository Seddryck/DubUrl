using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;
using DubUrl.ProviderPack.TestingModule;
using NUnit.Framework;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DubUrl.Core.Testing.Mapping;

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
    public async Task Install_Reinstall_RestoresVerifiedArchiveWithoutHashingInstalledFiles()
    {
        var package = CreatePackage("postgresql", "1.0.0");
        var store = CreateStore();
        var installed = await store.InstallAsync(package.Entry, package.Archive);
        var module = Path.Combine(installed.InstallPath, "lib", "module.dll");
        var original = await File.ReadAllBytesAsync(module);
        await File.WriteAllTextAsync(module, "modified locally");
        var extra = Path.Combine(installed.InstallPath, "extra.dll");
        await File.WriteAllTextAsync(extra, "untracked local file");
        package.Archive.Position = 0;

        var restored = await store.InstallAsync(package.Entry, package.Archive);

        Assert.Multiple(() =>
        {
            Assert.That(restored.Status, Is.EqualTo(ProviderPackStatus.Active));
            Assert.That(File.ReadAllBytes(module), Is.EqualTo(original));
            Assert.That(File.Exists(extra), Is.False);
            Assert.That(Directory.GetFileSystemEntries(Path.Combine(_root, ".staging")), Is.Empty);
        });
    }

    [Test]
    public async Task Inspect_InvalidDefinition_ReportsDiagnosticWithoutHidingValidPack()
    {
        var valid = CreatePackage("valid", "1.0.0");
        var corrupt = CreatePackage("corrupt", "1.0.0");
        var store = CreateStore();
        await store.InstallAsync(valid.Entry, valid.Archive);
        var installed = await store.InstallAsync(corrupt.Entry, corrupt.Archive);
        await File.WriteAllTextAsync(Path.Combine(installed.InstallPath, ConnectorDefinition.FileName), "invalid JSON");

        var packs = store.Inspect();
        Assert.Multiple(() =>
        {
            Assert.That(packs.Single(pack => pack.Manifest.Provider == "valid").Status, Is.EqualTo(ProviderPackStatus.Active));
            Assert.That(packs.Single(pack => pack.Manifest.Provider == "corrupt").Status, Is.EqualTo(ProviderPackStatus.Corrupt));
            Assert.That(packs.Single(pack => pack.Manifest.Provider == "corrupt").Diagnostics[0].Code, Is.EqualTo("manifest-invalid"));
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
    public async Task InstalledLoader_DiscoversAttributesWithoutAnEntryPointClass()
    {
        var assemblyPath = typeof(FixtureMapper).Assembly.Location;
        var package = CreatePackage(
            "testing",
            "1.0.0",
            assetBytes: await File.ReadAllBytesAsync(assemblyPath));
        var store = CreateStore();
        await store.InstallAsync(package.Entry, package.Archive);

        var diagnostics = new List<ProviderPackDiagnostic>();
        using var loader = new InstalledProviderModuleLoader();
        var modules = loader.LoadActive(store, diagnostics);

        Assert.Multiple(() =>
        {
            Assert.That(modules, Has.Count.EqualTo(1));
            Assert.That(modules[0], Is.TypeOf<AttributeProviderModule>());
            var registry = new ProviderCatalogBuilder().AddModule(modules[0]).Build();
            Assert.That(registry.GetMapper("testing").GetType().Name, Is.EqualTo(nameof(FixtureMapper)));
            Assert.That(registry.GetProviderFactory(["testing"]).GetType().Name, Is.EqualTo(nameof(FixtureFactory)));
            Assert.That(diagnostics, Is.Empty);
        });
        modules = [];
        loader.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private ProviderPackStore CreateStore()
        => new(_root, new Version(1, 5), "net10.0", "win-x64");

    [Test]
    public async Task Install_ExtractsDependenciesAndContent_WithoutAnInventory()
    {
        var package = RewriteArchive(CreatePackage("postgresql", "1.0.0"), zip =>
        {
            foreach (var name in new[] { "lib/module.deps.json", "lib/dependency.dll", "runtimes/win-x64/native/example.dll", "NOTICE.md" })
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(name);
            }
            zip.CreateEntry("empty/");
        });
        var installed = await CreateStore().InstallAsync(package.Entry, package.Archive);

        Assert.Multiple(() =>
        {
            Assert.That(installed.Status, Is.EqualTo(ProviderPackStatus.Active));
            Assert.That(File.ReadAllText(Path.Combine(installed.InstallPath, "lib", "module.deps.json")), Is.EqualTo("lib/module.deps.json"));
            Assert.That(File.ReadAllText(Path.Combine(installed.InstallPath, "lib", "dependency.dll")), Is.EqualTo("lib/dependency.dll"));
            Assert.That(File.ReadAllText(Path.Combine(installed.InstallPath, "runtimes", "win-x64", "native", "example.dll")), Is.EqualTo("runtimes/win-x64/native/example.dll"));
            Assert.That(File.Exists(Path.Combine(installed.InstallPath, "NOTICE.md")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(installed.InstallPath, "empty")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(installed.InstallPath, ConnectorDefinition.FileName)), Does.Not.Contain("\"assets\""));
        });
    }

    [TestCase("../escape.txt")]
    [TestCase("..\\escape.txt")]
    [TestCase("/absolute.txt")]
    [TestCase("C:/escape.txt")]
    [TestCase("folder//file.txt")]
    [TestCase("folder/../escape.txt")]
    [TestCase("module.dll:stream")]
    [TestCase("folder/file.")]
    [TestCase("folder/file ")]
    [TestCase("lib/module.dll")]
    [TestCase("lib\\module.dll")]
    public async Task Install_UnsafeOrDuplicateArchivePath_IsRejectedBeforeActivation(string path)
    {
        var package = RewriteArchive(CreatePackage("postgresql", "1.0.0"), zip => zip.CreateEntry(path));
        var store = CreateStore();

        var error = await Assert.ThrowsAsync<ProviderPackException>(async () => await store.InstallAsync(package.Entry, package.Archive));

        Assert.Multiple(() =>
        {
            Assert.That(error!.Code, Is.EqualTo("path-invalid"));
            Assert.That(store.Inspect(), Is.Empty);
            Assert.That(Directory.GetFileSystemEntries(Path.Combine(_root, ".staging")), Is.Empty);
            Assert.That(File.Exists(Path.Combine(_root, ".staging", "escape.txt")), Is.False);
        });
    }

    [Test]
    public async Task Install_SymbolicLink_IsRejected()
    {
        var package = RewriteArchive(CreatePackage("postgresql", "1.0.0"), zip =>
        {
            var link = zip.CreateEntry("link");
            link.ExternalAttributes = unchecked((int)0xA1FF0000);
            using var writer = new StreamWriter(link.Open());
            writer.Write("../escape");
        });
        var error = await Assert.ThrowsAsync<ProviderPackException>(async () => await CreateStore().InstallAsync(package.Entry, package.Archive));
        Assert.That(error!.Code, Is.EqualTo("path-invalid"));
    }

    [Test]
    public async Task Install_MissingModule_IsRejected()
    {
        var package = RewriteArchive(CreatePackage("postgresql", "1.0.0"), zip => zip.GetEntry("lib/module.dll")!.Delete());
        var store = CreateStore();
        var error = await Assert.ThrowsAsync<ProviderPackException>(async () => await store.InstallAsync(package.Entry, package.Archive));
        Assert.Multiple(() =>
        {
            Assert.That(error!.Code, Is.EqualTo("assembly-missing"));
            Assert.That(store.Inspect(), Is.Empty);
        });
    }

    [Test]
    public async Task Install_ModifiedArchive_WithOriginalDigest_IsRejected()
    {
        var original = CreatePackage("postgresql", "1.0.0");
        var modified = RewriteArchive(original, zip => zip.CreateEntry("extra.dll"));
        var store = CreateStore();
        var error = await Assert.ThrowsAsync<ProviderPackException>(async () => await store.InstallAsync(original.Entry, modified.Archive));
        Assert.Multiple(() =>
        {
            Assert.That(error!.Code, Is.EqualTo("pack-integrity"));
            Assert.That(store.Inspect(), Is.Empty);
            Assert.That(Directory.GetFileSystemEntries(Path.Combine(_root, ".staging")), Is.Empty);
        });
    }

    [Test]
    public async Task Inspect_DoesNotHashInstalledFiles_ButDetectsMissingModule()
    {
        var package = CreatePackage("postgresql", "1.0.0");
        var store = CreateStore();
        var installed = await store.InstallAsync(package.Entry, package.Archive);
        var module = Path.Combine(installed.InstallPath, "lib", "module.dll");
        await File.AppendAllTextAsync(module, "changed after installation");
        Assert.That(store.Inspect().Single().Status, Is.EqualTo(ProviderPackStatus.Active));

        File.Delete(module);
        var inspected = store.Inspect().Single();
        Assert.Multiple(() =>
        {
            Assert.That(inspected.Status, Is.EqualTo(ProviderPackStatus.Incomplete));
            Assert.That(inspected.Diagnostics.Single().Code, Is.EqualTo("assembly-missing"));
        });
    }

    [TestCase("assembly")]
    [TestCase("implementation")]
    [TestCase("driver")]
    public async Task Install_AssemblyPathsCannotEscapeTheConnector(string field)
    {
        var original = CreatePackage("testing", "1.0.0");
        var definition = field switch
        {
            "assembly" => original.Entry with { Assembly = "../outside.dll" },
            "implementation" => original.Entry with { ImplementationAssemblies = ["../outside.dll"] },
            _ => original.Entry with { DriverAssembly = "../outside.dll" }
        };
        var package = RewriteArchive(original, zip =>
        {
            zip.GetEntry(ConnectorDefinition.FileName)!.Delete();
            using var stream = zip.CreateEntry(ConnectorDefinition.FileName).Open();
            JsonSerializer.Serialize(stream, definition, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        });
        var error = await Assert.ThrowsAsync<ProviderPackException>(async () => await CreateStore().InstallAsync(package.Entry, package.Archive));
        Assert.That(error!.Code, Is.EqualTo("path-invalid"));
    }

    private static Package RewriteArchive(Package original, Action<ZipArchive> edit)
    {
        var archive = new MemoryStream();
        archive.Write(original.Archive.ToArray());
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Update, leaveOpen: true))
            edit(zip);
        archive.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(archive));
        archive.Position = 0;
        return new(original.Entry with { Sha256 = hash }, archive);
    }

    [TestCase("assembly")]
    [TestCase("implementations")]
    [TestCase("driver")]
    [TestCase("driverAssembly")]
    [TestCase("database")]
    [TestCase("schemes")]
    [TestCase("api")]
    public async Task Install_DefinitionDiffersFromCatalog_LeavesStoreUnchanged(string field)
    {
        var package = CreatePackage("postgresql", "1.0.0");
        var entry = field switch
        {
            "assembly" => package.Entry with { Assembly = "Other.dll" },
            "implementations" => package.Entry with { ImplementationAssemblies = ["Other.dll"] },
            "driver" => package.Entry with { Driver = "Other" },
            "driverAssembly" => package.Entry with { DriverAssembly = "Other.dll" },
            "database" => package.Entry with { DatabaseName = "Other" },
            "schemes" => package.Entry with { Schemes = ["other"] },
            _ => package.Entry with { DubUrlApiRange = "[1.0,3.0)" },
        };
        var store = CreateStore();
        var error = await Assert.ThrowsAsync<ProviderPackException>(async () => await store.InstallAsync(entry, package.Archive));
        Assert.Multiple(() =>
        {
            Assert.That(error!.Code, Is.EqualTo("catalog-mismatch"));
            Assert.That(store.Inspect(), Is.Empty);
        });
    }

    private static Package CreatePackage(
        string provider,
        string version,
        string apiRange = "[1.0,2.0)",
        byte[]? assetBytes = null)
    {
        assetBytes ??= Encoding.UTF8.GetBytes($"fake assembly for {provider} {version}");
        var manifest = new ConnectorDefinition
        {
            Provider = provider,
            Version = version,
            DubUrlApiRange = apiRange,
            Assembly = "lib/module.dll", DatabaseName = "Fixture", Driver = "Fixture",
            ImplementationAssemblies = ["lib/module.dll"], DriverAssembly = "lib/module.dll",
            Schemes = [provider],
            TargetFrameworks = ["net10.0"],
            RuntimeIdentifiers = ["win-x64"],
        };

        var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = zip.CreateEntry(ConnectorDefinition.FileName);
            using (var stream = manifestEntry.Open())
                JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var assetEntry = zip.CreateEntry("lib/module.dll");
            using var assetStream = assetEntry.Open();
            assetStream.Write(assetBytes);
        }
        archive.Position = 0;
        var archiveHash = Convert.ToHexString(SHA256.HashData(archive));
        archive.Position = 0;
        return new(manifest with { DownloadUri = new Uri($"https://packages.example/{provider}/{version}.zip"), Sha256 = archiveHash }, archive);
    }

    private sealed record Package(ConnectorDefinition Entry, MemoryStream Archive);
}
