using DubUrl.Mapping;
using DubUrl.Querying.Dialects;

namespace DubUrl.Cli;

public sealed class CliCapabilities
{
    public IReadOnlyList<int> SchemaDocumentVersions { get; } = [1];
    public IReadOnlyList<string> SchemaCommands { get; } = ["validate", "render", "deploy", "inspect"];
    public IReadOnlyList<string> Dialects { get; }
    public IReadOnlyList<string> Providers { get; }
    public IReadOnlyList<string> DeployableSchemes { get; }
    public IReadOnlyList<string> DiscoverableSchemes { get; init; } = [];
    public bool DeploymentAvailable => DeployableSchemes.Count > 0;

    public CliCapabilities(
        IDialectRegistry dialects,
        IEnumerable<string> providers,
        IEnumerable<string>? optionalProviderSchemes = null)
    {
        ArgumentNullException.ThrowIfNull(dialects);
        ArgumentNullException.ThrowIfNull(providers);

        Dialects = dialects.GetAll()
            .SelectMany(dialect => dialect.Aliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Providers = providers
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        DeployableSchemes = GetDeployableSchemes(Providers)
            .Concat(optionalProviderSchemes ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] GetDeployableSchemes(IEnumerable<string> providers)
    {
        var registeredProviders = providers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new NativeMapperIntrospector().Locate()
            .Where(mapping => registeredProviders.Contains(mapping.ProviderInvariantName))
            .SelectMany(mapping => mapping.Aliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
