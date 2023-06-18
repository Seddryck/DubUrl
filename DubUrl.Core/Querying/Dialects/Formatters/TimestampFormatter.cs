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
#if NET7_0_OR_GREATER
            => $"'{value:yyyy-MM-dd HH:mm:ss.FFFFFF}'";
#else
            => $"'{value:yyyy-MM-dd HH:mm:ss.FFF}'";
#endif

        public string Format(object obj)
            => obj is DateTime value ? Format(value) : throw new Exception();
    }
}
