namespace DubUrl.Worker;

public sealed class WorkerMethodException : Exception
{
    public WorkerMethodException(
        int errorCode,
        string message,
        object? errorDetails = null)
        : base(message)
    {
        if (errorCode is >= -32768 and <= -32000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(errorCode),
                "Application error codes must not use the JSON-RPC reserved range from -32768 through -32000.");
        }

        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
    }

    public int ErrorCode { get; }

    public object? ErrorDetails { get; }
}
