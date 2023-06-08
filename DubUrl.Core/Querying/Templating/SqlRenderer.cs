using Antlr4.StringTemplate;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating
{
    internal class SqlRenderer : StringRenderer
    {
        public override string ToString(object obj, string formatString, CultureInfo culture)
            => obj switch
            {
                null => FormatNull(formatString, culture),
                string str => FormatString(str, formatString, culture),
                bool boolean => FormatBoolean(boolean, formatString, culture),
                DateTime dt => FormatDateTime(dt, formatString, culture),
                DateOnly date => FormatDateOnly(date, formatString, culture),
                TimeOnly time => FormatTimeOnly(time, formatString, culture),
                TimeSpan ts => FormatTimeSpan(ts, formatString, culture),
                _ => FormatNumber(obj, formatString, culture)
            };

        protected virtual string FormatNull(string formatString, CultureInfo culture)
            => "NULL";

        protected virtual string FormatString(string str, string formatString, CultureInfo culture)
            => formatString switch
            {
                "value" => $"'{str}'",
                "identifier" => $"[{str}]",
                _ => base.ToString(str, formatString, culture)
            };

        protected virtual string FormatBoolean(bool boolean, string formatString, CultureInfo culture)
            => formatString switch
            {
                "value" => boolean ? "TRUE" : "FALSE",
                _ => base.ToString(boolean, formatString, culture)
            };

        protected virtual string FormatDateTime(DateTime dt, string formatString, CultureInfo culture)
            => formatString switch
            {
                "value" => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
                _ => new DateRenderer().ToString(dt, formatString, culture)
            };

        protected virtual string FormatDateOnly(DateOnly date, string formatString, CultureInfo culture)
            => formatString switch
            {
                "value" => $"'{date:yyyy-MM-dd}'",
                _ => new DateRenderer().ToString(date, formatString, culture)
            };

        protected virtual string FormatTimeOnly(TimeOnly time, string formatString, CultureInfo culture)
            => formatString switch
            {
                "value" => $"'{time:HH:mm:ss}'",
                _ => new DateRenderer().ToString(time, formatString, culture)
            };

        protected virtual string FormatTimeSpan(TimeSpan ts, string formatString, CultureInfo culture)
            => formatString switch
            {
                "value" => $"INTERVAL '{ts.Days} days {ts.Hours} hours {ts.Minutes} minutes {ts.Seconds} seconds'",
                _ => new DateRenderer().ToString(ts, formatString, culture)
            };

        protected virtual string FormatNumber(object number, string formatString, CultureInfo culture)
            => formatString switch
            {
                "value" => Convert.ToString(number, CultureInfo.InvariantCulture)!,
                _ => number.ToString()!
            };
    }
}
