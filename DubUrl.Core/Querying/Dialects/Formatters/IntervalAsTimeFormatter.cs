using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class IntervalAsTimeFormatter : IValueFormatter<TimeSpan>
    {
        public string Format(TimeSpan value)
            => $"'{Math.Floor(value.TotalHours):00}:{value.Minutes:00}:{value.Seconds:00}'";

        public string Format(object obj)
            => obj is TimeSpan value ? Format(value) : throw new Exception();
    }
}
