namespace DubUrl.Core.ProviderPacks;

public sealed record ProviderPackCatalogEntry(
    string Provider,
    string Version,
    Uri DownloadUri,
    string Sha256);

/// <summary>
/// Immutable allow-list of provider packs approved by the application distributor.
/// </summary>
public sealed class ProviderPackCatalog
{
    private readonly IReadOnlyDictionary<string, ProviderPackCatalogEntry> _entries;

    public ProviderPackCatalog(IEnumerable<ProviderPackCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = entries.ToDictionary(entry => entry.Provider, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ProviderPackCatalogEntry> Entries => _entries.Values.ToArray();

    public bool TryGet(string provider, out ProviderPackCatalogEntry? entry)
        => _entries.TryGetValue(provider, out entry);
}
