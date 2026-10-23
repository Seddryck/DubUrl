using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace DubUrl.Adbc;

internal sealed class AdbcCommandWrapper(DbConnection connection, DbCommand inner) : DbCommand
{
    [AllowNull]
    public override string CommandText { get => inner.CommandText; set => inner.CommandText = value; }
    public override int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
    public override CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    [AllowNull]
    protected override DbConnection DbConnection
    {
        get => connection;
        set
        {
            if (!ReferenceEquals(value, connection))
                throw new NotSupportedException("Create a command from its ADBC connection.");
        }
    }
    protected override DbParameterCollection DbParameterCollection => inner.Parameters;
    protected override DbTransaction? DbTransaction { get => inner.Transaction; set => inner.Transaction = value; }
    public override void Cancel() => inner.Cancel();
    public override void Prepare() => inner.Prepare();
    protected override DbParameter CreateDbParameter() => inner.CreateParameter();
    public override int ExecuteNonQuery() => inner.ExecuteNonQuery();

    public override object? ExecuteScalar()
    {
        using var reader = ExecuteReader();
        return reader.Read() && reader.FieldCount > 0 ? reader.GetValue(0) : null;
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var reader = inner.ExecuteReader(behavior & ~CommandBehavior.CloseConnection);
        return new AdbcDataReaderWrapper(reader, behavior.HasFlag(CommandBehavior.CloseConnection) ? connection : null);
    }

    public override async Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        await using var reader = await ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && reader.FieldCount > 0 ? reader.GetValue(0) : null;
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => inner.ExecuteNonQueryAsync(cancellationToken);

    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        var reader = await inner.ExecuteReaderAsync(behavior & ~CommandBehavior.CloseConnection, cancellationToken).ConfigureAwait(false);
        return new AdbcDataReaderWrapper(reader, behavior.HasFlag(CommandBehavior.CloseConnection) ? connection : null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            inner.Dispose();
        base.Dispose(disposing);
    }
}
