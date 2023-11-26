using System;
using System.Reflection;
using DubUrl.Querying.Parametrizing;
using Microsoft.Extensions.Logging;

namespace DubUrl.Extensions.DependencyInjection;

/// <summary>
/// An options class for configuring DubUrl services
/// </summary>
public class DubUrlServiceOptions
{
    public Type DatabaseUrlFactoryType { get; private set; } = typeof(DatabaseUrlFactory);

    /// <summary>
    /// Initializes a new instance of the <see cref="DubUrlServiceOptions"/> class.
    /// </summary>
    public DubUrlServiceOptions() { }

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    public ILogger? Logger { get; set; }

    public DubUrlServiceOptions WithMicroOrm()
    {
        DatabaseUrlFactoryType = typeof(MicroOrm.DatabaseUrlFactory);
        return this;
    }
}
