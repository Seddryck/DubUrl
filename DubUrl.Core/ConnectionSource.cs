using System.Data;
using System.Data.Common;

namespace DubUrl;

public interface IConnectionSource : IDisposable, IAsyncDisposable
{
    ConnectionLease Acquire();
    ValueTask<ConnectionLease> AcquireAsync(CancellationToken cancellationToken = default);
}

/// <summary>Owns connection lifetime and lends connections according to a fixed reuse strategy.</summary>
public sealed class ConnectionSource : IConnectionSource
{
    private readonly object _sync = new();
    private readonly Func<IDbConnection> _open;
    private readonly Func<CancellationToken, ValueTask<IDbConnection>> _openAsync;
    private readonly DbDataSource? _dataSource;
    private readonly SemaphoreSlim _exclusive = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IDbConnection? _retained;
    private int _active;
    private bool _disposed;

    internal ConnectionUrl QueryContext { get; }
    public ConnectionReuseMode ReuseMode { get; }
    /// <summary>A credential-free identifier explaining strategy selection.</summary>
    public string SelectionReason { get; }

    internal ConnectionSource(ConnectionUrl context, ConnectionReuseMode mode, DbDataSource? dataSource = null, string selectionReason = "explicit")
    {
        if (mode != ConnectionReuseMode.ProviderManaged && mode != ConnectionReuseMode.RetainedExclusive)
            throw new ArgumentOutOfRangeException(nameof(mode));
        QueryContext = context;
        ReuseMode = mode;
        SelectionReason = selectionReason;
        _dataSource = dataSource;
        _open = dataSource is null ? context.Open : () => dataSource.OpenConnection();
        _openAsync = dataSource is null ? OpenContextAsync : OpenDataSourceAsync;
    }

    private ValueTask<IDbConnection> OpenContextAsync(CancellationToken token)
    {
        if (QueryContext.HasAuthentication)
            return OpenAuthenticatedContextAsync(token);
        // Legacy wrappers can return IDbConnection without deriving from DbConnection.
        return OpenLegacyConnectionAsync(QueryContext.Connect(), token);
    }
    private async ValueTask<IDbConnection> OpenAuthenticatedContextAsync(CancellationToken token)
        => await QueryContext.OpenAsync(token).ConfigureAwait(false);
    private static async ValueTask<IDbConnection> OpenLegacyConnectionAsync(IDbConnection connection, CancellationToken token)
    {
        try
        {
            if (connection is DbConnection dbConnection)
                await dbConnection.OpenAsync(token).ConfigureAwait(false);
            else
            {
                token.ThrowIfCancellationRequested();
                connection.Open();
            }
            return connection;
        }
        catch
        {
            await DisposeConnectionAsync(connection, true).ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<IDbConnection> OpenDataSourceAsync(CancellationToken token)
        => await (_dataSource ?? throw new InvalidOperationException("No native data source is configured."))
            .OpenConnectionAsync(token).ConfigureAwait(false);

    public ConnectionLease Acquire() => AcquireCoreAsync(false, CancellationToken.None).GetAwaiter().GetResult();
    public ValueTask<ConnectionLease> AcquireAsync(CancellationToken cancellationToken = default)
        => AcquireCoreAsync(true, cancellationToken);

    private async ValueTask<ConnectionLease> AcquireCoreAsync(bool asynchronous, CancellationToken token)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _active++;
        }

        bool entered = false;
        IDbConnection? connection = null;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _shutdown.Token);
            var effectiveToken = linked.Token;
            if (ReuseMode == ConnectionReuseMode.RetainedExclusive)
            {
                if (asynchronous)
                    await _exclusive.WaitAsync(effectiveToken).ConfigureAwait(false);
                else
                    WaitForExclusiveConnection(effectiveToken);
                entered = true;
                if (_retained is not null && _retained.State != ConnectionState.Open)
                {
                    var stale = _retained;
                    _retained = null;
                    await DisposeConnectionAsync(stale, asynchronous).ConfigureAwait(false);
                }
                connection = _retained;
            }

            effectiveToken.ThrowIfCancellationRequested();
            connection ??= asynchronous
                ? await _openAsync(effectiveToken).ConfigureAwait(false)
                : _open();
            effectiveToken.ThrowIfCancellationRequested();
            lock (_sync)
                ObjectDisposedException.ThrowIf(_disposed, this);

            if (entered)
                _retained = connection;
            var borrowed = connection;
            return new ConnectionLease(borrowed, async useAsync =>
            {
                try
                {
                    if (!entered)
                        await DisposeConnectionAsync(borrowed, useAsync).ConfigureAwait(false);
                }
                finally
                {
                    if (entered) _exclusive.Release();
                    EndUse();
                }
            });
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            await AbandonAsync(connection, entered, asynchronous).ConfigureAwait(false);
            throw new ObjectDisposedException(nameof(ConnectionSource));
        }
        catch
        {
            await AbandonAsync(connection, entered, asynchronous).ConfigureAwait(false);
            throw;
        }
    }

    // The synchronous API must block without depending on an asynchronous provider implementation.
    private void WaitForExclusiveConnection(CancellationToken token) => _exclusive.Wait(token);

    private async ValueTask AbandonAsync(IDbConnection? connection, bool entered, bool asynchronous)
    {
        try
        {
            // An unsuccessful borrower does not own an existing retained connection. Leave it
            // available for the next borrower (or for shutdown to dispose after all users drain).
            if (connection is not null && (!entered || !ReferenceEquals(connection, _retained)))
                await DisposeConnectionAsync(connection, asynchronous).ConfigureAwait(false);
        }
        finally
        {
            if (entered) _exclusive.Release();
            EndUse();
        }
    }

    private void EndUse()
    {
        lock (_sync)
            if (--_active == 0 && _disposed) _drained.TrySetResult();
    }

    public void Dispose() => StartDisposal(false).GetAwaiter().GetResult();

    public ValueTask DisposeAsync() => StartDisposal(true);

    private ValueTask StartDisposal(bool asynchronous)
    {
        bool start;
        lock (_sync)
        {
            start = !_disposed;
            _disposed = true;
            if (_active == 0) _drained.TrySetResult();
        }
        if (start)
        {
            Exception? cancellationFailure = null;
            try { _shutdown.Cancel(); }
            catch (Exception exception) { cancellationFailure = exception; }
            // Even a provider's throwing cancellation callback must not strand shutdown.
            _ = FinishAsync(asynchronous, cancellationFailure);
        }
        return new ValueTask(_finished.Task);
    }

    private async Task FinishAsync(bool asynchronous, Exception? cancellationFailure)
    {
        var failures = new List<Exception>();
        if (cancellationFailure is not null) failures.Add(cancellationFailure);
        try
        {
            await _drained.Task.ConfigureAwait(false);
            try
            {
                if (_retained is not null)
                    await DisposeConnectionAsync(_retained, asynchronous).ConfigureAwait(false);
            }
            catch (Exception exception) { failures.Add(exception); }
            try
            {
                if (_dataSource is not null)
                    await DisposeConnectionAsync(_dataSource, asynchronous).ConfigureAwait(false);
            }
            catch (Exception exception) { failures.Add(exception); }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            _exclusive.Dispose();
            _shutdown.Dispose();
        }
        if (failures.Count == 0) _finished.TrySetResult();
        else _finished.TrySetException(failures.Count == 1 ? failures[0] : new AggregateException(failures));
    }

    internal static async ValueTask DisposeConnectionAsync(IDisposable connection, bool asynchronous)
    {
        if (asynchronous && connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else
            connection.Dispose();
    }
}
