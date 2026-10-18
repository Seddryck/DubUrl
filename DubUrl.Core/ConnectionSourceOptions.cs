namespace DubUrl;

public enum ConnectionReuseMode
{
    ProviderManaged,
    RetainedExclusive,
    Auto
}

public sealed class ConnectionSourceOptions
{
    public ConnectionReuseMode ReuseMode { get; init; } = ConnectionReuseMode.ProviderManaged;
    /// <summary>Ordered application/provider-extension policies, used only in Auto mode.</summary>
    public IReadOnlyList<IConnectionReusePolicyResolver> PolicyResolvers { get; init; } = [];
}
