using System.Data;
using System.Data.Common;
using DubUrl.Discovery;
using DubUrl.Provider.MySql.Discovery;
using DubUrl.Provider.MySql.Mapping;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.Tests.Discovery;

public class MySqlMetadataReaderTests
{
    [TestCase(typeof(MySqlConnectorMapper))]
    [TestCase(typeof(MySqlDataMapper))]
    public void NativeMappers_RegisterReader(Type mapper)
        => Assert.That(((SchemaDiscoveryAttribute)Attribute.GetCustomAttribute(mapper, typeof(SchemaDiscoveryAttribute))!).Create().Reader,
            Is.TypeOf<MySqlMetadataReader>());

    [TestCase(typeof(MariaDbConnectorMapper))]
    [TestCase(typeof(MariaDbDataRewriter))]
    public void CompatibleProviders_DoNotInheritMySqlDiscovery(Type mapper)
        => Assert.That((Attribute.GetCustomAttribute(mapper, typeof(SchemaDiscoveryAttribute)) as SchemaDiscoveryAttribute)?.Create().Reader,
            Is.Not.TypeOf<MySqlMetadataReader>());

    [Test]
    public async Task LiteralDefaultsAndFacets_AreTypedAndOrdered()
    {
        var connection = Catalog(Table(["database_name", "table_name"], ["db", "t"]),
            Table(["database_name", "table_name", "column_name", "ordinal", "type_name", "native_type_name", "nullable", "length", "precision", "scale", "default_expression", "extra"],
                ["db", "t", "amount", 2, "decimal", "decimal(10,2)", "NO", null, 10, 2, "12.25", ""],
                ["db", "t", "title", 1, "varchar", "varchar(40)", "YES", 40, null, null, "O'Brien", ""]));
        var result = await new MySqlMetadataReader().ReadAsync(connection.Object, new() { Features = [SchemaFeature.Columns, SchemaFeature.Defaults] });
        var table = result.Tables.Single();
        Assert.That(table.Identity, Is.EqualTo(new MetadataName("t", Database: "db")));
        Assert.That(table.Columns.Select(c => c.Name), Is.EqualTo(new[] { "title", "amount" }));
        Assert.That(table.Columns[0].Default, Is.EqualTo("O'Brien"));
        Assert.That(table.Columns[0].Length, Is.EqualTo(40));
        Assert.That(table.Columns[0].Nullable, Is.True);
        Assert.That(table.Columns[1].Default, Is.EqualTo(12.25m));
        Assert.That(table.Columns[1].Precision, Is.EqualTo(10));
        Assert.That(table.Columns[1].Scale, Is.EqualTo(2));
        Assert.That(result.Diagnostics, Is.Empty);
        connection.Verify(c => c.Close(), Times.Never);
        connection.Protected().Verify("Dispose", Times.Never(), ItExpr.IsAny<bool>());
    }

    [Test]
    public async Task UnsupportedGenerationDefaultsAndTypes_HaveObjectDiagnostics()
    {
        var connection = Catalog(Table(["database_name", "table_name"], ["db", "t"]),
            Table(["database_name", "table_name", "column_name", "ordinal", "type_name", "native_type_name", "extra", "default_expression"],
                ["db", "t", "id", 1, "int", "int", "auto_increment", null],
                ["db", "t", "stamp", 2, "timestamp", "timestamp", "DEFAULT_GENERATED", "CURRENT_TIMESTAMP"],
                ["db", "t", "unsigned_value", 3, "int", "int unsigned", "", null]));
        var result = await new MySqlMetadataReader().ReadAsync(connection.Object, new() { Features = [SchemaFeature.Columns, SchemaFeature.Generation, SchemaFeature.Defaults] });
        Assert.That(result.Diagnostics.Select(d => d.Feature), Is.EquivalentTo(new[] { SchemaFeature.Generation, SchemaFeature.Defaults, SchemaFeature.Columns }));
        Assert.That(result.Diagnostics.All(d => d.Code == "SDI009" && d.Object == new MetadataName("t", Database: "db") && d.Coverage == MetadataCoverage.Unrepresentable), Is.True);
        Assert.That(result.Tables.Single().Columns[0].Generation, Is.Null);
    }

    [TestCase("select `db`.`t`.`a` AS `label` from `db`.`t`", true)]
    [TestCase("select `a` from `t`", true)]
    [TestCase("select count(*) AS total from `db`.`t`", false)]
    [TestCase("select `a` from `db`.`t` where a > 0", false)]
    [TestCase("select `other`.`t`.`a` from `db`.`t`", false)]
    public async Task Views_PreserveDatabaseIdentityAndRejectNonPortableSql(string definition, bool portable)
    {
        var connection = Catalog(new DataTable(), new DataTable(),
            Table(["database_name", "table_name", "definition", "column_name", "ordinal", "type_name"], ["db", "v", definition, "label", 1, "int"]));
        var result = await new MySqlMetadataReader().ReadAsync(connection.Object, new() { Features = [SchemaFeature.Views] });
        var view = result.Views.Single();
        Assert.That(view.Source, Is.EqualTo(portable ? new MetadataName("t", Database: "db") : null));
        Assert.That(view.Columns.Single().Name, Is.EqualTo("label"));
    }

    [Test]
    public async Task CatalogFailure_DoesNotExposeDriverMessage()
    {
        var connection = new Mock<DbConnection>();
        var command = new Mock<DbCommand>();
        command.Protected().Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new CatalogException());
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        var result = await new MySqlMetadataReader().ReadAsync(connection.Object, new());
        Assert.That(result.Coverage.Values, Is.All.EqualTo(MetadataCoverage.Inaccessible));
        Assert.That(result.ToString(), Does.Not.Contain("sensitive"));
    }

    private sealed class CatalogException() : DbException("sensitive");

    private static DataTable Table(string[] columns, params object?[][] rows)
    {
        var table = new DataTable();
        foreach (var name in columns) table.Columns.Add(name, typeof(object));
        foreach (var row in rows) table.Rows.Add(row.Select(v => v ?? DBNull.Value).ToArray());
        return table;
    }

    private static Mock<DbConnection> Catalog(params DataTable[] tables)
    {
        var queue = new Queue<DataTable>(tables);
        var connection = new Mock<DbConnection>();
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(() =>
        {
            var command = new Mock<DbCommand>();
            command.SetupProperty(c => c.CommandText);
            command.Protected().Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
                .Returns(() => Task.FromResult<DbDataReader>(queue.Dequeue().CreateDataReader()));
            return command.Object;
        });
        return connection;
    }
}
