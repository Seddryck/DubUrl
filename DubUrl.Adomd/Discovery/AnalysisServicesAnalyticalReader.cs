using System.Data;
using DubUrl.Discovery;
using Microsoft.AnalysisServices.AdomdClient;
using static DubUrl.Adomd.Discovery.AnalysisServicesRowsets;

namespace DubUrl.Adomd.Discovery;

internal sealed class AnalysisServicesAnalyticalReader(bool tabularScheme) : IAnalyticalMetadataReader
{
    public bool IsAccessFailure(Exception exception) => IsDriverFailure(exception);

    public ValueTask<AnalyticalMetadata> ReadAsync(IDbConnection connection, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var adomd = AnalysisServicesMetadataCapability.RequireOpenConnection(connection);
        var mode = GetMode(connection, cancellationToken);
        var result = Inspect(adomd.Database, mode, (name, restrictions) =>
        {
            using var data = adomd.GetSchemaDataSet(name, restrictions);
            return data.Tables.Count == 0 ? [] : data.Tables[0].Rows.Cast<DataRow>().ToArray();
        }, cancellationToken);
        return ValueTask.FromResult(result);
    }

    private static AnalysisServicesServerMode GetMode(IDbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return new AnalysisServicesMetadataCapability().GetServerMode(connection);
        }
        catch (Exception exception) when (IsDriverFailure(exception) || exception is NotSupportedException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return AnalysisServicesServerMode.Unknown;
        }
    }

    internal AnalyticalMetadata Inspect(string catalog, AnalysisServicesServerMode mode,
        Func<string, AdomdRestrictionCollection, IReadOnlyList<DataRow>> discover, CancellationToken cancellationToken = default)
    {
        var rowsets = new AnalysisServicesRowsets(discover, cancellationToken);
        var inventory = new AnalysisServicesInventory();
        var catalogs = rowsets.Read("DBSCHEMA_CATALOGS");
        inventory.Record(AnalyticalFeature.Catalogs, catalogs.Coverage, "DBSCHEMA_CATALOGS");
        if (catalogs.Rows.Any(row => Text(row, "CATALOG_NAME") is null))
            inventory.Record(AnalyticalFeature.Catalogs, AnalyticalCoverage.Partial, "DBSCHEMA_CATALOGS");

        var names = catalogs.Rows.Select(row => Text(row, "CATALOG_NAME")).OfType<string>().Distinct().ToArray();
        if (names.Length == 1 && string.IsNullOrEmpty(catalog))
            catalog = names[0];
        foreach (var name in names)
            inventory.Add(AnalyticalFeature.Catalogs, Catalog(name));
        if (string.IsNullOrEmpty(catalog))
        {
            foreach (var feature in Enum.GetValues<AnalyticalFeature>().Where(feature => feature != AnalyticalFeature.Catalogs))
                inventory.Record(feature, AnalyticalCoverage.Inaccessible, "the current catalog (select a catalog in the connection URL)");
            return inventory.Complete();
        }
        // The open connection identifies its catalog even if the inventory rowset is denied.
        inventory.Add(AnalyticalFeature.Catalogs, Catalog(catalog));

        var inspection = new AnalysisServicesModelInspection(catalog, rowsets, inventory);
        var tables = mode == AnalysisServicesServerMode.Multidimensional ? null : rowsets.Read("TMSCHEMA_TABLES");
        if (tables?.Coverage == AnalyticalCoverage.Available)
        {
            inspection.ReadTabular(tables);
        }
        else
        {
            var includeLegacyTables = mode == AnalysisServicesServerMode.Tabular
                || (mode == AnalysisServicesServerMode.Unknown && tabularScheme);
            inspection.ReadOlap(includeLegacyTables);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return inventory.Complete();
    }

    private static AnalyticalObject Catalog(string name)
        => new(new(AnalyticalObjectKind.Catalog, name, null, name), name, name);
}
