using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Querying.Mdx.Formatters;

internal class MdxDateFormatter : IValueFormatter<DateOnly>
{
    public string Format(DateOnly value)
        => $"\"{value:yyyy-MM-dd}\"";
    public string Format(object obj)
        => obj is DateOnly value ? Format(value) : throw new Exception();
}
