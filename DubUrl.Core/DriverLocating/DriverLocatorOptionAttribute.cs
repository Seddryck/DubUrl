using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.DriverLocating
{
    [AttributeUsage(AttributeTargets.Enum, Inherited = true, AllowMultiple = false)]
    sealed class DriverLocatorOptionAttribute : Attribute
    {
        public DriverLocatorOptionAttribute()
        {}
    }
}
