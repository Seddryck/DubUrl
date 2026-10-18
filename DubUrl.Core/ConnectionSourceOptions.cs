namespace DubUrl;

public enum ConnectionReuseMode
{
    ProviderManaged,
    RetainedExclusive,
    Auto
}

public sealed class ConnectionSourceOptions
{
    /// <summary>Defaults to automatic selection; unknown providers use ProviderManaged.</summary>
    public ConnectionReuseMode ReuseMode { get; init; } = ConnectionReuseMode.Auto;
    /// <summary>Ordered application/provider-extension policies, used only in Auto mode.</summary>
    public IReadOnlyList<IConnectionReusePolicyResolver> PolicyResolvers { get; init; } = [];
}
