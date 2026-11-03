using System.Data.Common;
using DubUrl.Discovery;
using Moq;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Discovery;

public class CatalogNamespaceTests
{
    [TestCase("parent")]
    [TestCase("server.store.sales.parent")]
    public async Task CatalogMapping_PreservesAllNativeQualifiers(string source)
    {
        var metadata = await new Reader(source).ReadAsync(new Mock<DbConnection>().Object, new()
        { Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.ForeignKeys, SchemaFeature.Views] });
        var parent = new MetadataName("parent", "sales", "store", "server");
        Assert.That(metadata.Tables.Single(table => table.Identity.Name == "parent").Identity, Is.EqualTo(parent));
        Assert.That(metadata.Tables.Single(table => table.Identity.Name == "child").ForeignKeys.Single().Target, Is.EqualTo(parent));
        Assert.That(metadata.Views.Single().Source, Is.EqualTo(parent));
    }

    private sealed class Reader(string source) : CatalogSchemaMetadataReader
    {
        protected override string TablesSql => "tables";
        protected override string ColumnsSql => "columns";
        protected override string KeysSql => "keys";
        protected override string ViewsSql => "views";
        private static CatalogRow Row(string table, params (string Name, object? Value)[] fields)
            => new CatalogRow(new Dictionary<string, object?>
            { ["table_name"] = table, ["schema_name"] = "sales", ["database_name"] = "store", ["catalog_name"] = "server" }).With(fields);

        protected override Task<List<CatalogRow>> ReadCatalogRowsAsync(DbConnection connection, string sql,
            DiscoveryOptions options, CancellationToken cancellationToken)
            => Task.FromResult<List<CatalogRow>>(sql switch
            {
                "tables" => [Row("parent"), Row("child")],
                "columns" => [Row("parent", ("column_name", "id"), ("type_name", "integer"), ("nullable", 0)),
                    Row("child", ("column_name", "id"), ("type_name", "integer"), ("nullable", 0))],
                "keys" => [Row("child", ("kind", "FOREIGN KEY"), ("key_name", "fk"), ("column_name", "id"),
                    ("target_table", "parent"), ("target_schema", "sales"), ("target_database", "store"),
                    ("target_catalog", "server"), ("target_column", "id"))],
                "views" => [Row("projection", ("definition", "SELECT id FROM " + source),
                    ("column_name", "id"), ("type_name", "integer"), ("nullable", 0))],
                _ => []
            });
    }
}
