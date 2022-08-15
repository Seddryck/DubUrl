using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class DriverAttribute : Attribute
    {
        public string DatabaseName { get; protected set; } = string.Empty;
        public string[] Aliases { get; protected set; } = Array.Empty<string>();
        public string NamePattern { get; protected set; } = string.Empty;
        public Type[] Options { get; protected set; } = Array.Empty<Type>();
        public int ListingPriority { get; protected set; } = 5;

        protected DriverAttribute() { }

        public DriverAttribute(string databaseName, string[] aliases, string namePattern, Type[]? options = null, int listingPriority = 5)
            => (DatabaseName, Aliases, NamePattern, Options, ListingPriority) 
                = (databaseName, aliases, namePattern, options ?? Array.Empty<Type>(), listingPriority);       
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class DriverAttribute<R> : DriverAttribute where R : IDriverRegex
    {
        public DriverAttribute(string databaseName, string[] aliases, Type[]? options = null, int listingPriority = 5)
            : base(
                  databaseName
                  , aliases
                  , Activator.CreateInstance<R>().ToString()
                  , options ?? Array.Empty<Type>()
                  , listingPriority
            )
        { }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class DriverAttribute<R, M> : DriverAttribute
        where R : IDriverRegex
        where M : BaseMapper
    {
        public DriverAttribute()
            : base()
        {
            var attr = ReadMapperAttribute();
            var regex = Activator.CreateInstance<R>();
            DatabaseName = attr.DatabaseName;
            Aliases = attr.Aliases;
            NamePattern = regex.ToString();
            Options = regex.Options;
            ListingPriority = attr.ListingPriority;
        }

        private static BaseMapperAttribute ReadMapperAttribute()
            => (BaseMapperAttribute)typeof(M).GetCustomAttributes(typeof(BaseMapperAttribute), false)[0];
    }
}
