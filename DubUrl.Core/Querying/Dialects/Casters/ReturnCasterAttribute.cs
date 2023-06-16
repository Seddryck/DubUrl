using DubUrl.Querying.Dialects.Renderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters
{
    public class ReturnCasterAttribute : Attribute
    {
        public virtual Type CasterType { get; protected set; }

        public ReturnCasterAttribute(Type casterType)
        {
            CasterType = casterType;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    sealed public class ReturnCasterAttribute<R> : ReturnCasterAttribute where R : ICaster
    {
        public ReturnCasterAttribute()
            : base(typeof(R))
        { }
    }
}
