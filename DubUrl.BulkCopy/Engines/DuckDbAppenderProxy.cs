using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy.Engines;
internal class DuckDbAppenderProxy
{
    public Func<DuckDbAppenderRowProxy> CreateRow { get; }
    public Action Close { get; }

    internal DuckDbAppenderProxy(Func<DuckDbAppenderRowProxy> createRow, Action close)
        => (CreateRow, Close)
            = (createRow, close);
}
