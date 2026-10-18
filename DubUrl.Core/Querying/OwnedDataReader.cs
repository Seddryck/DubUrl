using System.Collections;
using System.Data;
using System.Data.Common;
using System.Threading;

namespace DubUrl.Querying;

/// <summary>
/// A reader that owns the underlying reader, command, and connection. Disposing or closing it releases all three.
/// </summary>
public sealed class OwnedDataReader : DbDataReader
{
    private readonly IDataReader _reader;
    private readonly IDbCommand _command;
    private readonly IDbConnection _connection;
    private int _disposed;

    internal OwnedDataReader(IDataReader reader, IDbCommand command, IDbConnection connection)
        => (_reader, _command, _connection) = (reader, command, connection);

    public override int Depth => _reader.Depth;
    public override int FieldCount => _reader.FieldCount;
    public override bool HasRows => _reader is DbDataReader dbReader && dbReader.HasRows;
    public override bool IsClosed => _reader.IsClosed;
    public override int RecordsAffected => _reader.RecordsAffected;
    public override object this[int ordinal] => _reader[ordinal];
    public override object this[string name] => _reader[name];

    public override bool GetBoolean(int ordinal) => _reader.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => _reader.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => _reader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => _reader.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => _reader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override string GetDataTypeName(int ordinal) => _reader.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => _reader.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => _reader.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => _reader.GetDouble(ordinal);
    public override IEnumerator GetEnumerator() => ((IEnumerable)_reader).GetEnumerator();
    public override Type GetFieldType(int ordinal) => _reader.GetFieldType(ordinal);
    public override float GetFloat(int ordinal) => _reader.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => _reader.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => _reader.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => _reader.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => _reader.GetInt64(ordinal);
    public override string GetName(int ordinal) => _reader.GetName(ordinal);
    public override int GetOrdinal(string name) => _reader.GetOrdinal(name);
    public override string GetString(int ordinal) => _reader.GetString(ordinal);
    public override object GetValue(int ordinal) => _reader.GetValue(ordinal);
    public override int GetValues(object[] values) => _reader.GetValues(values);
    public override bool IsDBNull(int ordinal) => _reader.IsDBNull(ordinal);
    public override bool NextResult() => _reader.NextResult();
    public override bool Read() => _reader.Read();

    public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
        => _reader is DbDataReader dbReader
            ? dbReader.NextResultAsync(cancellationToken)
            : Task.FromResult(NextResult());

    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        => _reader is DbDataReader dbReader
            ? dbReader.ReadAsync(cancellationToken)
            : Task.FromResult(Read());

    public override void Close() => Dispose();

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                _reader.Dispose();
            }
            finally
            {
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
    }

    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            if (_reader is IAsyncDisposable asyncReader)
                await asyncReader.DisposeAsync().ConfigureAwait(false);
            else
                _reader.Dispose();
        }
        finally
        {
            try
            {
                if (_command is DbCommand dbCommand)
                    await dbCommand.DisposeAsync().ConfigureAwait(false);
                else
                    _command.Dispose();
            }
            finally
            {
                if (_connection is DbConnection dbConnection)
                    await dbConnection.DisposeAsync().ConfigureAwait(false);
                else
                    _connection.Dispose();
            }
        }
    }
}
