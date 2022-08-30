using DubUrl.Querying.Dialecting;
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

        public override IEnumerable<MapperInfo> Locate()
            => Locate<MapperAttribute>();

        public IEnumerable<MapperInfo> LocateAlternative()
            => Locate<AlternativeMapperAttribute>();


        protected IEnumerable<MapperInfo> Locate<T>() where T : BaseMapperAttribute
        {
            var mappers = LocateAttribute<T>();
            var databases = LocateAttribute<DatabaseAttribute>();

            foreach (var mapper in mappers)
            {
                var db = databases.Single(x => x.Type == mapper.Attribute.Connectivity);
                yield return new MapperInfo(
                        mapper.Type
                        , db.Attribute.DatabaseName
                        , db.Attribute.Aliases
                        , db.Attribute.DialectType
                        , db.Attribute.ListingPriority
                        , mapper.Attribute.ProviderInvariantName
                    );
            }
        }

    }
}
