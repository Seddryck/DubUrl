using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Adomd.Querying.Formatters;

namespace DubUrl.Adomd.Querying;

internal class DaxValueFormatter : BaseValueFormatter
{
    public DaxValueFormatter()
    {
        With(new BooleanFormatter());
        With(new GuidFormatter());
        With<string>(new DoubleQuotedValueFormatter());
        With<char>(new DoubleQuotedValueFormatter());
        With<char>(new DoubleQuotedValueFormatter());
        With(new FunctionFormatter<DateOnly>("VALUE", new DaxDateFormatter()));
        With(new FunctionFormatter<TimeOnly>("VALUE", new DaxTimeFormatter()));
        With(new FunctionFormatter<DateTime>("VALUE", new DaxTimestampFormatter()));
        With(new FunctionFormatter<DateTimeOffset>("VALUE", new DaxTimestampTimeZoneFormatter()));
        With(new FunctionFormatter<TimeSpan>("VALUE", new DaxIntervalFormatter()));
        var numericTypes = new Type[] {
            typeof(byte), typeof(short), typeof(int), typeof(long)
            , typeof(float), typeof(double)
            , typeof(decimal)
        };
        foreach (var num in numericTypes)
            TypeFormatters.Add(num, new NumberFormatter());
    }
}
