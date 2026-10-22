using System.Reflection;
using DubUrl.Core.ProviderPacks;
using DubUrl.Mapping;

namespace DubUrl.BulkCopy.Testing;

internal static class ConnectorFixture
{
    public static AttributeProviderModule Create() => AttributeProviderModule.FromConnector(new ConnectorDefinition
    {
        Provider = "duckdb-native", Version = "0.0.0", DubUrlApiRange = "*",
        Assembly = "DubUrl.Provider.DuckDb.dll", ImplementationAssemblies = ["DubUrl.Provider.DuckDb.dll"], Driver = "DuckDB.NET.Data",
        Databases = [new("DuckDB", ["duck", "duckdb"])], TargetFrameworks = [$"net{Environment.Version.Major}.0"], RuntimeIdentifiers = []
    }, path => Assembly.Load(Path.GetFileNameWithoutExtension(path)));
}
