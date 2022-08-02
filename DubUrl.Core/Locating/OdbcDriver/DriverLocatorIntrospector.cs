using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    public class DriverLocatorIntrospector
    {
        public record struct DriverLocatorInfo(Type DriverLocatorType, string DatabaseName, string[] Aliases, string NamePattern, string[] Options, int ListingPriority) { }

        public IEnumerable<DriverLocatorInfo> Locate()
            => typeof(DriverLocatorFactory).Assembly
                    .GetTypes()
                    .Where(x => x.IsClass
                        && x.GetCustomAttributes(typeof(DriverAttribute), false).Length > 0)
                    .Select(x => (Type: x, Attribute: x.GetCustomAttribute<DriverAttribute>() ?? throw new InvalidOperationException()))
                    .Select(x => new DriverLocatorInfo(
                        x.Type,
                        x.Attribute.DatabaseName,
                        x.Attribute.Aliases,
                        x.Attribute.NamePattern,
                        x.Attribute.Options.Select(t => t.Name).ToArray(),
                        x.Attribute.ListingPriority
                   ));
    }
}
