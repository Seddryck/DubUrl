using System.Collections.ObjectModel;

namespace DubUrl.Core.Providers;

/// <summary>
/// Immutable, deterministic provider metadata catalog.
/// </summary>
public sealed class ProviderCatalog
{
    private readonly IReadOnlyDictionary<string, ProviderDescriptor> _byScheme;

    public IReadOnlyList<ProviderDescriptor> Providers { get; }

    public ProviderCatalog(IEnumerable<ProviderDescriptor> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var ordered = providers
            .OrderBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(provider => provider.Source, StringComparer.Ordinal)
            .ToArray();
        var byScheme = new Dictionary<string, ProviderDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in ordered)
        {
            foreach (var scheme in provider.Schemes)
            {
                if (!byScheme.TryAdd(scheme, provider))
                    throw new ArgumentException(
                        $"Scheme '{scheme}' is registered by both '{byScheme[scheme].Name}' and '{provider.Name}'.",
                        nameof(providers));
            }
        }

        Providers = Array.AsReadOnly(ordered);
        _byScheme = new ReadOnlyDictionary<string, ProviderDescriptor>(byScheme);
    }

    public bool TryGetByScheme(string scheme, out ProviderDescriptor? provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        return _byScheme.TryGetValue(scheme, out provider);
    }
}
