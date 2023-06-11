using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class IntervalTimeFormatter : IValueFormatter<TimeSpan>
    {
        public string Format(TimeSpan value)
        {
            var sb = new StringBuilder();
            sb.Append('\'');
            if (value.TotalDays >= 1)
                throw new ArgumentOutOfRangeException();
            sb.AppendFormat("00", value.Hours).Append(':');
            sb.AppendFormat("00", value.Minutes).Append(':');
            sb.AppendFormat("00", value.Seconds);
            sb.Append('\'');
            return sb.ToString();
        }

        public string Format(object obj)
            => obj is TimeSpan value ? Format(value) : throw new Exception();
    }
}
