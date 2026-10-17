using Microsoft.Extensions.Configuration;

namespace DubUrl.Worker;

internal sealed record SchemaWorkerPolicy(
    bool DeploymentEnabled,
    bool AllowDirectConnections,
    bool AllowDestructive)
{
    public static SchemaWorkerPolicy From(IConfiguration configuration)
        => new(
            Read(configuration, "Worker:Deployment:Enabled"),
            Read(configuration, "Worker:Deployment:AllowDirectConnections"),
            Read(configuration, "Worker:Deployment:AllowDestructive"));

    private static bool Read(IConfiguration configuration, string key)
        => bool.TryParse(configuration[key], out var value) && value;
}
