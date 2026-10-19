using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy.Engines;
internal class MsSqlServerBulkCopyFactory
{
    public virtual MsSqlServerBulkCopyProxy Create(IDbConnection connection, string destinationTableName)
    {
        var sqlConnectionType = connection.GetType();

        // Look for SqlBulkCopy constructor that accepts a SqlConnection
        var assembly = sqlConnectionType.Assembly;
        var bulkCopyType = assembly.GetType("Microsoft.Data.SqlClient.SqlBulkCopy")
                        ?? assembly.GetType("System.Data.SqlClient.SqlBulkCopy")
                        ?? throw new InvalidOperationException("SqlBulkCopy type not found.");

        var ctor = bulkCopyType.GetConstructor([sqlConnectionType])
                   ?? throw new InvalidOperationException("No suitable SqlBulkCopy constructor found.");

        var bulkCopy = ctor.Invoke(new[] { connection });

        // Set properties
        bulkCopyType.GetProperty("DestinationTableName")?.SetValue(bulkCopy, destinationTableName);
        bulkCopyType.GetProperty("BatchSize")?.SetValue(bulkCopy, 1000);
        bulkCopyType.GetProperty("EnableStreaming")?.SetValue(bulkCopy, true);
        bulkCopyType.GetProperty("BulkCopyTimeout")?.SetValue(bulkCopy, 0);

        // Methods
        var writeToServerMethod = bulkCopyType.GetMethod("WriteToServer", [typeof(IDataReader)])
                                ?? throw new InvalidOperationException("WriteToServer method not found.");

        var closeMethod = bulkCopyType.GetMethod("Close") ?? throw new InvalidOperationException("Close method not found.");

        return new MsSqlServerBulkCopyProxy(
            (IDataReader dr) => writeToServerMethod.Invoke(bulkCopy, [dr]),
            () => closeMethod.Invoke(bulkCopy, null)
        );
    }
}
