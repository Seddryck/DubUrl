namespace DubUrl;

/// <summary>A provider reported a timeout while executing a database command or reading its results.</summary>
public sealed class DubUrlCommandTimeoutException(Exception innerException)
    : TimeoutException("The database command timed out.", innerException);
