using System.Data;
using System.Data.Common;
using Moq;
using Moq.Protected;
using DubUrl.Discovery;
using DubUrl.Provider.MySql.Discovery;
using DubUrl.Provider.MySql.Mapping;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.Tests.Schema;

public class SingleStoreDiscoveryTests
{
    [Test]
    public void DiscoveryRegistrationDescribesNativeCoverage()
    {
        var attribute = (SchemaDiscoveryAttribute)Attribute.GetCustomAttribute(typeof(SingleStoreMapper), typeof(SchemaDiscoveryAttribute))!;
        var reader = attribute.Create().Reader;
        Assert.That(reader, Is.TypeOf<SingleStoreMetadataReader>());
        foreach (var feature in Enum.GetValues<SchemaFeature>())
            Assert.That(reader.Capabilities[feature], Is.EqualTo(feature is SchemaFeature.ForeignKeys or SchemaFeature.Checks
                ? MetadataCoverage.Unsupported : MetadataCoverage.Available), feature.ToString());
    }
    [TestCase("SELECT `db`.`items`.`code` AS `label` FROM `db`.`items`", "SELECT `code` AS `label` FROM `db`.`items`")]
    [TestCase("SELECT `items`.`code` AS `label` FROM `items` as `items`  /*!90623 OPTION(CLIENT_FOUND_ROWS=1)*/", "SELECT `code` AS `label` FROM `items`")]
    [TestCase("select i.code AS label from db.items AS i", "SELECT `code` AS label FROM db.items")]
    [TestCase("SELECT count(*) AS n FROM db.items", "SELECT count(*) AS n FROM db.items")]
    [TestCase("SELECT code FROM db.items WHERE id=1", "SELECT code FROM db.items WHERE id=1")]
    [TestCase("SELECT other.items.code FROM db.items", "SELECT other.items.code FROM db.items")]
    public void PortableViewsNormalizeOnlyMatchingNativeIdentifiers(string sql, string expected)
        => Assert.That(SingleStoreMetadataReader.NormalizeViewDefinition(sql), Is.EqualTo(expected));
    [Test]
    public async Task NativeViewFlowsThroughCatalogMapping()
    {
        var connection = new Mock<DbConnection>();
        var command = new Mock<DbCommand>();
        command.SetupAllProperties();
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        command.Protected().Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                var data = new DataTable();
                if (command.Object.CommandText.Contains("information_schema.views"))
                {
                    foreach (var name in new[] { "database_name", "table_name", "column_name", "definition", "type_name", "nullable" }) data.Columns.Add(name);
                    data.Rows.Add("db", "item_view", "label", "SELECT `items`.`code` AS `label` FROM `items` as `items` ", "varchar", "YES");
                }
                return Task.FromResult<DbDataReader>(data.CreateDataReader());
            });
        var result = await new SingleStoreMetadataReader().ReadAsync(connection.Object, new());
        Assert.That(result.Views.Single().Source, Is.EqualTo(new MetadataName("items", Database: "db")));
    }
}
