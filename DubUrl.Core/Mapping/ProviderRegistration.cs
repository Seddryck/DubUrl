using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace DubUrl.Mapping;

/// <summary>
/// Describes one provider without requiring attributes, constructor conventions, or type scanning.
/// </summary>
public sealed class ProviderRegistration
{
    private readonly Dictionary<Type, object> _capabilities;

    public string Name { get; }
    public IReadOnlyList<string> Aliases { get; }
    public string ProviderInvariantName { get; }
    public Func<DbProviderFactory?> ProviderFactory { get; }
    public IDialect Dialect { get; }
    public Func<IParametrizer> ParametrizerFactory { get; }
    public Func<ProviderCompilationContext, IMapper> Compile { get; }
    public IReadOnlyDictionary<Type, object> Capabilities => _capabilities;

    public ProviderRegistration(
        string name,
        IEnumerable<string> aliases,
        string providerInvariantName,
        Func<DbProviderFactory?> providerFactory,
        IDialect dialect,
        Func<IParametrizer> parametrizerFactory,
        Func<ProviderCompilationContext, IMapper> compile,
        IEnumerable<object>? capabilities = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerInvariantName);
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(providerFactory);
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(parametrizerFactory);
        ArgumentNullException.ThrowIfNull(compile);

        var normalizedAliases = aliases
            .Select(alias => alias?.Trim())
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedAliases.Length == 0)
            throw new ArgumentException("At least one non-empty scheme alias is required.", nameof(aliases));

        Name = name;
        Aliases = normalizedAliases;
        ProviderInvariantName = providerInvariantName;
        ProviderFactory = providerFactory;
        Dialect = dialect;
        ParametrizerFactory = parametrizerFactory;
        Compile = compile;
        _capabilities = (capabilities ?? [])
            .ToDictionary(capability => capability.GetType());
    }

    public bool TryGetCapability<TCapability>(out TCapability? capability) where TCapability : class
    {
        if (_capabilities.TryGetValue(typeof(TCapability), out var value))
        {
            capability = (TCapability)value;
            return true;
        }

        capability = null;
        return false;
    }
}

/// <summary>
/// Runtime inputs supplied to a provider's connection-string compiler.
/// </summary>
public sealed class ProviderCompilationContext
{
    public DbProviderFactory ProviderFactory { get; }
    public DbConnectionStringBuilder ConnectionStringBuilder { get; }
    public IDialect Dialect { get; }
    public IParametrizer Parametrizer { get; }
    public string RootPath { get; }
    public IReadOnlyCollection<Type> OdbcLocatorOptions { get; }

    internal ProviderCompilationContext(
        DbProviderFactory providerFactory,
        IDialect dialect,
        IParametrizer parametrizer,
        string rootPath,
        IReadOnlyCollection<Type> odbcLocatorOptions)
    {
        ProviderFactory = providerFactory;
        ConnectionStringBuilder = providerFactory.CreateConnectionStringBuilder()
            ?? throw new InvalidOperationException(
                $"Provider factory '{providerFactory.GetType().FullName}' did not create a connection-string builder.");
        Dialect = dialect;
        Parametrizer = parametrizer;
        RootPath = rootPath;
        OdbcLocatorOptions = odbcLocatorOptions;
    }
}
