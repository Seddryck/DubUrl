using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DubUrl.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up DubUrl services in an <see cref="T:Microsoft.Extensions.DependencyInjection.IServiceCollection" />.
/// </summary>
public static class DubUrlServiceExtensions
{
    public const string DubUrlLoggerCategory = "DubUrl";

    /// <summary>
    /// Adds a singleton <see cref="DubUrl" /> service implementation to the services collection
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddDubUrl(this IServiceCollection services)
        => (services ?? throw new ArgumentNullException(nameof(services)))
            .AddDubUrlCore();

    /// <summary>
    /// Adds DubUrl using explicitly registered provider modules.
    /// </summary>
    public static IServiceCollection AddDubUrl(
        this IServiceCollection services,
        IDubUrlProviderModule module,
        params IDubUrlProviderModule[] additionalModules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(additionalModules);

        var catalog = new ProviderCatalogBuilder().AddModule(module);
        foreach (var additionalModule in additionalModules)
            catalog.AddModule(additionalModule);
        return services.AddDubUrlCore(catalog.Build());
    }

    /// <summary>
    /// Adds a singleton <see cref="DubUrl" /> service implementation to the services collection
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="configure">The action used to configure this instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddDubUrl(this IServiceCollection services, Action<DubUrlServiceOptions> configure)
        => (services ?? throw new ArgumentNullException(nameof(services)))
            .AddDubUrlCore()
            .Configure(configure ?? throw new ArgumentNullException(nameof(configure)));

    /// <summary>
    /// Adds a singleton <see cref="DubUrl" /> service implementation to the services collection
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="options">The options used to configure the services.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddDubUrl(this IServiceCollection services, DubUrlServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return services.AddDubUrl(configure => configure.CopyFrom(options));
    }

    /// <summary>
    /// Adds a singleton <see cref="DubUrl" /> service implementation to the services collection
    /// </summary>
    /// <typeparam name="T">The <see cref="IConfigureOptions{DubUrlServiceOptions}"/> type that will configure options.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that calls can be chained.</returns>
    /// <exception cref="System.ArgumentNullException">services</exception>
    public static IServiceCollection AddDubUrl<T>(this IServiceCollection services) where T : IConfigureOptions<DubUrlServiceOptions>
        => (services ?? throw new ArgumentNullException(nameof(services)))
            .AddDubUrlCore()
            .ConfigureOptions(typeof(T));

    /// <summary>
    /// Adds the core services for DubUrl.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <exception cref="ArgumentNullException">services</exception>
    private static IServiceCollection AddDubUrlCore(this IServiceCollection services, ISchemeRegistry? schemeRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.AddOptions();
        services.AddOptions<DubUrlServiceOptions>()
            .PostConfigure<IServiceProvider>((options, provider) =>
            {
                options.Logger ??= provider.GetService<ILoggerFactory>()?.CreateLogger(DubUrlLoggerCategory);
            });
        if (schemeRegistry is not null)
            services.AddSingleton(schemeRegistry);
        else
            // Each resolution returns Core's current cached snapshot; this does not rebuild it.
            services.TryAddTransient<ISchemeRegistry>(_ => SchemeRegistryBuilder.GetDefault());
        services.AddSingleton<IDialectRegistry>(new DialectRegistryBuilder().Build());
        services.AddSingleton(provider => new ConnectionUrlFactory(
            () => provider.GetRequiredService<ISchemeRegistry>()));
        services.TryAddSingleton<IDatabaseUrlFactory>(provider =>
            (IDatabaseUrlFactory)ActivatorUtilities.CreateInstance(provider,
                provider.GetRequiredService<IOptions<DubUrlServiceOptions>>().Value.DatabaseUrlFactoryType));
        services.TryAddSingleton<IQueryLogger>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DubUrlServiceOptions>>().Value;
            return options.QueryLogger ?? (options.Logger is { } logger
                ? new LoggerQueryLogger(logger) : NullQueryLogger.Instance);
        });
        services.AddSingleton<CommandProvisionerFactory>();
        services.TryAddSingleton<IReflectionCache>(provider =>
            provider.GetRequiredService<IOptions<DubUrlServiceOptions>>().Value.ReflectionCache ?? new ReflectionCache());
        return services;
    }

    public static IServiceCollection WithQueryLogger(this IServiceCollection services, IQueryLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return services.Configure<DubUrlServiceOptions>(options => options.WithQueryLogger(logger));
    }

    public static IServiceCollection WithoutReflectionCache(this IServiceCollection services)
    {
        return services.Configure<DubUrlServiceOptions>(options => options.WithoutReflectionCache());
    }

    public static IServiceCollection WithReflectionCache(this IServiceCollection services, IReflectionCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        return services.Configure<DubUrlServiceOptions>(options => options.WithReflectionCache(cache));
    }

    public static IServiceCollection WithMicroOrm(this IServiceCollection services)
    {
        return services.Configure<DubUrlServiceOptions>(options => options.WithMicroOrm());
    }
}
