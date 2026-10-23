using System.Runtime.InteropServices;
using System.Security;
using Apache.Arrow.Adbc;
using Apache.Arrow.Adbc.DriverManager;
using Microsoft.Win32;
using Tomlyn;
using Tomlyn.Model;

namespace DubUrl.Provider.DuckDb.Adbc.Drivers.Discovery;

internal enum DriverPlatform { Windows, Linux, MacOS }
internal sealed record DriverLocation(string Path, string? EntryPoint);

// Complete the standard ADBC search order around Apache's manifest/native loader.
// Keep environment reads at Open time, never at singleton initialization time.
internal sealed class DuckdbDriverDiscovery
{
    internal const string DefaultEntryPoint = "duckdb_adbc_init";
    private const string RegistryPath = @"SOFTWARE\ADBC\Drivers\duckdb";
    private readonly DriverPlatform platform;
    private readonly Func<string, string?> variable;
    private readonly Func<Environment.SpecialFolder, string> folder;
    private readonly Func<bool, DriverLocation?> registry;
    private readonly Func<DriverLocation, AdbcDriver> load;
    private readonly Func<string?> platformLibrary;

    public DuckdbDriverDiscovery() : this(
        OperatingSystem.IsWindows() ? DriverPlatform.Windows : OperatingSystem.IsMacOS() ? DriverPlatform.MacOS : DriverPlatform.Linux,
        Environment.GetEnvironmentVariable,
        Environment.GetFolderPath,
        ReadRegistry,
        LoadLocation,
        DuckdbNativeLibrary.Find) { }

    internal DuckdbDriverDiscovery(DriverPlatform platform,
        Func<string, string?> variable, Func<Environment.SpecialFolder, string> folder,
        Func<bool, DriverLocation?> registry, Func<DriverLocation, AdbcDriver> load,
        Func<string?> platformLibrary)
        => (this.platform, this.variable, this.folder, this.registry, this.load, this.platformLibrary)
            = (platform, variable, folder, registry, load, platformLibrary);

    public AdbcDriver Load()
    {
        var searched = new List<string>();
        try
        {
            foreach (var source in Sources())
            {
                searched.Add(source.Description);
                var location = source.Find();
                if (location is not null)
                    return load(location);
            }
        }
        catch (Exception exception) when (exception is AdbcException or IOException or UnauthorizedAccessException
            or ArgumentException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException
            or BadImageFormatException or SecurityException or TomlException)
        {
            // Manifest text, native loader errors, environment values, and connection
            // options can contain secrets. Do not retain them in an inner exception.
            throw Failure($"Discovery or loading failed ({exception.GetType().Name}).", searched);
        }
        throw Failure("No installed driver was found.", searched);
    }

    private static InvalidOperationException Failure(string reason, List<string> searched)
        => new($"Could not load the DuckDB ADBC driver. {reason} Searched: {string.Join("; ", searched.Distinct())}. " +
            "Install a DuckDB library matching the process architecture and an ADBC driver manifest, " +
            "set ADBC_DRIVER_PATH to its directory, or register AdbcProviderFactory.FromNative(libraryPath, entryPoint) explicitly. " +
            "Check the selected manifest, its library path, and entrypoint (DuckDB default: duckdb_adbc_init).");

    private IEnumerable<(string Description, Func<DriverLocation?> Find)> Sources()
    {
        // Preserve source order and evaluate environment/configuration only at Open time.
        foreach (var path in AbsoluteDirectories("ADBC_DRIVER_PATH"))
            yield return ("ADBC_DRIVER_PATH", () => FindInDirectory(path));
        foreach (var source in ConfigurationSources())
            yield return source;
        yield return (LibraryPathVariable + " native-library directories", FindInLibraryPath);
        yield return ("platform shared-library lookup (application/OS search paths)", () =>
        {
            var path = platformLibrary();
            return path is null ? null : new(path, null);
        });
    }

    private IEnumerable<(string Description, Func<DriverLocation?> Find)> ConfigurationSources()
        => platform switch
        {
            DriverPlatform.Windows =>
            [
                (@"HKCU\SOFTWARE\ADBC\Drivers\duckdb", () => registry(false)),
                ("%LOCALAPPDATA%/ADBC/Drivers", () => FindBelow(folder(Environment.SpecialFolder.LocalApplicationData), "ADBC", "Drivers")),
                (@"HKLM\SOFTWARE\ADBC\Drivers\duckdb", () => registry(true))
            ],
            DriverPlatform.MacOS =>
            [
                ("~/Library/Application Support/ADBC/Drivers", () => FindBelow(folder(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "ADBC", "Drivers")),
                ("/Library/Application Support/ADBC/Drivers", () => FindInDirectory("/Library/Application Support/ADBC/Drivers"))
            ],
            _ =>
            [
                ("$XDG_CONFIG_HOME/adbc/drivers or ~/.config/adbc/drivers", FindInLinuxConfiguration),
                ("/etc/adbc/drivers", () => FindInDirectory("/etc/adbc/drivers"))
            ]
        };

    private string LibraryPathVariable => platform switch
    {
        DriverPlatform.Windows => "PATH",
        DriverPlatform.MacOS => "DYLD_LIBRARY_PATH",
        _ => "LD_LIBRARY_PATH"
    };

    private IEnumerable<string> AbsoluteDirectories(string name)
        => (variable(name) ?? "").Split(platform == DriverPlatform.Windows ? ';' : ':',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Path.IsPathFullyQualified);

    private DriverLocation? FindInLibraryPath()
        => AbsoluteDirectories(LibraryPathVariable).Select(FindLibraryInDirectory).FirstOrDefault(location => location is not null);

    private DriverLocation? FindInLinuxConfiguration()
    {
        var config = variable("XDG_CONFIG_HOME");
        return !string.IsNullOrEmpty(config) && Path.IsPathFullyQualified(config)
            ? FindBelow(config, "adbc", "drivers")
            : FindBelow(folder(Environment.SpecialFolder.UserProfile), ".config", "adbc", "drivers");
    }

    private DriverLocation? FindBelow(string root, params string[] segments)
        => string.IsNullOrEmpty(root) ? null : FindInDirectory(Path.Combine([root, .. segments]));

    private DriverLocation? FindInDirectory(string path)
    {
        var manifest = Path.Combine(path, "duckdb.toml");
        if (File.Exists(manifest))
            return new(manifest, null);
        return FindLibraryInDirectory(path);
    }

    private DriverLocation? FindLibraryInDirectory(string path)
    {
        var filenames = platform switch
        {
            DriverPlatform.Windows => new[] { "duckdb.dll" },
            DriverPlatform.MacOS => ["libduckdb.dylib", "duckdb.dylib", "libduckdb.so", "duckdb.so"],
            _ => ["libduckdb.so", "duckdb.so"]
        };
        return filenames.Select(name => Path.Combine(path, name)).Where(File.Exists)
            .Select(file => new DriverLocation(file, null)).FirstOrDefault();
    }

    internal static AdbcDriver LoadLocation(DriverLocation location)
    {
        var path = location.Path;
        if (!Path.GetExtension(path).Equals(".toml", StringComparison.OrdinalIgnoreCase)
            && File.Exists(Path.ChangeExtension(path, ".toml")))
            path = Path.ChangeExtension(path, ".toml");
        var entryPoint = location.EntryPoint;
        if (Path.GetExtension(path).Equals(".toml", StringComparison.OrdinalIgnoreCase))
        {
            // Apache derives a different symbol for DuckDB when metadata is absent.
            // Inspect only this optional field; Apache still resolves/loads the manifest.
            var manifest = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(path))!;
            var hasEntryPoint = manifest.TryGetValue("Driver", out var table)
                && table is TomlTable driver && driver.ContainsKey("entrypoint");
            entryPoint ??= hasEntryPoint ? null : DefaultEntryPoint;
        }
        else
            entryPoint ??= DefaultEntryPoint;
        return AdbcDriverManager.FindLoadDriver(path, entryPoint, AdbcLoadFlags.None);
    }

    private static DriverLocation? ReadRegistry(bool machine)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        using var hive = RegistryKey.OpenBaseKey(machine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Default);
        using var key = hive.OpenSubKey(RegistryPath, writable: false);
        if (key is null)
            return null;
        var path = key.GetValue("driver") as string;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidOperationException("Invalid ADBC registry driver path.");
        var entryPoint = key.GetValue("entrypoint") as string;
        return new(path, string.IsNullOrWhiteSpace(entryPoint) ? null : entryPoint);
    }
}
