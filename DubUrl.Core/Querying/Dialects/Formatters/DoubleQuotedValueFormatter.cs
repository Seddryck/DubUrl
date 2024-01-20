using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

public class DoubleQuotedValueFormatter : IValueFormatter<string>, IValueFormatter<char>
{
    public string Format(string value)
        => $"\"{value}\"";
    public string Format(char value)
        => $"\"{value}\"";
    public string Format(object obj)
        => obj switch
        {
            char c => Format(c),
            string str => Format(str),
            _ => throw new Exception()
        };
}
