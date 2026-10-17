namespace DubUrl.Cli;

public sealed class CliCapabilities
{
    public IReadOnlyList<int> SchemaDocumentVersions { get; } = [1];
    public IReadOnlyList<string> SchemaCommands { get; } = [];
    public IReadOnlyList<string> Dialects { get; } =
    [
        "cockroachdb", "cratedb", "duckdb", "mariadb", "mssql", "mysql", "pgsql",
        "questdb", "singlestore", "sqlite"
    ];
    public IReadOnlyList<string> Providers { get; } = ["Microsoft.Data.Sqlite"];
    public bool DeploymentAvailable => Providers.Count > 0;
}
