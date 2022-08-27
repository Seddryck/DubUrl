using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    public class DriverLocatorIntrospector : BaseIntrospector
    {
        public record struct DriverLocatorInfo(Type DriverLocatorType, string DatabaseName, string[] Aliases, string NamePattern, int ListingPriority, Type[] Options) { }

        public DriverLocatorIntrospector()
            : this(new AssemblyTypesProbe()) { }

        internal DriverLocatorIntrospector(AssemblyTypesProbe introspector)
            : base(introspector) { }

        public IEnumerable<DriverLocatorInfo> Locate()
        {
            var databases = LocateAttribute<DatabaseAttribute>();
            var drivers = LocateAttribute<DriverAttribute>();

            foreach (var driver in drivers)
            {
                var db = databases.Single(x => x.Type == driver.Attribute.Database);
                yield return new DriverLocatorInfo(
                        driver.Type
                        , db.Attribute.DatabaseName
                        , db.Attribute.Aliases
                        , driver.Attribute.RegexPattern
                        , db.Attribute.ListingPriority
                        , driver.Attribute.Options
                    );
            }
        }
    }
}
