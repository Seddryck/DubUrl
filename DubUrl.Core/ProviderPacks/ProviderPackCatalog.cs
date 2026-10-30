using System.Text.Json;

namespace DubUrl.Core.ProviderPacks;

public sealed record ProviderPackCatalogDocument(
    int SchemaVersion,
    IReadOnlyList<ConnectorDefinition> Entries)
{
    public const int CurrentSchemaVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Reads connector definitions, including catalogs without optional distribution fields.</summary>
    public static ProviderPackCatalogDocument Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var document = JsonSerializer.Deserialize<ProviderPackCatalogDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("The connector catalog is empty.");
        if (document.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Connector catalog schema version '{document.SchemaVersion}' is not supported; expected '{CurrentSchemaVersion}'.");
        if (document.Entries is null)
            throw new InvalidDataException("The connector catalog does not contain an entries collection.");
        foreach (var entry in document.Entries)
            if (entry is null || !entry.HasCompleteMetadata())
                throw new InvalidDataException("The connector catalog has incomplete connector metadata.");
        return document;
    }
}

/// <summary>
/// Immutable allow-list of provider packs approved by the application distributor.
/// </summary>
public sealed class ProviderPackCatalog
{
    private readonly IReadOnlyDictionary<string, ConnectorDefinition> _entries;
    private readonly IReadOnlyCollection<ConnectorDefinition> _canonicalEntries;

    public ProviderPackCatalog(IEnumerable<ConnectorDefinition> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var canonicalEntries = entries.ToArray();
        var lookup = new Dictionary<string, ConnectorDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in canonicalEntries)
        {
            AddName(lookup, entry.Provider, entry);
            foreach (var alias in entry.Aliases ?? [])
                AddName(lookup, alias, entry);
        }

        _entries = lookup;
        _canonicalEntries = canonicalEntries;
    }

    public IReadOnlyCollection<ConnectorDefinition> Entries => _canonicalEntries;
    public IReadOnlyList<ProviderPackDiagnostic> Diagnostics { get; init; } = [];

    public static ProviderPackCatalog Read(Stream stream)
    {
        var document = ProviderPackCatalogDocument.Read(stream);
        foreach (var entry in document.Entries)
            ValidateOfficialEntry(entry);
        return new(document.Entries);
    }

    public bool TryGet(string provider, out ConnectorDefinition? entry)
        => _entries.TryGetValue(provider, out entry);

    private static void AddName(
        IDictionary<string, ConnectorDefinition> lookup,
        string name,
        ConnectorDefinition entry)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Connector IDs and aliases cannot be empty.", nameof(entry));
        if (!lookup.TryAdd(name, entry))
            throw new ArgumentException($"Connector ID or alias '{name}' is assigned more than once.", nameof(entry));
    }

    private static void ValidateOfficialEntry(ConnectorDefinition? entry)
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
        if (!entry.HasCompleteMetadata())
            throw new InvalidDataException($"Connector '{entry.Provider}' has incomplete connector metadata.");
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
