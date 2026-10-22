using System.Reflection;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;

namespace DubUrl.Provider.Testing;

internal static class ConnectorFixture
{
    public static ConnectorDefinition Definition => new()
    {
        Provider = "postgresql-npgsql", Version = "0.0.1", DubUrlApiRange = "[0.0.0,1.0.0)",
        Assembly = "DubUrl.Provider.PostgreSql.dll", ImplementationAssemblies = ["DubUrl.Provider.PostgreSql.dll"],
        Driver = "Npgsql", Databases = AttributeProviderModule.Discover(Assembly.Load("DubUrl.Provider.PostgreSql"))
            .Where(mapping => mapping.ProviderInvariantName == "Npgsql")
            .Select(mapping => new ConnectorDatabase(mapping.Database!.Name, mapping.Aliases)).ToArray(),
        TargetFrameworks = [$"net{Environment.Version.Major}.0"], RuntimeIdentifiers = []
    };
    public static AttributeProviderModule Create() => AttributeProviderModule.FromConnector(Definition,
        path => Assembly.Load(Path.GetFileNameWithoutExtension(path)));
}
