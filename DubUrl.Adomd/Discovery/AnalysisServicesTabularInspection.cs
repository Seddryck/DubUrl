using System.Data;
using DubUrl.Discovery;
using static DubUrl.Adomd.Discovery.AnalysisServicesRowsets;

namespace DubUrl.Adomd.Discovery;

internal sealed partial class AnalysisServicesModelInspection
{
    internal void ReadTabular(AnalysisServicesRowset tables)
    {
        inventory.Record(AnalyticalFeature.Tables, tables.Coverage, "TMSCHEMA_TABLES");
        foreach (var row in tables.Rows)
        {
            var key = Require(row, "ID", AnalyticalFeature.Tables);
            var name = Require(row, "Name", AnalyticalFeature.Tables);
            if (key is not null && name is not null)
                Add(AnalyticalFeature.Tables, Id(AnalyticalObjectKind.Table, key), name, null,
                    CatalogId, hidden: Flag(row, "IsHidden"));
        }

        ReadTabularMembers("TMSCHEMA_COLUMNS", AnalyticalFeature.Columns, AnalyticalObjectKind.Column,
            row => Text(row, "ExplicitName") ?? Text(row, "InferredName"),
            row => AnalysisServicesInventory.DataType(row,
                Text(row, "ExplicitDataType") is null or "1" ? "InferredDataType" : "ExplicitDataType", tabular: true));
        ReadTabularMembers("TMSCHEMA_MEASURES", AnalyticalFeature.Measures, AnalyticalObjectKind.Measure,
            row => Text(row, "Name"), row => AnalysisServicesInventory.DataType(row, "DataType", tabular: true));
        ReadTabularPerspectives();
    }

    private void ReadTabularMembers(string rowset, AnalyticalFeature feature, AnalyticalObjectKind kind,
        Func<DataRow, string?> nameOf, Func<DataRow, AnalyticalDataType?> typeOf)
    {
        foreach (var row in Read(rowset, feature).Rows)
        {
            var key = Require(row, "ID", feature);
            var parent = Require(row, "TableID", feature);
            var name = nameOf(row);
            if (name is null)
                inventory.Record(feature, AnalyticalCoverage.Partial, $"{rowset} object names");
            if (key is not null && parent is not null && name is not null)
                Add(feature, Id(kind, key), name, null, Id(AnalyticalObjectKind.Table, parent),
                    type: typeOf(row), hidden: Flag(row, "IsHidden"), folder: Text(row, "DisplayFolder"), expectsType: true);
        }
    }

    private void ReadTabularPerspectives()
    {
        var perspectives = Read("TMSCHEMA_PERSPECTIVES", AnalyticalFeature.Perspectives);
        inventory.Record(AnalyticalFeature.PerspectiveMembership, perspectives.Coverage, "TMSCHEMA_PERSPECTIVES");
        foreach (var row in perspectives.Rows)
        {
            var key = Require(row, "ID", AnalyticalFeature.Perspectives);
            var name = Require(row, "Name", AnalyticalFeature.Perspectives);
            if (key is not null && name is not null)
                Add(AnalyticalFeature.Perspectives, Id(AnalyticalObjectKind.Perspective, key), name, null, CatalogId);
            else
                inventory.Record(AnalyticalFeature.PerspectiveMembership, AnalyticalCoverage.Partial, "TMSCHEMA_PERSPECTIVES");
        }
        if (perspectives.Rows.Count == 0)
            return;

        var perspectiveTables = ReadPerspectiveTables();
        if (perspectiveTables.Values.Any(table => !table.IncludeAll))
        {
            ReadExplicitMembership("TMSCHEMA_PERSPECTIVE_COLUMNS", "ColumnID", AnalyticalObjectKind.Column, perspectiveTables);
            ReadExplicitMembership("TMSCHEMA_PERSPECTIVE_MEASURES", "MeasureID", AnalyticalObjectKind.Measure, perspectiveTables);
        }
        inventory.LimitPerspectiveCoverage(AnalyticalFeature.Tables, AnalyticalFeature.Columns, AnalyticalFeature.Measures);
    }

    private Dictionary<string, (AnalyticalObjectId Perspective, AnalyticalObjectId Table, bool IncludeAll)> ReadPerspectiveTables()
    {
        const AnalyticalFeature feature = AnalyticalFeature.PerspectiveMembership;
        var perspectiveTables = new Dictionary<string, (AnalyticalObjectId Perspective, AnalyticalObjectId Table, bool IncludeAll)>();
        foreach (var row in Read("TMSCHEMA_PERSPECTIVE_TABLES", feature).Rows)
        {
            var key = Require(row, "ID", feature);
            var perspective = Require(row, "PerspectiveID", feature);
            var table = Require(row, "TableID", feature);
            if (key is null || perspective is null || table is null)
                continue;
            var perspectiveId = Id(AnalyticalObjectKind.Perspective, perspective);
            var tableId = Id(AnalyticalObjectKind.Table, table);
            var includeAllFlag = Flag(row, "IncludeAll");
            if (includeAllFlag is null)
                inventory.Record(feature, AnalyticalCoverage.Partial, "TMSCHEMA_PERSPECTIVE_TABLES.IncludeAll");
            var includeAll = includeAllFlag == true;
            perspectiveTables.TryAdd(key, (perspectiveId, tableId, includeAll));
            inventory.Include(perspectiveId, tableId);
            if (includeAll)
            {
                foreach (var child in inventory.Children(tableId))
                    inventory.Include(perspectiveId, child);
            }
        }
        return perspectiveTables;
    }

    private void ReadExplicitMembership(string rowset, string memberField, AnalyticalObjectKind kind,
        IReadOnlyDictionary<string, (AnalyticalObjectId Perspective, AnalyticalObjectId Table, bool IncludeAll)> perspectiveTables)
    {
        const AnalyticalFeature feature = AnalyticalFeature.PerspectiveMembership;
        foreach (var row in Read(rowset, feature).Rows)
        {
            var table = Require(row, "PerspectiveTableID", feature);
            var key = Require(row, memberField, feature);
            if (table is null || key is null)
                continue;
            var member = Id(kind, key);
            if (perspectiveTables.TryGetValue(table, out var owner) && inventory.IsChildOf(member, owner.Table))
                inventory.Include(owner.Perspective, member);
            else
                inventory.Record(feature, AnalyticalCoverage.Partial, rowset);
        }
    }
}
