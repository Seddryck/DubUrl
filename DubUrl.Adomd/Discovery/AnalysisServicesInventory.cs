using System.Data;
using System.Globalization;
using DubUrl.Discovery;
using static DubUrl.Adomd.Discovery.AnalysisServicesRowsets;

namespace DubUrl.Adomd.Discovery;

internal sealed class AnalysisServicesInventory
{
    private readonly Dictionary<AnalyticalObjectId, AnalyticalObject> objects = [];
    private readonly HashSet<AnalyticalPerspectiveMember> members = [];
    private readonly Dictionary<AnalyticalFeature, AnalyticalCoverage> coverage = [];
    private readonly HashSet<AnalyticalDiagnostic> diagnostics = [];

    internal void Record(AnalyticalFeature feature, AnalyticalCoverage status, string source)
    {
        coverage[feature] = coverage.TryGetValue(feature, out var previous) ? Merge(previous, status) : status;
        if (status != AnalyticalCoverage.Available)
            diagnostics.Add(new(feature, status, $"Analytical metadata from {source} was {status.ToString().ToLowerInvariant()}."));
    }

    private static AnalyticalCoverage Merge(AnalyticalCoverage previous, AnalyticalCoverage current)
        => (previous, current) switch
        {
            _ when previous == current => current,
            (AnalyticalCoverage.Available or AnalyticalCoverage.Partial, _) or (_, AnalyticalCoverage.Available or AnalyticalCoverage.Partial)
                => AnalyticalCoverage.Partial,
            _ => AnalyticalCoverage.Inaccessible
        };

    internal void Add(AnalyticalFeature feature, AnalyticalObject value, bool expectsType = false)
    {
        if (!objects.TryAdd(value.Id, value))
            return;
        if (expectsType && value.DataType?.Type is null)
        {
            coverage[AnalyticalFeature.DataTypes] = AnalyticalCoverage.Partial;
            diagnostics.Add(new(AnalyticalFeature.DataTypes, AnalyticalCoverage.Partial,
                "The endpoint did not report a recognized data type for this object.", value.Id));
        }
        else if (expectsType)
        {
            coverage.TryAdd(AnalyticalFeature.DataTypes, AnalyticalCoverage.Available);
        }

        if (value.Parent is not null && !objects.ContainsKey(value.Parent))
        {
            coverage[feature] = AnalyticalCoverage.Partial;
            diagnostics.Add(new(feature, AnalyticalCoverage.Partial, "The parent object was not available.", value.Id));
        }
    }

    internal bool Contains(AnalyticalObjectId id) => objects.ContainsKey(id);

    internal bool IsChildOf(AnalyticalObjectId member, AnalyticalObjectId parent)
        => objects.TryGetValue(member, out var item) && item.Parent == parent;

    internal IEnumerable<AnalyticalObjectId> Children(AnalyticalObjectId parent)
        => objects.Values.Where(item => item.Parent == parent).Select(item => item.Id);

    internal void Include(AnalyticalObjectId perspective, AnalyticalObjectId member)
    {
        if (Contains(perspective) && Contains(member))
            members.Add(new(perspective, member));
        else
            Record(AnalyticalFeature.PerspectiveMembership, AnalyticalCoverage.Partial, "the perspective membership rowset");
    }

    internal void LimitPerspectiveCoverage(params AnalyticalFeature[] features)
    {
        foreach (var feature in features.Where(feature => coverage.GetValueOrDefault(feature) != AnalyticalCoverage.Available))
            Record(AnalyticalFeature.PerspectiveMembership, AnalyticalCoverage.Partial, feature.ToString());
    }

    internal AnalyticalMetadata Complete()
    {
        CompleteTypeCoverage();
        return new(
            objects.Values.ToArray(), members.ToArray(),
            Enum.GetValues<AnalyticalFeature>().ToDictionary(feature => feature,
                feature => coverage.GetValueOrDefault(feature, AnalyticalCoverage.Unsupported)),
            diagnostics.ToArray());
    }

    private void CompleteTypeCoverage()
    {
        var typedFeatures = new[] { AnalyticalFeature.Columns, AnalyticalFeature.Levels, AnalyticalFeature.Measures };
        foreach (var feature in typedFeatures)
        {
            if (coverage.TryGetValue(feature, out var status))
                Record(AnalyticalFeature.DataTypes, status, feature.ToString());
        }
    }

    internal static AnalyticalDataType? DataType(DataRow row, string column, bool tabular)
    {
        var native = Text(row, column);
        if (native is null)
            return null;
        var type = tabular ? TabularType(native) : OleDbType(native);
        return new(native, type, Number(row, "CHARACTER_MAXIMUM_LENGTH"),
            Facet(row, "NUMERIC_PRECISION"), Facet(row, "NUMERIC_SCALE"));
    }

    private static long? Number(DataRow row, string column)
        => long.TryParse(Text(row, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 0
            ? number : null;

    private static int? Facet(DataRow row, string column)
        => Number(row, column) is long number && number <= int.MaxValue ? (int)number : null;

    // The tabular DataType enumeration and the OLE DB DBTYPE enumeration have different values.
    private static DbType? TabularType(string code) => code switch
    {
        "2" => DbType.String,
        "6" => DbType.Int64,
        "8" => DbType.Double,
        "9" => DbType.DateTime,
        "10" => DbType.Decimal,
        "11" => DbType.Boolean,
        "17" => DbType.Binary,
        "20" => DbType.Object,
        _ => null
    };

    private static DbType? OleDbType(string code) => code switch
    {
        "2" => DbType.Int16,
        "3" => DbType.Int32,
        "4" => DbType.Single,
        "5" => DbType.Double,
        "6" => DbType.Currency,
        "7" or "135" => DbType.DateTime,
        "8" or "130" => DbType.String,
        "11" => DbType.Boolean,
        "12" => DbType.Object,
        "14" or "131" or "139" => DbType.Decimal,
        "16" => DbType.SByte,
        "17" => DbType.Byte,
        "18" => DbType.UInt16,
        "19" => DbType.UInt32,
        "20" => DbType.Int64,
        "21" => DbType.UInt64,
        "72" => DbType.Guid,
        "128" => DbType.Binary,
        "129" => DbType.AnsiString,
        "133" => DbType.Date,
        "134" => DbType.Time,
        _ => null
    };
}
