using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

public class DialectBuilder
{
    protected Dictionary<Type, List<string>> DialectAliases = [];
    protected Dictionary<Type, IDialect> Dialects = [];
    protected bool IsBuilt = false;

    public void AddAliases<T>(string[] aliases)
        => AddAliases(typeof(T), aliases);
    public void AddAliases(Type dialectType, string[] aliases)
    {
        IsBuilt = false;

        var existing = DialectAliases
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

        if (DialectAliases.TryGetValue(dialectType, out var existingAliases))
            existingAliases.AddRange(aliases.Except(existingAliases));
        else
            DialectAliases.Add(dialectType, [.. aliases]);
    }

    public void AddAlias(string alias, string original)
    {
        IsBuilt = false;

        if (DialectAliases.Values.Any(x => x.Contains(alias)))
            throw new ArgumentOutOfRangeException(nameof(alias));

        if (!DialectAliases.Values.Any(x => x.Contains(original)))
            throw new ArgumentOutOfRangeException(nameof(original));

        var dialect = DialectAliases.First(x => x.Value.Contains(original));
        dialect.Value.Add(alias);
    }

    public void Build()
    {
        var languages = new Dictionary<string, ILanguage>
        (
            DialectAliases.Select
            (
                x => x.Key.GetCustomAttribute<ParentLanguageAttribute>()?.Language ?? throw new NullReferenceException()
            ).Distinct(new LanguageComparer())
            .Select(x => new KeyValuePair<string, ILanguage>(x.Extension, x))
        );

        Dialects.Clear();
        foreach (var dialectInfo in DialectAliases)
        {
            try
            {
                var renderer = Activator.CreateInstance(
                                    dialectInfo.Key.GetCustomAttribute<RendererAttribute>()?.RendererType
                                        ?? throw new NullReferenceException("Can't find renderer.")
                                    , []);

                var casters = (dialectInfo.Key.GetCustomAttributes<ReturnCasterAttribute>().Select(
                                        x => (ICaster)Activator.CreateInstance(x.CasterType)!)
                                ?? Array.Empty<ICaster>()).ToArray();

                var language = dialectInfo.Key.GetCustomAttribute<ParentLanguageAttribute>()?.Language.Extension
                    ?? throw new NullReferenceException("Can't find parent language.");

                var dbTypeMapperType = dialectInfo.Key.GetCustomAttribute<DbTypeMapperAttribute>()?.DbTypeMapperType
                    ?? throw new NullReferenceException("Can't find DbTypeMapper.");

                var dbTypeMapper = dbTypeMapperType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null) as IDbTypeMapper
                    ?? throw new NullReferenceException("Can't instantiate DbTypeMapper.");

                Dialects.Add(dialectInfo.Key,
                    (IDialect)(
                        Activator.CreateInstance(dialectInfo.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null
                            , [languages[language], dialectInfo.Value.ToArray(), renderer, casters!, dbTypeMapper], null
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
        IsBuilt = true;
    }

    public IDialect Get<T>()
        => Get(typeof(T));

    public IDialect Get(Type dialectType)
    {
        if (!IsBuilt)
            throw new InvalidOperationException();
        if (!Dialects.ContainsKey(dialectType))
            throw new DialectNotFoundException(dialectType, [.. Dialects.Keys]);
        return Dialects[dialectType];
    }

    public IDialect Get(string scheme)
        => Get(DialectAliases.FirstOrDefault(x => x.Value.Contains(scheme)).Key
                ?? throw new DialectNotFoundException(scheme, [.. Dialects.Keys])
           );
}
