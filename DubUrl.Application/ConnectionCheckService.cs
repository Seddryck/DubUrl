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
    /// <summary>Additional bounded opening allowance for interactive authentication. Query execution retains TimeoutSeconds.</summary>
    public int AuthenticationTimeoutSeconds { get; init; }
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
        if (options.TimeoutSeconds <= 0 || options.AuthenticationTimeoutSeconds is < 0 or > 3600 || (options.Engine && options.Table is not null))
            throw new ArgumentException("Specify a positive timeout and at most one of engine or table.");
        var family = ConnectionCheckSql.GetFamily(ConnectionUri.Parse(target.Url).Scheme.Canonical);
        using var attempt = new CheckAttempt(options, family, cancellationToken);
        try
        {
            await attempt.ExecuteAsync(target, cancellationToken).ConfigureAwait(false);
            return attempt.Result(true, []);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested) { throw new OperationCanceledException(cancellationToken); }
        catch (OperationCanceledException) when (attempt.AcquiringCredentials || !attempt.TimedOut) { throw; }
        catch (Exception error) when (error is DbException or OperationCanceledException or TimeoutException or IOException or ConnectionAuthenticationException
            || error.GetType().FullName == "BAMCIS.PrestoClient.Model.PrestoQueryException")
        { return attempt.Result(false, [attempt.Classify(error)]); }
    }

    private sealed class CheckAttempt : IDisposable
    {
        private readonly ConnectionCheckOptions options;
        private readonly string family;
        private readonly string mode;
        private readonly string? sql;
        private readonly CancellationTokenSource budget;
        private readonly Stopwatch elapsed = Stopwatch.StartNew();
        private string? provider;
        private bool executing;
        internal bool AcquiringCredentials { get; private set; } = true;
        internal bool TimedOut => budget.IsCancellationRequested;
        internal CheckAttempt(ConnectionCheckOptions options, string family, CancellationToken cancellationToken)
        {
            this.options = options; this.family = family;
            mode = GetMode(options);
            sql = GetStatement(options, family);
            budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }
        internal async Task ExecuteAsync(ConnectionUrl target, CancellationToken cancellationToken)
        {
            // Credential acquisition creates a closed connection, before starting the DB budget.
            using var connection = await target.ConnectAsync(cancellationToken).ConfigureAwait(false);
            AcquiringCredentials = false;
            var databaseClock = Stopwatch.StartNew();
            budget.CancelAfter(TimeSpan.FromSeconds((double)options.TimeoutSeconds + options.AuthenticationTimeoutSeconds));
            provider = connection.GetType().Namespace;
            ValidateFileProvider(family, provider);
            RequireExistingFile(connection, family);
            await target.OpenPreparedAsync(connection, budget.Token).ConfigureAwait(false);
            budget.Token.ThrowIfCancellationRequested();
            if (options.AuthenticationTimeoutSeconds > 0)
            {
                databaseClock.Restart();
                budget.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            }
            if (sql is not null) await ExecuteStatementAsync(connection, databaseClock).ConfigureAwait(false);
            budget.Token.ThrowIfCancellationRequested();
        }
        private async Task ExecuteStatementAsync(DbConnection connection, Stopwatch databaseClock)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql ?? throw new InvalidOperationException("No check statement was selected.");
            command.CommandTimeout = Math.Max(1, (int)Math.Ceiling(options.TimeoutSeconds - databaseClock.Elapsed.TotalSeconds));
            executing = true;
            // Successful execution with zero rows is success. No alternate query follows.
            using var reader = await command.ExecuteReaderAsync(budget.Token).ConfigureAwait(false);
        }
        internal ConnectionCheckResult Result(bool success, IReadOnlyList<ConnectionCheckDiagnostic> diagnostics)
            => new(success, mode, provider, elapsed.ElapsedMilliseconds, diagnostics);
        internal ConnectionCheckDiagnostic Classify(Exception error)
        {
            if (budget.IsCancellationRequested || error is TimeoutException)
                return new("timeout", "The database check exceeded its time budget.");
            if (error is ConnectionAuthenticationException authentication) return new(authentication.Code, authentication.Message);
            return ConnectionCheckErrors.Classify(family, provider, error, executing && mode == "table", options.Table);
        }
        public void Dispose() => budget.Dispose();
        private static string GetMode(ConnectionCheckOptions options)
        {
            if (options.Table is not null) return "table";
            return options.Engine ? "engine" : "connection";
        }
        private static string? GetStatement(ConnectionCheckOptions options, string family)
        {
            if (options.Table is not null) return ConnectionCheckSql.Table(family, options.Table);
            return options.Engine ? ConnectionCheckSql.Engine(family) : null;
        }
        private static void ValidateFileProvider(string family, string? provider)
        {
            if (family is "access" or "excel" or "text") throw new NotSupportedException("This file driver has no verified existing-file-only opening mode.");
            if (family is "sqlite" or "duckdb" && provider is "System.Data.Odbc" or "System.Data.OleDb")
                throw new NotSupportedException("This file driver has no verified existing-file-only opening mode.");
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
}
