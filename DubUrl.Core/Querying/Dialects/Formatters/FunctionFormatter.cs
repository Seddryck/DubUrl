using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    internal class FunctionFormatter<T> : IValueFormatter<T>
    {
        protected string FunctionName { get; }
        protected IValueFormatter<T> Formatter { get; }

        public FunctionFormatter(string functionName, IValueFormatter<T> formatter)
            => (FunctionName, Formatter) = (functionName, formatter);

        public string Format(T obj)
            => $"{FunctionName}({Formatter.Format(obj)})";
        public string Format(object obj)
            => obj is T value ? Format(value) : throw new Exception();
    }
}
