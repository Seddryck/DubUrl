using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

internal class DateCrossSurroundingFormatter : IValueFormatter<DateOnly>
{
    public string Format(DateOnly value)
        => $"#{value:MM/dd/yyyy}#";
    public string Format(object obj)
        => obj is DateOnly value ? Format(value) : throw new Exception();
}
