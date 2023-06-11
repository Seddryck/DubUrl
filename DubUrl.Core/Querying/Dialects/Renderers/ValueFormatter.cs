using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers
{
    internal class ValueFormatter
    {
        protected IDictionary<Type, IValueFormatter> TypeFormatters { get; } = new Dictionary<Type, IValueFormatter>();

        public IReadOnlyDictionary<Type, IValueFormatter> Values
        {
            get => new ReadOnlyDictionary<Type, IValueFormatter>(TypeFormatters);
        }

        public ValueFormatter()
        {
            With(new BooleanFormatter());
            With(new SimpleQuotedValueFormatter());
            With(new DateFormatter());
            With(new TimeFormatter());
            With(new TimestampFormatter());
            With(new IntervalFormatter());
            var numericTypes = new Type[] {
                typeof(byte), typeof(short), typeof(int), typeof(long)
                , typeof(float), typeof(double)
                , typeof(decimal)
            };
            foreach (var num in numericTypes)
                TypeFormatters.Add(num, new NumberFormatter());
        }

        public ValueFormatter With<T>(IValueFormatter<T> formatter)
        {
            if (TypeFormatters.ContainsKey(typeof(T)))
                TypeFormatters[typeof(T)] = formatter;
            else
                TypeFormatters.Add(typeof(T), formatter);

            return this;
        }
    }
}
