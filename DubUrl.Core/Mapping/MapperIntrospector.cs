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

        protected MapperClassIntrospector MapperClassIntrospectorInstance { get; } = new();

        public MapperIntrospector() {}
        internal MapperIntrospector(MapperClassIntrospector introspector)
            => MapperClassIntrospectorInstance = introspector;

        public IEnumerable<MapperInfo> Locate()
            => Locate<MapperAttribute>();

        public IEnumerable<MapperInfo> LocateAlternative()
            => Locate<AlternativeMapperAttribute>();

        public IEnumerable<MapperInfo> Locate<T>() where T : BaseMapperAttribute
            => MapperClassIntrospectorInstance.LocateClass<T>()
                    .Where(
                        x => x.IsClass
                        && x.GetCustomAttributes(typeof(T), false).Length > 0
                    )
                    .Select(x => (Type: x, Attribute: x.GetCustomAttribute<T>() ?? throw new InvalidOperationException()))
                    .Select(x => new MapperInfo(
                        x.Type,
                        x.Attribute.DatabaseName,
                        x.Attribute.Aliases,
                        x.Attribute.ProviderInvariantName,
                        x.Attribute.ListingPriority
                   ));

        public class MapperClassIntrospector
        {
            public virtual IEnumerable<Type> LocateClass<T>() where T : BaseMapperAttribute
                => typeof(SchemeMapperBuilder).Assembly.GetTypes();
        }
    }
}
