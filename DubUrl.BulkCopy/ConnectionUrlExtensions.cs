using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy;

/// <summary>
/// Extension methods for ConnectionUrl related to bulk copy operations.
/// </summary>
public static class ConnectionUrlExtensions
{
    /// <summary>
    /// Performs a bulk copy operation from the provided data reader to the specified table.
    /// </summary>
    /// <param name="connectionUrl">The connection URL</param>
    /// <param name="tableName">Name of the target table</param>
    /// <param name="dataReader">Data reader containing the source data</param>
    /// <exception cref="ArgumentNullException">Thrown when connectionUrl, tableName, or dataReader is null</exception>
    public static void BulkCopy(this ConnectionUrl connectionUrl, string tableName, IDataReader dataReader)
    {
        ArgumentNullException.ThrowIfNull(connectionUrl, nameof(connectionUrl));
        ArgumentNullException.ThrowIfNull(tableName, nameof(tableName));
        ArgumentNullException.ThrowIfNull(dataReader, nameof(dataReader));

        var factory = new BulkCopyEngineFactory();
        using var bulkCopy = factory.Create(connectionUrl);
        bulkCopy.Write(tableName, dataReader);
    }
}
