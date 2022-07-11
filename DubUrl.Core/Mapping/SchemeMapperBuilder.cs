using DubUrl.DriverLocating;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public class SchemeMapperBuilder
    {
        private readonly record struct ProviderInfo(string ProviderName, Type Mapper, DriverLocatorFactory? DriverLocatorFactory);
        private readonly Dictionary<string, ProviderInfo> Schemes = new();

        private DbProviderFactory? Provider { get; set; }
        private IMapper? Mapper { get; set; }

        public SchemeMapperBuilder()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            AddSchemes("Oracle", typeof(OracleMapper), new[] { "oracle", "or", "ora" });
            AddSchemes("MySql", typeof(MySqlConnectorMapper), new[] { "mysql", "my", "mariadb", "maria", "percona", "aurora" });
            AddSchemes("System.Data.SqlClient", typeof(MssqlMapper), new[] { "mssql", "ms", "sqlserver" });
            AddSchemes("Npgsql", typeof(PgsqlMapper), new[] { "pgsql", "postgres", "pg", "postgresql" });
            AddSchemes("Microsoft.Data.Sqlite", typeof(SqliteMapper), new[] { "sqlite", "sq" });
            AddSchemes("IBM.Data.DB2.Core", typeof(Db2Mapper), new[] { "db2" });
            AddSchemes("Teradata.Client", typeof(TeradataMapper), new[] { "td", "tera", "teradata" });
            AddSchemes("Snowflake.Data.Client", typeof(SnowflakeMapper), new[] { "sf", "snwoflake" });
            AddSchemes("FirebirdSql.Data.FirebirdClient", typeof(FirebirdSqlMapper), new[] { "fb", "firebird" });
            AddSchemes("Npgsql", typeof(CockRoachMapper), new[] { "cr", "cockroach", "crdb", "cdb" });
            AddSchemes("System.Data.Odbc", typeof(OdbcMapper), new[] { "odbc" });

            void AddSchemes(string providerName, Type mapper, string[] aliases)
            {
                foreach (var alias in aliases)
                    AddMapping(alias, providerName, mapper);
            }
        }

        internal void Build(string name)
            => Build(new string[] { name });

        public virtual void Build(string[] schemes)
        {
            (Provider, Mapper) = (null, null);

            var mainScheme = schemes.Length == 1
                ? schemes[0]
                : schemes.Contains("odbc")
                    ? "odbc"
                    : throw new ArgumentOutOfRangeException();

            Provider = GetProvider(GetProviderName(mainScheme)) ?? throw new NullReferenceException();

            var ctorParamTypes= new List<Type>() { typeof(DbConnectionStringBuilder) };
            var ctorParams = new List<object>() { Provider.CreateConnectionStringBuilder() ?? throw new NullReferenceException() };

            if (GetDriverLocatorFactory(mainScheme)!=null)
            {
                ctorParamTypes.Add(typeof(DriverLocatorFactory));
                ctorParams.Add(GetDriverLocatorFactory(mainScheme) ?? throw new NullReferenceException());
            }

            var mapperType = GetMapperType(mainScheme);
            var ctor = mapperType.GetConstructor(
                            BindingFlags.Instance | BindingFlags.Public,
                            ctorParamTypes.ToArray()
                        ) ?? throw new NullReferenceException();
            Mapper = ctor.Invoke(ctorParams.ToArray()) as IMapper
                    ?? throw new NullReferenceException();
        }

        public virtual DbProviderFactory GetProviderFactory()
            => Provider ?? throw new InvalidOperationException();

        public virtual IMapper GetMapper()
            => Mapper ?? throw new InvalidOperationException();

        protected internal string GetProviderName(string scheme)
        {
            if (Schemes.ContainsKey(scheme))
                return Schemes[scheme].ProviderName;

            throw new SchemeNotFoundException(scheme, Schemes.Keys.ToArray());
        }

        protected internal static DbProviderFactory GetProvider(string providerName)
        {
            if (DbProviderFactories.TryGetFactory(providerName, out var providerFactory))
                return providerFactory;

            throw new ProviderNotFoundException(providerName, DbProviderFactories.GetProviderInvariantNames().ToArray());
        }

        protected internal Type GetMapperType(string scheme)
        {
            if (Schemes.ContainsKey(scheme))
                return Schemes[scheme].Mapper;

            throw new SchemeNotFoundException(scheme, Schemes.Keys.ToArray());
        }

        protected internal DriverLocatorFactory? GetDriverLocatorFactory(string scheme)
        {
            if (Schemes.ContainsKey(scheme))
                return Schemes[scheme].DriverLocatorFactory;

            throw new SchemeNotFoundException(scheme, Schemes.Keys.ToArray());
        }

        #region Add, remove aliases and mappings

        public void AddAlias(string alias, string original)
        {
            if (Schemes.ContainsKey(alias))
                throw new ArgumentException();

            if (!Schemes.ContainsKey(original))
                throw new ArgumentException();

            Schemes.Add(alias, Schemes[original]);
        }

        public void AddMapping(string alias, string providerName, Type mapper)
        {
            if (Schemes.ContainsKey(alias))
                throw new ArgumentException();

            if (!mapper.IsAssignableTo(typeof(BaseMapper)))
                throw new ArgumentException();

            Schemes.Add(alias, new ProviderInfo(providerName, mapper, null));
        }

        public void RemoveMapping(string providerName)
        {
            foreach (var scheme in Schemes)
            {
                if (scheme.Value.ProviderName == providerName)
                    Schemes.Remove(scheme.Key);
            }
        }

        public void ReplaceMapper(Type oldMapper, Type newMapper)
        {
            foreach (var scheme in Schemes.Where(x => x.Value.Mapper == oldMapper))
                Schemes[scheme.Key] = new ProviderInfo(scheme.Value.ProviderName, newMapper, scheme.Value.DriverLocatorFactory);
        }

        public void ReplaceDriverLocatorFactory(Type mapper, DriverLocatorFactory factory)
        {
            foreach (var scheme in Schemes.Where(x => x.Value.Mapper == mapper))
                Schemes[scheme.Key] = new ProviderInfo(scheme.Value.ProviderName, scheme.Value.Mapper, factory);
        }

        #endregion
    }
}
