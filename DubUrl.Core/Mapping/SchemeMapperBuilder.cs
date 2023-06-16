using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
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
        private bool IsBuilt { get; set; } = false;

        private readonly List<MapperInfo> MapperData = new();

        protected Dictionary<string, IMapper> Mappers { get; set; } = new();
        private BaseMapperIntrospector[] MapperIntrospectors { get; } = new BaseMapperIntrospector[] { new NativeMapperIntrospector(), new WrapperMapperIntrospector() };
        private DialectBuilder DialectBuilder { get; } = new();
        private ParametrizerFactory ParametrizerFactory { get; } = new();

        public SchemeMapperBuilder()
         : this(new[] { typeof(SchemeMapperBuilder).Assembly }) { }

        public SchemeMapperBuilder(Assembly[] assemblies)
        {
            var asmTypesProbe = new AssemblyTypesProbe(assemblies);
            MapperIntrospectors = new BaseMapperIntrospector[]
            {
                new NativeMapperIntrospector(asmTypesProbe)
                , new WrapperMapperIntrospector(asmTypesProbe)
            };
            Initialize();
        }

        protected virtual void Initialize()
        {
            foreach (var mapperData 
                in MapperIntrospectors.Aggregate(
                    Array.Empty<MapperInfo>(), (data, introspector)
                    => data.Concat(introspector.Locate()).ToArray())
            )
                AddMapping(mapperData);
        }

        public virtual void Build()
        {
            foreach (var mapperData in MapperData)
                DialectBuilder.AddAliases(mapperData.DialectType, mapperData.Aliases.ToArray());
            DialectBuilder.Build();

            Mappers.Clear();
            foreach (var mapperData in MapperData)
            {
                var provider = GetProvider(mapperData.ProviderInvariantName);
                if (provider == null)
                {
                    Debug.WriteLine($"No provider registered with the name '{mapperData.ProviderInvariantName}', skipping associated mapper.");
                    continue;
                }

                var ctorParamTypes = new List<Type>() { typeof(DbConnectionStringBuilder), typeof(IDialect), typeof(IParametrizer) };
                var ctorParams = new List<object>() {
                    provider.CreateConnectionStringBuilder() ?? throw new NullReferenceException()
                    , DialectBuilder.Get(mapperData.Aliases.First())
                    , ParametrizerFactory.Instantiate(mapperData.ParametrizerType)
                };

                //if (mapperData.DriverLocatorFactory != null)
                //{
                //    ctorParamTypes.Add(typeof(DriverLocatorFactory));
                //    ctorParams.Add(mapperData.Value.DriverLocatorFactory);
                //}

                var ctor = mapperData.MapperType.GetConstructor(
                                BindingFlags.Instance | BindingFlags.Public,
                                ctorParamTypes.ToArray()
                            ) ?? throw new NullReferenceException($"Unable to find a suitable constructor for the mapper of type '{mapperData.MapperType.Name}'.");
                var mapper = ctor.Invoke(ctorParams.ToArray()) as IMapper
                        ?? throw new NullReferenceException();

                foreach (var alias in mapperData.Aliases)
                    Mappers.Add(alias, mapper);
            }

            IsBuilt = true;
        }

        private static string GetAlias(string[] aliases)
        {
            var mainAlias = aliases.Length == 1
                ? aliases[0]
                : aliases.Contains("oledb")
                    ? "oledb"
                    : aliases.Contains("odbc")
                        ? "odbc"
                        : throw new ArgumentOutOfRangeException(nameof(aliases));

            var secondAlias = aliases.SkipWhile(x => x.Equals(mainAlias)).FirstOrDefault();

            return string.IsNullOrEmpty(secondAlias) ? mainAlias : $"{mainAlias}+{secondAlias}";
        }

        public IMapper GetMapper(string alias)
            => GetMapper(new[] { alias });

        public virtual IMapper GetMapper(string[] aliases)
        {
            if (!IsBuilt)
                throw new InvalidOperationException();

            var alias = GetAlias(aliases);

            if (!Mappers.ContainsKey(alias))
                throw new SchemeNotFoundException(alias, Mappers.Keys.ToArray());

            return Mappers[alias];
        }

        public virtual DbProviderFactory GetProviderFactory(string[] aliases)
        {
            if (!IsBuilt)
                throw new InvalidOperationException();

            var alias = GetAlias(aliases);

            if (!Mappers.ContainsKey(alias))
                throw new SchemeNotFoundException(alias, Mappers.Keys.ToArray());

            var mapper = Mappers[alias];
            return GetProvider(mapper.GetProviderName())
                ?? throw new ProviderNotFoundException(mapper.GetProviderName(), DbProviderFactories.GetProviderInvariantNames().ToArray());
        }

        protected internal static DbProviderFactory? GetProvider(string providerName)
        {
            DbProviderFactories.TryGetFactory(providerName, out var providerFactory);
            return providerFactory;
        }

        #region Add, remove aliases and mappings

        public void AddAlias(string alias, string original)
        {
            var mapperData = MapperData.Single(x => x.Aliases.Contains(original));
            var newAliases = mapperData.Aliases.ToList();
            newAliases.Add(alias);
            mapperData.Aliases = newAliases.ToArray();
        }

        public void AddMapping<M, D, P>(string databaseName, string alias, string providerName)
            where M : IMapper
            where D : IDialect
            where P : IParametrizer
            => AddMapping<M, D, P>(databaseName, new[] { alias }, providerName);

        public void AddMapping<M, D, P>(string databaseName, string[] aliases, string providerName)
            where M : IMapper
            where D : IDialect
            where P : IParametrizer
            => AddMapping(new MapperInfo(typeof(M), databaseName, aliases, typeof(D), 9, providerName, typeof(P)));

        public void AddMapping(MapperInfo mapperInfo)
        {
            if (!mapperInfo.MapperType.IsAssignableTo(typeof(IMapper)))
                throw new ArgumentException(nameof(mapperInfo.MapperType));

            if (!mapperInfo.DialectType.IsAssignableTo(typeof(IDialect)))
                throw new ArgumentException(nameof(mapperInfo.DialectType));

            if (!mapperInfo.ParametrizerType.IsAssignableTo(typeof(IParametrizer)))
                throw new ArgumentException(nameof(mapperInfo.ParametrizerType));

            if (MapperData.Contains(mapperInfo))
                throw new ArgumentException($"The mapper information for '{mapperInfo.MapperType.Name}' is already registered");

            MapperData.Add(mapperInfo);
        }

        public void RemoveMapping(string alias)
        {
            var mapperData = MapperData.Single(x => x.Aliases.Contains(alias));
            MapperData.Remove(mapperData);
        }

        public void ReplaceMapper(Type oldMapper, Type newMapper)
        {
            var mapperData = MapperData.Single(x => x.MapperType.Equals(oldMapper));
            mapperData.MapperType = newMapper;
        }

        public void ReplaceDriverLocatorFactory(Type mapper, DriverLocatorFactory factory)
            => throw new NotImplementedException();

        #endregion
    }
}
