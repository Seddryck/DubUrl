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
        public record struct DriverLocatorInfo(Type DriverLocatorType, string DatabaseName, string[] Aliases, string NamePattern, int ListingPriority, Type[] Options, string Slug, string MainColor, string SecondaryColor) { }

        public DriverLocatorIntrospector()
            : this(new AssemblyTypesProbe()) { }

        internal DriverLocatorIntrospector(AssemblyTypesProbe introspector)
            : base(introspector) { }

        public DriverLocatorInfo[] Locate()
            => LocateDrivers().ToArray();

        protected virtual IEnumerable<DriverLocatorInfo> LocateDrivers()
        {
            var databases = LocateAttribute<DatabaseAttribute>();
            var drivers = LocateAttribute<DriverAttribute>();
            var brands = LocateAttribute<BrandAttribute>();

            foreach (var driver in drivers)
            {
                var db = databases.Single(x => x.Type == driver.Attribute.Database);
                var brand = brands.SingleOrDefault(x => x.Type == driver.Attribute.Database);

                yield return new DriverLocatorInfo(
                        driver.Type
                        , db.Attribute.DatabaseName
                        , db.Attribute.Aliases
                        , driver.Attribute.RegexPattern
                        , db.Attribute.ListingPriority
                        , driver.Attribute.Options
                        , brand?.Attribute.Slug ?? string.Empty
                        , brand?.Attribute.MainColor ?? BrandAttribute.DefaultMainColor
                        , brand?.Attribute.SecondaryColor ?? BrandAttribute.DefaultSecondaryColor
                    );
            }
        }
    }
}
