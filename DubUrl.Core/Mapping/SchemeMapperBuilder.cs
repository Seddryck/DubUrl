using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public class SchemeMapperBuilder
    {
        private readonly record struct ProviderInfo(string ProviderName, List<string> Aliases, Type DialectType, DriverLocatorFactory? DriverLocatorFactory);
        private bool IsBuilt { get; }

        private readonly Dictionary<Type, ProviderInfo> MapperData = new();

        protected Dictionary<string, IMapper> Mappers { get; set; } = new();
        private MapperIntrospector MapperIntrospector { get; } = new MapperIntrospector();

        private DialectBuilder DialectBuilder { get; } = new DialectBuilder();

        public SchemeMapperBuilder()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            foreach (var mapperData in MapperIntrospector.Locate())
                AddMapping(mapperData.MapperType, mapperData.ProviderInvariantName, mapperData.Aliases, mapperData.DialectType);
        }

        public virtual void Build()
        {
            foreach (var mapperData in MapperData)
                DialectBuilder.AddAliases(mapperData.Value.DialectType, mapperData.Value.Aliases.ToArray());
            DialectBuilder.Build();

            Mappers.Clear();
            foreach (var mapperData in MapperData)
            {
                var provider = GetProvider(mapperData.Value.ProviderName);
                if (provider==null)
                {
                    Debug.WriteLine($"No provider registered with the name '{mapperData.Value.ProviderName}', skipping associated mapper.");
                    continue;
                }

                var ctorParamTypes = new List<Type>() { typeof(DbConnectionStringBuilder), typeof(IDialect) };
                var ctorParams = new List<object>() {
                    provider.CreateConnectionStringBuilder() ?? throw new NullReferenceException()
                    , DialectBuilder.Get(mapperData.Value.Aliases.First())
                };

                if (mapperData.Value.DriverLocatorFactory != null)
                {
                    ctorParamTypes.Add(typeof(DriverLocatorFactory));
                    ctorParams.Add(mapperData.Value.DriverLocatorFactory);
                }

                var ctor = mapperData.Key.GetConstructor(
                                BindingFlags.Instance | BindingFlags.Public,
                                ctorParamTypes.ToArray()
                            ) ?? throw new NullReferenceException($"Unable to find a suitable constructor for the mapper of type '{mapperData.Key.Name}'.");
                var mapper = ctor.Invoke(ctorParams.ToArray()) as IMapper
                        ?? throw new NullReferenceException();

                foreach (var alias in mapperData.Value.Aliases)
                    Mappers.Add(alias, mapper);
            }
        }

        private string GetMainAlias(string[] aliases)
            => aliases.Length == 1
                ? aliases[0]
                : aliases.Contains("oledb")
                    ? "oledb"
                    : aliases.Contains("odbc")
                        ? "odbc"
                        : throw new ArgumentOutOfRangeException();

        public IMapper GetMapper(string alias)
            => GetMapper(new[] { alias });

        public virtual IMapper GetMapper(string[] aliases)
        {
            var mainAlias = GetMainAlias(aliases);

            if (!Mappers.ContainsKey(mainAlias))
                throw new SchemeNotFoundException(mainAlias, Mappers.Keys.ToArray());

            return Mappers[mainAlias];
        }

        public virtual DbProviderFactory GetProviderFactory(string[] aliases)
        {
            var mainAlias = GetMainAlias(aliases);

            if (!Mappers.ContainsKey(mainAlias))
                throw new SchemeNotFoundException(mainAlias, Mappers.Keys.ToArray());
            
            var mapper = Mappers[mainAlias];
            return GetProvider(MapperData[mapper.GetType()].ProviderName)
                ?? throw new ProviderNotFoundException(MapperData[mapper.GetType()].ProviderName, DbProviderFactories.GetProviderInvariantNames().ToArray());
        }

        protected internal static DbProviderFactory? GetProvider(string providerName)
        {
            DbProviderFactories.TryGetFactory(providerName, out var providerFactory);
            return providerFactory;
        }

        #region Add, remove aliases and mappings

        public void AddAlias(string alias, string original)
        {
            if (!MapperData.Any(x => x.Value.Aliases.Contains(original)))
                throw new ArgumentOutOfRangeException();

            MapperData.First(x => x.Value.Aliases.Contains(original))
                .Value.Aliases.Add(alias);
        }

        public void AddMapping<M, D>(string providerName, string alias)
            where M : IMapper
            where D : IDialect
            => AddMapping<M, D>(providerName, new[] { alias });

        public void AddMapping<M, D>(string providerName, string[] aliases)
            where M : IMapper
            where D : IDialect
            => AddMapping(typeof(M), providerName, aliases, typeof(D));
        public void AddMapping(Type mapperType, string providerName, string[] aliases, Type dialectType)
        {
            if (!mapperType.IsAssignableTo(typeof(IMapper)))
                throw new ArgumentException(nameof(mapperType));

            if (!dialectType.IsAssignableTo(typeof(IDialect)))
                throw new ArgumentException(nameof(dialectType));

            if (MapperData.ContainsKey(mapperType))
                throw new ArgumentException();

            MapperData.Add(mapperType, new ProviderInfo(providerName, aliases.ToList(), dialectType, null));
        }

        public void RemoveMapping(string providerName)
        {
            foreach (var provider in MapperData)
            {
                if (provider.Value.ProviderName == providerName)
                    MapperData.Remove(provider.Key);
            }
        }

        public void ReplaceMapper(Type oldMapper, Type newMapper)
        {
            MapperData.Add(newMapper, MapperData[oldMapper]);
            MapperData.Remove(oldMapper);
        }

        public void ReplaceDriverLocatorFactory(Type mapper, DriverLocatorFactory factory)
            => MapperData[mapper] = new ProviderInfo(
                MapperData[mapper].ProviderName,
                MapperData[mapper].Aliases,
                MapperData[mapper].DialectType,
                factory);

        #endregion
    }
}
