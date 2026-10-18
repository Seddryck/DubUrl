using StreamJsonRpc;

namespace DubUrl.Worker;

public static class WorkerHost
{
    public static async Task RunAsync(
        Stream input,
        Stream output,
        WorkerMethodRegistry registry,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var lifetime = new WorkerLifetime();
        var formatter = new SystemTextJsonFormatter
        {
            JsonSerializerOptions = WorkerJson.CreateSerializerOptions(),
        };
        await using var messageHandler = new NewLineDelimitedMessageHandler(output, input, formatter)
        {
            NewLine = NewLineDelimitedMessageHandler.NewLineStyle.Lf,
        };
        using var rpc = new JsonRpc(new ProtocolErrorMessageHandler(messageHandler));

        var infrastructure = new WorkerInfrastructureMethods(registry, version, lifetime);
        rpc.AddLocalRpcTarget(infrastructure);
        registry.AddMethodsTo(rpc);
        rpc.CancelLocallyInvokedMethodsWhenConnectionIsClosed = true;
        rpc.StartListening();

        using var registration = cancellationToken.Register(lifetime.RequestShutdown);
        await Task.WhenAny(rpc.Completion, lifetime.ShutdownRequested);
    }

    private sealed class WorkerInfrastructureMethods(
        WorkerMethodRegistry registry,
        string version,
        WorkerLifetime lifetime)
    {
        [JsonRpcMethod("worker.capabilities")]
        public WorkerCapabilities GetCapabilities()
            => new(version,
            [
                new("worker.capabilities", "Lists the JSON-RPC methods registered by this worker."),
                new("worker.shutdown", "Requests graceful worker termination. Send as a notification."),
                .. registry.Capabilities,
            ],
            registry.Features);

        [JsonRpcMethod("worker.shutdown")]
        public void Shutdown() => lifetime.RequestShutdown();
    }

    private sealed class WorkerLifetime
    {
        private readonly TaskCompletionSource _shutdown = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ShutdownRequested => _shutdown.Task;

        public void RequestShutdown() => _shutdown.TrySetResult();
    }
}
