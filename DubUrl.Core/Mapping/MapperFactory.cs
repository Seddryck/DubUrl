using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class MapperFactory
    {
        private readonly record struct ProviderInfo(string ProviderName, Type Mapper);

        private readonly Dictionary<string, ProviderInfo> schemes = new();

        public MapperFactory()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            AddSchemes("System.Data.SqlClient", typeof(MssqlMapper), new[] { "mssql", "ms", "sqlserver" });
            AddSchemes("Npgsql", typeof(PgsqlMapper), new[] {"postgres", "pg", "postgresql"});

            void AddSchemes(string providerName, Type mapper, string[] aliases)
            {
                foreach (var alias in aliases)
                    schemes.Add(alias, new ProviderInfo(providerName, mapper));
            }
        }

        public IMapper Instantiate(string scheme)
        {
            var mapperType = GetMapperType(scheme);
            var ctor = mapperType.GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public,
                        new[] { typeof(DbConnectionStringBuilder) }
                    ) ?? throw new NullReferenceException(); ;
            var csb = GetProvider(GetProviderName(scheme)).CreateConnectionStringBuilder()
                    ?? throw new NullReferenceException();
            return ctor.Invoke(new object[] { csb }) as IMapper
                ?? throw new NullReferenceException();
        }

        protected internal string GetProviderName(string scheme)
        {
            if (schemes.ContainsKey(scheme))
                return schemes[scheme].ProviderName;

            throw new SchemeNotFoundException(scheme, schemes.Keys.ToArray());
        }

        protected internal static DbProviderFactory GetProvider(string providerName)
        {
            if (DbProviderFactories.TryGetFactory(providerName, out var providerFactory))
                return providerFactory;

            throw new ProviderNotFoundException(providerName, DbProviderFactories.GetProviderInvariantNames().ToArray());
        }

        protected internal Type GetMapperType(string scheme)
        {
            if (schemes.ContainsKey(scheme))
                return schemes[scheme].Mapper;

            throw new SchemeNotFoundException(scheme, schemes.Keys.ToArray());
        }

        
    }
}
