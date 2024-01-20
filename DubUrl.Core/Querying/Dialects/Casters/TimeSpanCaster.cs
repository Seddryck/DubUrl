using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters;

internal class TimeSpanCaster<T> : BaseCaster<T, TimeSpan>
{
    public TimeSpanCaster()
        : base("FromTimeSpan", [typeof(TimeSpan)]) { }

    public override T? ThrowCastException(object value)
        => throw new ArgumentOutOfRangeException($"Cannot cast returned value to type '{typeof(T).Name}' by truncating the TimeSpan '{value}' because we can't find a method named {MethodName} accepting {MethodTypeArgs.Length} parameter{(MethodTypeArgs.Length > 1 ? "s" : "")}.");

    public override T? Cast(TimeSpan value)
        => TryTruncate(value, out var result)
                ? result
                : ThrowCastException(value);

    protected bool TryTruncate(TimeSpan value, out T? result)
    {
        var parse = GetMethod();
        result = (T?)(parse?.Invoke(null, new object[] { value }));
        return parse is not null;
    }
}
