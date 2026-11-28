using System.Data;
using DubUrl.Discovery;
using Microsoft.AnalysisServices.AdomdClient;
using static DubUrl.Adomd.Discovery.AnalysisServicesRowsets;

namespace DubUrl.Adomd.Discovery;

internal sealed partial class AnalysisServicesModelInspection(
    string catalog, AnalysisServicesRowsets rowsets, AnalysisServicesInventory inventory)
{
    private AnalyticalObjectId CatalogId => Id(AnalyticalObjectKind.Catalog, catalog);

    private AnalyticalObjectId Id(AnalyticalObjectKind kind, string key, string? scope = null)
        => new(kind, catalog, scope, key);

    private AnalysisServicesRowset Read(string rowset, AnalyticalFeature feature,
        AdomdRestrictionCollection? restrictions = null)
    {
        var result = rowsets.Read(rowset, restrictions);
        inventory.Record(feature, result.Coverage, rowset);
        return result;
    }

    private string? Require(DataRow row, string column, AnalyticalFeature feature)
    {
        var value = Text(row, column);
        if (value is null)
            inventory.Record(feature, AnalyticalCoverage.Partial, $"rowset field {column}");
        return value;
    }

    private AnalyticalObject Add(AnalyticalFeature feature, AnalyticalObjectId id, string name, string? caption,
        AnalyticalObjectId parent, string? uniqueName = null, AnalyticalDataType? type = null,
        bool? hidden = null, string? folder = null, bool expectsType = false)
    {
        var item = new AnalyticalObject(id, name, caption ?? name)
        {
            Parent = parent,
            UniqueName = uniqueName,
            DataType = type,
            IsHidden = hidden,
            DisplayFolder = folder
        };
        inventory.Add(feature, item, expectsType);
        return item;
    }
}
