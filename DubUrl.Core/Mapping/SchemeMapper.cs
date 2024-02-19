using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting;

namespace DubUrl.Mapping;
public class SchemeMapper(IReadOnlyDictionary<string, IMapper> mappers, SchemeNormalizer normalizer)
{
    protected IReadOnlyDictionary<string, IMapper> Mappers { get; } = mappers;
    protected SchemeNormalizer Normalizer { get; } = normalizer;

    public bool CanHandle(string scheme)
        => Mappers.ContainsKey(Normalizer.Normalize(scheme));

    public virtual IMapper GetMapper(string scheme)
    {
        var normalized = Normalizer.Normalize(scheme);
        if (!Mappers.TryGetValue(normalized, out var mapper))
            throw new SchemeNotFoundException(scheme, [.. Mappers.Keys]);

        return mapper;
    }

    public virtual IMapper GetMapper(string[] schemes)
        => GetMapper(Normalizer.Normalize(schemes));

    public virtual DbProviderFactory GetProviderFactory(string[] schemes)
    {
        var normalizedScheme = Normalizer.Normalize(schemes);

        if (!Mappers.TryGetValue(normalizedScheme, out var mapper))
            throw new SchemeNotFoundException(normalizedScheme, [.. Mappers.Keys]);

        return GetProvider(mapper.GetProviderName())
            ?? throw new ProviderNotFoundException(mapper.GetProviderName(), DbProviderFactories.GetProviderInvariantNames().ToArray());
    }

    protected internal static DbProviderFactory? GetProvider(string providerName)
    {
        DbProviderFactories.TryGetFactory(providerName, out var providerFactory);
        return providerFactory;
    }
}
