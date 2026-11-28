using System.Data;
using System.Data.Common;
using DubUrl.Adomd.Discovery;
using DubUrl.Adomd.Mapping;
using DubUrl.Adomd.Wrappers;
using DubUrl.Discovery;
using DubUrl.Mapping;
using Microsoft.AnalysisServices.AdomdClient;
using NUnit.Framework;

namespace DubUrl.Adomd.Testing;

public class AnalyticalMetadataReaderTest
{
    [TestCase("ssasdax")]
    [TestCase("ssasmdx")]
    [TestCase("asa")]
    [TestCase("pbipremium")]
    [TestCase("pbix")]
    public void AdomdSchemesExposeAnalyticalDiscovery(string scheme)
    {
        DbProviderFactories.RegisterFactory("Microsoft.AnalysisServices.AdomdClient", AdomdFactory.Instance);
        var registry = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule(typeof(SsasTabularMapper).Assembly))
            .Build();
        var url = new ConnectionUrl($"{scheme}://localhost/Sales", registry);
        Assert.That(url.TryGetCapability<AnalyticalDiscoveryCapability>(out var capability), Is.True);
        Assert.That(capability!.Reader, Is.InstanceOf<AnalysisServicesAnalyticalReader>());
        Assert.That(url.TryGetCapability<AnalysisServicesMetadataCapability>(out _), Is.True);
    }

    [Test]
    public void UnknownModeWithModernRowsetsMapsTabularTypesAndPerspectiveMembership()
    {
        var rowsets = new Fixture();
        rowsets.Add("TMSCHEMA_TABLES", "ID Name", ["1", "Sales"], ["2", "Products"]);
        rowsets.Add("TMSCHEMA_COLUMNS", "ID TableID ExplicitName InferredName ExplicitDataType InferredDataType",
            ["10", "1", null, "Number", "1", "6"], ["11", "1", "Internal", null, "2", null], ["20", "2", "Name", null, "2", null]);
        rowsets.Add("TMSCHEMA_MEASURES", "ID TableID Name DataType", ["15", "1", "Revenue", "10"]);
        rowsets.Add("TMSCHEMA_PERSPECTIVES", "ID Name", ["35", "Published"]);
        rowsets.Add("TMSCHEMA_PERSPECTIVE_TABLES", "ID PerspectiveID TableID IncludeAll",
            ["50", "35", "1", "false"], ["51", "35", "2", "true"]);
        rowsets.Add("TMSCHEMA_PERSPECTIVE_COLUMNS", "PerspectiveTableID ColumnID", ["50", "10"]);
        rowsets.Add("TMSCHEMA_PERSPECTIVE_MEASURES", "PerspectiveTableID MeasureID", ["50", "15"]);

        var result = rowsets.Inspect(AnalysisServicesServerMode.Unknown, tabularScheme: false);
        var table = Find(result, AnalyticalObjectKind.Table, "Sales");
        var column = Find(result, AnalyticalObjectKind.Column, "Number");
        var measure = Find(result, AnalyticalObjectKind.Measure, "Revenue");
        var perspective = Find(result, AnalyticalObjectKind.Perspective, "Published");

        Assert.Multiple(() =>
        {
            Assert.That(table.Id.Key, Is.EqualTo("1"));
            Assert.That(column.Parent, Is.EqualTo(table.Id));
            Assert.That(column.DataType, Is.EqualTo(new AnalyticalDataType("6", DbType.Int64, null, null, null)));
            Assert.That(measure.DataType?.Type, Is.EqualTo(DbType.Decimal));
            Assert.That(result.Coverage[AnalyticalFeature.PerspectiveMembership], Is.EqualTo(AnalyticalCoverage.Available));
            Assert.That(result.PerspectiveMembers.Where(link => link.Perspective == perspective.Id).Select(link => link.Member.Key),
                Is.EquivalentTo(new[] { "1", "2", "10", "15", "20" }));
        });
    }

    [Test]
    public void MultidimensionalModelPreservesScopedUniqueNamesParentsAndNativeTypes()
    {
        var rowsets = new Fixture();
        rowsets.Add("MDSCHEMA_CUBES", "CATALOG_NAME CUBE_NAME BASE_CUBE_NAME CUBE_CAPTION",
            ["Sales", "Public", "Cube", "Public view"], ["Sales", "Cube", null, "Sales cube"], ["Another", "Other", null, null]);
        rowsets.Add("MDSCHEMA_DIMENSIONS", "CUBE_NAME DIMENSION_UNIQUE_NAME DIMENSION_NAME DIMENSION_CAPTION",
            ["Cube", "[Date]", "Date", "Calendar"], ["Public", "[Date]", "Date", "Calendar"]);
        rowsets.Add("MDSCHEMA_HIERARCHIES", "CUBE_NAME DIMENSION_UNIQUE_NAME HIERARCHY_UNIQUE_NAME HIERARCHY_NAME",
            ["Cube", "[Date]", "[Date].[Calendar]", "Calendar"], ["Public", "[Date]", "[Date].[Calendar]", "Calendar"]);
        rowsets.Add("MDSCHEMA_LEVELS", "CUBE_NAME HIERARCHY_UNIQUE_NAME LEVEL_UNIQUE_NAME LEVEL_NAME LEVEL_DBTYPE",
            ["Cube", "[Date].[Calendar]", "[Date].[Calendar].[Year]", "Year", "3"],
            ["Public", "[Date].[Calendar]", "[Date].[Calendar].[Year]", "Year", "3"]);
        rowsets.Add("MDSCHEMA_MEASURES", "CUBE_NAME MEASURE_UNIQUE_NAME MEASURE_NAME MEASURE_CAPTION DATA_TYPE NUMERIC_PRECISION NUMERIC_SCALE",
            ["Cube", "[Measures].[Revenue]", "Revenue", "Total revenue", "131", "19", "4"]);

        var result = rowsets.Inspect(AnalysisServicesServerMode.Multidimensional);
        var cube = Find(result, AnalyticalObjectKind.Cube, "Cube");
        var perspective = Find(result, AnalyticalObjectKind.Perspective, "Public");
        var dimension = Find(result, AnalyticalObjectKind.Dimension, "Date", "Cube");
        var hierarchy = Find(result, AnalyticalObjectKind.Hierarchy, "Calendar", "Cube");
        var level = Find(result, AnalyticalObjectKind.Level, "Year", "Cube");
        var scopedLevel = Find(result, AnalyticalObjectKind.Level, "Year", "Public");
        var measure = Find(result, AnalyticalObjectKind.Measure, "Revenue", "Cube");

        Assert.Multiple(() =>
        {
            Assert.That(perspective.Parent, Is.EqualTo(cube.Id));
            Assert.That(dimension.Caption, Is.EqualTo("Calendar"));
            Assert.That(hierarchy.Parent, Is.EqualTo(dimension.Id));
            Assert.That(level.Parent, Is.EqualTo(hierarchy.Id));
            Assert.That(level.UniqueName, Is.EqualTo("[Date].[Calendar].[Year]"));
            Assert.That(level.DataType?.Type, Is.EqualTo(DbType.Int32));
            Assert.That(scopedLevel.Id, Is.Not.EqualTo(level.Id));
            Assert.That(result.PerspectiveMembers, Does.Contain(new AnalyticalPerspectiveMember(perspective.Id, scopedLevel.Id)));
            Assert.That(result.PerspectiveMembers, Does.Not.Contain(new AnalyticalPerspectiveMember(perspective.Id, measure.Id)));
            Assert.That(measure.DataType, Is.EqualTo(new AnalyticalDataType("131", DbType.Decimal, null, 19, 4)));
            Assert.That(rowsets.Requests.Count(request => request == "MDSCHEMA_LEVELS"), Is.EqualTo(2));
            Assert.That(rowsets.Requests, Does.Not.Contain("TMSCHEMA_TABLES"));
        });
    }

    [Test]
    public void UnknownModeOnLegacyTabularSchemeUsesDbSchemaAndOlapRowsets()
    {
        var rowsets = new Fixture();
        rowsets.Add("MDSCHEMA_CUBES", "CATALOG_NAME CUBE_NAME BASE_CUBE_NAME", ["Sales", "Model", null], ["Sales", "Public", "Model"]);
        rowsets.Add("DBSCHEMA_TABLES", "TABLE_SCHEMA TABLE_NAME", ["Model", "Orders"], ["Model", "$Orders"], ["Public", "Orders"]);
        rowsets.Add("DBSCHEMA_COLUMNS", "TABLE_SCHEMA TABLE_NAME COLUMN_NAME DATA_TYPE CHARACTER_MAXIMUM_LENGTH",
            ["Model", "$Orders", "RowNumber", "3", null], ["Model", "$Orders", "Item", "130", "32"],
            ["Public", "$Orders", "Item", "130", "32"]);
        rowsets.Add("MDSCHEMA_MEASURES", "CUBE_NAME MEASURE_UNIQUE_NAME MEASURE_NAME MEASUREGROUP_NAME DATA_TYPE",
            ["Model", "[Measures].[Total]", "Total", "Orders", "6"]);

        var result = rowsets.Inspect(AnalysisServicesServerMode.Unknown);
        var column = Find(result, AnalyticalObjectKind.Column, "Item", "Model");
        var table = Find(result, AnalyticalObjectKind.Table, "Orders", "Model");
        var measure = Find(result, AnalyticalObjectKind.Measure, "Total", "Model");

        Assert.Multiple(() =>
        {
            Assert.That(column.Parent, Is.EqualTo(table.Id));
            Assert.That(column.DataType, Is.EqualTo(new AnalyticalDataType("130", DbType.String, 32, null, null)));
            Assert.That(measure.Parent, Is.EqualTo(table.Id));
            Assert.That(result.Objects.Any(item => item.Name == "RowNumber"), Is.False);
            Assert.That(result.PerspectiveMembers.Any(link => link.Member.Kind == AnalyticalObjectKind.Column && link.Member.Scope == "Public"), Is.True);
        });
    }

    [Test]
    public void DeniedUnadvertisedAndEmptyRowsetsHaveDifferentCoverage()
    {
        var rowsets = new Fixture();
        rowsets.Add("TMSCHEMA_TABLES", "ID Name", ["1", "Sales"]);
        rowsets.Add("TMSCHEMA_MEASURES", "ID TableID Name DataType");
        rowsets.Denied.Add("TMSCHEMA_COLUMNS");

        var result = rowsets.Inspect(AnalysisServicesServerMode.Tabular);

        Assert.Multiple(() =>
        {
            Assert.That(result.Coverage[AnalyticalFeature.Measures], Is.EqualTo(AnalyticalCoverage.Available));
            Assert.That(result.Coverage[AnalyticalFeature.Columns], Is.EqualTo(AnalyticalCoverage.Inaccessible));
            Assert.That(result.Coverage[AnalyticalFeature.Perspectives], Is.EqualTo(AnalyticalCoverage.Unsupported));
            Assert.That(result.Coverage[AnalyticalFeature.DataTypes], Is.EqualTo(AnalyticalCoverage.Partial));
            Assert.That(result.Diagnostics.Any(item => item.Message.Contains("TMSCHEMA_COLUMNS", StringComparison.Ordinal)), Is.True);
            Assert.That(result.Diagnostics.Any(item => item.Message.Contains("driver details", StringComparison.Ordinal)), Is.False);
        });
    }

    [Test]
    public void MissingAndUnmappedTypesKeepObjectsAndReportPartialCoverage()
    {
        var rowsets = new Fixture();
        rowsets.Add("TMSCHEMA_TABLES", "ID Name", ["1", "Sales"]);
        rowsets.Add("TMSCHEMA_COLUMNS", "ID TableID ExplicitName ExplicitDataType",
            ["10", "1", "Missing", null], ["11", "1", "Future", "80"]);
        rowsets.Add("TMSCHEMA_MEASURES", "ID TableID Name DataType");

        var result = rowsets.Inspect(AnalysisServicesServerMode.Tabular);

        Assert.Multiple(() =>
        {
            Assert.That(result.Coverage[AnalyticalFeature.Columns], Is.EqualTo(AnalyticalCoverage.Available));
            Assert.That(result.Coverage[AnalyticalFeature.DataTypes], Is.EqualTo(AnalyticalCoverage.Partial));
            Assert.That(Find(result, AnalyticalObjectKind.Column, "Missing").DataType, Is.Null);
            Assert.That(Find(result, AnalyticalObjectKind.Column, "Future").DataType?.NativeType, Is.EqualTo("80"));
            Assert.That(result.Diagnostics.Count(item => item.Feature == AnalyticalFeature.DataTypes && item.Object is not null), Is.EqualTo(2));
        });
    }

    private static AnalyticalObject Find(AnalyticalMetadata result, AnalyticalObjectKind kind, string name, string? scope = null)
        => result.Objects.Single(item => item.Id.Kind == kind && item.Id.Scope == scope && item.Name == name);

    private sealed class Fixture
    {
        private readonly Dictionary<string, IReadOnlyList<DataRow>> responses = new()
        {
            ["DBSCHEMA_CATALOGS"] = Rows("CATALOG_NAME", [["Sales"]])
        };

        internal List<string> Requests { get; } = [];
        internal HashSet<string> Denied { get; } = [];

        internal void Add(string rowset, string fields, params string?[][] rows)
            => responses.Add(rowset, Rows(fields, rows));

        internal AnalyticalMetadata Inspect(AnalysisServicesServerMode mode, bool tabularScheme = true)
            => new AnalysisServicesAnalyticalReader(tabularScheme).Inspect("Sales", mode, Discover);

        private IReadOnlyList<DataRow> Discover(string rowset, AdomdRestrictionCollection restrictions)
        {
            Requests.Add(rowset);
            if (rowset == "DISCOVER_SCHEMA_ROWSETS")
                return Rows("SchemaName", responses.Keys.Concat(Denied).Select(name => new string?[] { name }));
            if (Denied.Contains(rowset))
                throw new DeniedException();
            if (rowset is "MDSCHEMA_DIMENSIONS" or "MDSCHEMA_HIERARCHIES" or "MDSCHEMA_LEVELS" or "MDSCHEMA_MEASURES")
                Assert.That(restrictions.Count, Is.EqualTo(2));
            return responses[rowset];
        }

        private static IReadOnlyList<DataRow> Rows(string fields, IEnumerable<string?[]> rows)
        {
            var table = new DataTable();
            foreach (var field in fields.Split(' '))
                table.Columns.Add(field, typeof(string));
            foreach (var row in rows)
                table.Rows.Add(row);
            return table.Rows.Cast<DataRow>().ToArray();
        }

        private sealed class DeniedException() : DbException("driver details");
    }
}
