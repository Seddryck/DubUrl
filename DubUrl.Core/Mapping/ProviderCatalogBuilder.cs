using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DubUrl.Core.ProviderPacks;
using DubUrl.Querying.Dialects;

namespace DubUrl.Mapping;

/// <summary>
/// Creates scheme registries from explicitly registered provider modules.
/// </summary>
public sealed class ProviderCatalogBuilder : IProviderCatalogBuilder
{
    private readonly List<ProviderRegistration> _providers = [];
    private readonly Dictionary<string, ProviderRegistration> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Type> _odbcLocatorOptions = [];
    private string _rootPath = string.Empty;

    public ProviderCatalogBuilder WithRootPath(string rootPath)
    {
        _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        return this;
    }

    public ProviderCatalogBuilder AddModule(IDubUrlProviderModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Register(this);
        return this;
    }

    public ProviderCatalogBuilder AddInstalledPacks(
        ProviderPackStore store,
        ICollection<ProviderPackDiagnostic>? diagnostics = null)
    {
        var loader = new InstalledProviderModuleLoader();
        foreach (var module in loader.LoadActive(store, diagnostics))
            AddModule(module);
        return this;
    }

    public IProviderCatalogBuilder AddProvider(ProviderRegistration registration)
        => AddProvider(registration, fallback: false);

    internal ProviderCatalogBuilder AddFallbackModule(IDubUrlProviderModule module)
    {
        module.Register(new FallbackCatalog(this));
        return this;
    }

    private IProviderCatalogBuilder AddProvider(ProviderRegistration registration, bool fallback)
    {
        ArgumentNullException.ThrowIfNull(registration);

        foreach (var alias in registration.Aliases)
        {
            if (!fallback && _aliases.TryGetValue(alias, out var existing))
                throw new ProviderRegistrationException(alias, existing.Name, registration.Name);
        }

        _providers.Add(registration);
        foreach (var alias in registration.Aliases)
            _aliases.TryAdd(alias, registration);
        return this;
    }

    public IProviderCatalogBuilder AddOdbcLocatorOption<TOption>() where TOption : struct, Enum
    {
        _odbcLocatorOptions.Add(typeof(TOption));
        return this;
    }

    public SchemeRegistry Build()
    {
        var registrations = new Dictionary<string, SchemeRegistration>(StringComparer.OrdinalIgnoreCase);
        var optionTypes = _odbcLocatorOptions.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();

        foreach (var provider in _providers)
        {
            var aliases = GetEffectiveAliases(provider);
            if (aliases.Length == 0)
                continue;
            var providerFactory = provider.ProviderFactory();
            if (providerFactory is null)
            {
                Debug.WriteLine($"Skipping provider '{provider.Name}' because '{provider.ProviderInvariantName}' is not registered.");
                continue;
            }

            IMapper CreateMapper()
                => provider.Compile(new ProviderCompilationContext(
                    providerFactory,
                    provider.Dialect,
                    provider.ParametrizerFactory(),
                    _rootPath,
                    optionTypes));

            var schemeRegistration = new SchemeRegistration(
                CreateMapper,
                providerFactory,
                provider.ProviderInvariantName,
                provider.Capabilities,
                provider.BulkCopyProvider);

            foreach (var alias in aliases)
                registrations.Add(alias, schemeRegistration);
        }

        return SchemeRegistry.FromRegistrations(registrations);
    }

    public IDialectRegistry BuildDialects()
    {
        var builder = new DialectRegistryBuilder();
        foreach (var provider in _providers)
        {
            var aliases = GetEffectiveAliases(provider);
            if (aliases.Length != 0)
                builder.AddDialect(provider.Dialect.GetType(), aliases);
        }
        return builder.Build();
    }

    private string[] GetEffectiveAliases(ProviderRegistration provider)
        => provider.Aliases.Where(alias => ReferenceEquals(_aliases[alias], provider)).ToArray();

    private sealed class FallbackCatalog(ProviderCatalogBuilder owner) : IProviderCatalogBuilder
    {
        public IProviderCatalogBuilder AddProvider(ProviderRegistration registration)
        {
            owner.AddProvider(registration, fallback: true);
            return this;
        }

        public IProviderCatalogBuilder AddOdbcLocatorOption<TOption>() where TOption : struct, Enum
        {
            owner.AddOdbcLocatorOption<TOption>();
            return this;
        }
    }
}

internal sealed record SchemeRegistration(
    Func<IMapper> MapperFactory,
    System.Data.Common.DbProviderFactory ProviderFactory,
    string ProviderInvariantName,
    IReadOnlyDictionary<Type, object> Capabilities,
    DubUrl.BulkCopy.IBulkCopyProvider? BulkCopyProvider);
