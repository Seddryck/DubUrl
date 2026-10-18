using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace DubUrl.Mapping;

/// <summary>
/// Compatibility adapter for providers that still use DubUrl's mapping attributes.
/// </summary>
/// <remarks>
/// This adapter scans exported types and invokes mapper constructors through reflection. Prefer an
/// explicit <see cref="IDubUrlProviderModule"/> for trimming and Native AOT applications.
/// </remarks>
public sealed class AttributeProviderModule : IDubUrlProviderModule
{
    private readonly Assembly[] _assemblies;

    public AttributeProviderModule(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        _assemblies = assemblies.Distinct().ToArray();
        if (_assemblies.Length == 0)
            throw new ArgumentException("At least one assembly is required.", nameof(assemblies));
    }

    [RequiresUnreferencedCode("Attribute provider discovery scans exported types and reflects over constructors.")]
    public void Register(IProviderCatalogBuilder catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var probe = new AssemblyTypesProbe(_assemblies);
        var mappings = new NativeMapperIntrospector(probe).Locate()
            .Concat(new WrapperMapperIntrospector(probe).Locate())
            .ToArray();
        var dialectBuilder = new DialectRegistryBuilder();
        foreach (var mapping in mappings)
            dialectBuilder.AddDialect(mapping.DialectType, mapping.Aliases);
        var dialects = dialectBuilder.Build();

        foreach (var mapping in mappings)
        {
            catalog.AddProvider(new ProviderRegistration(
                mapping.DatabaseName,
                mapping.Aliases,
                mapping.ProviderInvariantName,
                () => SchemeRegistryBuilder.GetProvider(mapping.ProviderInvariantName),
                dialects.Get(mapping.DialectType),
                () => ParametrizerFactory.Instantiate(mapping.ParametrizerType),
                context => Compile(mapping, context)));
        }
    }

    private static IMapper Compile(MapperInfo mapping, ProviderCompilationContext context)
    {
        var parameterTypes = new List<Type>
        {
            typeof(DbConnectionStringBuilder),
            typeof(IDialect),
            typeof(IParametrizer),
        };
        var parameters = new List<object>
        {
            context.ConnectionStringBuilder,
            context.Dialect,
            context.Parametrizer,
        };

        if (typeof(IFileBasedMapper).IsAssignableFrom(mapping.MapperType))
        {
            parameterTypes.Add(typeof(string));
            parameters.Add(context.RootPath);
        }

        var constructor = mapping.MapperType.GetConstructor(parameterTypes.ToArray())
            ?? throw new InvalidOperationException(
                $"Compatibility provider '{mapping.DatabaseName}' expects a public constructor " +
                $"with parameters ({string.Join(", ", parameterTypes.Select(type => type.Name))}).");
        return (IMapper)constructor.Invoke(parameters.ToArray());
    }
}
