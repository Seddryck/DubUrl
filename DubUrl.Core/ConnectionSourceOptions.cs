namespace DubUrl;

public enum ConnectionReuseMode
{
    ProviderManaged,
    RetainedExclusive
}

public sealed class ConnectionSourceOptions
{
    public ConnectionReuseMode ReuseMode { get; init; } = ConnectionReuseMode.ProviderManaged;
}
