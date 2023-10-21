using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    public class NumberFormatter : IValueFormatter<object>
    {
        public string Format(object value)
            => Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }
}
