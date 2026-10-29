namespace DubUrl;

/// <summary>Settings eligible for explicit optional configuration.</summary>
public enum ConnectionConfigurationOption
{
    ApplicationName,
    Pooling,
    ConnectionTimeout,
    CommandTimeout
}

/// <summary>Credential-free information about an unsupported optional setting.</summary>
public sealed record SkippedConnectionOption(ConnectionConfigurationOption Option, string Provider, string Reason);

internal sealed record ConnectionConfigurationRequest(ConnectionConfigurationOption Option, object Value, bool Optional);
