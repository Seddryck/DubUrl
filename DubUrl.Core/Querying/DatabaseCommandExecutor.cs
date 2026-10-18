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

    public DatabaseCommandExecutor(ConnectionUrl connectionUrl, CommandProvisionerFactory commandProvisionerFactory, IQueryLogger queryLogger)
        => (_connectionUrl, _commandProvisionerFactory, _queryLogger) = (connectionUrl, commandProvisionerFactory, queryLogger);

    public T ExecuteScalar<T>(ICommandProvider commandProvider, Func<IDbCommand, T> consume)
    {
        using var connection = _connectionUrl.Open();
        using var command = CreateCommand(commandProvider, connection);
        return consume(command);
    }

    public T ExecuteReader<T>(ICommandProvider commandProvider, Func<IDataReader, T> consume)
    {
        using var connection = _connectionUrl.Open();
        using var command = CreateCommand(commandProvider, connection);
        using var reader = command.ExecuteReader();
        return consume(reader);
    }

    public IDbCommand PrepareOwnedCommand(ICommandProvider commandProvider)
    {
        var connection = _connectionUrl.Open();
        try
        {
            return new OwnedDbCommand(CreateCommand(commandProvider, connection), connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public OwnedDataReader ExecuteOwnedReader(ICommandProvider commandProvider)
    {
        var connection = _connectionUrl.Open();
        try
        {
            var command = CreateCommand(commandProvider, connection);
            try
            {
                var reader = command.ExecuteReader();
                return new OwnedDataReader(reader, command, connection);
            }
            catch
            {
                command.Dispose();
                throw;
            }
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public async Task<OwnedDataReader> ExecuteOwnedReaderAsync(ICommandProvider commandProvider, CancellationToken cancellationToken)
    {
        var connection = CreateDbConnection();
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var command = CreateDbCommand(commandProvider, connection);
            try
            {
                var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                return new OwnedDataReader(reader, command, connection);
            }
            catch
            {
                await command.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<T> ExecuteScalarAsync<T>(
        ICommandProvider commandProvider,
        Func<DbCommand, CancellationToken, Task<T>> consume,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateDbCommand(commandProvider, connection);
        return await consume(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> ExecuteReaderAsync<T>(
        ICommandProvider commandProvider,
        Func<DbDataReader, CancellationToken, Task<T>> consume,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateDbCommand(commandProvider, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await consume(reader, cancellationToken).ConfigureAwait(false);
    }

    public IEnumerable<T> Stream<T>(ICommandProvider commandProvider, Func<IDataReader, T> project)
    {
        using var connection = _connectionUrl.Open();
        using var command = CreateCommand(commandProvider, connection);
        using var reader = command.ExecuteReader();

        while (reader.Read())
            yield return project(reader);
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(
        ICommandProvider commandProvider,
        Func<DbDataReader, T> project,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var connection = CreateDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateDbCommand(commandProvider, connection);
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

    private DbConnection CreateDbConnection()
        => _connectionUrl.Connect() as DbConnection
            ?? throw new NotSupportedException("Asynchronous execution requires a DbConnection provider.");
}
