using DubUrl.Locating.OdbcDriver.Implementation;
using DubUrl.Locating.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DubUrl.Locating.OdbcDriver;

public class DriverLocatorFactory : BaseLocatorFactory
{
    private readonly Dictionary<string, Func<IDictionary<Type, object>, IDriverLocator>> _factories =
        new(StringComparer.OrdinalIgnoreCase);

    public DriverLocatorFactory()
        => Initialize();

    protected virtual void Initialize()
    {
        AddDrivers(["drill"], static _ => new DrillDriverLocator());
        AddDrivers(["duck", "duckdb"], static _ => new DuckdbDriverLocator());
        AddDrivers(["maria", "mariadb"], static _ => new MariaDbDriverLocator());
        AddDrivers(["accdb", "access", "msaccess", "mdb"], static _ => new MsAccessDriverLocator());
        AddDrivers(["xls", "xlsx", "xlsb", "xlsm"], static _ => new MsExcelDriverLocator());
        AddDrivers(["mssql", "ms", "sqlserver", "mssqlserver"], static _ => new MssqlDriverLocator());
        AddDrivers(["mysql", "my"], static options => new MySqlConnectorDriverLocator(
            GetOption(options, EncodingOption.Unspecified)));
        AddDrivers(["pg", "pgx", "pgsql", "postgres", "postgresql"], static options => new PostgresqlDriverLocator(
            GetOption(options, EncodingOption.Unspecified),
            GetOption(options, ArchitectureOption.Unspecified)));
        AddDrivers(["quest", "questdb"], static options => new QuestDbDriverLocator(
            GetOption(options, EncodingOption.Unspecified),
            GetOption(options, ArchitectureOption.Unspecified)));
        AddDrivers(["txt", "csv", "tsv"], static _ => new TextDriverLocator());
        AddDrivers(["ts", "timescale"], static options => new TimescaleDriverLocator(
            GetOption(options, EncodingOption.Unspecified),
            GetOption(options, ArchitectureOption.Unspecified)));
        AddDrivers(["tr", "trino"], static _ => new TrinoDriverLocator());
    }

    public virtual IDriverLocator Instantiate(string scheme)
        => Instantiate(scheme, new Dictionary<Type, object>());

    public virtual IDriverLocator Instantiate(string scheme, IDictionary<Type, object> options)
    {
        if (!Schemes.ContainsKey(scheme))
            throw new ArgumentException($"There is no scheme registered with the alias '{scheme}'.", nameof(scheme));

        if (_factories.TryGetValue(scheme, out var factory))
            return factory(options);

        // Compatibility path for callers using the legacy Type-based registration method.
        var driverLocatorType = Schemes[scheme];
        var constructors = driverLocatorType.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        var constructor = constructors.FirstOrDefault(candidate =>
            candidate.GetParameters().Length == options.Count
            && candidate.GetParameters().All(parameter => options.ContainsKey(parameter.ParameterType)))
            ?? throw new InvalidOperationException(
                $"No public constructor on '{driverLocatorType.FullName}' matches the registered ODBC options.");
        var parameters = constructor.GetParameters()
            .Select(parameter => options[parameter.ParameterType])
            .ToArray();

        return constructor.Invoke(parameters) as IDriverLocator
            ?? throw new InvalidCastException($"'{driverLocatorType.FullName}' is not an ODBC driver locator.");
    }

    /// <summary>
    /// Registers a locator type using the legacy constructor-reflection adapter.
    /// </summary>
    public void AddDriver(string alias, Type locator) => AddElement(alias, locator);

    /// <summary>
    /// Registers an ODBC driver locator with an explicit factory.
    /// </summary>
    public void AddDriver<TLocator>(string alias, Func<IDictionary<Type, object>, TLocator> factory)
        where TLocator : IDriverLocator
    {
        ArgumentNullException.ThrowIfNull(factory);
        AddElement(alias, typeof(TLocator));
        _factories.Add(alias, options => factory(options));
    }

    private void AddDrivers<TLocator>(IEnumerable<string> aliases, Func<IDictionary<Type, object>, TLocator> factory)
        where TLocator : IDriverLocator
    {
        foreach (var alias in aliases)
            AddDriver(alias, factory);
    }

    private static TOption GetOption<TOption>(IDictionary<Type, object> options, TOption defaultValue)
        where TOption : struct, Enum
        => options.TryGetValue(typeof(TOption), out var value) ? (TOption)value : defaultValue;
}
