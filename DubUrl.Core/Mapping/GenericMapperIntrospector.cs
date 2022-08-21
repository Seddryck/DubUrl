using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.OleDbProvider;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public class GenericMapperIntrospector : BaseMapperIntrospector
    {
        public GenericMapperIntrospector()
            : this(new AssemblyClassesIntrospector()) { }
        internal GenericMapperIntrospector(AssemblyClassesIntrospector introspector)
            : base(introspector) { }

        public override IEnumerable<MapperInfo> Locate()
        {
            var mappers = LocateAttribute<GenericMapperAttribute>();
            var databases = LocateAttribute<DatabaseAttribute>();
            var connectivities = LocateAttribute<GenericConnectivityAttribute>();
            var locators = LocateAttribute<LocatorAttribute>();

            foreach (var locator in locators)
            {
                var mapper = mappers.Single(x => x.Type == locator.Attribute.Mapper);
                var database = databases.Single(x => x.Type == locator.Attribute.Database);
                var connectivity = connectivities.Single(x => x.Type == mapper.Attribute.Database);
                yield return new MapperInfo(
                        mapper.Type
                        , $"{connectivity.Attribute.ConnectivityName} for {database.Attribute.DatabaseName}"
                        , CartesianProduct(connectivity.Attribute.Aliases, database.Attribute.Aliases).ToArray()
                        , database.Attribute.DialectType
                        , database.Attribute.ListingPriority
                        , mapper.Attribute.ProviderInvariantName
                    );
            }
        }

        private static IEnumerable<string> CartesianProduct(string[] firstArray, string[] secondArray)
        {
            foreach (var item1 in firstArray)
                foreach (var item2 in secondArray)
                    yield return $"{item1}+{item2}";
        }
    }
}
