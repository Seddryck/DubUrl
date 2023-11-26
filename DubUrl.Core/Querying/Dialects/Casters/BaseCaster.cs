using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters;

public abstract class BaseCaster<T, U> : ICaster<T, U>
{
    protected string MethodName { get; }
    protected Type[] MethodTypeArgs { get; }

    public BaseCaster(string methodName, Type[] methodTypeArgs)
        => (MethodName, MethodTypeArgs) = (methodName, methodTypeArgs);

    public abstract T? ThrowCastException(object value);

    public virtual bool CanCast(Type from, Type to)
        => typeof(T).Equals(to) && typeof(U).Equals(from);

    public MethodInfo? GetMethod()
      => typeof(T).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .FirstOrDefault(c => c.Name == MethodName
                                    && c.GetParameters().Length == MethodTypeArgs.Length
                                    && MethodTypeArgs.Select((a, i) => new { Value = a, Index = i })
                                                 .All(x => c.GetParameters()[x.Index].ParameterType == x.Value)
                );
    public abstract T? Cast(U value);

    public object? Cast(object value)
        => value is U v ? Cast(v) : ThrowCastException(value);
}
