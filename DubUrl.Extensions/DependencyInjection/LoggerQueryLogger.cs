using DubUrl.Querying;
using Microsoft.Extensions.Logging;

namespace DubUrl.Extensions.DependencyInjection;

internal sealed class LoggerQueryLogger(ILogger logger) : IQueryLogger
{
    public void Log(string message) => logger.LogDebug("{Query}", message);
}
