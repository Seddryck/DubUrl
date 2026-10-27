using System.Data.Common;
using System.Diagnostics;
using DubUrl.Parsing;
using DubUrl.Authentication;

namespace DubUrl.Application;

public sealed record ConnectionCheckOptions
{
    public bool Engine { get; init; }
    public string? Table { get; init; }
    public int TimeoutSeconds { get; init; } = 15;
}

public sealed record ConnectionCheckDiagnostic(string Code, string Message, string? NativeCode = null, string? SqlState = null);
public sealed record ConnectionCheckResult(bool Success, string Mode, string? Provider, long ElapsedMilliseconds,
    IReadOnlyList<ConnectionCheckDiagnostic> Diagnostics);

public interface IConnectionCheckService
{
    Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default);
}

/// <summary>One connection attempt and, optionally, one application SQL statement. Never inspects metadata.</summary>
public sealed class ConnectionCheckService : IConnectionCheckService
{
    public async Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);
        if (options.TimeoutSeconds <= 0 || (options.Engine && options.Table is not null))
            throw new ArgumentException("Specify a positive timeout and at most one of engine or table.");
        var clock = Stopwatch.StartNew();
        var mode = options.Table is not null ? "table" : options.Engine ? "engine" : "connection";
        var family = ConnectionCheckSql.GetFamily(ConnectionUri.Parse(target.Url).Scheme.Canonical);
        var sql = options.Table is not null ? ConnectionCheckSql.Table(family, options.Table)
            : options.Engine ? ConnectionCheckSql.Engine(family) : null;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string? provider = null;
        var executing = false;
        try
        {
            // Creating/configuring the connection must not open it. A provider's async operations may
            // implement cancellation only partially; always await completion before disposing it.
            using var connection = await target.ConnectAsync(cancellationToken).ConfigureAwait(false);
            // Explicit password/store acquisition prepares a closed connection before the DB budget.
            var databaseClock = Stopwatch.StartNew();
            budget.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            provider = connection.GetType().Namespace;
            if (family is "access" or "excel" or "text" || (family is "sqlite" or "duckdb" && provider is "System.Data.Odbc" or "System.Data.OleDb"))
                throw new NotSupportedException("This file driver has no verified existing-file-only opening mode.");
            RequireExistingFile(connection, family);
            await target.OpenPreparedAsync(connection, budget.Token).ConfigureAwait(false);
            budget.Token.ThrowIfCancellationRequested();
            if (sql is not null)
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandTimeout = Math.Max(1, (int)Math.Ceiling(options.TimeoutSeconds - databaseClock.Elapsed.TotalSeconds));
                executing = true;
                using var reader = await command.ExecuteReaderAsync(budget.Token).ConfigureAwait(false);
                // Executing the statement is the check. A zero-row result is successful.
            }
            budget.Token.ThrowIfCancellationRequested();
            return new(true, mode, provider, clock.ElapsedMilliseconds, []);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or OperationCanceledException or TimeoutException or IOException or ConnectionAuthenticationException)
        {
            var diagnostic = budget.IsCancellationRequested || exception is TimeoutException
                ? new ConnectionCheckDiagnostic("timeout", "The database check exceeded its time budget.")
                : exception is ConnectionAuthenticationException authentication
                    ? new ConnectionCheckDiagnostic(authentication.Code, authentication.Message)
                    : ConnectionCheckErrors.Classify(family, provider, exception, executing && mode == "table", options.Table);
            return new(false, mode, provider, clock.ElapsedMilliseconds, [diagnostic]);
        }
    }

    private static void RequireExistingFile(DbConnection connection, string family)
    {
        if (family is not ("sqlite" or "duckdb")) return;
        var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
        if (family == "sqlite")
        {
            if (builder.TryGetValue("Data Source", out var source) && source.ToString() == ":memory:") return;
            builder["Mode"] = "ReadOnly";
        }
        else
        {
            if (builder.TryGetValue("DataSource", out var source) && source.ToString() == ":memory:") return;
            builder["Access_Mode"] = "Read_Only";
        }
        connection.ConnectionString = builder.ConnectionString;
    }
}
