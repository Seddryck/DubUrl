using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DubUrl.Schema.Constraints;
using Json.Schema;
using NUnit.Framework;

namespace DubUrl.Schema.Documents.Testing;

public class SchemaDocumentExportTests
{
    [TestCase(SchemaDocumentFormat.Json)]
    [TestCase(SchemaDocumentFormat.Yaml)]
    public void Export_RoundTrip_PreservesOrderFacetsKeysGenerationAndUnicode(SchemaDocumentFormat format)
    {
        var id = new Column("id", DbType.Int32, constraints: [new NotNullableConstraint()], generation: new IdentityColumnGeneration(10, 2));
        var text = new VarLengthColumn("name", DbType.String, 40, "é \"line\"\n😀", [new NullableConstraint()], description: "hello");
        var amount = new NumericColumn("amount", DbType.Decimal, 10, 3, 12.125m, [new NotNullableConstraint()]);
        var table = new Table(new DatabaseObjectName("quoted.table", "a.schema"), [id, text, amount],
            [new PrimaryKeyConstraint([amount, id], "pk"), new CheckConstraint(new ColumnIdentityExpression("amount"), ">", new ValueExpression(0m), "positive")], "table");
        var schema = new Schema([table], [new Index(new DatabaseObjectName("ix", "a.schema"), table.Identity, [new IndexColumn("name", IndexSortDirection.Descending)], true)]);
        var exporter = new SchemaDocumentExporter();
        using var stream = new MemoryStream();
        new SchemaDocumentWriter().Write(stream, exporter.Map(schema), format);
        Assert.That(stream.CanWrite, Is.True);
        stream.Position = 0;
        var result = new SchemaDocumentMapper().Map(new SchemaDocumentReader().Read(stream, format));
        var actual = result.Tables.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(actual.Identity, Is.EqualTo(table.Identity));
            Assert.That(actual.Columns.Keys, Is.EqualTo(table.Columns.Keys));
            Assert.That(actual.Columns["name"].DefaultValue, Is.EqualTo(text.DefaultValue));
            Assert.That(((NumericColumn)actual.Columns["amount"]).Scale, Is.EqualTo(3));
            Assert.That(((IdentityColumnGeneration)actual.Columns["id"].Generation!).Increment, Is.EqualTo(2));
            Assert.That(actual.Constraints.OfType<PrimaryKeyConstraint>().Single().Columns.Keys, Is.EqualTo(new[] { "amount", "id" }));
            Assert.That(result.Indexes.Values.Single().Columns.Values.Single().SortDirection, Is.EqualTo(IndexSortDirection.Descending));
        });
        if (format == SchemaDocumentFormat.Json)
        {
            var published = JsonSchema.FromText(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "schemas", "schema-document", "v1", "schema.json")), new BuildOptions { SchemaRegistry = new SchemaRegistry() });
            using var json = JsonDocument.Parse(stream.ToArray());
            var validation = published.Evaluate(json.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.That(validation.IsValid, Is.True, JsonSerializer.Serialize(validation));
        }
    }

    [TestCase(SchemaDocumentFormat.Json)]
    [TestCase(SchemaDocumentFormat.Yaml)]
    public void Export_IsDeterministicAcrossCultures(SchemaDocumentFormat format)
    {
        var schema = new Schema([new Table("test", [new Column("value", DbType.Decimal, 12.34m)])]);
        var previous = CultureInfo.CurrentCulture;
        try
        {
            string Export(string culture)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                using var stream = new MemoryStream();
                new SchemaDocumentWriter().Write(stream, new SchemaDocumentExporter().Map(schema), format);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
            Assert.That(Export("fr-BE"), Is.EqualTo(Export("en-US")));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Test]
    public void Export_ScopedNativeType_IsRejectedBecauseV1CannotEncodeScope()
    {
        var column = new Column("value", DbType.String, nativeType: new NativeDatabaseType("JSONB", typeof(DubUrl.Provider.PostgreSql.Sql.PgsqlDialect)));
        Assert.Throws<SchemaDocumentMappingException>(() => new SchemaDocumentExporter().Map(new Schema([new Table("t", [column])])));
    }

    [Test]
    public void Export_UnknownConstraint_IsRejected()
    {
        Assert.Throws<SchemaDocumentMappingException>(() => new SchemaDocumentExporter().Map(
            new Schema([new Table("t", [new Column("value", DbType.Int32, constraints: [new CustomConstraint()])])])));
    }

    [TestCase(SchemaDocumentFormat.Json)]
    [TestCase(SchemaDocumentFormat.Yaml)]
    public void Export_AllRepresentableConstructs_AreStableAfterRoundTrip(SchemaDocumentFormat format)
    {
        var id = new Column("id", DbType.Int32, constraints: [new NotNullableConstraint()]);
        var text = new Column("text", DbType.String, "hello", [
            new MembershipCheckConstraint(new ColumnIdentityExpression("text"),new object?[]{"hello",null},NullMembershipBehavior.MatchNull),
            new RegexCheckConstraint(new ColumnIdentityExpression("text"),"^[a-z]+$",RegexNullBehavior.AllowNull)]);
        var generated = new Column("normalized", DbType.String, generation: new GeneratedColumnGeneration(
            new FunctionExpression("upper", new ColumnIdentityExpression("text")), GeneratedColumnStorage.Stored));
        var table = new Table("sample", [id,text,generated,
            new Column("native",DbType.String,nativeType:new NativeDatabaseType("JSONB"),nativeTypeFallback:NativeTypeFallback.LogicalType),
            new Column("binary",DbType.Binary,new byte[]{0,127,255}),
            new Column("guid",DbType.Guid,Guid.Parse("fe68d064-85a4-466b-8f3d-6290388ce31d")),
            new Column("timestamp",DbType.DateTime,new DateTime(2024,1,2,3,4,5,DateTimeKind.Utc))],
            [new PrimaryKeyConstraint(id, "pk"), new DubUrl.Schema.Constraints.ForeignKeyConstraint("self", ["id"], "sample", ["id"])]);
        var schema = new Schema([table], [], [new View("projection", new ViewQuery(table.Identity, new ViewProjection(new ColumnIdentityExpression("text"), "label")))]);
        byte[] Write(Schema current)
        {
            using var stream = new MemoryStream();
            new SchemaDocumentWriter().Write(stream, new SchemaDocumentExporter().Map(current), format);
            return stream.ToArray();
        }
        var first = Write(schema);
        using var input = new MemoryStream(first);
        var mapped = new SchemaDocumentMapper().Map(new SchemaDocumentReader().Read(input, format));
        Assert.That(Write(mapped), Is.EqualTo(first));
    }
    private sealed class CustomConstraint : IConstraint { }
}
