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
            AddSchemes("Npgsql", typeof(PgsqlMapper), new[] { "pgsql", "postgres", "pg", "postgresql" });
            AddSchemes("MySql", typeof(MySqlConnectorMapper), new[] { "mysql", "my", "mariadb", "maria", "percona", "aurora" });
            AddSchemes("Oracle", typeof(OracleMapper), new[] { "oracle", "or", "ora" });

            void AddSchemes(string providerName, Type mapper, string[] aliases)
            {
                foreach (var alias in aliases)
                    AddMapping(alias, providerName, mapper);
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

        public void AddAlias(string alias, string original)
        {
            if (schemes.ContainsKey(alias))
                throw new ArgumentException();

            if (!schemes.ContainsKey(original))
                throw new ArgumentException();

            schemes.Add(alias, schemes[original]);
        }

        public void AddMapping(string alias, string providerName, Type mapper)
        {
            if (schemes.ContainsKey(alias))
                throw new ArgumentException();

            if (!mapper.IsAssignableTo(typeof(BaseMapper)))
                throw new ArgumentException();

            schemes.Add(alias, new ProviderInfo(providerName, mapper));
        }

        public void RemoveMapping(string providerName)
        {
            foreach (var scheme in schemes)
            {
                if (scheme.Value.ProviderName == providerName)
                    schemes.Remove(scheme.Key);
            }
        }

        public void ReplaceMapping(Type oldMapper, Type newMapper)
        {
            foreach (var scheme in schemes.Where(x => x.Value.Mapper == oldMapper))
                schemes[scheme.Key] = new ProviderInfo(scheme.Value.ProviderName, newMapper);
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
