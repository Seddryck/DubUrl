using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class PrefixFormatter<T> : IValueFormatter<T>
    {
        protected string Prefix { get; }
        protected IValueFormatter<T> Formatter { get; }

        public PrefixFormatter(string prefix, IValueFormatter<T> formatter)
            => (Prefix, Formatter) = (prefix, formatter);

        public string Format(T obj)
            => $"{Prefix} {Formatter.Format(obj)}";
        public string Format(object obj)
            => obj is T value ? Format(value) : throw new Exception();
    }
}
