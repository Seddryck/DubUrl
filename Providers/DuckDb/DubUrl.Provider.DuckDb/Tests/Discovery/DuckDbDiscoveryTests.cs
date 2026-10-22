using DubUrl.Application;
using DubUrl.Discovery;
using DubUrl.Provider.DuckDb.Discovery;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Schema;
using DuckDB.NET.Data;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Discovery;

public class DuckDbDiscoveryTests
{
    private static readonly DiscoveryOptions Basic = new()
    {
        Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.PrimaryKeys, SchemaFeature.ForeignKeys]
    };

    [Test]
    public async Task LiveCatalog_CompositeKeysFacetsAndQuotedNames_RoundTripThroughRenderer()
    {
        await using var source = new DuckDBConnection("DataSource=:memory:");
        await source.OpenAsync();
        await using (var command = source.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "parent.table" ("second key" INTEGER NOT NULL, "first key" INTEGER NOT NULL, amount DECIMAL(12,3), title VARCHAR,
                  CONSTRAINT parent_pk PRIMARY KEY ("second key","first key"));
                CREATE TABLE child (a INTEGER,b INTEGER, CONSTRAINT child_fk FOREIGN KEY(a,b) REFERENCES "parent.table"("second key","first key"));
                """;
            await command.ExecuteNonQueryAsync();
        }
        var service = new SchemaInspectionService();
        var result = await service.InspectAsync(source, new DuckDbMetadataReader(), Basic);
        Assert.That(result.IsSuccess, Is.True, string.Join(";", result.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        var parent = result.Inspection.Schema!.Tables.Values.Single(table => table.Name == "parent.table");
        Assert.Multiple(() =>
        {
            Assert.That(parent.Columns.Keys, Is.EqualTo(new[] { "second key", "first key", "amount", "title" }));
            Assert.That(parent.Constraints.OfType<DubUrl.Schema.Constraints.PrimaryKeyConstraint>().Single().Columns.Keys,
                Is.EqualTo(new[] { "second key", "first key" }));
            Assert.That(((NumericColumn)parent.Columns["amount"]).Precision, Is.EqualTo(12));
            Assert.That(((NumericColumn)parent.Columns["amount"]).Scale, Is.EqualTo(3));
            Assert.That(source.State, Is.EqualTo(System.Data.ConnectionState.Open));
        });
        var sql = new SchemaScriptRenderer(DubUrl.Mapping.DefaultRegistry.GetDialects().Get<DuckDbDialect>()).Render(new global::DubUrl.Schema.Schema([parent]));
        await using var destination = new DuckDBConnection("DataSource=:memory:");
        await destination.OpenAsync();
        await using (var command = destination.CreateCommand()) { command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
        var reinspected = await service.InspectAsync(destination, new DuckDbMetadataReader(), Basic);
        Assert.That(reinspected.IsSuccess, Is.True, string.Join(";", reinspected.Diagnostics.Select(item => item.Message)));
        Assert.That(reinspected.Document!.Tables.Select(table => table.Identity.Name), Is.EqualTo(new[] { "parent.table" }));
    }

    [Test]
    public async Task LiveCatalog_FilterRequiresExplicitReferenceClosure()
    {
        await using var source = new DuckDBConnection("DataSource=:memory:");
        await source.OpenAsync();
        await using (var command = source.CreateCommand())
        {
            command.CommandText = "CREATE TABLE parent(id INTEGER PRIMARY KEY); CREATE TABLE child(id INTEGER REFERENCES parent(id));";
            await command.ExecuteNonQueryAsync();
        }
        var inspector = new SchemaInspector();
        var filtered = await inspector.InspectAsync(source, new DuckDbMetadataReader(), Basic with { Tables = [new("child", "main")] });
        var closure = await inspector.InspectAsync(source, new DuckDbMetadataReader(), Basic with { Tables = [new("child", "main")], IncludeReferencedTables = true });
        Assert.That(filtered.IsComplete, Is.False);
        Assert.That(filtered.Diagnostics.Select(item => item.Code), Does.Contain("SDI005"));
        Assert.That(closure.IsComplete, Is.True);
    }
}
