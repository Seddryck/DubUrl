using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

internal class TimestampTimeZoneFormatter : IValueFormatter<DateTimeOffset>
{
    public string Format(DateTimeOffset value)
#if NET7_0_OR_GREATER
        => $"'{value:yyyy-MM-dd HH:mm:ss.FFFFFFzzz}'";
#else
        => $"'{value:yyyy-MM-dd HH:mm:ss.FFFzzz}'";
#endif

    public string Format(object obj)
        => obj is DateTimeOffset value ? Format(value) : throw new Exception();
}
