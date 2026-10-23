using DubUrl.Mapping;
using DubUrl.Provider.DuckDb.Adbc;
using DubUrl.Provider.DuckDb.Adbc.Drivers.Discovery;
using System.Data;
using System.Data.Common;
using System.Runtime.InteropServices;
using Apache.Arrow.Adbc;
using Moq;
using NUnit.Framework;

namespace DubUrl.Adbc.Testing;

[NonParallelizable]
public class DuckdbDriverDiscoveryTests
{
    private string root = null!;
    private string? previousDriverPath;
    private const string Provider = "DubUrl.Provider.DuckDb.Adbc";

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"discovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        previousDriverPath = Environment.GetEnvironmentVariable("ADBC_DRIVER_PATH");
    }

    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", previousDriverPath);
        DbProviderFactories.UnregisterFactory(Provider);
        var parent = Path.GetFullPath(TestContext.CurrentContext.WorkDirectory) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(root).StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected test directory.");
        Directory.Delete(root, recursive: true);
    }

    private string DirectoryWith(string name, string filename)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, filename), "test placeholder");
        return path;
    }

    private DuckdbDriverDiscovery Discovery(DriverPlatform platform,
        Func<string, string?>? variable = null,
        Func<bool, DriverLocation?>? registry = null,
        Func<DriverLocation, AdbcDriver>? load = null,
        Func<string?>? native = null)
        => new(platform, variable ?? (_ => null), _ => root,
            registry ?? (_ => null), load ?? (_ => new Mock<AdbcDriver>().Object), native ?? (() => null));

    [Test]
    public void EnvironmentDirectories_AreSearchedInOrderBeforeRegistry()
    {
        var first = DirectoryWith("first", "duckdb.dll");
        var second = DirectoryWith("second", "duckdb.dll");
        DriverLocation? selected = null;
        using var driver = Discovery(DriverPlatform.Windows,
            variable: name => name == "ADBC_DRIVER_PATH" ? first + ";" + second : null,
            registry: _ => throw new AssertionException("Registry should not be searched after environment match"),
            load: location => { selected = location; return new Mock<AdbcDriver>().Object; }).Load();
        Assert.That(selected, Is.EqualTo(new DriverLocation(Path.Combine(first, "duckdb.dll"), null)));
    }

    [Test]
    public void EnvironmentDirectories_SkipMissingCandidates()
    {
        var second = DirectoryWith("second", "duckdb.dll");
        DriverLocation? selected = null;
        using var driver = Discovery(DriverPlatform.Windows,
            variable: _ => Path.Combine(root, "absent") + ";" + second,
            load: location => { selected = location; return new Mock<AdbcDriver>().Object; }).Load();
        Assert.That(selected!.Path, Is.EqualTo(Path.Combine(second, "duckdb.dll")));
    }

    [Test]
    public void Directory_ManifestTakesPrecedenceOverBareLibrary()
    {
        var path = DirectoryWith("drivers", "duckdb.dll");
        File.WriteAllText(Path.Combine(path, "duckdb.toml"), "placeholder");
        DriverLocation? selected = null;
        using var driver = Discovery(DriverPlatform.Windows,
            variable: _ => path,
            load: location => { selected = location; return new Mock<AdbcDriver>().Object; }).Load();
        Assert.That(selected, Is.EqualTo(new DriverLocation(Path.Combine(path, "duckdb.toml"), null)));
    }

    [Test]
    public void Windows_UserRegistryPrecedesUserDirectoryAndMachineRegistry()
    {
        var local = Path.Combine(root, "ADBC", "Drivers");
        Directory.CreateDirectory(local);
        File.WriteAllText(Path.Combine(local, "duckdb.dll"), "placeholder");
        var expected = new DriverLocation(Path.Combine(root, "registered.dll"), "registered_entrypoint");
        DriverLocation? selected = null;
        using var driver = Discovery(DriverPlatform.Windows,
            registry: machine => machine ? throw new AssertionException("HKLM should not be searched") : expected,
            load: location => { selected = location; return new Mock<AdbcDriver>().Object; }).Load();
        Assert.That(selected, Is.SameAs(expected));
    }

    [Test]
    public void Windows_UserDirectoryPrecedesMachineRegistry()
    {
        var local = Path.Combine(root, "ADBC", "Drivers");
        Directory.CreateDirectory(local);
        File.WriteAllText(Path.Combine(local, "duckdb.toml"), "placeholder");
        DriverLocation? selected = null;
        using var driver = Discovery(DriverPlatform.Windows,
            registry: machine => machine ? throw new AssertionException("HKLM should not be searched") : null,
            load: location => { selected = location; return new Mock<AdbcDriver>().Object; }).Load();
        Assert.That(selected!.Path, Is.EqualTo(Path.Combine(local, "duckdb.toml")));
    }

    [Test]
    public void Windows_MachineRegistryPrecedesPlatformLookup()
    {
        var expected = new DriverLocation(Path.Combine(root, "machine.dll"), "machine_entrypoint");
        using var driver = Discovery(DriverPlatform.Windows,
            registry: machine => machine ? expected : null,
            native: () => throw new AssertionException("Platform lookup should not run")).Load();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Linux_UsesXdgOrHomeConfigurationDirectory(bool xdg)
    {
        var directory = Path.Combine(root, xdg ? "adbc" : ".config", xdg ? "drivers" : "adbc");
        if (!xdg)
            directory = Path.Combine(directory, "drivers");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "libduckdb.so"), "placeholder");
        DriverLocation? selected = null;
        using var driver = Discovery(DriverPlatform.Linux,
            variable: key => xdg && key == "XDG_CONFIG_HOME" ? root : null,
            load: location => { selected = location; return new Mock<AdbcDriver>().Object; }).Load();
        Assert.That(selected!.Path, Is.EqualTo(Path.Combine(directory, "libduckdb.so")));
    }

    [Test]
    public void MacOS_UsesApplicationSupportDirectoryAndDylib()
    {
        var directory = Path.Combine(root, "Library", "Application Support", "ADBC", "Drivers");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "libduckdb.dylib"), "placeholder");
        DriverLocation? selected = null;
        using var driver = Discovery(DriverPlatform.MacOS,
            load: location => { selected = location; return new Mock<AdbcDriver>().Object; }).Load();
        Assert.That(selected!.Path, Is.EqualTo(Path.Combine(directory, "libduckdb.dylib")));
    }

    [Test]
    public void Windows_PlatformPathDirectory_IsSearchedAfterConfiguration()
    {
        var directory = DirectoryWith("path", "duckdb.dll");
        DriverLocation? selected = null;
        using var driver = Discovery(DriverPlatform.Windows,
            variable: key => key == "PATH" ? directory : null,
            load: location => { selected = location; return new Mock<AdbcDriver>().Object; },
            native: () => throw new AssertionException("Native lookup should not be needed")).Load();
        Assert.That(selected!.Path, Is.EqualTo(Path.Combine(directory, "duckdb.dll")));
    }

    [Test]
    public void MissingDriver_ReportsMechanismsAndRemediationWithoutEnvironmentContents()
    {
        const string sensitive = "sensitive-environment-sentinel";
        var exception = Assert.Throws<InvalidOperationException>(() => Discovery(DriverPlatform.Windows,
            variable: _ => Path.Combine(root, sensitive)).Load())!;
        Assert.That(exception.Message, Does.Contain("ADBC_DRIVER_PATH").And.Contain("HKCU").And.Contain("HKLM")
            .And.Contain("platform shared-library").And.Contain("FromNative"));
        Assert.That(exception.ToString(), Does.Not.Contain(sensitive));
        Assert.That(exception.InnerException, Is.Null);
    }

    [Test]
    public void SelectedDriverLoadFailure_DoesNotFallThroughOrExposeLoaderMessage()
    {
        var directory = DirectoryWith("broken", "duckdb.dll");
        var exception = Assert.Throws<InvalidOperationException>(() => Discovery(DriverPlatform.Windows,
            variable: _ => directory,
            load: _ => throw new DllNotFoundException("sensitive-loader-sentinel"),
            native: () => throw new AssertionException("Must not fall through")).Load())!;
        Assert.That(exception.ToString(), Does.Not.Contain("sensitive-loader-sentinel"));
        Assert.That(exception.Message, Does.Contain("DllNotFoundException"));
    }

    private static string LibraryPath
    {
        get
        {
            var name = OperatingSystem.IsWindows() ? "duckdb.dll" : OperatingSystem.IsMacOS() ? "libduckdb.dylib" : "libduckdb.so";
            return Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", name);
        }
    }

    private string WriteManifest(string? entryPoint)
    {
        var table = new Tomlyn.Model.TomlTable { ["shared"] = LibraryPath };
        if (entryPoint is not null)
            table["entrypoint"] = entryPoint;
        var model = new Tomlyn.Model.TomlTable { ["manifest_version"] = 1L, ["Driver"] = table };
        File.WriteAllText(Path.Combine(root, "duckdb.toml"), Tomlyn.TomlSerializer.Serialize(model));
        return root;
    }

    [TestCase(null)]
    [TestCase("duckdb_adbc_init")]
    public void NativeManifest_UsesMetadataOrDuckdbDefaultEntryPoint(string? entryPoint)
    {
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", WriteManifest(entryPoint));
        DbProviderFactories.RegisterFactory(Provider, DuckdbAdbcFactory.Instance);
        DefaultRegistry.Refresh();
        Assert.That(new DatabaseUrl("adbc+duckdb://").ReadScalar<long>("SELECT 42::BIGINT"), Is.EqualTo(42));
    }

    [Test]
    public void NativeManifest_InvalidMetadataEntryPointIsNotReplacedByDefault()
    {
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", WriteManifest("sensitive-invalid-entrypoint"));
        DbProviderFactories.RegisterFactory(Provider, DuckdbAdbcFactory.Instance);
        DefaultRegistry.Refresh();
        var exception = Assert.Throws<InvalidOperationException>(() => new ConnectionUrl("adbc+duckdb://").Open())!;
        Assert.That(exception.ToString(), Does.Not.Contain("sensitive-invalid-entrypoint"));
        Assert.That(exception.Message, Does.Contain("Could not load the DuckDB ADBC driver"));
    }

    [Test]
    public void NativeManifest_ParseFailure_IsActionableAndDoesNotExposeManifestText()
    {
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", root);
        File.WriteAllText(Path.Combine(root, "duckdb.toml"), "invalid = ??? sensitive-manifest-sentinel");
        DbProviderFactories.RegisterFactory(Provider, DuckdbAdbcFactory.Instance);
        DefaultRegistry.Refresh();
        var exception = Assert.Throws<InvalidOperationException>(() => new ConnectionUrl("adbc+duckdb://").Open())!;
        Assert.That(exception.ToString(), Does.Not.Contain("sensitive-manifest-sentinel"));
        Assert.That(exception.Message, Does.Contain("ADBC_DRIVER_PATH").And.Contain("Check the selected manifest"));
    }

    [Test]
    public void NativeBareLibrary_DiscoveredFromEnvironmentDirectory()
    {
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", Path.GetDirectoryName(LibraryPath));
        DbProviderFactories.RegisterFactory(Provider, DuckdbAdbcFactory.Instance);
        DefaultRegistry.Refresh();
        Assert.That(new DatabaseUrl("adbc+duckdb://").ReadScalar<long>("SELECT 42::BIGINT"), Is.EqualTo(42));
    }

    [Test]
    public void Singleton_DefersDiscoveryUntilOpen_ForBothApis()
    {
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", Path.Combine(root, "not-installed"));
        DbProviderFactories.RegisterFactory(Provider, DuckdbAdbcFactory.Instance);
        DefaultRegistry.Refresh();
        using var connection = new ConnectionUrl("adbc+duckdb://").Connect();
        var nativeUrl = new AdbcConnectionUrl("adbc+duckdb://");
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", WriteManifest("duckdb_adbc_init"));
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 42::BIGINT";
        Assert.That(command.ExecuteScalar(), Is.EqualTo(42));
        using var session = nativeUrl.Open();
        using var statement = session.Connection.CreateStatement();
        statement.SqlQuery = "SELECT 42::BIGINT";
        using var stream = statement.ExecuteQuery().Stream!;
        Assert.That(stream.Schema.FieldsList, Has.Count.EqualTo(1));
    }

    [Test]
    public void ExplicitNativeOverride_BypassesBrokenDiscovery()
    {
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", root);
        File.WriteAllText(Path.Combine(root, "duckdb.toml"), "invalid = ??? sensitive-sentinel");
        DbProviderFactories.RegisterFactory(Provider, AdbcProviderFactory.FromNative(LibraryPath, "duckdb_adbc_init"));
        DefaultRegistry.Refresh();
        Assert.That(new DatabaseUrl("adbc+duckdb://").ReadScalar<long>("SELECT 42::BIGINT"), Is.EqualTo(42));
    }

    [Test]
    public void NativeDiscovery_DisposalReleasesFileDatabaseForBothApis()
    {
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", WriteManifest(null));
        DbProviderFactories.RegisterFactory(Provider, DuckdbAdbcFactory.Instance);
        DefaultRegistry.Refresh();
        var file = Path.Combine(root, "discovered.db");
        var url = $"adbc+duckdb:////{file.Replace('\\', '/')}";
        using (var session = new AdbcConnectionUrl(url).Open())
            Assert.That(session.Connection, Is.Not.Null);
        Assert.That(new DatabaseUrl(url).ReadScalar<long>("SELECT 42::BIGINT"), Is.EqualTo(42));
        File.Delete(file);
        Assert.That(File.Exists(file), Is.False);
    }

    [Test]
    public void NativePlatformLookup_ReturnsLoadableDriver()
    {
        using var driver = Discovery(
            OperatingSystem.IsWindows() ? DriverPlatform.Windows : OperatingSystem.IsMacOS() ? DriverPlatform.MacOS : DriverPlatform.Linux,
            load: DuckdbDriverDiscovery.LoadLocation, native: DuckdbNativeLibrary.Find).Load();
        using var database = driver.Open(new Dictionary<string, string> { ["path"] = ":memory:" });
        using var connection = database.Connect(null);
        using var statement = connection.CreateStatement();
        statement.SqlQuery = "SELECT 42::BIGINT";
        using var stream = statement.ExecuteQuery().Stream!;
        Assert.That(stream.Schema.FieldsList, Has.Count.EqualTo(1));
    }
    [TestCase(false)]
    [TestCase(true)]
    public async Task InstalledConnector_IsolatesDriverButSharesPublicArrowContracts(bool nativeApi)
    {
        const string family = "DubUrl.Provider.DuckDb";
        var definition = new DubUrl.Core.ProviderPacks.ConnectorDefinition
        {
            Provider = "duckdb-adbc", Version = "0.0.0", DubUrlApiRange = "[0.0.0,1.0.0)",
            Assembly = family + ".dll", ImplementationAssemblies = [family + ".dll"],
            Driver = Provider,
            Databases = [new("DuckDB", ["adbc+duck", "adbc+duckdb"])],
            TargetFrameworks = ["net8.0", "net9.0", "net10.0"], RuntimeIdentifiers = []
        };
        using var archive = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(archive, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            foreach (var path in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
            {
                using var target = zip.CreateEntry(Path.GetFileName(path)).Open();
                using var source = File.OpenRead(path);
                source.CopyTo(target);
            }
            // Exercise dependency resolution inside the pack, rather than default-context fallback.
            using (var target = zip.CreateEntry(family + ".deps.json").Open())
            using (var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "DubUrl.Adbc.Testing.deps.json")))
                source.CopyTo(target);
            using var manifest = new StreamWriter(zip.CreateEntry("provider-pack.json").Open());
            manifest.Write(System.Text.Json.JsonSerializer.Serialize(definition, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        }
        archive.Position = 0;
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(archive));
        archive.Position = 0;
        var store = new DubUrl.Core.ProviderPacks.ProviderPackStore(Path.Combine(root, "store"));
        await store.InstallAsync(definition with { DownloadUri = new Uri("https://example.test/duckdb-adbc.zip"), Sha256 = digest }, archive);
        var diagnostics = new List<DubUrl.Core.ProviderPacks.ProviderPackDiagnostic>();
        using var loader = new InstalledProviderModuleLoader();
        var modules = loader.LoadActive(store, diagnostics);
        Assert.That(diagnostics, Is.Empty);
        Assert.That(modules, Has.Count.EqualTo(1));
        var registry = new ProviderCatalogBuilder().AddModule(modules[0]).Build();
        var factory = registry.GetProviderFactory(["adbc", "duckdb"]);
        Assert.That(System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(factory.GetType().Assembly),
            Is.Not.SameAs(System.Runtime.Loader.AssemblyLoadContext.Default));
        Assert.That(factory, Is.InstanceOf<AdbcProviderFactory>());
        Environment.SetEnvironmentVariable("ADBC_DRIVER_PATH", WriteManifest(null));
        if (nativeApi)
        {
            using var session = new AdbcConnectionUrl("adbc+duckdb://", registry).Open();
            using var statement = session.Connection.CreateStatement();
            statement.SqlQuery = "SELECT 42::BIGINT";
            using var stream = statement.ExecuteQuery().Stream!;
            using var batch = await stream.ReadNextRecordBatchAsync();
            Assert.That(((Apache.Arrow.Int64Array)batch!.Column(0)).GetValue(0), Is.EqualTo(42));
        }
        else
            Assert.That(new DatabaseUrl(new ConnectionUrlFactory(registry), "adbc+duckdb://")
                .ReadScalar<long>("SELECT 42::BIGINT"), Is.EqualTo(42));
    }
}
