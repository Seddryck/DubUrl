using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

internal class IntervalAsTimeFormatter : IValueFormatter<TimeSpan>
{
    public string Format(TimeSpan value)
    {
        var sb = new StringBuilder();
        sb.Append('\'');
        sb.AppendFormat("{0:00}:{1:00}:{2:00}", Math.Floor(value.TotalHours), value.Minutes, value.Seconds);
        if (value.Milliseconds > 0
#if NET7_0_OR_GREATER
                || value.Microseconds > 0
#endif
            )
            sb.Append('.').AppendFormat("{0:000}", value.Milliseconds);
#if NET7_0_OR_GREATER
        if (value.Microseconds > 0)
            sb.AppendFormat("{0:000}", value.Microseconds);
#endif
        sb.Append('\'');
        return sb.ToString();
    }

    public string Format(object obj)
        => obj is TimeSpan value ? Format(value) : throw new Exception();
}
