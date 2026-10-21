using System;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using Microsoft.Extensions.Logging;

namespace DubUrl.Extensions.DependencyInjection;

/// <summary>
/// An options class for configuring DubUrl services
/// </summary>
public class DubUrlServiceOptions
{
    public Type DatabaseUrlFactoryType { get; private set; } = typeof(DubUrl.DatabaseUrlFactory);

    /// <summary>
    /// Initializes a new instance of the <see cref="DubUrlServiceOptions"/> class.
    /// </summary>
    public DubUrlServiceOptions() { }

    /// <summary>
    /// Receives query messages at Debug level unless an explicit QueryLogger is supplied.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>An explicit query logger, taking precedence over Microsoft.Extensions.Logging.</summary>
    public IQueryLogger? QueryLogger { get; set; }

    /// <summary>A custom reflection cache; null selects the default cache.</summary>
    public IReflectionCache? ReflectionCache { get; set; }

    public DubUrlServiceOptions WithMicroOrm()
    {
        DatabaseUrlFactoryType = typeof(MicroOrm.DatabaseUrlFactory);
        return this;
    }

    public DubUrlServiceOptions WithQueryLogger(IQueryLogger logger)
    {
        QueryLogger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    public DubUrlServiceOptions WithoutReflectionCache()
        => WithReflectionCache(new NoneReflectionCache());

    public DubUrlServiceOptions WithReflectionCache(IReflectionCache cache)
    {
        ReflectionCache = cache ?? throw new ArgumentNullException(nameof(cache));
        return this;
    }

    internal void CopyFrom(DubUrlServiceOptions options)
        => (DatabaseUrlFactoryType, Logger, QueryLogger, ReflectionCache) =
            (options.DatabaseUrlFactoryType, options.Logger, options.QueryLogger, options.ReflectionCache);
}
