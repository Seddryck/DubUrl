using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy;

public static class ConnectionUrlExtensions
{
    public static void BulkCopy(this ConnectionUrl connectionUrl, string tableName, IDataReader dataReader)
    {
        var factory = new BulkCopyEngineFactory();
        using var bulkCopy = factory.Create(connectionUrl);
        bulkCopy.Write(tableName, dataReader);
    }
}
