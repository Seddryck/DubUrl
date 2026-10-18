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

    private async ValueTask<IDbConnection> OpenContextAsync(CancellationToken token)
    {
        var connection = QueryContext.Connect();
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
        => await _dataSource!.OpenConnectionAsync(token).ConfigureAwait(false);

    public ConnectionLease Acquire() => AcquireCoreAsync(false, default).GetAwaiter().GetResult();
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
                    _exclusive.Wait(effectiveToken);
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

    private async ValueTask AbandonAsync(IDbConnection? connection, bool entered, bool asynchronous)
    {
        try
        {
            if (ReferenceEquals(connection, _retained) && entered) _retained = null;
            if (connection is not null)
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

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    public ValueTask DisposeAsync()
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
            _shutdown.Cancel();
            _ = FinishAsync();
        }
        return new ValueTask(_finished.Task);
    }

    private async Task FinishAsync()
    {
        try
        {
            await _drained.Task.ConfigureAwait(false);
            try
            {
                if (_retained is not null)
                    await DisposeConnectionAsync(_retained, true).ConfigureAwait(false);
            }
            finally
            {
                if (_dataSource is not null)
                    await _dataSource.DisposeAsync().ConfigureAwait(false);
            }
            _finished.TrySetResult();
        }
        catch (Exception exception)
        {
            _finished.TrySetException(exception);
        }
        finally
        {
            _exclusive.Dispose();
            _shutdown.Dispose();
        }
    }

    internal static async ValueTask DisposeConnectionAsync(IDisposable connection, bool asynchronous)
    {
        if (asynchronous && connection is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else
            connection.Dispose();
    }
}
