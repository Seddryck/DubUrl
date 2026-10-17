using System.Text;
using DubUrl.Schema.Documents.V1;
using NUnit.Framework;

namespace DubUrl.Schema.Documents.Testing;

public class SchemaDocumentReaderTests
{
    private const string JsonDocument = """
        {
          "version": 1,
          "tables": [
            {
              "identity": { "schema": "sales", "name": "orders" },
              "description": "Placed orders",
              "columns": [
                { "name": "tenant_id", "type": "int32", "nullable": false },
                { "name": "order_id", "type": "int64", "identity": { "seed": 10, "increment": 5 } },
                {
                  "name": "total",
                  "type": "decimal",
                  "precision": 12,
                  "scale": 2,
                  "default": { "type": "decimal", "value": "0.00" },
                  "checks": [
                    {
                      "name": "ck_total",
                      "kind": "comparison",
                      "left": { "kind": "column", "name": "total" },
                      "operator": ">=",
                      "right": { "kind": "value", "value": { "type": "decimal", "value": "0.00" } }
                    }
                  ]
                },
                {
                  "name": "taxed_total",
                  "type": "decimal",
                  "generated": {
                    "storage": "stored",
                    "expression": {
                      "kind": "function",
                      "function": "multiply",
                      "arguments": [
                        { "kind": "column", "name": "total" },
                        { "kind": "value", "value": { "type": "decimal", "value": "1.21" } }
                      ]
                    }
                  }
                }
              ],
              "primaryKey": { "name": "pk_orders", "columns": ["tenant_id", "order_id"] },
              "checks": [
                {
                  "name": "ck_tenant",
                  "kind": "membership",
                  "expression": { "kind": "column", "name": "tenant_id" },
                  "values": [{ "type": "int32", "value": "1" }, { "type": "int32", "value": "2" }]
                }
              ],
              "foreignKeys": [
                {
                  "name": "fk_orders_tenant",
                  "sourceColumns": ["tenant_id"],
                  "targetTable": { "schema": "sales", "name": "tenants" },
                  "targetColumns": ["id"]
                }
              ]
            },
            {
              "identity": { "schema": "sales", "name": "tenants" },
              "columns": [{ "name": "id", "type": "int32" }]
            }
          ],
          "indexes": [
            {
              "identity": { "schema": "sales", "name": "ux_orders_total" },
              "table": { "schema": "sales", "name": "orders" },
              "unique": true,
              "columns": [
                { "name": "tenant_id", "direction": "ascending" },
                { "name": "total", "direction": "descending" }
              ]
            }
          ],
          "views": [
            {
              "identity": { "schema": "reporting", "name": "order_totals" },
              "query": {
                "source": { "schema": "sales", "name": "orders" },
                "projections": [
                  { "expression": { "kind": "column", "name": "tenant_id" } },
                  {
                    "expression": {
                      "kind": "function",
                      "function": "round",
                      "arguments": [{ "kind": "column", "name": "total" }]
                    },
                    "alias": "rounded_total"
                  }
                ]
              }
            },
            {
              "identity": { "schema": "reporting", "name": "rounded_order_totals" },
              "query": {
                "source": { "schema": "reporting", "name": "order_totals" },
                "projections": [{ "expression": { "kind": "column", "name": "rounded_total" } }]
              }
            }
          ]
        }
        """;

    private const string YamlDocument = """
        version: 1
        tables:
          - identity: { schema: sales, name: orders }
            description: Placed orders
            columns:
              - { name: tenant_id, type: int32, nullable: false }
              - name: order_id
                type: int64
                identity: { seed: 10, increment: 5 }
              - name: total
                type: decimal
                precision: 12
                scale: 2
                default: { type: decimal, value: "0.00" }
                checks:
                  - name: ck_total
                    kind: comparison
                    left: { kind: column, name: total }
                    operator: ">="
                    right: { kind: value, value: { type: decimal, value: "0.00" } }
              - name: taxed_total
                type: decimal
                generated:
                  storage: stored
                  expression:
                    kind: function
                    function: multiply
                    arguments:
                      - { kind: column, name: total }
                      - { kind: value, value: { type: decimal, value: "1.21" } }
            primaryKey: { name: pk_orders, columns: [tenant_id, order_id] }
            checks:
              - name: ck_tenant
                kind: membership
                expression: { kind: column, name: tenant_id }
                values:
                  - { type: int32, value: "1" }
                  - { type: int32, value: "2" }
            foreignKeys:
              - name: fk_orders_tenant
                sourceColumns: [tenant_id]
                targetTable: { schema: sales, name: tenants }
                targetColumns: [id]
          - identity: { schema: sales, name: tenants }
            columns:
              - { name: id, type: int32 }
        indexes:
          - identity: { schema: sales, name: ux_orders_total }
            table: { schema: sales, name: orders }
            unique: true
            columns:
              - { name: tenant_id, direction: ascending }
              - { name: total, direction: descending }
        views:
          - identity: { schema: reporting, name: order_totals }
            query:
              source: { schema: sales, name: orders }
              projections:
                - expression: { kind: column, name: tenant_id }
                - expression:
                    kind: function
                    function: round
                    arguments:
                      - { kind: column, name: total }
                  alias: rounded_total
          - identity: { schema: reporting, name: rounded_order_totals }
            query:
              source: { schema: reporting, name: order_totals }
              projections:
                - expression: { kind: column, name: rounded_total }
        """;

    private readonly SchemaDocumentReader reader = new();

    [Test]
    public void DtoCollections_AreInitialized()
    {
        var document = new SchemaDocument();
        var table = new TableDocument();
        var column = new ColumnDocument();

        Assert.Multiple(() =>
        {
            Assert.That(document.Tables, Is.Empty);
            Assert.That(document.Indexes, Is.Empty);
            Assert.That(document.Views, Is.Empty);
            Assert.That(table.Columns, Is.Empty);
            Assert.That(table.Checks, Is.Empty);
            Assert.That(table.ForeignKeys, Is.Empty);
            Assert.That(column.Checks, Is.Empty);
        });
    }

    [Test]
    public void Read_EquivalentJsonAndYaml_PreserveCompleteV1ContractAndOrder()
    {
        var json = Read(JsonDocument, SchemaDocumentFormat.Json);
        var yaml = Read(YamlDocument, SchemaDocumentFormat.Yaml);

        Assert.Multiple(() =>
        {
            Assert.That(json.Tables.Select(table => table.Identity.Name), Is.EqualTo(new[] { "orders", "tenants" }));
            Assert.That(yaml.Tables.Select(table => table.Identity.Name), Is.EqualTo(new[] { "orders", "tenants" }));
            Assert.That(json.Tables[0].Columns.Select(column => column.Name),
                Is.EqualTo(yaml.Tables[0].Columns.Select(column => column.Name)));
            Assert.That(json.Tables[0].PrimaryKey!.Columns, Is.EqualTo(new[] { "tenant_id", "order_id" }));
            Assert.That(yaml.Tables[0].PrimaryKey!.Columns, Is.EqualTo(json.Tables[0].PrimaryKey!.Columns));
            Assert.That(json.Tables[0].Columns[1].Identity!.Seed, Is.EqualTo(10));
            Assert.That(yaml.Tables[0].Columns[1].Identity!.Increment, Is.EqualTo(5));
            Assert.That(json.Tables[0].Columns[3].Generated!.Expression.Arguments.Select(argument => argument.Kind),
                Is.EqualTo(new[] { "column", "value" }));
            Assert.That(yaml.Tables[0].Checks[0].Values.Select(value => value.Value), Is.EqualTo(new[] { "1", "2" }));
            Assert.That(json.Tables[0].ForeignKeys[0].TargetTable.Name, Is.EqualTo("tenants"));
            Assert.That(yaml.Indexes[0].Unique, Is.True);
            Assert.That(json.Indexes[0].Columns.Select(column => column.Direction),
                Is.EqualTo(new[] { "ascending", "descending" }));
            Assert.That(yaml.Indexes[0].Columns.Select(column => column.Direction),
                Is.EqualTo(json.Indexes[0].Columns.Select(column => column.Direction)));
            Assert.That(json.Views.Select(view => view.Identity.Name),
                Is.EqualTo(new[] { "order_totals", "rounded_order_totals" }));
            Assert.That(yaml.Views[0].Query.Projections.Select(projection => projection.Alias),
                Is.EqualTo(json.Views[0].Query.Projections.Select(projection => projection.Alias)));
            Assert.That(json.Views[1].Query.Source.Name, Is.EqualTo("order_totals"));
        });
    }

    [TestCase(SchemaDocumentFormat.Json, JsonDocument)]
    [TestCase(SchemaDocumentFormat.Yaml, YamlDocument)]
    public void Read_Stream_DoesNotCloseInput(SchemaDocumentFormat format, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        _ = reader.Read(stream, format);

        Assert.That(stream.CanRead, Is.True);
    }

    [TestCase("document.json", JsonDocument)]
    [TestCase("document.yaml", YamlDocument)]
    [TestCase("document.yml", YamlDocument)]
    public void Read_File_InfersKnownFormat(string fileName, string content)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duburl-schema-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        try
        {
            File.WriteAllText(path, content);
            var document = (SchemaDocument)reader.Read(path);
            Assert.That(document.Version, Is.EqualTo(1));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void Read_FileWithUnknownExtension_ThrowsActionableError()
    {
        var exception = Assert.Throws<SchemaDocumentException>(() => reader.Read("schema.txt"));
        Assert.That(exception!.Message, Does.Contain(".json, .yaml, or .yml"));
    }

    [TestCase(SchemaDocumentFormat.Json, "{ \"tables\": [] }")]
    [TestCase(SchemaDocumentFormat.Yaml, "tables: []")]
    public void Read_MissingVersion_ThrowsActionableError(SchemaDocumentFormat format, string content)
    {
        var exception = Assert.Throws<SchemaDocumentException>(() => Read(content, format));
        Assert.That(exception!.Message, Does.Contain("must declare a version"));
    }

    [TestCase(SchemaDocumentFormat.Json, "{ \"version\": 2 }")]
    [TestCase(SchemaDocumentFormat.Yaml, "version: 2")]
    public void Read_UnsupportedVersion_ThrowsActionableError(SchemaDocumentFormat format, string content)
    {
        var exception = Assert.Throws<SchemaDocumentException>(() => Read(content, format));
        Assert.That(exception!.Message, Does.Contain("version 2 is not supported"));
    }

    [TestCase(SchemaDocumentFormat.Json, "{ \"version\": 1, \"tablse\": [] }")]
    [TestCase(SchemaDocumentFormat.Yaml, "version: 1\ntablse: []")]
    public void Read_UnknownProperty_ThrowsRatherThanDiscardingIt(SchemaDocumentFormat format, string content)
    {
        var exception = Assert.Throws<SchemaDocumentException>(() => Read(content, format));
        Assert.That(exception!.Message, Does.Contain("malformed"));
    }

    [TestCase(SchemaDocumentFormat.Json, "{ \"version\": 1,")]
    [TestCase(SchemaDocumentFormat.Yaml, "version: [")]
    public void Read_MalformedDocument_ReportsLocation(SchemaDocumentFormat format, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var exception = Assert.Throws<SchemaDocumentException>(() => reader.Read(stream, format, "broken.schema"));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.SourceName, Is.EqualTo("broken.schema"));
            Assert.That(exception.Line, Is.Not.Null);
            Assert.That(exception.Column, Is.Not.Null);
            Assert.That(exception.Message, Does.Contain("broken.schema"));
        });
    }

    [TestCase(SchemaDocumentFormat.Json)]
    [TestCase(SchemaDocumentFormat.Yaml)]
    public void Read_EmptyDocument_ThrowsActionableError(SchemaDocumentFormat format)
    {
        var exception = Assert.Throws<SchemaDocumentException>(() => Read("  ", format));
        Assert.That(exception!.Message, Does.Contain("empty"));
    }

    private SchemaDocument Read(string content, SchemaDocumentFormat format)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return (SchemaDocument)reader.Read(stream, format);
    }
}
