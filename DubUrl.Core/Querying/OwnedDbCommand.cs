using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace DubUrl.Querying;

internal sealed class OwnedDbCommand : IDbCommand
{
    private readonly IDbCommand _command;
    private IDbConnection _connection;
    private readonly IDisposable _owner;
    private readonly object _sync = new();
    private bool _ownershipTransferred;
    private bool _disposed;
    private readonly DisposalCompletion _disposal = new();

    public OwnedDbCommand(IDbCommand command, IDbConnection connection, IDisposable? owner = null)
        => (_command, _connection, _owner) = (command, connection, owner ?? connection);

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
    public int ExecuteNonQuery() => Execute(_command.ExecuteNonQuery);
    public object? ExecuteScalar() => Execute(_command.ExecuteScalar);
    public void Prepare() => Execute(() => { _command.Prepare(); return 0; });
    public IDataReader ExecuteReader() => ExecuteReader(CommandBehavior.Default);

    public IDataReader ExecuteReader(CommandBehavior behavior)
    {
        lock (_sync)
        {
            EnsureActive();
            try
            {
                var reader = _command.ExecuteReader(behavior & ~CommandBehavior.CloseConnection);
                _ownershipTransferred = true;
                return new OwnedDataReader(reader, _command, _owner);
            }
            catch
            {
                Dispose();
                throw;
            }
        }
    }

    private T Execute<T>(Func<T> action)
    {
        lock (_sync)
        {
            EnsureActive();
            return action();
        }
    }

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_ownershipTransferred)
            throw new InvalidOperationException("The reader owns this command and its connection lease.");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_ownershipTransferred) return;
            _disposed = true;
        }

        _disposal.Run(() =>
        {
            try { _command.Dispose(); }
            finally { _owner.Dispose(); }
            return default;
        }).GetAwaiter().GetResult();
    }
}
