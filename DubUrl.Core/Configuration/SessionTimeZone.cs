using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace DubUrl;

// Keep the actual native connection (and provider casts), and initialize every logical open.
internal static class SessionTimeZone
{
    private sealed class State { internal CancellationToken Cancellation; }
    private static readonly ConditionalWeakTable<DbConnection, State> states = new();

    internal static void Configure(DbConnection connection, string mapping, string zone)
    {
        var sql = mapping switch
        {
            "sql:mysql" => "SET SESSION time_zone = '" + zone + "'",
            "sql:oracle" => "ALTER SESSION SET TIME_ZONE = '" + zone + "'",
            "sql:firebird" => "SET TIME ZONE '" + zone + "'",
            "sql:teradata" => "SET TIME ZONE INTERVAL " + (zone[0] == '-' ? "-" : "") + "'" + zone[1..] + "' HOUR TO MINUTE",
            _ => throw new NotSupportedException("Unknown session time-zone mapping.")
        };
        // Zone values have already passed the strict identifier/offset allowlist.
        var state = states.GetOrCreateValue(connection);
        connection.StateChange += (_, change) =>
        {
            if (change.CurrentState != ConnectionState.Open) return;
            try
            {
                state.Cancellation.ThrowIfCancellationRequested();
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                using var registration = state.Cancellation.Register(() => { try { command.Cancel(); } catch { } });
                command.ExecuteNonQuery();
                state.Cancellation.ThrowIfCancellationRequested();
            }
            catch
            {
                connection.Dispose();
                state.Cancellation.ThrowIfCancellationRequested();
                throw;
            }
        };
    }

    internal static void SetCancellation(DbConnection connection, CancellationToken cancellation)
    {
        if (states.TryGetValue(connection, out var state)) state.Cancellation = cancellation;
    }
}
