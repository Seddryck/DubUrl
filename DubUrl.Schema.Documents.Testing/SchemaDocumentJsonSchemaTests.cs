using System.Text.Json;
using System.Text.Json.Serialization;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Documents.V1;
using Json.Schema;
using NUnit.Framework;

namespace DubUrl.Schema.Documents.Testing;

public class SchemaDocumentJsonSchemaTests
{
    private const string SchemaId = "https://seddryck.github.io/DubUrl/schemas/schema-document/v1/schema.json";
    private static readonly string SchemaPath = Path.Combine(
        TestContext.CurrentContext.TestDirectory, "schemas", "schema-document", "v1", "schema.json");
    private static readonly EvaluationOptions EvaluationOptions = new() { OutputFormat = OutputFormat.List };
    private static readonly JsonSchema PublishedSchema = JsonSchema.FromText(File.ReadAllText(SchemaPath));

    private static readonly IReadOnlyDictionary<Type, string?> DtoSchemas = new Dictionary<Type, string?>
    {
        [typeof(SchemaDocument)] = null,
        [typeof(DatabaseObjectNameDocument)] = "databaseObjectName",
        [typeof(TableDocument)] = "table",
        [typeof(ColumnDocument)] = "column",
        [typeof(NativeTypeDocument)] = "nativeType",
        [typeof(IdentityColumnDocument)] = "identityColumn",
        [typeof(GeneratedColumnDocument)] = "generatedColumn",
        [typeof(LiteralDocument)] = "literal",
        [typeof(ExpressionDocument)] = "expression",
        [typeof(CheckDocument)] = "check",
        [typeof(PrimaryKeyDocument)] = "primaryKey",
        [typeof(ForeignKeyDocument)] = "foreignKey",
        [typeof(IndexDocument)] = "index",
        [typeof(IndexColumnDocument)] = "indexColumn",
        [typeof(ViewDocument)] = "view",
        [typeof(ViewQueryDocument)] = "viewQuery",
        [typeof(ViewProjectionDocument)] = "viewProjection"
    };

    [Test]
    public void PublishedSchema_IsValidDraft202012SchemaWithStableIdentity()
    {
        using var schemaDocument = JsonDocument.Parse(File.ReadAllText(SchemaPath));

        var result = MetaSchemas.Draft202012.Evaluate(schemaDocument.RootElement, EvaluationOptions);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True, JsonSerializer.Serialize(result));
            Assert.That(schemaDocument.RootElement.GetProperty("$schema").GetString(),
                Is.EqualTo("https://json-schema.org/draft/2020-12/schema"));
            Assert.That(schemaDocument.RootElement.GetProperty("$id").GetString(), Is.EqualTo(SchemaId));
        });
    }

    [Test]
    public void PublishedSchema_DescribesEveryDtoPropertyAndRejectsUnknownProperties()
    {
        using var schemaDocument = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        var root = schemaDocument.RootElement;

        Assert.Multiple(() =>
        {
            foreach (var (type, definition) in DtoSchemas)
            {
                var schema = definition is null
                    ? root
                    : root.GetProperty("$defs").GetProperty(definition);
                var actual = schema.GetProperty("properties").EnumerateObject()
                    .Select(property => property.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                var expected = type.GetProperties()
                    .Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();

                Assert.That(actual, Is.EqualTo(expected), $"JSON Schema drift for {type.Name}");
                Assert.That(schema.GetProperty("additionalProperties").GetBoolean(), Is.False,
                    $"Unknown-property policy for {type.Name}");
                foreach (var property in schema.GetProperty("properties").EnumerateObject())
                    Assert.That(property.Value.TryGetProperty("description", out _), Is.True,
                        $"Missing description for {type.Name}.{property.Name}");
            }
        });
    }

    [TestCase("schema.json")]
    [TestCase("schema.yaml")]
    public void PublishedExamples_ConformToSchemaAndDocumentValidator(string fileName)
    {
        var path = ExamplePath(fileName);
        var source = ReadExampleAsJson(path);
        var schemaResult = Evaluate(source);

        Assert.Multiple(() =>
        {
            Assert.That(schemaResult.IsValid, Is.True,
                $"JSON Schema validation failed for {fileName}: {JsonSerializer.Serialize(schemaResult)}");
            var document = new SchemaDocumentReader().Read(path);
            Assert.That(new SchemaDocumentValidator().Validate(document), Is.Empty,
                $"Document validation failed for {fileName}");
        });
    }

    [TestCase("{\"version\":2}")]
    [TestCase("{\"version\":1,\"unknown\":true}")]
    [TestCase("{\"version\":1,\"views\":[{\"identity\":{\"name\":\"v\"}}]}")]
    [TestCase("{\"version\":1,\"tables\":[{\"identity\":{\"name\":\"t\"},\"columns\":[{\"name\":\"id\",\"type\":\"int32\",\"primaryKey\":true}]}]}")]
    [TestCase("{\"version\":1,\"tables\":[{\"identity\":{\"name\":\"t\"},\"columns\":[{\"name\":\"value\",\"type\":\"string\",\"length\":0}]}]}")]
    [TestCase("{\"version\":1,\"indexes\":[{\"identity\":{\"name\":\"i\"},\"table\":{\"name\":\"t\"},\"columns\":[{\"name\":\"c\",\"direction\":\"sideways\"}]}]}")]
    public void InvalidDocuments_AreRejected(string source)
        => Assert.That(Evaluate(source).IsValid, Is.False);

    [Test]
    public void JsonExample_ReadValidateMapRender_ProducesExpectedPostgresqlScript()
    {
        var document = new SchemaDocumentReader().Read(ExamplePath("schema.json"));
        var domain = new SchemaDocumentMapper().Map(document);
        var dialectBuilder = new DialectRegistryBuilder();
        dialectBuilder.AddDialect<PgsqlDialect>(["pgsql"]);
        var dialects = dialectBuilder.Build();

        var script = new SchemaScriptRenderer(dialects.Get<PgsqlDialect>()).Render(domain);
        var dropScript = new SchemaScriptRenderer(
            dialects.Get<PgsqlDialect>(), SchemaCreationOptions.DropIfExists).Render(domain);

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("CONSTRAINT \"pk_customers\" PRIMARY KEY (\"tenant_id\", \"id\")"));
            Assert.That(script, Does.Contain("\"name_length\" INTEGER GENERATED ALWAYS AS (LENGTH(\"name\")) STORED"));
            Assert.That(script, Does.Contain("CONSTRAINT \"ck_orders_amount\" CHECK \"amount\" >= 0"));
            Assert.That(script, Does.Contain("CONSTRAINT \"fk_orders_customer\" FOREIGN KEY (\"tenant_id\", \"customer_id\") REFERENCES \"sales\".\"customers\" (\"tenant_id\", \"id\")"));
            Assert.That(script, Does.Contain("CREATE UNIQUE INDEX \"sales\".\"ux_orders_tenant_amount\" ON \"sales\".\"orders\""));
            Assert.That(script, Does.Contain("\"tenant_id\" ASC").And.Contain("\"amount\" DESC"));
            Assert.That(script.IndexOf("CREATE UNIQUE INDEX", StringComparison.Ordinal),
                Is.GreaterThan(script.LastIndexOf("CREATE TABLE", StringComparison.Ordinal)),
                "Indexes must be rendered after table definitions, not inside CREATE TABLE.");
            var firstView = script.IndexOf(
                "CREATE VIEW \"reporting\".\"customer_names\"", StringComparison.Ordinal);
            var secondView = script.IndexOf(
                "CREATE VIEW \"reporting\".\"customer_name_lengths\"", StringComparison.Ordinal);
            Assert.That(firstView, Is.GreaterThan(script.IndexOf("CREATE UNIQUE INDEX", StringComparison.Ordinal)));
            Assert.That(secondView, Is.GreaterThan(firstView));
            Assert.That(script, Does.Contain("LENGTH(\"name\") AS \"name_length\""));

            var secondDrop = dropScript.IndexOf(
                "DROP VIEW IF EXISTS \"reporting\".\"customer_name_lengths\"", StringComparison.Ordinal);
            var firstDrop = dropScript.IndexOf(
                "DROP VIEW IF EXISTS \"reporting\".\"customer_names\"", StringComparison.Ordinal);
            var tableDrop = dropScript.IndexOf("DROP TABLE IF EXISTS", StringComparison.Ordinal);
            Assert.That(secondDrop, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstDrop, Is.GreaterThan(secondDrop));
            Assert.That(tableDrop, Is.GreaterThan(firstDrop));
        });
    }

    private static EvaluationResults Evaluate(string source)
    {
        using var instance = JsonDocument.Parse(source);
        return PublishedSchema.Evaluate(instance.RootElement, EvaluationOptions);
    }

    private static string ReadExampleAsJson(string path)
    {
        if (Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase))
            return File.ReadAllText(path);

        var document = new SchemaDocumentReader().Read(path);
        return JsonSerializer.Serialize(document, document.GetType(), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static string ExamplePath(string fileName)
        => Path.Combine(TestContext.CurrentContext.TestDirectory,
            "schemas", "schema-document", "v1", "examples", fileName);
}
