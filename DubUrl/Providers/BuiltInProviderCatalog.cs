using DubUrl.Core.ProviderPacks;
using DubUrl.Core.Providers;

namespace DubUrl.Providers;

/// <summary>
/// Provider-family metadata from the embedded connector catalog.
/// Describing a connector does not install or activate its driver.
/// </summary>
public static class BuiltInProviderCatalog
{
    public static ProviderCatalog Create()
    {
        using var stream = typeof(BuiltInProviderCatalog).Assembly.GetManifestResourceStream("DubUrl.ConnectorCatalog")
            ?? throw new InvalidOperationException("The embedded connector catalog is missing.");
        var catalog = ProviderPackCatalogDocument.Read(stream);
        return new ProviderCatalog(catalog.Entries
            .GroupBy(entry => entry.DatabaseName ?? entry.Provider, StringComparer.OrdinalIgnoreCase)
            .Select(database => new ProviderDescriptor(database.Key, "mapping", database.SelectMany(entry => entry.Schemes))));
    }
}
