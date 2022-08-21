using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ProviderAttribute : LocatorAttribute
    {
        public ProviderAttribute(IProviderRegex providerRegex, Type mapper, Type database)
            : base(providerRegex.ToString(), providerRegex.Options, mapper, database) { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ProviderAttribute<R, O, D> : ProviderAttribute
        where R : IProviderRegex
        where O : IOleDbMapper
        where D : IDatabase
    {
        public ProviderAttribute()
            : base(
                  Activator.CreateInstance<R>()
                  , typeof(O)
                  , typeof(D)
            )
        { }
    }
}
