using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class TimestampFormatter : IValueFormatter<DateTime>
    {
        public string Format(DateTime value)
            => $"'{value:yyyy-MM-dd HH:mm:ss}'";

        public string Format(object obj)
            => obj is DateTime value ? Format(value) : throw new Exception();
    }
}
