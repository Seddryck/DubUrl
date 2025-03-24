using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.BulkCopy.Engines;
using DubUrl.Querying.Dialects;

namespace DubUrl.BulkCopy;
public class BulkCopyEngineFactory
{
    public virtual IBulkCopyEngine Create(ConnectionUrl connectionUrl)
    {
        ArgumentNullException.ThrowIfNull(connectionUrl, nameof(connectionUrl));
        return connectionUrl.Dialect switch
        {
            DuckdbDialect _ => new DuckDbBulkCopyEngine(connectionUrl),
            TSqlDialect _ => new MsSqlServerBulkCopyEngine(connectionUrl),
            _ => throw new NotSupportedException($"Dialect '{connectionUrl.Dialect}' not supported")
        };
    }
}
