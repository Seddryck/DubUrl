using System.Data;
using DubUrl.Application;
using DubUrl.Discovery;
using DubUrl.Provider.DuckDb.Discovery;
using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Schema;
using DubUrl.Schema.Documents;
using DuckDB.NET.Data;
using NUnit.Framework;

namespace DubUrl.Provider.DuckDb.Tests.Discovery;

public partial class DuckDbDiscoveryTests
{
    [Test]
    public async Task LiveCatalog_SequenceGenerationAndOrderedIndexes_RoundTripWithoutAdvancingSequence()
    {
        await using var source = new DuckDBConnection("DataSource=:memory:");
        await source.OpenAsync();
        await Execute(source, """
            CREATE SCHEMA "sales.schema";
            CREATE SEQUENCE "sales.schema"."id'seq" START 10 INCREMENT 2;
            CREATE TABLE "sales.schema"."odd.table"(
              id BIGINT DEFAULT nextval('"sales.schema"."id''seq"'),
              "a,b" VARCHAR DEFAULT 'GENERATED ALWAYS AS(x)',
              normalized VARCHAR GENERATED ALWAYS AS (upper("a,b")) VIRTUAL,
              size BIGINT GENERATED ALWAYS AS (length("a,b")) VIRTUAL);
            CREATE UNIQUE INDEX "ix,odd" ON "sales.schema"."odd.table"("a,b" DESC,id ASC);
            """);
        var service = new SchemaInspectionService();
        var options = new DiscoveryOptions { Schemas = ["sales.schema"] };
        var result = await service.InspectAsync(source, new DuckDbMetadataReader(), options);
        Assert.That(result.IsSuccess, Is.True, string.Join(";", result.Diagnostics.Select(item => item.Code + ":" + item.Message)));
        var table = result.Inspection.Schema!.Tables.Values.Single();
        var index = result.Inspection.Schema.Indexes.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(((IdentityColumnGeneration)table.Columns["id"].Generation!).Seed, Is.EqualTo(10));
            Assert.That(((IdentityColumnGeneration)table.Columns["id"].Generation!).Increment, Is.EqualTo(2));
            Assert.That(table.Columns["id"].DefaultValue, Is.Null);
            Assert.That(table.Columns["a,b"].Generation, Is.Null);
            Assert.That(table.Columns["a,b"].DefaultValue, Is.EqualTo("GENERATED ALWAYS AS(x)"));
            Assert.That(((GeneratedColumnGeneration)table.Columns["normalized"].Generation!).Storage, Is.EqualTo(GeneratedColumnStorage.Virtual));
            Assert.That(index.IsUnique, Is.True);
            Assert.That(index.Columns.Keys, Is.EqualTo(new[] { "a,b", "id" }));
            Assert.That(index.Columns.Values.Select(column => column.SortDirection), Is.All.Null,
                "DuckDB discards ASC/DESC; discovery must not invent a direction.");
        });
        Assert.That(await Scalar(source, "SELECT last_value FROM duckdb_sequences()"), Is.EqualTo(DBNull.Value));
        var mapped = new SchemaDocumentMapper().Map(result.Document!);
        var renderer = new SchemaScriptRenderer(DubUrl.Mapping.DefaultRegistry.GetDialects().Get<DuckDbDialect>());
        var sql = renderer.Render(mapped);
        Assert.That(renderer.Render(mapped), Is.EqualTo(sql));
        await using var target = new DuckDBConnection("DataSource=:memory:");
        await target.OpenAsync();
        await Execute(target, """CREATE SCHEMA "sales.schema" """);
        await Execute(target, sql);
        var reinspected = await service.InspectAsync(target, new DuckDbMetadataReader(), options);
        Assert.That(reinspected.IsSuccess, Is.True, string.Join(";", reinspected.Diagnostics.Select(item => item.Message)));
        using var first = new MemoryStream();
        using var second = new MemoryStream();
        var writer = new SchemaDocumentWriter();
        writer.Write(first, result.Document!, SchemaDocumentFormat.Json);
        writer.Write(second, reinspected.Document!, SchemaDocumentFormat.Json);
        Assert.That(second.ToArray(), Is.EqualTo(first.ToArray()));
        await Execute(target, """INSERT INTO "sales.schema"."odd.table"("a,b") VALUES ('hello'),('world')""");
        Assert.That(await Scalar(target, """SELECT string_agg(id::VARCHAR,',' ORDER BY id) FROM "sales.schema"."odd.table" """), Is.EqualTo("10,12"));
        Assert.That(await Scalar(target, """SELECT normalized FROM "sales.schema"."odd.table" WHERE id=10"""), Is.EqualTo("HELLO"));
        Assert.That(await Scalar(target, """SELECT size FROM "sales.schema"."odd.table" WHERE id=10"""), Is.EqualTo(5L));
    }

    [TestCase(0, 2)]
    [TestCase(-3, -2)]
    public async Task Render_IdentitySeedAndStep_AndDropExistingResetAreExecutable(long seed, long step)
    {
        var schema = new global::DubUrl.Schema.Schema([new Table("ids",
            [new Column("id", DbType.Int64, generation: new IdentityColumnGeneration(seed, step))])]);
        var dialect = DubUrl.Mapping.DefaultRegistry.GetDialects().Get<DuckDbDialect>();
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await Execute(connection, new SchemaScriptRenderer(dialect).Render(schema));
        await Execute(connection, "INSERT INTO ids DEFAULT VALUES; INSERT INTO ids DEFAULT VALUES");
        Assert.That(await Scalar(connection, "SELECT sum(id)::BIGINT FROM ids"), Is.EqualTo(seed + seed + step));
        await Execute(connection, new SchemaScriptRenderer(dialect, SchemaCreationOptions.DropIfExists).Render(schema));
        await Execute(connection, "INSERT INTO ids DEFAULT VALUES");
        Assert.That(await Scalar(connection, "SELECT id FROM ids"), Is.EqualTo(seed));
        var result = await new SchemaInspector().InspectAsync(connection, new DuckDbMetadataReader());
        Assert.That(result.IsComplete, Is.True, string.Join(";", result.Diagnostics.Select(item => item.Message)));
        var identity = (IdentityColumnGeneration)result.Schema!.Tables.Values.Single().Columns["id"].Generation!;
        Assert.That(identity.Seed, Is.EqualTo(seed));
        Assert.That(identity.Increment, Is.EqualTo(step));
    }

    [TestCase("CREATE SEQUENCE s CYCLE; CREATE TABLE t(id BIGINT DEFAULT nextval('s'))")]
    [TestCase("CREATE SEQUENCE s MAXVALUE 100; CREATE TABLE t(id BIGINT DEFAULT nextval('s'))")]
    [TestCase("CREATE SEQUENCE s; CREATE TABLE t(a BIGINT DEFAULT nextval('s'),b BIGINT DEFAULT nextval('s'))")]
    [TestCase("CREATE SEQUENCE s; CREATE TABLE t(id BIGINT DEFAULT nextval('s')); CREATE TABLE other(id BIGINT DEFAULT nextval('s'))")]
    [TestCase("CREATE SEQUENCE s; CREATE TABLE t(id BIGINT DEFAULT (nextval('s')+1))")]
    [TestCase("CREATE SEQUENCE s; CREATE TABLE t(a BIGINT DEFAULT (nextval('s')+1),b BIGINT DEFAULT nextval('s'))")]
    public async Task LiveCatalog_UnrepresentableSequenceSemantics_AreNotFlattenedIntoIdentity(string ddl)
    {
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await Execute(connection, ddl);
        var result = await new SchemaInspectionService().InspectAsync(connection, new DuckDbMetadataReader());
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Inspection.Coverage[SchemaFeature.Generation], Is.EqualTo(MetadataCoverage.Unrepresentable));
        Assert.That(result.Document, Is.Null);
        Assert.That(result.Diagnostics.Any(item => item.Feature == SchemaFeature.Generation && item.Code == "SDI009"), Is.True);
        Assert.That(await Scalar(connection, "SELECT last_value FROM duckdb_sequences()"), Is.EqualTo(DBNull.Value));
    }

    [Test]
    public async Task LiveCatalog_ExpressionIndexesAndUnsupportedGeneration_AreDiagnosed()
    {
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await Execute(connection, """
            CREATE TABLE t(id INTEGER,title VARCHAR,calculated INTEGER AS (id+1));
            CREATE INDEX ordinary ON t(title,id);
            CREATE INDEX expression_index ON t(lower(title));
            """);
        var result = await new SchemaInspector().InspectAsync(connection, new DuckDbMetadataReader(), new() { AllowPartial = true });
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.IsComplete, Is.False);
        Assert.That(result.Schema!.Indexes.Values.Single().Name, Is.EqualTo("ordinary"));
        Assert.That(result.Schema.Tables.Values.Single().Columns["calculated"].DefaultValue, Is.Null);
        Assert.That(result.Diagnostics.Select(item => item.Feature), Does.Contain(SchemaFeature.Generation));
        Assert.That(result.Diagnostics.Select(item => item.Feature), Does.Contain(SchemaFeature.Indexes));
    }

    [Test]
    public async Task LiveCatalog_GeneratedExpression_IsNotMistakenForALiteralDefault()
    {
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await Execute(connection, "CREATE TABLE t(a INTEGER,b INTEGER AS (a))");
        var result = await new SchemaInspector().InspectAsync(connection, new DuckDbMetadataReader(), new()
        { Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.Defaults] });
        Assert.That(result.IsComplete, Is.True, string.Join(";", result.Diagnostics.Select(item => item.Message)));
        Assert.That(result.Schema!.Tables.Values.Single().Columns["b"].Generation, Is.Null);
        Assert.That(result.Schema.Tables.Values.Single().Columns["b"].DefaultValue, Is.Null);
    }

    private static async Task Execute(DuckDBConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> Scalar(DuckDBConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
