using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public class NativeMapperIntrospector : BaseMapperIntrospector
    {
        public NativeMapperIntrospector()
            : this(new AssemblyTypesProbe()) { }
        public NativeMapperIntrospector(Assembly[] assemblies)
            : this(new AssemblyTypesProbe(assemblies.Distinct().ToArray())) { }
        public NativeMapperIntrospector(ITypesProbe probe)
            : base(probe) { }

        public override MapperInfo[] Locate()
            => Locate<MapperAttribute>().ToArray();

        public MapperInfo[] LocateAlternative()
            => Locate<AlternativeMapperAttribute>().ToArray();

        protected IEnumerable<MapperInfo> Locate<T>() where T : BaseMapperAttribute
        {
            var mappers = LocateAttribute<T>();
            var databases = LocateAttribute<DatabaseAttribute>();
            var brands = LocateAttribute<BrandAttribute>();

            foreach (var mapper in mappers)
            {
                var db = databases.Single(x => x.Type == mapper.Attribute.Connectivity);
                var brand = brands.SingleOrDefault(x => x.Type == mapper.Attribute.Connectivity);
                yield return new MapperInfo(
                        mapper.Type
                        , db.Attribute.DatabaseName
                        , db.Attribute.Aliases
                        , db.Attribute.DialectType
                        , db.Attribute.ListingPriority
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
