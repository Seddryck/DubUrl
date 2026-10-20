using System.Reflection;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;

namespace DubUrl.Provider.Testing;

internal static class ConnectorFixture
{
    public static ConnectorDefinition Definition => new()
    {
        Provider = "postgresql-npgsql", DatabaseName = "PostgreSQL", Version = "0.0.1", DubUrlApiRange = "[0.0.0,1.0.0)",
        Assembly = "DubUrl.Provider.PostgreSql.dll", ImplementationAssemblies = ["DubUrl.Provider.PostgreSql.dll"],
        Driver = "Npgsql", Schemes = ["pg", "pgx", "pgsql", "postgres", "postgresql"],
        TargetFrameworks = [$"net{Environment.Version.Major}.0"], RuntimeIdentifiers = []
    };
    public static AttributeProviderModule Create() => AttributeProviderModule.FromConnector(Definition,
        path => Assembly.Load(Path.GetFileNameWithoutExtension(path)));
}
