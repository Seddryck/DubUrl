using DubUrl.Querying.Reading;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace DubUrl.Querying;

internal sealed class DatabaseCommandExecutor
{
    private readonly ConnectionUrl _connectionUrl;
    private readonly CommandProvisionerFactory _commandProvisionerFactory;
    private readonly IQueryLogger _queryLogger;
    private readonly Func<ConnectionSource> _source;

    public DatabaseCommandExecutor(ConnectionUrl connectionUrl, CommandProvisionerFactory commandProvisionerFactory, IQueryLogger queryLogger, Func<ConnectionSource> source)
        => (_connectionUrl, _commandProvisionerFactory, _queryLogger, _source) = (connectionUrl, commandProvisionerFactory, queryLogger, source);

    public T ExecuteScalar<T>(ICommandProvider commandProvider, Func<IDbCommand, T> consume)
    {
        using var lease = _source().Acquire();
        using var command = CreateCommand(commandProvider, lease.Handle);
        return consume(command);
    }

    public T ExecuteReader<T>(ICommandProvider commandProvider, Func<IDataReader, T> consume)
    {
        using var lease = _source().Acquire();
        using var command = CreateCommand(commandProvider, lease.Handle);
        using var reader = command.ExecuteReader();
        return consume(reader);
    }

    public IDbCommand PrepareOwnedCommand(ICommandProvider commandProvider)
    {
        var lease = _source().Acquire();
        try
        {
            return new OwnedDbCommand(CreateCommand(commandProvider, lease.Handle), lease.Handle, lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public OwnedDataReader ExecuteOwnedReader(ICommandProvider commandProvider)
    {
        var lease = _source().Acquire();
        try
        {
            var command = CreateCommand(commandProvider, lease.Handle);
            try
            {
                var reader = command.ExecuteReader();
                return new OwnedDataReader(reader, command, lease);
            }
            catch
            {
                command.Dispose();
                throw;
            }
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public async Task<OwnedDataReader> ExecuteOwnedReaderAsync(ICommandProvider commandProvider, CancellationToken cancellationToken)
    {
        var lease = await _source().AcquireAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var command = CreateDbCommand(commandProvider, lease.Connection);
            try
            {
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                return new OwnedDataReader(reader, command, lease);
            }
            catch
            {
                await command.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            await lease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<T> ExecuteScalarAsync<T>(
        ICommandProvider commandProvider,
        Func<DbCommand, CancellationToken, Task<T>> consume,
        CancellationToken cancellationToken)
    {
        await using var lease = await _source().AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateDbCommand(commandProvider, lease.Connection);
        return await consume(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> ExecuteReaderAsync<T>(
        ICommandProvider commandProvider,
        Func<DbDataReader, CancellationToken, Task<T>> consume,
        CancellationToken cancellationToken)
    {
        await using var lease = await _source().AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateDbCommand(commandProvider, lease.Connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await consume(reader, cancellationToken).ConfigureAwait(false);
    }

    public IEnumerable<T> Stream<T>(ICommandProvider commandProvider, Func<IDataReader, T> project)
    {
        using var lease = _source().Acquire();
        using var command = CreateCommand(commandProvider, lease.Handle);
        using var reader = command.ExecuteReader();

        while (reader.Read())
            yield return project(reader);
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(
        ICommandProvider commandProvider,
        Func<DbDataReader, T> project,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var lease = await _source().AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateDbCommand(commandProvider, lease.Connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            yield return project(reader);
    }

    private IDbCommand CreateCommand(ICommandProvider commandProvider, IDbConnection connection)
    {
        var command = connection.CreateCommand();
        try
        {
            Provision(commandProvider, command);
            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    private DbCommand CreateDbCommand(ICommandProvider commandProvider, DbConnection connection)
    {
        var command = connection.CreateCommand();
        try
        {
            Provision(commandProvider, command);
            return command;
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    private void Provision(ICommandProvider commandProvider, IDbCommand command)
    {
        foreach (var provisioner in _commandProvisionerFactory.Instantiate(commandProvider, _connectionUrl))
            provisioner.Execute(command);
        _queryLogger.Log(command.CommandText);
    }

}
