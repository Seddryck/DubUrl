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
    public IReadOnlyList<string> ConnectionCommands { get; } = ["check"];
    public IReadOnlyList<string> ConnectionCheckModes { get; } = ["connection", "engine", "table"];
    public IReadOnlyList<string> CredentialCommands { get; } = ["add", "remove", "exists"];
    public IReadOnlyList<string> AuthenticationOptions { get; } = ["--password-prompt", "--username", "--credential", "--os-identity", "--client-certificate", "--client-key", "--ca-certificate", "--certificate-password-prompt", "--certificate-credential", "--oauth", "--oauth-client-id", "--oauth-tenant", "--oauth-authority", "--oauth-scope", "--oauth-redirect-uri", "--oauth-login-hint", "--oauth-account", "--oauth-timeout", "--allow-oauth-interaction"];
    public IReadOnlyList<string> OAuthFlows { get; } = ["sqlserver:browser", "sqlserver:device-code", "snowflake:browser"];
    public IReadOnlyList<string> CredentialStoreBackends { get; } = ["windows-credential-manager", "macos-keychain", "linux-secret-service"];
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
