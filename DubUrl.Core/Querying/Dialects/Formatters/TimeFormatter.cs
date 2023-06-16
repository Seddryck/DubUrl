using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class TimeFormatter : IValueFormatter<TimeOnly>
    {
        public string Format(TimeOnly value)
            => $"'{value:HH:mm:ss}'";

        public string Format(object obj)
            => obj is TimeOnly value ? Format(value) : throw new Exception();
    }
}
