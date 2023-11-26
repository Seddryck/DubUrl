using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers;

public class ValueFormatter : BaseValueFormatter
{
    public ValueFormatter()
    {
        With(new BooleanFormatter());
        With(new SimpleQuotedValueFormatter());
        With(new PrefixFormatter<DateOnly>("DATE", new DateFormatter()));
        With(new PrefixFormatter<TimeOnly>("TIME", new TimeFormatter()));
        With(new PrefixFormatter<DateTime>("TIMESTAMP", new TimestampFormatter()));
        With(new PrefixFormatter<DateTimeOffset>("TIMESTAMPTZ", new TimestampTimeZoneFormatter()));
        With(new PrefixFormatter<TimeSpan>("INTERVAL", new IntervalFormatter()));
        var numericTypes = new Type[] {
            typeof(byte), typeof(short), typeof(int), typeof(long)
            , typeof(float), typeof(double)
            , typeof(decimal)
        };
        foreach (var num in numericTypes)
            TypeFormatters.Add(num, new NumberFormatter());
    }
}
