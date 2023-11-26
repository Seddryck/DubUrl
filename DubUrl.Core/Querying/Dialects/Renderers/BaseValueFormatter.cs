using DubUrl.Querying.Dialects.Formatters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers;

public abstract class BaseValueFormatter
{
    protected IDictionary<Type, IValueFormatter> TypeFormatters { get; } = new Dictionary<Type, IValueFormatter>();

    public IReadOnlyDictionary<Type, IValueFormatter> Values
    {
        get => new ReadOnlyDictionary<Type, IValueFormatter>(TypeFormatters);
    }

    public BaseValueFormatter With<T>(IValueFormatter<T> formatter)
    {
        if (TypeFormatters.ContainsKey(typeof(T)))
            TypeFormatters[typeof(T)] = formatter;
        else
            TypeFormatters.Add(typeof(T), formatter);

        return this;
    }
}
