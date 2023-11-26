using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

internal class TimeFormatter : IValueFormatter<TimeOnly>
{
    public string Format(TimeOnly value)
#if NET7_0_OR_GREATER
        => $"'{value:HH:mm:ss.FFFFFF}'";
#else
        => $"'{value:HH:mm:ss.FFF}'";
#endif

    public string Format(object obj)
        => obj is TimeOnly value ? Format(value) : throw new Exception();
}
