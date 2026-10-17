using DubUrl.Querying.Dialects;

namespace DubUrl.Cli;

public sealed class CliCapabilities(IEnumerable<string> providers)
{
    public IReadOnlyList<int> SchemaDocumentVersions { get; } = [1];
    public IReadOnlyList<string> SchemaCommands { get; } = ["validate", "render", "deploy"];
    public IReadOnlyList<string> Dialects { get; } =
    [
        "cockroachdb", "cratedb", "duckdb", "mariadb", "mssql", "mysql", "pgsql",
        "questdb", "singlestore", "sqlite"
    ];
    public IReadOnlyList<string> Providers { get; } = providers
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    public bool DeploymentAvailable => Providers.Count > 0;

    internal string? FindDialect(IDialect dialect)
        => dialect.Aliases.FirstOrDefault(alias => Dialects.Contains(alias, StringComparer.OrdinalIgnoreCase));
}
