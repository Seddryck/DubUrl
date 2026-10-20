using DubUrl.Core.ProviderPacks;
using DubUrl.Core.Providers;

namespace DubUrl.Application;

/// <summary>The generated default catalog shared by application hosts. Hosts may supply a replacement catalog.</summary>
public static class BundledProviderCatalog
{
    public static ProviderPackCatalog Create()
    {
        using var stream = typeof(BundledProviderCatalog).Assembly.GetManifestResourceStream("DubUrl.ConnectorCatalog")
            ?? throw new InvalidOperationException("The bundled provider catalog is missing.");
        return new ProviderPackCatalog(ProviderPackCatalogDocument.Read(stream).Entries);
    }

    public static ProviderCatalog Describe()
        => new(Create().Entries
            .SelectMany(entry => entry.Databases)
            .GroupBy(database => database.Name, StringComparer.OrdinalIgnoreCase)
            .Select(database => new ProviderDescriptor(database.Key, "mapping", database.SelectMany(mapping => mapping.Schemes))));
}
