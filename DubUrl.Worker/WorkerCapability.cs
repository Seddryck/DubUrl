namespace DubUrl.Worker;

public sealed record WorkerCapability(string Name, string Description);

public sealed record WorkerCapabilities(
    string Version,
    IReadOnlyList<WorkerCapability> Methods,
    IReadOnlyDictionary<string, object> Features);
