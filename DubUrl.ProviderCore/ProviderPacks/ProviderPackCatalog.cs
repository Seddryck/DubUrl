using System.Text.Json;

namespace DubUrl.Core.ProviderPacks;

public sealed record ProviderPackCatalogEntry(
    string Provider,
    string Version,
    Uri DownloadUri,
    string Sha256,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? Schemes = null,
    string? DubUrlApiRange = null,
    IReadOnlyList<string>? TargetFrameworks = null,
    IReadOnlyList<string>? RuntimeIdentifiers = null);

public sealed record ProviderPackCatalogDocument(
    int SchemaVersion,
    IReadOnlyList<ProviderPackCatalogEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;
}

/// <summary>
/// Immutable allow-list of provider packs approved by the application distributor.
/// </summary>
public sealed class ProviderPackCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyDictionary<string, ProviderPackCatalogEntry> _entries;
    private readonly IReadOnlyCollection<ProviderPackCatalogEntry> _canonicalEntries;

    public ProviderPackCatalog(IEnumerable<ProviderPackCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var canonicalEntries = entries.ToArray();
        var lookup = new Dictionary<string, ProviderPackCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in canonicalEntries)
        {
            AddName(lookup, entry.Provider, entry);
            foreach (var alias in entry.Aliases ?? [])
                AddName(lookup, alias, entry);
        }

        _entries = lookup;
        _canonicalEntries = canonicalEntries;
    }

    public IReadOnlyCollection<ProviderPackCatalogEntry> Entries => _canonicalEntries;

    public static ProviderPackCatalog Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var document = JsonSerializer.Deserialize<ProviderPackCatalogDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("The connector catalog is empty.");
        if (document.SchemaVersion != ProviderPackCatalogDocument.CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Connector catalog schema version '{document.SchemaVersion}' is not supported; expected '{ProviderPackCatalogDocument.CurrentSchemaVersion}'.");
        if (document.Entries is null)
            throw new InvalidDataException("The connector catalog does not contain an entries collection.");

        foreach (var entry in document.Entries)
            ValidateOfficialEntry(entry);
        return new(document.Entries);
    }

    public bool TryGet(string provider, out ProviderPackCatalogEntry? entry)
        => _entries.TryGetValue(provider, out entry);

    private static void AddName(
        IDictionary<string, ProviderPackCatalogEntry> lookup,
        string name,
        ProviderPackCatalogEntry entry)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connector IDs and aliases cannot be empty.", nameof(entry));
        if (!lookup.TryAdd(name, entry))
            throw new ArgumentException($"Connector ID or alias '{name}' is assigned more than once.", nameof(entry));
    }

    private static void ValidateOfficialEntry(ProviderPackCatalogEntry? entry)
    {
        if (entry is null
            || string.IsNullOrWhiteSpace(entry.Provider)
            || string.IsNullOrWhiteSpace(entry.Version)
            || entry.DownloadUri is null
            || !entry.DownloadUri.IsAbsoluteUri
            || entry.DownloadUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Every connector catalog entry requires an ID, version, and absolute HTTPS download URL.");
        if (string.IsNullOrWhiteSpace(entry.Sha256)
            || entry.Sha256.Length != 64
            || !entry.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException($"Connector '{entry.Provider}' has an invalid archive SHA-256 digest.");
        if (entry.Schemes is not { Count: > 0 }
            || string.IsNullOrWhiteSpace(entry.DubUrlApiRange)
            || entry.TargetFrameworks is not { Count: > 0 }
            || entry.RuntimeIdentifiers is null)
            throw new InvalidDataException($"Connector '{entry.Provider}' has incomplete compatibility metadata.");
        ValidateNames(entry.Provider, "scheme", entry.Schemes);
        ValidateNames(entry.Provider, "target framework", entry.TargetFrameworks);
        ValidateNames(entry.Provider, "runtime identifier", entry.RuntimeIdentifiers);
    }

    private static void ValidateNames(string provider, string kind, IReadOnlyList<string> values)
    {
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
            throw new InvalidDataException($"Connector '{provider}' has an empty or duplicate {kind}.");
    }
}
