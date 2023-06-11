using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class SimpleQuotedValueFormatter : IValueFormatter<string>
    {
        public string Format(string value)
            => $"'{value}'";
        public string Format(object obj)
            => obj is string value ? Format(value) : throw new Exception();
    }
}
