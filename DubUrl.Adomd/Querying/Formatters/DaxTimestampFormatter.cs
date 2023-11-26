using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Querying.Formatters;

internal class DaxTimestampFormatter : IValueFormatter<DateTime>
{
    public string Format(DateTime value)
#if NET7_0_OR_GREATER
        => $"\"{value:yyyy-MM-dd HH:mm:ss.FFFFFF}\"";
#else
        => $"\"{value:yyyy-MM-dd HH:mm:ss.FFF}\"";
#endif

    public string Format(object obj)
        => obj is DateTime value ? Format(value) : throw new Exception();
}
