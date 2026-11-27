using System.Collections;
using System.Data;
using System.Data.Common;
using System.Threading;

namespace DubUrl.Querying;

/// <summary>
/// A reader that owns its underlying reader and, when returned by DatabaseUrl, its command and connection lease.
/// </summary>
public sealed class OwnedDataReader : DbDataReader
{
    private readonly IDataReader _reader;
    private readonly IDbCommand? _command;
    private readonly IDisposable? _owner;
    private readonly DisposalCompletion _disposal = new();

    internal OwnedDataReader(IDataReader reader, IDbCommand? command = null, IDisposable? owner = null)
        => (_reader, _command, _owner) = (reader, command, owner);

    public override int Depth => _reader.Depth;
    public override int FieldCount => _reader.FieldCount;
    public override bool HasRows => CommandTimeoutTranslation.Execute(() => _reader is DbDataReader { HasRows: true });
    public override bool IsClosed => _reader.IsClosed;
    public override int RecordsAffected => _reader.RecordsAffected;
    public override object this[int ordinal] => CommandTimeoutTranslation.Execute(() => _reader[ordinal]);
    public override object this[string name] => CommandTimeoutTranslation.Execute(() => _reader[name]);

    public override bool GetBoolean(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetBoolean(ordinal));
    public override byte GetByte(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetByte(ordinal));
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => CommandTimeoutTranslation.Execute(() => _reader.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length));
    public override char GetChar(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetChar(ordinal));
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => CommandTimeoutTranslation.Execute(() => _reader.GetChars(ordinal, dataOffset, buffer, bufferOffset, length));
    public override string GetDataTypeName(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetDataTypeName(ordinal));
    public override DateTime GetDateTime(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetDateTime(ordinal));
    public override decimal GetDecimal(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetDecimal(ordinal));
    public override double GetDouble(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetDouble(ordinal));
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);
    public override Type GetFieldType(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetFieldType(ordinal));
    public override float GetFloat(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetFloat(ordinal));
    public override Guid GetGuid(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetGuid(ordinal));
    public override short GetInt16(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetInt16(ordinal));
    public override int GetInt32(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetInt32(ordinal));
    public override long GetInt64(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetInt64(ordinal));
    public override string GetName(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetName(ordinal));
    public override int GetOrdinal(string name) => CommandTimeoutTranslation.Execute(() => _reader.GetOrdinal(name));
    public override string GetString(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetString(ordinal));
    public override object GetValue(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.GetValue(ordinal));
    public override int GetValues(object[] values) => CommandTimeoutTranslation.Execute(() => _reader.GetValues(values));
    public override bool IsDBNull(int ordinal) => CommandTimeoutTranslation.Execute(() => _reader.IsDBNull(ordinal));
    public override bool NextResult() => CommandTimeoutTranslation.Execute(_reader.NextResult);
    public override bool Read() => CommandTimeoutTranslation.Execute(_reader.Read);

    public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
        => _reader is DbDataReader dbReader
            ? CommandTimeoutTranslation.ExecuteAsync(dbReader.NextResultAsync, cancellationToken)
            : base.NextResultAsync(cancellationToken);

    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        => _reader is DbDataReader dbReader
            ? CommandTimeoutTranslation.ExecuteAsync(dbReader.ReadAsync, cancellationToken)
            : base.ReadAsync(cancellationToken);

    public override T GetFieldValue<T>(int ordinal)
        => _reader is DbDataReader dbReader
            ? CommandTimeoutTranslation.Execute(() => dbReader.GetFieldValue<T>(ordinal))
            : base.GetFieldValue<T>(ordinal);

    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => _reader is DbDataReader dbReader
            ? CommandTimeoutTranslation.ExecuteAsync(token => dbReader.GetFieldValueAsync<T>(ordinal, token), cancellationToken)
            : base.GetFieldValueAsync<T>(ordinal, cancellationToken);

    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken)
        => _reader is DbDataReader dbReader
            ? CommandTimeoutTranslation.ExecuteAsync(token => dbReader.IsDBNullAsync(ordinal, token), cancellationToken)
            : base.IsDBNullAsync(ordinal, cancellationToken);

    public override DataTable? GetSchemaTable() => CommandTimeoutTranslation.Execute(_reader.GetSchemaTable);

    public override void Close() => Dispose();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _disposal.Run(() => DisposeOwnedAsync(false)).GetAwaiter().GetResult();
    }

    public override ValueTask DisposeAsync() => _disposal.Run(() => DisposeOwnedAsync(true));

    private async ValueTask DisposeOwnedAsync(bool asynchronous)
    {
        try
        {
            await ConnectionSource.DisposeConnectionAsync(_reader, asynchronous).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (_command is not null)
                    await ConnectionSource.DisposeConnectionAsync(_command, asynchronous).ConfigureAwait(false);
            }
            finally
            {
                if (_owner is not null)
                    await ConnectionSource.DisposeConnectionAsync(_owner, asynchronous).ConfigureAwait(false);
            }
        }
    }
}
