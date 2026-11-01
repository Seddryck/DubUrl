using DubUrl.Discovery;
using DubUrl.Provider.Sqlite.Discovery;
using DubUrl.Schema;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.QA;

[Category("Sqlite")]
public class SqliteDiscoveryTests
{
    [Test]
    public async Task AttachedDatabasesKeepIndependentDefinitionsAndViewSources()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            ATTACH DATABASE ':memory:' AS "aux.db";
            CREATE TABLE main.t (raw TEXT, generated TEXT AS (upper(raw)) STORED,
                CONSTRAINT main_check CHECK (length(raw) > 0));
            CREATE TABLE "aux.db".t (raw TEXT, generated TEXT AS (lower(raw)) VIRTUAL,
                CONSTRAINT aux_check CHECK (length(raw) < 10));
            CREATE VIEW main.projection AS SELECT raw FROM main.t;
            CREATE VIEW "aux.db".projection AS SELECT raw FROM t;
            """;
        await command.ExecuteNonQueryAsync();
        var metadata = await new SqliteMetadataReader().ReadAsync(connection, new()
        {
            Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.Generation, SchemaFeature.Checks, SchemaFeature.Views]
        });
        var main = metadata.Tables.Single(table => table.Identity.Database == "main");
        var attached = metadata.Tables.Single(table => table.Identity.Database == "aux.db");
        Assert.Multiple(() =>
        {
            Assert.That(metadata.Diagnostics, Is.Empty);
            Assert.That(metadata.Coverage[SchemaFeature.Checks], Is.EqualTo(MetadataCoverage.Available));
            Assert.That(main.Identity, Is.EqualTo(new MetadataName("t", Database: "main")));
            Assert.That(attached.Identity, Is.EqualTo(new MetadataName("t", Database: "aux.db")));
            Assert.That(main.Columns[1].Generation!.Expression!.Name, Is.EqualTo("Upper"));
            Assert.That(main.Columns[1].Generation!.Stored, Is.True);
            Assert.That(attached.Columns[1].Generation!.Expression!.Name, Is.EqualTo("Lower"));
            Assert.That(attached.Columns[1].Generation!.Stored, Is.False);
            Assert.That(main.Checks.Single().Name, Is.EqualTo("main_check"));
            Assert.That(attached.Checks.Single().Name, Is.EqualTo("aux_check"));
            Assert.That(metadata.Views.Single(view => view.Identity.Database == "main").Source, Is.EqualTo(main.Identity));
            Assert.That(metadata.Views.Single(view => view.Identity.Database == "aux.db").Source, Is.EqualTo(attached.Identity));
        });
    }

    [Test]
    public async Task ReadsGeneratedExpressionsStorageAndCheckNamesWithoutDefaults()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE t (raw TEXT, stored TEXT AS (upper(raw)) STORED,
                virtual TEXT AS (lower(raw)) VIRTUAL,
                CONSTRAINT "length.check" CHECK (length(raw) > 0), CHECK (raw <> 'bad'));
            INSERT INTO t(raw) VALUES (NULL), ('Good');
            """;
        await command.ExecuteNonQueryAsync();
        var metadata = await new SqliteMetadataReader().ReadAsync(connection, new() { Features =
            [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.Generation, SchemaFeature.Checks, SchemaFeature.Defaults] });
        var table = metadata.Tables.Single();
        Assert.Multiple(() =>
        {
            Assert.That(metadata.Diagnostics, Is.Empty);
            Assert.That(metadata.Coverage[SchemaFeature.Checks], Is.EqualTo(MetadataCoverage.Available));
            Assert.That(table.Columns[1].Generation!.Stored, Is.True);
            Assert.That(table.Columns[2].Generation!.Stored, Is.False);
            Assert.That(table.Columns[1].Generation!.Expression!.Name, Is.EqualTo("Upper"));
            Assert.That(table.Columns[1].Default, Is.Null);
            Assert.That(table.Checks, Has.Count.EqualTo(2));
            Assert.That(table.Checks.Select(check => check.Name), Does.Contain("length.check"));
        });
        command.CommandText = "INSERT INTO t(raw) VALUES ('')";
        await Assert.ThatAsync(async () => await command.ExecuteNonQueryAsync(), Throws.TypeOf<SqliteException>());
        var mapped = await new SchemaInspector().InspectAsync(connection, new SqliteMetadataReader(), new()
        {
            Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.Generation, SchemaFeature.Checks]
        });
        Assert.That(mapped.IsComplete, Is.True, string.Join(";", mapped.Diagnostics));
        Assert.That(mapped.Schema!.Tables.Values.Single().Columns["stored"].Generation, Is.Not.Null);
    }

    [Test]
    public async Task UnsupportedSqlKeepsExplicitGenerationAndCheckDiagnostics()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE t (x INTEGER, y INTEGER AS (x + 1), CHECK (x IS NOT NULL))";
        await command.ExecuteNonQueryAsync();
        var options = new DiscoveryOptions { Features =
            [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.Generation, SchemaFeature.Checks] };
        var metadata = await new SqliteMetadataReader().ReadAsync(connection, options);
        Assert.Multiple(() =>
        {
            Assert.That(metadata.Tables.Single().Columns[1].Generation, Is.Null);
            Assert.That(metadata.Tables.Single().Checks, Is.Empty);
            Assert.That(metadata.Diagnostics.Select(item => item.Feature), Is.EquivalentTo(new[] { SchemaFeature.Generation, SchemaFeature.Checks }));
            Assert.That(metadata.Diagnostics.All(item => item.Code == "SDI009"), Is.True);
        });
        var strict = await new SchemaInspector().InspectAsync(connection, new SqliteMetadataReader(), options);
        var partial = await new SchemaInspector().InspectAsync(connection, new SqliteMetadataReader(), options with { AllowPartial = true });
        var basic = await new SchemaInspector().InspectAsync(connection, new SqliteMetadataReader(), options with
            { Features = [SchemaFeature.Tables, SchemaFeature.Columns] });
        Assert.Multiple(() =>
        {
            Assert.That(strict.IsSuccess, Is.False);
            Assert.That(strict.IsComplete, Is.False);
            Assert.That(partial.IsSuccess, Is.True);
            Assert.That(partial.IsComplete, Is.False);
            Assert.That(basic.IsComplete, Is.True);
            Assert.That(basic.Diagnostics, Is.Empty);
        });
    }
}
