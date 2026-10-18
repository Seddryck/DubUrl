namespace DubUrl;

/// <summary>Runs cleanup once and lets every disposer observe the same completion or failure.</summary>
internal sealed class DisposalCompletion
{
    private readonly object _sync = new();
    private Task? _completion;

    public ValueTask Run(Func<ValueTask> cleanup)
    {
        TaskCompletionSource completion;
        lock (_sync)
        {
            if (_completion is not null) return new ValueTask(_completion);
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion.Task;
        }
        // Publish the task before invoking provider code, without holding our lock across cleanup.
        _ = CompleteAsync(cleanup, completion);
        return new ValueTask(completion.Task);
    }

    private static async Task CompleteAsync(Func<ValueTask> cleanup, TaskCompletionSource completion)
    {
        try
        {
            await cleanup().ConfigureAwait(false);
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
