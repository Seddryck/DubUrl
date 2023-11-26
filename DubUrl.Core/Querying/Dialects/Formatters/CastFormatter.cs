using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

internal class CastFormatter<T> : IValueFormatter<T>
{
    protected string TypeName { get; }
    protected IValueFormatter<T> Formatter { get; }

    public CastFormatter(string typeName, IValueFormatter<T> formatter)
        => (TypeName, Formatter) = (typeName, formatter);

    public string Format(T obj)
        => $"CAST ({Formatter.Format(obj)} AS {TypeName})";

    public string Format(object obj)
        => obj is T value ? Format(value) : throw new Exception();
}
