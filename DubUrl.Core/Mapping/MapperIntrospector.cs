using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public class MapperIntrospector
    {
        public record struct MapperInfo(Type MapperType, string DatabaseName, string[] Aliases, string ProviderInvariantName, int ListingPriority) { }

        public IEnumerable<MapperInfo> Locate()
            => typeof(SchemeMapperBuilder).Assembly
                    .GetTypes()
                    .Where(x => x.IsClass
                        && x.GetCustomAttributes(typeof(MapperAttribute), false).Length > 0
                        && x.GetCustomAttributes(typeof(AlternativeMapperAttribute), false).Length == 0)
                    .Select(x => (Type: x, Attribute: x.GetCustomAttribute<MapperAttribute>() ?? throw new InvalidOperationException()))
                    .Select(x => new MapperInfo(
                        x.Type,
                        x.Attribute.DatabaseName,
                        x.Attribute.Aliases, 
                        x.Attribute.ProviderInvariantName,
                        x.Attribute.ListingPriority
                   ));
    }
}
