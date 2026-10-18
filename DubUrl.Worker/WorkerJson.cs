using System.Text.Json;

namespace DubUrl.Worker;

internal static class WorkerJson
{
    public static JsonSerializerOptions CreateSerializerOptions()
        => new(JsonSerializerDefaults.Web);
}
