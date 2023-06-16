using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters
{
    internal class NumericParser<T> : BaseCaster<T, string>
    {
        public NumericParser()
            : base("Parse", new[] { typeof(string), typeof(IFormatProvider) }) { }

        public override T? ThrowCastException(object value)
            => throw new ArgumentOutOfRangeException($"Cannot cast returned value to type '{typeof(T).Name}' by parsing the string '{value}' because we can't find a method named {MethodName} accepting {MethodTypeArgs.Length} parameter{(MethodTypeArgs.Length>1?"s":"")}.");

        public override T? Cast(string value)
            => TryParse(value, out var result)
                    ? result
                    : ThrowCastException(value);
        protected virtual bool TryParse(string value, out T? result)
        {
#if NET7_0_OR_GREATER
            if (!(typeof(T).GetInterfaces().Any(c => c.IsGenericType && c.GetGenericTypeDefinition() == typeof(IParsable<>))))
                return false;
#endif
            var parse = GetMethod();
            result = (T?)(parse?.Invoke(null, new object?[] { value, CultureInfo.InvariantCulture.NumberFormat }));
            return parse is not null;
        }
    }
}
