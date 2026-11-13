using System.Data.Common;
using DubUrl.Discovery;
using Moq;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Discovery;

public class CatalogNamespaceTests
{
    [TestCase(SchemaFeature.Annotations, SchemaFeature.ColumnAnnotations)]
    [TestCase(SchemaFeature.ColumnAnnotations, SchemaFeature.Annotations)]
    public async Task AnnotationCoverageIsReportedOnlyWhenRequested(SchemaFeature requested, SchemaFeature other)
    {
        var reader = new Reader("parent");
        var connection = new Mock<DbConnection>().Object;
        var defaultResult = await reader.ReadAsync(connection, new());
        var requestedResult = await reader.ReadAsync(connection, new() { Features = [requested] });

        Assert.That(defaultResult.Coverage.ContainsKey(requested), Is.False);
        Assert.That(requestedResult.Coverage.ContainsKey(other), Is.False);
        Assert.That(requestedResult.Coverage[requested], Is.EqualTo(MetadataCoverage.Unsupported));
        Assert.That(requestedResult.Tables.All(table => table.Annotations.Count == 0), Is.True);
        Assert.That(requestedResult.Tables.SelectMany(table => table.Columns).All(column => column.Annotations.Count == 0), Is.True);
    }

    [Test]
    public async Task ColumnAnnotationsCanBeRequestedIndependentlyOfTableAnnotationsAndComments()
    {
        var reader = new Reader("parent", supportsColumnAnnotations: true);
        var connection = new Mock<DbConnection>().Object;
        var commentsOnly = await reader.ReadAsync(connection, new() { Features = [SchemaFeature.Comments] });
        var annotationsAndComments = await reader.ReadAsync(connection, new()
        { Features = [SchemaFeature.Annotations, SchemaFeature.ColumnAnnotations, SchemaFeature.Comments] });

        var commentOnlyColumn = commentsOnly.Tables.Single(table => table.Identity.Name == "parent").Columns.Single();
        Assert.That(commentOnlyColumn.Comment, Is.EqualTo("Identifier"));
        Assert.That(commentOnlyColumn.Annotations, Is.Empty);
        Assert.That(annotationsAndComments.Coverage[SchemaFeature.Annotations], Is.EqualTo(MetadataCoverage.Unsupported));
        Assert.That(annotationsAndComments.Coverage[SchemaFeature.ColumnAnnotations], Is.EqualTo(MetadataCoverage.Available));
        Assert.That(annotationsAndComments.Coverage[SchemaFeature.Comments], Is.EqualTo(MetadataCoverage.Available));
        var column = annotationsAndComments.Tables.Single(table => table.Identity.Name == "parent").Columns.Single();
        Assert.That(column.Comment, Is.EqualTo("Identifier"));
        Assert.That(column.Annotations["owner"], Is.EqualTo("sales"));
    }

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

    [Test]
    public async Task UnrepresentableDefinitionsRetainTheirTableAndExactMember()
    {
        var metadata = await new Reader("parent", includeUnsupported: true).ReadAsync(new Mock<DbConnection>().Object, new());
        var diagnostics = metadata.Diagnostics;
        Assert.That(diagnostics.Select(item => item.Object),
            Is.All.EqualTo(new MetadataName("parent", "sales", "store", "server")));
        Assert.That(diagnostics.Select(item => item.Code), Is.All.EqualTo("SDI009"));
        Assert.That(diagnostics.Select(item => item.Coverage), Is.All.EqualTo(MetadataCoverage.Unrepresentable));
        Assert.That(diagnostics.Select(item => (item.Feature, item.Member, item.Constraint, item.NativeDefinition)), Is.EquivalentTo(new[]
        {
            (SchemaFeature.Columns, "node", (string?)null, "hierarchyid"),
            (SchemaFeature.Columns, "notes", null, "nvarchar(max)"),
            (SchemaFeature.Defaults, "ordered_at", "DF_ordered", "(getdate())"),
            (SchemaFeature.Generation, "total", null, "([price]*[quantity])"),
            (SchemaFeature.Indexes, "IX_parent", null, "NONCLUSTERED; filter: ([id]>(0)); included column: [notes]"),
            (SchemaFeature.Checks, "CK_parent", null, "([id]>(0) OR [id] IS NULL)")
        }));
        Assert.That(diagnostics.Single(item => item.Member == "ordered_at").Message,
            Does.Contain("ordered_at").And.Contain("DF_ordered").And.Contain("(getdate())"));
    }

    private sealed class Reader(string source, bool supportsColumnAnnotations = false, bool includeUnsupported = false) : CatalogSchemaMetadataReader
    {
        protected override string TablesSql => "tables";
        protected override string ColumnsSql => "columns";
        protected override string KeysSql => "keys";
        protected override string? IndexesSql => includeUnsupported ? "indexes" : null;
        protected override string? ChecksSql => includeUnsupported ? "checks" : null;
        protected override string ViewsSql => "views";
        protected override bool SupportsColumnAnnotations => supportsColumnAnnotations;

        protected override MetadataColumn MapCatalogColumn(CatalogRow row, DiscoveryOptions options, Action<SchemaFeature, string> diagnose)
        {
            var column = base.MapCatalogColumn(row, options, diagnose);
            return SupportsColumnAnnotations && options.Features.Contains(SchemaFeature.ColumnAnnotations)
                ? column with { Annotations = new Dictionary<string, string> { ["owner"] = "sales" } }
                : column;
        }
        private static CatalogRow Row(string table, params (string Name, object? Value)[] fields)
            => new CatalogRow(new Dictionary<string, object?>
            { ["table_name"] = table, ["schema_name"] = "sales", ["database_name"] = "store", ["catalog_name"] = "server" }).With(fields);

        protected override Task<List<CatalogRow>> ReadCatalogRowsAsync(DbConnection connection, string sql,
            DiscoveryOptions options, CancellationToken cancellationToken)
            => Task.FromResult<List<CatalogRow>>(sql switch
            {
                "tables" => [Row("parent"), Row("child")],
                "columns" when includeUnsupported =>
                [
                    Row("parent", ("column_name", "node"), ("type_name", "hierarchyid")),
                    Row("parent", ("column_name", "notes"), ("type_name", "nvarchar"), ("length", -1), ("native_type_declaration", "nvarchar(max)")),
                    Row("parent", ("column_name", "ordered_at"), ("type_name", "datetime"), ("default_name", "DF_ordered"), ("default_expression", "(getdate())")),
                    Row("parent", ("column_name", "total"), ("type_name", "int"), ("generation_expression", "([price]*[quantity])"))
                ],
                "columns" => [Row("parent", ("column_name", "id"), ("type_name", "integer"), ("nullable", 0), ("comment", "Identifier")),
                    Row("child", ("column_name", "id"), ("type_name", "integer"), ("nullable", 0))],
                "keys" => [Row("child", ("kind", "FOREIGN KEY"), ("key_name", "fk"), ("column_name", "id"),
                    ("target_table", "parent"), ("target_schema", "sales"), ("target_database", "store"),
                    ("target_catalog", "server"), ("target_column", "id"))],
                "views" => [Row("projection", ("definition", "SELECT id FROM " + source),
                    ("column_name", "id"), ("type_name", "integer"), ("nullable", 0))],
                "indexes" =>
                [
                    Row("parent", ("index_name", "IX_parent"), ("column_name", "id"), ("unsupported", true),
                        ("index_kind", "NONCLUSTERED"), ("filter_definition", "([id]>(0))")),
                    Row("parent", ("index_name", "IX_parent"), ("column_name", "notes"), ("unsupported", true),
                        ("index_kind", "NONCLUSTERED"), ("filter_definition", "([id]>(0))"), ("included_column", "[notes]"))
                ],
                "checks" => [Row("parent", ("check_name", "CK_parent"), ("expression", "([id]>(0) OR [id] IS NULL)"))],
                _ => []
            });
    }
}
