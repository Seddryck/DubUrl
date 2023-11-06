using DubUrl.OleDb;
using DubUrl.Locating;
using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting;
using Duburl.OleDb.Mapping;

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
                P.GetCustomAttribute<ProviderAttribute>()?.RegexPattern ?? throw new NullReferenceException($"Missing attribute 'Provider' for type '{P.Name}'.")
                , P.GetCustomAttribute<ProviderAttribute>()?.Options ?? throw new NullReferenceException($"Missing attribute 'Provider' for type '{P.Name}'.")
                , P.GetCustomAttribute<ProviderAttribute>()?.Mapper ?? throw new NullReferenceException($"Missing attribute 'Provider' for type '{P.Name}'.")
                , P.GetCustomAttribute<ProviderAttribute>()?.Database ?? throw new NullReferenceException($"Missing attribute 'Provider' for type '{P.Name}'.")
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
