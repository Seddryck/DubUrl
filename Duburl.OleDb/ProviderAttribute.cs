using DubUrl.OleDb;
using DubUrl.Locating;
using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting;

namespace DubUrl.OleDb
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ProviderAttribute : LocatorAttribute
    {
        protected ProviderAttribute(string regex, Type[] options, Type mapper, Type database, string[]? aliases = null)
            : base(regex, options, mapper, database) { }
        public ProviderAttribute(IProviderRegex providerRegex, Type mapper, Type database, string[]? aliases = null)
            : this(providerRegex.ToString(), providerRegex.Options, mapper, database) { }
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

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ProviderSpecializationAttribute : ProviderAttribute
    {
        public string[] Aliases { get; }

        public ProviderSpecializationAttribute(Type P, string alias)
           : this(P, new[] { alias }) { }

        public ProviderSpecializationAttribute(Type P, string[] aliases)
            : base(
                P.GetCustomAttribute<ProviderAttribute>()?.RegexPattern ?? throw new ArgumentException()
                , P.GetCustomAttribute<ProviderAttribute>()?.Options ?? throw new ArgumentException()
                , P.GetCustomAttribute<ProviderAttribute>()?.Mapper ?? throw new ArgumentException()
                , P.GetCustomAttribute<ProviderAttribute>()?.Database ?? throw new ArgumentException()
            )
        { Aliases = aliases; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ProviderSpecializationAttribute<P> : ProviderSpecializationAttribute
        where P : IProviderLocator
    {
        public ProviderSpecializationAttribute(string alias)
            : base(typeof(P), alias) { }
    }
}
