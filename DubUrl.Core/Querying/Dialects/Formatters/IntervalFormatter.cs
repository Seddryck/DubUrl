using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class IntervalFormatter : IValueFormatter<TimeSpan>
    {
        public string Format(TimeSpan value)
        {
            var sb = new StringBuilder();
            sb.Append("INTERVAL ");
            sb.Append('\'');
            if (value.TotalDays >= 1)
                sb.Append(value.Days).Append(" DAYS ");
            if (value.TotalHours >= 1)
                sb.Append(value.Hours).Append(" HOURS ");
            if (value.TotalMinutes >= 1)
                sb.Append(value.Minutes).Append(" MINUTES ");
            if (value.TotalSeconds >= 1)
                sb.Append(value.Seconds).Append(" SECONDS");
            sb.Append('\'');
            return sb.ToString();
        }
        public string Format(object obj)
            => obj is TimeSpan value ? Format(value) : throw new Exception();
    }
}
