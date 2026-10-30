namespace DubUrl;

/// <summary>Records optional settings. Security and correctness settings are deliberately unavailable.</summary>
public sealed class OptionalConnectionOptions
{
    private readonly List<ConnectionConfigurationRequest> requests = [];
    internal OptionalConnectionOptions() { }
    internal ConnectionConfigurationRequest[] Snapshot() => requests.ToArray();

    public OptionalConnectionOptions WithApplicationName(string applicationName)
        => Add(ConnectionConfigurationOption.ApplicationName, ValidateApplicationName(applicationName));

    public OptionalConnectionOptions WithPooling(ConnectionPoolingOptions options)
        => Add(ConnectionConfigurationOption.Pooling, ValidatePooling(options));

    /// <summary>Zero requests the provider's infinite timeout. Positive durations round up to seconds.</summary>
    public OptionalConnectionOptions WithConnectionTimeout(TimeSpan timeout)
        => Add(ConnectionConfigurationOption.ConnectionTimeout, ValidateTimeout(timeout));

    /// <summary>Sets the native default for commands; this is not an end-to-end deadline.</summary>
    public OptionalConnectionOptions WithCommandTimeout(TimeSpan timeout)
        => Add(ConnectionConfigurationOption.CommandTimeout, ValidateTimeout(timeout));

    private OptionalConnectionOptions Add(ConnectionConfigurationOption option, object value)
    {
        requests.Add(new(option, value, true));
        return this;
    }

    internal static string ValidateApplicationName(string applicationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        if (applicationName.Any(char.IsControl)) throw new ArgumentException("Application names cannot contain control characters.", nameof(applicationName));
        return applicationName;
    }

    internal static ConnectionPoolingOptions ValidatePooling(ConnectionPoolingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        return options;
    }

    internal static int ValidateTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero || timeout.TotalSeconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        return checked((int)Math.Ceiling(timeout.TotalSeconds));
    }
}
