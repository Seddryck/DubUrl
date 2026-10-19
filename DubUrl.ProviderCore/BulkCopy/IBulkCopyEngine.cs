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
}
