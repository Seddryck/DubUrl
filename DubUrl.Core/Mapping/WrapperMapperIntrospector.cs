using DubUrl.Locating;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public abstract class MissingElementException : DubUrlException
    {
        protected MissingElementException(string elementName, string attributeName, Type locatorType, Type elementType, IEnumerable<Type> types)
            : this(elementName, elementName + 's', attributeName, locatorType, elementType, types) { }

        protected MissingElementException(string elementName, string elementNamePlural, string attributeName, Type locatorType, Type elementType, IEnumerable<Type> types)
            : base(
                  $"No {elementName} found for the {attributeName} '{locatorType.Name}'. The framework was looking for the {elementName} '{elementType.Name}' but the list of potential {elementNamePlural} is {(types.Any() ? "\'" + string.Join("\', \'", types.Select(x => x.Name)) + "\'" : "empty")}."
            )
        { }
    }

    public class MissingMapperException : MissingElementException
    {
        public MissingMapperException(Type locatorType, Type elementType, IEnumerable<Type> types)
            : base("mapper", "locator", locatorType, elementType, types) { }
    }

    public class MissingDatabaseException : MissingElementException
    {
        public MissingDatabaseException(Type locatorType, Type elementType, IEnumerable<Type> types)
            : base("database", "locator", locatorType, elementType, types) { }
    }

    public class MissingConnectivityException : MissingElementException
    {
        public MissingConnectivityException(Type locatorType, Type elementType, IEnumerable<Type> types)
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

        public override MapperInfo[] Locate()
            => LocateMappers().ToArray();

        protected virtual IEnumerable<MapperInfo> LocateMappers()
        {
            var mappers = LocateAttribute<WrapperMapperAttribute>();
            var databases = LocateAttribute<DatabaseAttribute>();
            var connectivities = LocateAttribute<WrapperConnectivityAttribute>();
            var locators = LocateAttribute<LocatorAttribute>();
            var brands = LocateAttribute<BrandAttribute>();

            foreach (var locator in locators)
            {
                var mapper = mappers.SingleOrDefault(x => x.Type == locator.Attribute.Mapper)
                    ?? throw new MissingMapperException(locator.Type, locator.Attribute.Mapper, mappers.Select(x => x.Type));

                var database = databases.SingleOrDefault(x => x.Type == locator.Attribute.Database)
                    ?? throw new MissingDatabaseException(locator.Type, locator.Attribute.Database, databases.Select(x => x.Type));

                var connectivity = connectivities.SingleOrDefault(x => x.Type == mapper.Attribute.Connectivity)
                    ?? throw new MissingConnectivityException(mapper.Type, mapper.Attribute.Connectivity, connectivities.Select(x => x.Type));

                var connectivityInstance = Activator.CreateInstance(connectivity.Type) as IWrapperConnectivity
                    ?? throw new InvalidCastException();

                var brand = brands.SingleOrDefault(x => x.Type == locator.Attribute.Database);

                yield return new MapperInfo(
                        mapper.Type
                        , $"{connectivity.Attribute.ConnectivityName} for {database.Attribute.DatabaseName}"
                        , connectivityInstance.DefineAliases(connectivity.Attribute, database.Attribute, locator.Attribute).ToArray()
                        , database.Attribute.DialectType
                        , database.Attribute.ListingPriority
                        , mapper.Attribute.ProviderInvariantName
                        , mapper.Attribute.Parametrizer
                        , brand?.Attribute.Slug ?? string.Empty
                        , brand?.Attribute.MainColor ?? BrandAttribute.DefaultMainColor
                        , brand?.Attribute.SecondaryColor ?? BrandAttribute.DefaultSecondaryColor
                    );
            }
        }
    }
}
