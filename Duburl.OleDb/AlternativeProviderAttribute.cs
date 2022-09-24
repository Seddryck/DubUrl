using DubUrl.OleDb;
using DubUrl.Locating;
using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting;

namespace DubUrl.OleDb
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AlternativeProviderAttribute : BaseLocatorAttribute
    {
        public AlternativeProviderAttribute(IProviderRegex providerRegex, Type mapper, Type database)
            : base(providerRegex.ToString(), providerRegex.Options, mapper, database) { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class AlternativeProviderAttribute<R, O, D> : AlternativeProviderAttribute
        where R : IProviderRegex
        where O : IOleDbMapper
        where D : IDatabase
    {
        public AlternativeProviderAttribute()
            : base(
                  Activator.CreateInstance<R>()
                  , typeof(O)
                  , typeof(D)
            )
        { }
    }
}
