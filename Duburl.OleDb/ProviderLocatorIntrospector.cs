using Duburl.OleDb;
using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb
{
    public class ProviderLocatorIntrospector : BaseIntrospector
    {
        public record struct ProviderLocatorInfo(Type ProviderLocatorType, string DatabaseName, string[] Aliases, string NamePattern, int ListingPriority, Type[] Options) { }

        public ProviderLocatorIntrospector()
            : this(new AssemblyTypesProbeOleDb()) { }

        internal ProviderLocatorIntrospector(ITypesProbe probe)
            : base(probe) { }

        public IEnumerable<ProviderLocatorInfo> Locate()
        {
            var databases = LocateAttribute<DatabaseAttribute>();
            var providers = LocateAttribute<ProviderAttribute>();

            foreach (var provider in providers)
            {
                var db = databases.Single(x => x.Type == provider.Attribute.Database);
                yield return new ProviderLocatorInfo(
                        provider.Type
                        , db.Attribute.DatabaseName
                        , (provider.Attribute as ProviderSpecializationAttribute)?.Aliases ?? db.Attribute.Aliases
                        , provider.Attribute.RegexPattern
                        , db.Attribute.ListingPriority
                        , provider.Attribute.Options
                    );
            }
        }
    }
}
