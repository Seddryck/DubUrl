﻿using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.Tracing;
using System.Reflection;

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
    /// <param name="options">The <see cref="LiteDatabaseServiceOptions"/> instance used to configure this instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IServiceCollection AddDubUrl(this IServiceCollection services, DubUrlServiceOptions options)
        => services.AddDubUrl(configure =>
        {
            if (options.Logger != null)
                configure.Logger = options.Logger;
        });

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
    private static IServiceCollection AddDubUrlCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.AddOptions();
        services.AddOptions<DubUrlServiceOptions>()
            .Configure<IServiceProvider>((options, provider) =>
            {
                var factory = provider.GetService<ILoggerFactory>();
                if (factory != null)
                    options.Logger = factory.CreateLogger(DubUrlLoggerCategory);

                var configuration = provider.GetService<IConfiguration>();
                if (configuration != null)
                {
                    //Let's put up some configuration options here ;-)
                }
            });
        services.AddSingleton<ISchemeRegistry>(SchemeRegistryBuilder.GetDefault());
        services.AddSingleton<IDialectRegistry>(new DialectRegistryBuilder().Build());
        services.AddSingleton<ConnectionUrlFactory>();
        services.AddSingleton<IDatabaseUrlFactory, DatabaseUrlFactory>();
        services.AddSingleton<IQueryLogger>(NullQueryLogger.Instance);
        services.AddSingleton<CommandProvisionerFactory>();
        services.AddSingleton<IReflectionCache, ReflectionCache>();
        return services;
    }

    public static IServiceCollection WithQueryLogger(this IServiceCollection services, IQueryLogger logger)
    {
        services.Replace(new ServiceDescriptor(typeof(IQueryLogger), logger));
        return services;
    }

    public static IServiceCollection WithoutReflectionCache(this IServiceCollection services)
    {
        services.Replace(new ServiceDescriptor(typeof(IReflectionCache), typeof(NoneReflectionCache), ServiceLifetime.Singleton));
        return services;
    }

    public static IServiceCollection WithReflectionCache(this IServiceCollection services, IReflectionCache cache)
    {
        services.Replace(new ServiceDescriptor(typeof(IReflectionCache), cache));
        return services;
    }

    public static IServiceCollection WithMicroOrm(this IServiceCollection services)
    {
        services.Replace(new ServiceDescriptor(typeof(IDatabaseUrlFactory), typeof(MicroOrm.DatabaseUrlFactory), ServiceLifetime.Singleton));
        return services;
    }
}
