using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.Options;

[AttributeUsage(AttributeTargets.Enum, Inherited = true, AllowMultiple = false)]
sealed class LocatorOptionAttribute : Attribute
{
    public LocatorOptionAttribute()
    { }
}
