using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters
{
    internal class Converter<T> : ICaster<T, object>
    {
        public virtual T? ThrowCastException(object value)
            => throw new ArgumentOutOfRangeException($"Cannot cast returned value to type '{typeof(T).Name}' by converting from the type '{value.GetType().Name}' because we can't find an implicit or explicit operator in this type.");

        public virtual T? Cast(object value)
            => HasImplicitConversion(value!.GetType(), typeof(T)) || HasExplicitConversion(value!.GetType(), typeof(T))
                    ? (T?)(dynamic?)value
                    : ThrowCastException(value);

        //https://stackoverflow.com/questions/32025201/how-can-i-determine-if-an-implicit-cast-exists-in-c
        public static bool HasImplicitConversion(Type baseType, Type targetType)
        {
            return baseType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(mi => mi.Name == "op_Implicit" && mi.ReturnType == targetType)
                .Any(mi => {
                    var pi = mi.GetParameters().FirstOrDefault();
                    return pi != null && pi.ParameterType == baseType;
                });
        }

        public static bool HasExplicitConversion(Type baseType, Type targetType)
        {
            return baseType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(mi => mi.Name == "op_Explicit" && mi.ReturnType == targetType)
                .Any(mi => {
                    var pi = mi.GetParameters().FirstOrDefault();
                    return pi != null && pi.ParameterType == baseType;
                });
        }

        public virtual bool CanCast(Type from, Type to)
            => typeof(T).Equals(to);

        object? ICaster.Cast(object value) => Cast(value);
    }
}
