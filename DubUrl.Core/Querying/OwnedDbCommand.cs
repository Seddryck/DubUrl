using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace DubUrl.Querying;

internal sealed class OwnedDbCommand : IDbCommand
{
    private readonly IDbCommand _command;
    private IDbConnection _connection;
    private int _ownershipTransferred;
    private int _disposed;

    public OwnedDbCommand(IDbCommand command, IDbConnection connection)
        => (_command, _connection) = (command, connection);

    [AllowNull]
    public string CommandText { get => _command.CommandText; set => _command.CommandText = value; }
    public int CommandTimeout { get => _command.CommandTimeout; set => _command.CommandTimeout = value; }
    public CommandType CommandType { get => _command.CommandType; set => _command.CommandType = value; }
    public IDbConnection? Connection
    {
        get => _connection;
        set
        {
            if (!ReferenceEquals(value, _connection))
                throw new InvalidOperationException("The connection owned by this command cannot be replaced.");
            _connection = value ?? throw new InvalidOperationException("The owned connection cannot be null.");
        }
    }
    public IDataParameterCollection Parameters => _command.Parameters;
    public IDbTransaction? Transaction { get => _command.Transaction; set => _command.Transaction = value; }
    public UpdateRowSource UpdatedRowSource { get => _command.UpdatedRowSource; set => _command.UpdatedRowSource = value; }

    public void Cancel() => _command.Cancel();
    public IDbDataParameter CreateParameter() => _command.CreateParameter();
    public int ExecuteNonQuery() => _command.ExecuteNonQuery();
    public object? ExecuteScalar() => _command.ExecuteScalar();
    public void Prepare() => _command.Prepare();
    public IDataReader ExecuteReader() => ExecuteReader(CommandBehavior.Default);

    public IDataReader ExecuteReader(CommandBehavior behavior)
    {
        try
        {
            var reader = _command.ExecuteReader(behavior);
            Interlocked.Exchange(ref _ownershipTransferred, 1);
            return new OwnedDataReader(reader, _command, _connection);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Volatile.Read(ref _ownershipTransferred) != 0 || Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _command.Dispose();
        }
        finally
        {
            _connection.Dispose();
        }
    }
}
