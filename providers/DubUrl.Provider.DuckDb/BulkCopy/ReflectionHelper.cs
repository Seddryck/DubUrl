using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy.Engines;
internal class ReflectionHelper
{
    public static MethodInfo? GetCaster(Type casterType, Type sourceType, Type targetType)
    {
        return casterType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "op_Implicit" || m.Name == "op_Explicit")  // Find explicit operators
            .FirstOrDefault(m =>
            {
                ParameterInfo[] parameters = m.GetParameters();
                return parameters.Length == 1 &&
                       parameters[0].ParameterType == sourceType &&
                       m.ReturnType == targetType;
            });
    }
}
