using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public abstract class NoElementFoundException : DubUrlException
    {
        protected NoElementFoundException(string elementName, string attributeName, Type locatorType, Type elementType, IEnumerable<Type> types)
            : this(elementName, elementName + 's', attributeName, locatorType, elementType, types) { }

        protected NoElementFoundException(string elementName, string elementNamePlural, string attributeName, Type locatorType, Type elementType, IEnumerable<Type> types)
            : base(
                  $"No {elementName} found for the {attributeName} '{locatorType.Name}'. The framework was looking for the {elementName} '{elementType.Name}' but the list of potential {elementNamePlural} is {(types.Any() ? "\'" + string.Join("\', \'", types.Select(x => x.Name)) + "\'" : "empty")}."
            )
        { }
    }

    public class NoMapperFoundException : NoElementFoundException
    {
        public NoMapperFoundException(Type locatorType, Type elementType, IEnumerable<Type> types)
            : base("mapper", "locator", locatorType, elementType, types) { }
    }

    public class NoDatabaseFoundException : NoElementFoundException
    {
        public NoDatabaseFoundException(Type locatorType, Type elementType, IEnumerable<Type> types)
            : base("database", "locator", locatorType, elementType, types) { }
    }

    public class NoConnectivityFoundException : NoElementFoundException
    {
        public NoConnectivityFoundException(Type locatorType, Type elementType, IEnumerable<Type> types)
            : base("connectivity", "connectivities", "mapper", locatorType, elementType, types) { }
    }

    public class WrapperMapperIntrospector : BaseMapperIntrospector
    {
        public WrapperMapperIntrospector()
            : this(new AssemblyTypesProbe()) { }
        public WrapperMapperIntrospector(Assembly[] assemblies)
            : this(new AssemblyTypesProbe(assemblies.Distinct().ToArray())) { }
        public WrapperMapperIntrospector(ITypesProbe probe)
            : base(probe) { }

        public override IEnumerable<MapperInfo> Locate()
        {
            var mappers = LocateAttribute<WrapperMapperAttribute>();
            var databases = LocateAttribute<DatabaseAttribute>();
            var connectivities = LocateAttribute<WrapperConnectivityAttribute>();
            var locators = LocateAttribute<LocatorAttribute>();

            foreach (var locator in locators)
            {
                var mapper = mappers.SingleOrDefault(x => x.Type == locator.Attribute.Mapper)
                    ?? throw new NoMapperFoundException(locator.Type, locator.Attribute.Mapper, mappers.Select(x => x.Type));

                var database = databases.SingleOrDefault(x => x.Type == locator.Attribute.Database)
                    ?? throw new NoDatabaseFoundException(locator.Type, locator.Attribute.Database, databases.Select(x => x.Type));

                var connectivity = connectivities.SingleOrDefault(x => x.Type == mapper.Attribute.Connectivity)
                    ?? throw new NoConnectivityFoundException(mapper.Type, mapper.Attribute.Connectivity, connectivities.Select(x => x.Type));

                var connectivityInstance = Activator.CreateInstance(connectivity.Type) as IWrapperConnectivity
                    ?? throw new InvalidCastException();

                yield return new MapperInfo(
                        mapper.Type
                        , $"{connectivity.Attribute.ConnectivityName} for {database.Attribute.DatabaseName}"
                        , connectivityInstance.DefineAliases(connectivity.Attribute, database.Attribute, locator.Attribute).ToArray()
                        , database.Attribute.DialectType
                        , database.Attribute.ListingPriority
                        , mapper.Attribute.ProviderInvariantName
                        , mapper.Attribute.Parametrizer
                    );
            }
        }

        //private static IEnumerable<string> CartesianProduct(string[] firstArray, string[] secondArray)
        //{
        //    foreach (var item1 in firstArray)
        //        foreach (var item2 in secondArray)
        //            yield return $"{item1}+{item2}";
        //}
    }
}
