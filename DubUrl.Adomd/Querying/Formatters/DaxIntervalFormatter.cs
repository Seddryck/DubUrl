using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Querying.Formatters;

internal class DaxIntervalFormatter : IValueFormatter<TimeSpan>
{
    public string Format(TimeSpan value)
    {
        var sb = new StringBuilder();
        sb.Append('\"');
        if (value.TotalDays >= 1)
            sb.Append(value.Days).Append(" DAYS ");
        if (value.TotalHours >= 1)
            sb.Append(value.Hours).Append(" HOURS ");
        if (value.TotalMinutes >= 1)
            sb.Append(value.Minutes).Append(" MINUTES ");
        if (value.TotalSeconds >= 1)
            sb.Append(value.Seconds).Append(" SECONDS");
        if (value.Milliseconds > 0)
            sb.Append(' ').Append(value.Milliseconds).Append(" MILLISECONDS");
#if NET7_0_OR_GREATER
        if (value.Microseconds > 0)
            sb.Append(' ').Append(value.Microseconds).Append(" MICROSECONDS");
#endif
        sb.Append('\"');
        return sb.ToString();
    }

    public string Format(object obj)
        => obj is TimeSpan value ? Format(value) : throw new Exception();
}
