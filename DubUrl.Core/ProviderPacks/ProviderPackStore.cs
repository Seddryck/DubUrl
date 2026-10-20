using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace DubUrl.Core.ProviderPacks;

/// <summary>
/// Installs verified provider packs into a user-scoped, versioned store.
/// </summary>
public sealed class ProviderPackStore
{
    private const string ManifestInvalid = "manifest-invalid";
    private const string PathInvalid = "path-invalid";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly Version _apiVersion;
    private readonly string _targetFramework;
    private readonly string _runtimeIdentifier;

    public string RootPath { get; }

    public ProviderPackStore(
        string? rootPath = null,
        Version? apiVersion = null,
        string? targetFramework = null,
        string? runtimeIdentifier = null)
    {
        RootPath = Path.GetFullPath(rootPath ?? ProviderPackPaths.GetUserStorePath());
        _apiVersion = apiVersion ?? typeof(ProviderPackStore).Assembly.GetName().Version ?? new Version(0, 0);
        _targetFramework = targetFramework ?? GetCurrentTargetFramework();
        _runtimeIdentifier = runtimeIdentifier ?? System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
    }

    public async Task<InstalledProviderPack> InstallAsync(
        ConnectorDefinition catalogEntry,
        Stream archive,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogEntry);
        ArgumentNullException.ThrowIfNull(archive);
        ValidateIdentifier(catalogEntry.Provider, nameof(catalogEntry));

        var stagingRoot = Path.Combine(RootPath, ".staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingRoot);
        try
        {
            var archivePath = Path.Combine(stagingRoot, "pack.zip");
            await using (var destination = File.Create(archivePath))
                await archive.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            var archiveHash = await ComputeHashAsync(archivePath, cancellationToken).ConfigureAwait(false);
            if (catalogEntry.Sha256 is null || !FixedEquals(archiveHash, catalogEntry.Sha256))
                throw new ProviderPackException("pack-integrity", "The provider pack archive does not match its curated catalog digest.");

            await using var packageStream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var package = new ZipArchive(packageStream, ZipArchiveMode.Read);
            var manifestEntry = package.GetEntry(ConnectorDefinition.FileName)
                ?? throw new ProviderPackException("manifest-missing", $"The provider pack does not contain {ConnectorDefinition.FileName}.");
            ConnectorDefinition manifest;
            await using (var manifestStream = await OpenEntryAsync(manifestEntry, cancellationToken).ConfigureAwait(false))
            {
                manifest = await JsonSerializer.DeserializeAsync<ConnectorDefinition>(manifestStream, JsonOptions, cancellationToken).ConfigureAwait(false)
                    ?? throw new ProviderPackException(ManifestInvalid, "The provider pack manifest is empty.");
            }

            ValidateManifest(manifest, catalogEntry);
            var contentRoot = Path.Combine(stagingRoot, "content");
            Directory.CreateDirectory(contentRoot);
            await ExtractArchiveAsync(package, contentRoot, cancellationToken).ConfigureAwait(false);
            if (RequiredAssemblyPaths(manifest).Any(path => !File.Exists(GetAssemblyPath(contentRoot, path))))
                throw new ProviderPackException("assembly-missing", "The connector archive does not contain its required assembly.");

            await File.WriteAllTextAsync(
                Path.Combine(contentRoot, ConnectorDefinition.FileName),
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken).ConfigureAwait(false);

            var providerRoot = Path.Combine(RootPath, "packs", manifest.Provider);
            var installPath = Path.Combine(providerRoot, manifest.Version);
            Directory.CreateDirectory(providerRoot);
            string? replaced = null;
            if (Directory.Exists(installPath))
            {
                replaced = Path.Combine(stagingRoot, "replaced");
                Directory.Move(installPath, replaced);
            }

            try
            {
                Directory.Move(contentRoot, installPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (replaced is not null)
                    Directory.Move(replaced, installPath);
                throw;
            }
            Activate(manifest.Provider, manifest.Version);
            return InspectPath(installPath, manifest.Version);
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, recursive: true);
        }
    }

    public IReadOnlyList<InstalledProviderPack> Inspect()
    {
        var packsRoot = Path.Combine(RootPath, "packs");
        if (!Directory.Exists(packsRoot))
            return [];

        var result = new List<InstalledProviderPack>();
        foreach (var providerPath in Directory.EnumerateDirectories(packsRoot).Order(StringComparer.OrdinalIgnoreCase))
        {
            var provider = Path.GetFileName(providerPath);
            var activeVersion = GetActiveVersion(provider);
            foreach (var versionPath in Directory.EnumerateDirectories(providerPath).Order(StringComparer.OrdinalIgnoreCase))
                result.Add(InspectPath(versionPath, activeVersion));
        }
        return result;
    }

    public bool Remove(string provider)
    {
        ValidateIdentifier(provider, nameof(provider));
        var packsRoot = Path.Combine(RootPath, "packs");
        var providerPath = Directory.Exists(packsRoot)
            ? Directory.EnumerateDirectories(packsRoot)
                .FirstOrDefault(path => Path.GetFileName(path).Equals(provider, StringComparison.OrdinalIgnoreCase))
            : null;
        if (providerPath is null)
            return false;

        var stagingRoot = Path.Combine(RootPath, ".staging");
        Directory.CreateDirectory(stagingRoot);
        var installedProvider = Path.GetFileName(providerPath);
        var removedPath = Path.Combine(stagingRoot, $"removed-{installedProvider}-{Guid.NewGuid():N}");
        Directory.Move(providerPath, removedPath);
        var activationPath = GetActivationPath(installedProvider);
        if (File.Exists(activationPath))
            File.Delete(activationPath);
        Directory.Delete(removedPath, recursive: true);
        return true;
    }

    private InstalledProviderPack InspectPath(string installPath, string? activeVersion)
    {
        var invalidManifest = TryReadManifest(installPath, out var manifest);
        if (invalidManifest is not null)
            return invalidManifest;
        if (manifest is null)
            throw new InvalidOperationException("Manifest inspection succeeded without returning a manifest.");

        var compatibility = GetCompatibilityDiagnostic(manifest);
        if (compatibility is not null)
            return new(manifest, installPath, ProviderPackStatus.Incompatible, [compatibility]);

        try
        {
            if (RequiredAssemblyPaths(manifest).Any(path => !File.Exists(GetAssemblyPath(installPath, path))))
                return new(manifest, installPath, ProviderPackStatus.Incomplete, [new("assembly-missing", "A required connector assembly is missing.")]);
        }
        catch (ProviderPackException exception)
        {
            return new(manifest, installPath, ProviderPackStatus.Corrupt, [new(exception.Code, exception.Message)]);
        }

        var status = string.Equals(manifest.Version, activeVersion, StringComparison.OrdinalIgnoreCase)
            ? ProviderPackStatus.Active
            : ProviderPackStatus.Installed;
        return new(manifest, installPath, status, []);
    }

    private static InstalledProviderPack? TryReadManifest(
        string installPath,
        out ConnectorDefinition? manifest)
    {
        manifest = null;
        try
        {
            var manifestPath = Path.Combine(installPath, ConnectorDefinition.FileName);
            if (!File.Exists(manifestPath))
                return InvalidPack(installPath, ProviderPackStatus.Incomplete, "manifest-missing", "The installed pack manifest is missing.");
            manifest = JsonSerializer.Deserialize<ConnectorDefinition>(File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new JsonException("Empty manifest.");
            return IsManifestStructurallyValid(manifest)
                ? null
                : InvalidPack(installPath, ProviderPackStatus.Corrupt, ManifestInvalid, "The installed pack manifest is incomplete.");
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return InvalidPack(installPath, ProviderPackStatus.Corrupt, ManifestInvalid, exception.Message);
        }
    }

    private void ValidateManifest(ConnectorDefinition manifest, ConnectorDefinition entry)
    {
        if (!IsManifestStructurallyValid(manifest))
            throw new ProviderPackException(ManifestInvalid, "The provider pack manifest is incomplete.");
        ValidateManifestIdentity(manifest, entry);
        foreach (var path in RequiredAssemblyPaths(manifest)) ValidateRelativePath(path);

        var compatibility = GetCompatibilityDiagnostic(manifest);
        if (compatibility is not null)
            throw new ProviderPackException(compatibility.Code, compatibility.Message);
        if (manifest.Assembly != entry.Assembly
            || !SameDatabases(manifest.Databases, entry.Databases)
            || manifest.Driver != entry.Driver
            || manifest.DriverAssembly != entry.DriverAssembly
            || !manifest.ImplementationAssemblies.SequenceEqual(entry.ImplementationAssemblies, StringComparer.OrdinalIgnoreCase)
            || manifest.DubUrlApiRange != entry.DubUrlApiRange
            || !manifest.Schemes.SequenceEqual(entry.Schemes, StringComparer.OrdinalIgnoreCase)
            || !manifest.TargetFrameworks.SequenceEqual(entry.TargetFrameworks, StringComparer.OrdinalIgnoreCase)
            || !manifest.RuntimeIdentifiers.SequenceEqual(entry.RuntimeIdentifiers, StringComparer.OrdinalIgnoreCase))
            throw new ProviderPackException("catalog-mismatch", "The local connector definition does not match the catalog definition.");
    }

    private static void ValidateManifestIdentity(ConnectorDefinition manifest, ConnectorDefinition entry)
    {
        ValidateIdentifier(manifest.Provider, nameof(manifest.Provider));
        ValidateIdentifier(manifest.Version, nameof(manifest.Version));
        if (!string.Equals(manifest.Provider, entry.Provider, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(manifest.Version, entry.Version, StringComparison.OrdinalIgnoreCase))
            throw new ProviderPackException("catalog-mismatch", "The provider pack identity does not match the curated catalog entry.");
    }

    private static bool SameDatabases(IReadOnlyList<ConnectorDatabase> left, IReadOnlyList<ConnectorDatabase> right)
        => left.Count == right.Count && left.Zip(right).All(pair => pair.First.Name == pair.Second.Name
            && pair.First.Schemes.SequenceEqual(pair.Second.Schemes, StringComparer.OrdinalIgnoreCase));

    private static IEnumerable<string> RequiredAssemblyPaths(ConnectorDefinition definition)
        => new[] { definition.Assembly, definition.DriverAssemblyPath }.Concat(definition.ImplementationAssemblies).Distinct(StringComparer.OrdinalIgnoreCase);

    private static string GetAssemblyPath(string root, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(root, ValidateRelativePath(relativePath)));
        EnsureWithin(root, path);
        return path;
    }

    private static async Task ExtractArchiveAsync(ZipArchive archive, string root, CancellationToken cancellationToken)
    {
        var destinations = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Extract ordinary files/directories only; never follow archive-provided links.
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000
                || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new ProviderPackException(PathInvalid, "Connector archives cannot contain symbolic links.");
            var name = entry.FullName.Replace('\\', '/');
            var directory = name.EndsWith('/');
            var relativePath = ValidateRelativePath(directory ? name[..^1] : name);
            var destination = Path.GetFullPath(Path.Combine(root, relativePath));
            EnsureWithin(root, destination);
            if (!destinations.Add(destination))
                throw new ProviderPackException(PathInvalid, $"Duplicate archive path '{entry.FullName}'.");
            if (directory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var source = await OpenEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, FileOptions.Asynchronous);
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        }
    }

    private ProviderPackDiagnostic? GetCompatibilityDiagnostic(ConnectorDefinition manifest)
    {
        if (!VersionRange.Contains(manifest.DubUrlApiRange, _apiVersion))
            return new("api-incompatible", $"Pack requires DubUrl API '{manifest.DubUrlApiRange}', but this host is {_apiVersion}.");
        if (!manifest.TargetFrameworks.Contains(_targetFramework, StringComparer.OrdinalIgnoreCase))
            return new("tfm-incompatible", $"Pack does not support target framework '{_targetFramework}'.");
        if (manifest.RuntimeIdentifiers.Count > 0
            && !manifest.RuntimeIdentifiers.Contains(_runtimeIdentifier, StringComparer.OrdinalIgnoreCase))
            return new("rid-incompatible", $"Pack does not support runtime '{_runtimeIdentifier}'.");
        return null;
    }

    private void Activate(string provider, string version)
    {
        var activationPath = GetActivationPath(provider);
        var activationDirectory = Path.GetDirectoryName(activationPath)
            ?? throw new InvalidOperationException("The activation path has no parent directory.");
        Directory.CreateDirectory(activationDirectory);
        var temporaryPath = $"{activationPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new Activation(version), JsonOptions));
        File.Move(temporaryPath, activationPath, overwrite: true);
    }

    private string? GetActiveVersion(string provider)
    {
        try
        {
            var path = GetActivationPath(provider);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Activation>(File.ReadAllText(path), JsonOptions)?.Version
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string GetActivationPath(string provider)
        => Path.Combine(RootPath, "active", $"{provider}.json");

    private static InstalledProviderPack InvalidPack(string path, ProviderPackStatus status, string code, string message)
        => new(
            new ConnectorDefinition
            {
                Provider = Path.GetFileName(Path.GetDirectoryName(path)) ?? "unknown",
                Version = Path.GetFileName(path),
                DubUrlApiRange = string.Empty,
                Assembly = string.Empty, Driver = string.Empty, ImplementationAssemblies = [],
                Databases = [],
                TargetFrameworks = [],
                RuntimeIdentifiers = [],
            },
            path,
            status,
            [new(code, message)]);

    private static bool IsManifestStructurallyValid(ConnectorDefinition definition)
        => definition.HasCompleteMetadata();

    private static string ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            throw new ProviderPackException(PathInvalid, "Connector archive paths must be relative.");
        var normalized = path.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
        if (normalized.Split(Path.DirectorySeparatorChar).Any(part => part is ".." or "." or "" || part.Contains(':') || part.EndsWith('.') || part.EndsWith(' ')))
            throw new ProviderPackException(PathInvalid, $"Connector archive path '{path}' is invalid.");
        return normalized;
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Provider and version identifiers cannot be empty.", parameterName);
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value is "." or "..")
            throw new ArgumentException("Provider and version identifiers must be valid path segments.", parameterName);
    }

    [SuppressMessage(
        "Reliability",
        "S6966:Awaitable method should be used",
        Justification = "ZipArchiveEntry.OpenAsync is only available when targeting .NET 10 or later.")]
    private static async ValueTask<Stream> OpenEntryAsync(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
#if NET10_0
        return await entry.OpenAsync(cancellationToken).ConfigureAwait(false);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return entry.Open();
#endif
    }

    private static void EnsureWithin(string root, string path)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(normalizedRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ProviderPackException(PathInvalid, "A connector archive path escapes the installation directory.");
    }

    private static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }

    private static bool FixedEquals(string actual, string expected)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual),
                Convert.FromHexString(expected));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string GetCurrentTargetFramework()
    {
#if NET10_0
        return "net10.0";
#elif NET9_0
        return "net9.0";
#else
        return "net8.0";
#endif
    }

    private sealed record Activation(string Version);
}

internal static class VersionRange
{
    public static bool Contains(string range, Version version)
    {
        if (string.IsNullOrWhiteSpace(range))
            return false;
        range = range.Trim();
        if (!(range.StartsWith('[') || range.StartsWith('(')) || !(range.EndsWith(']') || range.EndsWith(')')))
            return Version.TryParse(range, out var exact) && exact == version;
        var limits = range[1..^1].Split(',', 2);
        if (limits.Length != 2)
            return false;
        return MeetsMinimum(limits[0], range[0], version)
            && MeetsMaximum(limits[1], range[^1], version);
    }

    private static bool MeetsMinimum(string value, char boundary, Version version)
        => !Version.TryParse(value.Trim(), out var minimum)
            || version.CompareTo(minimum) > 0
            || (version == minimum && boundary == '[');

    private static bool MeetsMaximum(string value, char boundary, Version version)
        => !Version.TryParse(value.Trim(), out var maximum)
            || version.CompareTo(maximum) < 0
            || (version == maximum && boundary == ']');
}
