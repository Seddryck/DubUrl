using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy.Engines;
internal class MsSqlServerBulkCopyProxy : IDisposable
{
    public Action<IDataReader> WriteToServer { get; }
    public Action Close { get; }

    internal MsSqlServerBulkCopyProxy(Action<IDataReader> writeToServer, Action close)
        => (WriteToServer, Close) = (writeToServer, close);

    public void Dispose()
        => Close();
}
