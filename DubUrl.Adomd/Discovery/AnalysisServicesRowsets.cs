using System.Data;
using System.Data.Common;
using System.Globalization;
using DubUrl.Discovery;
using Microsoft.AnalysisServices.AdomdClient;

namespace DubUrl.Adomd.Discovery;

internal sealed record AnalysisServicesRowset(AnalyticalCoverage Coverage, IReadOnlyList<DataRow> Rows);

internal sealed class AnalysisServicesRowsets
{
    private readonly Func<string, AdomdRestrictionCollection, IReadOnlyList<DataRow>> discover;
    private readonly CancellationToken cancellationToken;
    private readonly HashSet<string>? advertised;

    internal AnalysisServicesRowsets(Func<string, AdomdRestrictionCollection, IReadOnlyList<DataRow>> discover,
        CancellationToken cancellationToken)
    {
        this.discover = discover;
        this.cancellationToken = cancellationToken;
        var names = Read("DISCOVER_SCHEMA_ROWSETS").Rows
            .Select(row => Text(row, "SchemaName")).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        advertised = names.Count == 0 ? null : names;
    }

    internal AnalysisServicesRowset Read(string name, AdomdRestrictionCollection? restrictions = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (advertised is not null && !advertised.Contains(name))
            return new(AnalyticalCoverage.Unsupported, []);
        try
        {
            var rows = discover(name, restrictions ?? new AdomdRestrictionCollection());
            cancellationToken.ThrowIfCancellationRequested();
            return new(AnalyticalCoverage.Available, rows);
        }
        catch (NotSupportedException)
        {
            return new(AnalyticalCoverage.Unsupported, []);
        }
        catch (Exception exception) when (IsDriverFailure(exception))
        {
            return new(AnalyticalCoverage.Inaccessible, []);
        }
    }

    internal static bool IsDriverFailure(Exception exception)
        => exception is AdomdException or DbException or TimeoutException;

    internal static string? Text(DataRow row, string column)
    {
        if (!row.Table.Columns.Contains(column) || row.IsNull(column))
            return null;
        var value = Convert.ToString(row[column], CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    internal static bool? Flag(DataRow row, string column)
        => Text(row, column) switch
        {
            "1" => true,
            "0" => false,
            var value when bool.TryParse(value, out var flag) => flag,
            _ => null
        };
}
