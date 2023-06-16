using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class IntervalTrinoFormatter : IValueFormatter<TimeSpan>
    {
        public string Format(TimeSpan value)
        {
            var sb = new StringBuilder();
            if (value.Days >= 1)
                sb.Append("INTERVAL \'").Append(value.Days).Append("\' DAY");
            if (value.Hours >= 1)
                sb.Append(" + INTERVAL \'", Convert.ToInt16(sb.Length == 0) * 3, 13 - Convert.ToInt16(sb.Length == 0) * 3).Append(value.Hours).Append("\' HOUR");
            if (value.Minutes >= 1)                                               
                sb.Append(" + INTERVAL \'", Convert.ToInt16(sb.Length == 0) * 3, 13 - Convert.ToInt16(sb.Length == 0) * 3).Append(value.Minutes).Append("\' MINUTE");
            if (value.Seconds >= 1)                                               
                sb.Append(" + INTERVAL \'", Convert.ToInt16(sb.Length == 0) * 3, 13 - Convert.ToInt16(sb.Length == 0) * 3).Append(value.Seconds).Append("\' SECOND");
            return sb.ToString().TrimEnd();
        }
        public string Format(object obj)
            => obj is TimeSpan value ? Format(value) : throw new Exception();
    }
}
