using System.Data.SqlTypes;
using System.Numerics;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;

namespace DubUrl;

public sealed partial class TableTarget
{
    private TableSqlRenderer Sql => _database.Dialect.Language is SqlLanguage
        ? _database.Dialect.TableSqlRenderer
        : throw new NotSupportedException("Table operations require a relational SQL dialect.");

    /// <summary>Streams the table or view. Dispose the returned reader when finished.</summary>
    public OwnedDataReader SelectAll() => _database.ExecuteReader(Sql.RenderSelectAll(SqlName));

    /// <inheritdoc cref="SelectAll"/>
    public Task<OwnedDataReader> SelectAllAsync(CancellationToken cancellationToken = default)
        => _database.ExecuteReaderAsync(Sql.RenderSelectAll(SqlName), cancellationToken);

    /// <summary>Tests whether a zero-row query resolves the target using the current connection.</summary>
    /// <remarks>A successful probe is independent of permissions to select every column.</remarks>
    public bool Exists()
    {
        var sql = Sql;
        var command = sql.RenderExists(SqlName);
        try
        {
            using var reader = _database.ExecuteReader(command);
            _ = reader.Read();
            return true;
        }
        catch (Exception error) when (error is DubUrlTableNotFoundException || sql.IsMissingTable(error))
        {
            return false;
        }
    }

    /// <inheritdoc cref="Exists"/>
    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sql = Sql;
        var command = sql.RenderExists(SqlName);
        try
        {
            await using var reader = await _database.ExecuteReaderAsync(command, cancellationToken).ConfigureAwait(false);
            _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested
            && (error is DubUrlTableNotFoundException || sql.IsMissingTable(error)))
        {
            return false;
        }
    }

    /// <summary>Checks whether the target contains at least one visible row.</summary>
    public bool HasRows()
    {
        using var reader = _database.ExecuteReader(Sql.RenderHasRows(SqlName));
        return reader.Read();
    }

    /// <inheritdoc cref="HasRows"/>
    public async Task<bool> HasRowsAsync(CancellationToken cancellationToken = default)
    {
        await using var reader = await _database.ExecuteReaderAsync(Sql.RenderHasRows(SqlName), cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns an exact count of visible rows.</summary>
    public long CountRows() => ToRowCount(_database.ReadScalarNonNull(Sql.RenderCountRows(SqlName)));

    /// <inheritdoc cref="CountRows"/>
    public async Task<long> CountRowsAsync(CancellationToken cancellationToken = default)
        => ToRowCount(await _database.ReadScalarNonNullAsync(Sql.RenderCountRows(SqlName), cancellationToken).ConfigureAwait(false));

    /// <summary>Executes native truncation on a supported table without a row-count result.</summary>
    public void Truncate() => _database.ExecuteNonQuery(Sql.RenderTruncate(SqlName));

    /// <inheritdoc cref="Truncate"/>
    public Task TruncateAsync(CancellationToken cancellationToken = default)
        => _database.ExecuteNonQueryAsync(Sql.RenderTruncate(SqlName), cancellationToken);

    private static long ToRowCount(object value)
    {
        var count = value switch
        {
            long number => number,
            int number => number,
            short number => number,
            sbyte number => number,
            uint number => number,
            ushort number => number,
            byte number => number,
            ulong number => checked((long)number),
            BigInteger number => (long)number,
            SqlDecimal number => ToIntegralCount(number.Value),
            decimal number => ToIntegralCount(number),
            _ => throw new InvalidOperationException("The provider did not return an exact integer row count.")
        };

        return count >= 0 ? count : throw new InvalidOperationException("The provider returned a negative row count.");
    }

    private static long ToIntegralCount(decimal value)
        => value == decimal.Truncate(value) ? decimal.ToInt64(value)
            : throw new InvalidOperationException("The provider returned a fractional row count.");
}
