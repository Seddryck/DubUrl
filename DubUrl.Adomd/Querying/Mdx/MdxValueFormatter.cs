using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Formatters;
using DubUrl.Adomd.Querying.Mdx.Formatters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Querying.Mdx;

internal class MdxValueFormatter : BaseValueFormatter
{
    public MdxValueFormatter()
    {
        With(new BooleanFormatter());
        With(new GuidFormatter());
        With<string>(new ValueDoubleQuotedFormatter());
        With<char>(new ValueDoubleQuotedFormatter());
        With(new FunctionFormatter<DateOnly>("CDATE", new MdxDateFormatter()));
        With(new FunctionFormatter<TimeOnly>("CDATE", new MdxTimeFormatter()));
        With(new FunctionFormatter<DateTime>("CDATE", new MdxTimestampFormatter()));
        With(new FunctionFormatter<DateTimeOffset>("CDATE", new MdxTimestampTimeZoneFormatter()));
        With(new FunctionFormatter<TimeSpan>("CDATE", new MdxIntervalFormatter()));
        var numericTypes = new Type[] {
            typeof(byte), typeof(short), typeof(int), typeof(long)
            , typeof(float), typeof(double)
            , typeof(decimal)
        };
        foreach (var num in numericTypes)
            TypeFormatters.Add(num, new NumberFormatter());
    }
}
