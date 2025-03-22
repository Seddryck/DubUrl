using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy.Engines;
internal class DuckDbAppenderRowProxy
{
    public Action AppendValueNull { get; }
    public Action<object> AppendValue { get; }
    public Action EndRow { get; }

    internal DuckDbAppenderRowProxy(Action appendNull, Action<object> append, Action endRow)
        => (AppendValueNull, AppendValue, EndRow)
            = (appendNull, append, endRow);
}
