using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy;

/// <summary>
/// Defines operations for bulk copying data into a database table.
/// </summary>
public interface IBulkCopyEngine : IDisposable
{
    /// <summary>
    /// Writes data from the provided IDataReader to the specified table.
    /// </summary>
    /// <param name="tableName">The name of the target table</param>
    /// <param name="dataReader">The data source to read from</param>
    void Write(string tableName, IDataReader dataReader);

    /// <summary>Writes data using the requested bulk-copy options.</summary>
    /// <exception cref="NotSupportedException">The provider cannot honor the requested options.</exception>
    void Write(string tableName, IDataReader dataReader, BulkCopyOptions options)
    {
        if (options.TableLock)
            throw new NotSupportedException("TableLock is not supported by the selected bulk-copy provider.");

        Write(tableName, dataReader);
    }
}
