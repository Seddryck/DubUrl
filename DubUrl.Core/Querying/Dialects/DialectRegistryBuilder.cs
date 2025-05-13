using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

public class DialectRegistryBuilder
{
    private readonly Dictionary<Type, List<string>> _aliases = [];
    
    public void AddDialect<T>(string[] aliases) where T : IDialect
        => AddDialect(typeof(T), aliases);

    public void AddDialect(Type dialectType, string[] aliases)
    {
        var existing = _aliases
                        .Where(x => x.Value.Any(d => aliases.Contains(d)))
                        .SingleOrDefault(x => x.Key != dialectType);
        {
            if (existing.Key is not null)
                throw new DialectAliasAlreadyExistingException(
                        aliases.Intersect(existing.Value).First()
                        , existing.Key
                        , dialectType
                    );
        }

        if (_aliases.TryGetValue(dialectType, out var existingAliases))
            existingAliases.AddRange(aliases.Except(existingAliases));
        else
            _aliases.Add(dialectType, [.. aliases]);
    }

    public void CreateAlias(string alias, string original)
    {
        if (_aliases.Values.Any(x => x.Contains(alias)))
            throw new ArgumentOutOfRangeException(nameof(alias));

        if (!_aliases.Values.Any(x => x.Contains(original)))
            throw new ArgumentOutOfRangeException(nameof(original));

        var dialect = _aliases.First(x => x.Value.Contains(original));
        dialect.Value.Add(alias);
    }

    public IDialectRegistry Build()
    {
        var languages = new Dictionary<string, ILanguage>
        (
            _aliases.Select
            (
                x => x.Key.GetCustomAttribute<ParentLanguageAttribute>()?.Language ?? throw new NullReferenceException()
            ).Distinct(new LanguageComparer())
            .Select(x => new KeyValuePair<string, ILanguage>(x.Extension, x))
        );

        var dialects = new Dictionary<Type, IDialect>();
        foreach (var dialectInfo in _aliases)
        {
            try
            {
                var renderer = Activator.CreateInstance(
                                    dialectInfo.Key.GetCustomAttribute<RendererAttribute>()?.RendererType
                                        ?? throw new InvalidOperationException("Can't find renderer.")
                                    , []);

                var casters = (dialectInfo.Key.GetCustomAttributes<ReturnCasterAttribute>().Select(
                                        x => (ICaster)Activator.CreateInstance(x.CasterType)!)).ToArray();

                var language = dialectInfo.Key.GetCustomAttribute<ParentLanguageAttribute>()?.Language.Extension
                    ?? throw new InvalidOperationException("Can't find parent language.");

                var dbTypeMapper = GetComponent<DbTypeMapperAttribute, IDbTypeMapper>(
                         dialectInfo.Key
                         , x => x?.DbTypeMapperType);

                var sqlFunctionMapper = GetComponent<SqlFunctionMapperAttribute, ISqlFunctionMapper>(
                        dialectInfo.Key
                        , x => x?.SqlFunctionMapperType);

                dialects.Add(dialectInfo.Key,
                    (IDialect)(
                        Activator.CreateInstance(dialectInfo.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null
                            , [languages[language], dialectInfo.Value.ToArray(), renderer, casters!, dbTypeMapper, sqlFunctionMapper], null
                        )
                        ?? throw new ArgumentException()
                     )
                );
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(dialectInfo.Key.Name, ex);
            }
        }
        return new DialectRegistry(
            new Dictionary<Type, IDialect>(dialects),
            _aliases.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToList())   // deep copy of alias lists
        );
    }

    private I GetComponent<A, I>(Type dialect, Func<A?, Type?> getMember) where A : Attribute where I : class
    {
        var dialectName = dialect.Name.Replace("Dialect", string.Empty);
        var propertyName = nameof(I).Substring(1);

        var type = getMember(dialect.GetCustomAttribute<A>())
                    ?? throw new InvalidOperationException($"Can't find {propertyName} for dialect {dialectName}.");

        var obj = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.GetValue(null) as I
            ?? throw new InvalidOperationException($"Can't instantiate {propertyName} for dialect {dialectName}.");

        return obj;
    }
}
