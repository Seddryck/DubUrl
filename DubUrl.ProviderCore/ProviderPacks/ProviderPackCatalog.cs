namespace DubUrl.Core.ProviderPacks;

public sealed record ProviderPackCatalogEntry(
    string Provider,
    string Version,
    Uri DownloadUri,
    string Sha256,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? Schemes = null);

/// <summary>
/// Immutable allow-list of provider packs approved by the application distributor.
/// </summary>
public sealed class ProviderPackCatalog
{
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
}
