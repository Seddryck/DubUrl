using System.Data;
using DubUrl.Discovery;
using Microsoft.AnalysisServices.AdomdClient;
using static DubUrl.Adomd.Discovery.AnalysisServicesRowsets;

namespace DubUrl.Adomd.Discovery;

internal sealed partial class AnalysisServicesModelInspection
{
    internal void ReadOlap(bool includeLegacyTables)
    {
        var cubes = Read("MDSCHEMA_CUBES", AnalyticalFeature.Cubes, CatalogRestriction());
        inventory.Record(AnalyticalFeature.Perspectives, cubes.Coverage, "MDSCHEMA_CUBES");
        inventory.Record(AnalyticalFeature.PerspectiveMembership, cubes.Coverage, "MDSCHEMA_CUBES");
        var scopes = ReadScopes(cubes.Rows);
        if (includeLegacyTables)
            ReadLegacyTables(scopes);

        if (scopes.Count == 0)
        {
            foreach (var feature in new[] { AnalyticalFeature.Dimensions, AnalyticalFeature.Hierarchies, AnalyticalFeature.Levels, AnalyticalFeature.Measures })
                inventory.Record(feature, cubes.Coverage, "MDSCHEMA_CUBES");
            return;
        }

        foreach (var scope in scopes.Values)
        {
            var restrictions = CatalogRestriction();
            restrictions.Add(new AdomdRestriction("CUBE_NAME", scope.Name));
            ReadOlapMembers(scope, restrictions, "MDSCHEMA_DIMENSIONS", "DIMENSION",
                AnalyticalFeature.Dimensions, AnalyticalObjectKind.Dimension);
            ReadOlapMembers(scope, restrictions, "MDSCHEMA_HIERARCHIES", "HIERARCHY",
                AnalyticalFeature.Hierarchies, AnalyticalObjectKind.Hierarchy, AnalyticalObjectKind.Dimension, "DIMENSION_UNIQUE_NAME");
            ReadOlapMembers(scope, restrictions, "MDSCHEMA_LEVELS", "LEVEL",
                AnalyticalFeature.Levels, AnalyticalObjectKind.Level, AnalyticalObjectKind.Hierarchy, "HIERARCHY_UNIQUE_NAME", "LEVEL_DBTYPE");
            ReadOlapMembers(scope, restrictions, "MDSCHEMA_MEASURES", "MEASURE",
                AnalyticalFeature.Measures, AnalyticalObjectKind.Measure, typeField: "DATA_TYPE", hasLegacyTables: includeLegacyTables);
        }
    }

    private AdomdRestrictionCollection CatalogRestriction()
        => new() { new AdomdRestriction("CATALOG_NAME", catalog) };

    private Dictionary<string, AnalyticalObject> ReadScopes(IReadOnlyList<DataRow> rows)
    {
        var scopes = new Dictionary<string, AnalyticalObject>(StringComparer.Ordinal);
        var perspectives = new List<(DataRow Row, string Name, string Base)>();
        foreach (var row in rows)
        {
            if (Text(row, "CATALOG_NAME") is { } rowCatalog && rowCatalog != catalog)
                continue;

            var name = Require(row, "CUBE_NAME", AnalyticalFeature.Cubes);
            if (name is null)
            {
                inventory.Record(AnalyticalFeature.Perspectives, AnalyticalCoverage.Partial, "MDSCHEMA_CUBES");
                inventory.Record(AnalyticalFeature.PerspectiveMembership, AnalyticalCoverage.Partial, "MDSCHEMA_CUBES");
                continue;
            }
            var baseName = Text(row, "BASE_CUBE_NAME");
            if (baseName is not null && baseName != name)
                perspectives.Add((row, name, baseName));
            else
                scopes.TryAdd(name, Add(AnalyticalFeature.Cubes, Id(AnalyticalObjectKind.Cube, name),
                    name, Text(row, "CUBE_CAPTION"), CatalogId, hidden: IsHidden(row, "CUBE")));
        }

        foreach (var (row, name, baseName) in perspectives)
        {
            scopes.TryAdd(name, Add(AnalyticalFeature.Perspectives, Id(AnalyticalObjectKind.Perspective, name),
                name, Text(row, "CUBE_CAPTION"), Id(AnalyticalObjectKind.Cube, baseName), hidden: IsHidden(row, "CUBE")));
        }
        return scopes;
    }

    private void ReadOlapMembers(AnalyticalObject scope, AdomdRestrictionCollection restrictions,
        string rowset, string prefix, AnalyticalFeature feature, AnalyticalObjectKind kind,
        AnalyticalObjectKind? parentKind = null, string? parentField = null, string? typeField = null, bool hasLegacyTables = false)
    {
        var result = Read(rowset, feature, restrictions);
        var isPerspective = scope.Id.Kind == AnalyticalObjectKind.Perspective;
        if (isPerspective)
            inventory.Record(AnalyticalFeature.PerspectiveMembership, result.Coverage, rowset);
        foreach (var row in result.Rows)
        {
            if (Text(row, "CATALOG_NAME") is { } rowCatalog && rowCatalog != catalog
                || Text(row, "CUBE_NAME") is { } rowScope && rowScope != scope.Name)
                continue;

            var uniqueName = Require(row, $"{prefix}_UNIQUE_NAME", feature);
            if (uniqueName is null)
            {
                if (isPerspective)
                    inventory.Record(AnalyticalFeature.PerspectiveMembership, AnalyticalCoverage.Partial, rowset);
                continue;
            }

            var parent = scope.Id;
            if (parentKind is { } parentType && parentField is not null)
            {
                var parentName = Require(row, parentField, feature);
                if (parentName is not null)
                    parent = Id(parentType, parentName, scope.Name);
            }
            else if (hasLegacyTables && Text(row, "MEASUREGROUP_NAME") is { } group)
            {
                var tableId = Id(AnalyticalObjectKind.Table, group, scope.Name);
                if (inventory.Contains(tableId))
                    parent = tableId;
            }

            var id = Id(kind, uniqueName, scope.Name);
            Add(feature, id, Text(row, $"{prefix}_NAME") ?? uniqueName, Text(row, $"{prefix}_CAPTION"), parent,
                uniqueName, typeField is null ? null : AnalysisServicesInventory.DataType(row, typeField, tabular: false),
                IsHidden(row, prefix), Text(row, $"{prefix}_DISPLAY_FOLDER"), typeField is not null);
            if (isPerspective)
                inventory.Include(scope.Id, id);
        }
    }

    private void ReadLegacyTables(IReadOnlyDictionary<string, AnalyticalObject> scopes)
    {
        var tables = CatalogRestrictionForTables();
        foreach (var row in Read("DBSCHEMA_TABLES", AnalyticalFeature.Tables, tables).Rows)
        {
            var name = Require(row, "TABLE_NAME", AnalyticalFeature.Tables);
            if (name is null || name.StartsWith('$'))
                continue;
            var schema = Text(row, "TABLE_SCHEMA");
            var scope = FindScope(scopes, schema);
            var id = Id(AnalyticalObjectKind.Table, name, schema);
            Add(AnalyticalFeature.Tables, id, name, null, scope?.Id ?? CatalogId);
            if (scope?.Id.Kind == AnalyticalObjectKind.Perspective)
                inventory.Include(scope.Id, id);
        }

        foreach (var row in Read("DBSCHEMA_COLUMNS", AnalyticalFeature.Columns, CatalogRestrictionForTables()).Rows)
        {
            var table = Require(row, "TABLE_NAME", AnalyticalFeature.Columns);
            var name = Require(row, "COLUMN_NAME", AnalyticalFeature.Columns);
            if (table is null || name is null || !table.StartsWith('$') || name == "RowNumber")
                continue;
            table = table[1..];
            var schema = Text(row, "TABLE_SCHEMA");
            var id = Id(AnalyticalObjectKind.Column, $"{table.Length}:{table}{name}", schema);
            Add(AnalyticalFeature.Columns, id, name, null, Id(AnalyticalObjectKind.Table, table, schema),
                type: AnalysisServicesInventory.DataType(row, "DATA_TYPE", tabular: false), expectsType: true);
            var scope = FindScope(scopes, schema);
            if (scope?.Id.Kind == AnalyticalObjectKind.Perspective)
                inventory.Include(scope.Id, id);
        }
        if (scopes.Values.Any(scope => scope.Id.Kind == AnalyticalObjectKind.Perspective))
            inventory.LimitPerspectiveCoverage(AnalyticalFeature.Tables, AnalyticalFeature.Columns);
    }

    private AdomdRestrictionCollection CatalogRestrictionForTables()
        => new() { new AdomdRestriction("TABLE_CATALOG", catalog) };

    private static AnalyticalObject? FindScope(IReadOnlyDictionary<string, AnalyticalObject> scopes, string? name)
        => name is null ? null : scopes.GetValueOrDefault(name);

    private static bool? IsHidden(DataRow row, string prefix)
        => Flag(row, $"{prefix}_IS_VISIBLE") is bool visible ? !visible : null;
}
