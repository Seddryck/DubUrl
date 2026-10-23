using System.Collections;
using System.Data;
using System.Data.Common;

namespace DubUrl.Adbc;

// Preserve CloseConnection semantics for the owning DubUrl connection, so closing
// a reader also releases the native database and driver.
internal sealed class AdbcDataReaderWrapper(DbDataReader inner, DbConnection? connection) : DbDataReader
{
    private bool closed;
    private bool? hasRows;
    private bool prefetched;
    private bool finished;
    public override void Close()
    {
        if (closed)
            return;
        closed = true;
        try { inner.Dispose(); }
        finally { connection?.Close(); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Close();
        base.Dispose(disposing);
    }
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override int VisibleFieldCount => inner.VisibleFieldCount;
    public override bool HasRows
    {
        get
        {
            if (hasRows is null)
                prefetched = Read();
            return hasRows == true;
        }
    }
    public override bool IsClosed => closed || inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;
    public override bool Read()
    {
        if (prefetched)
        {
            prefetched = false;
            return true;
        }
        if (finished)
            return false;
        while (inner.Read())
            if (inner.HasRows)
            {
                hasRows = true;
                return true;
            }
        hasRows ??= false;
        finished = true;
        return false;
    }
    public override bool NextResult() => inner.NextResult();
    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (prefetched)
        {
            prefetched = false;
            return true;
        }
        if (finished)
            return false;
        while (await inner.ReadAsync(cancellationToken).ConfigureAwait(false))
            if (inner.HasRows)
            {
                hasRows = true;
                return true;
            }
        hasRows ??= false;
        finished = true;
        return false;
    }
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => inner.NextResultAsync(cancellationToken);
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, false);
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override object GetValue(int ordinal) => inner.GetValue(ordinal) ?? DBNull.Value;
    public override int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
            values[i] = GetValue(i);
        return count;
    }
    // The Apache client may return DBNull.Value while its IsDBNull checks null.
    public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;
    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override string GetString(int ordinal) => inner.GetString(ordinal);
    public override T GetFieldValue<T>(int ordinal) => inner.GetFieldValue<T>(ordinal);
    public override DataTable? GetSchemaTable() => inner.GetSchemaTable();
}
