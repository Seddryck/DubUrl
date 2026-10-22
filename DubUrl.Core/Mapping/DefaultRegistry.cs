using System.Diagnostics.CodeAnalysis;
using DubUrl.Core.ProviderPacks;
using DubUrl.Querying.Dialects;
using DubUrl.Rewriting;

namespace DubUrl.Mapping;

/// <summary>Provides cached discovery snapshots, refreshed when installed connectors change.</summary>
public static class DefaultRegistry
{
    private static readonly DefaultRegistryCache DefaultCache = new(CreateDefaultCatalog);
    /// <summary>
    /// Reuses a registry snapshot until the installed connector store changes.
    /// </summary>
    [RequiresUnreferencedCode("Default discovery loads provider modules and reflects over dialect attributes. Use ProviderCatalogBuilder with a custom explicit module for Native AOT.")]
    public static SchemeRegistry Get()
        => DefaultCache.GetSchemes(new ProviderPackStore());

    /// <summary>Rebuilds the default registry, for example after loading application providers dynamically.</summary>
    public static SchemeRegistry Refresh()
    {
        DefaultCache.Invalidate();
        return Get();
    }

    private static ProviderCatalogBuilder CreateDefaultCatalog(ProviderPackStore store)
    {
        var builder = new ProviderCatalogBuilder();
        var diagnostics = new List<ProviderPackDiagnostic>();
        builder.AddInstalledPacks(store, diagnostics);
        if (diagnostics.Count != 0)
            throw new ProviderPackException("module-load-failed",
                string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message)));

        builder.AddApplicationProviders();
        return builder;
    }

    public static IDialectRegistry GetDialects(params string[] schemes)
    {
        var registry = (DialectRegistry)DefaultCache.GetDialects(new ProviderPackStore());

        if (schemes.Length == 0)
            return registry;

        var selectedDialects = new List<IDialect>();
        var unknownSchemes = new List<string>();
        foreach (var scheme in schemes)
        {
            try
            {
                selectedDialects.Add(registry.Get(scheme));
            }
            catch (DialectNotFoundException)
            {
                unknownSchemes.Add(scheme);
            }
        }
        if (unknownSchemes.Count > 0)
            throw new ArgumentException($"No dialect is mapped to: {string.Join(", ", unknownSchemes)}.", nameof(schemes));

        return registry.Select(selectedDialects.Select(dialect => dialect.GetType()));
    }
}
