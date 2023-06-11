using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    public interface IFormatter
    {
        string Format(object obj);
    }

    public interface IFormatter<T> : IFormatter
    {
        string Format(T obj);
    }

    public interface IValueFormatter : IFormatter
    { }

    public interface IValueFormatter<T> : IFormatter<T>, IValueFormatter
    { }

    public interface IIdentifierFormatter : IFormatter<string>
    { }

    public interface INullFormatter
    {
        string Format();
    }
}