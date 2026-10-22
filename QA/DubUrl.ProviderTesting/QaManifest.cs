using System.Text.Json;

namespace DubUrl.ProviderTesting;

public static class QaManifest
{
    public static void Validate(string path, string provider, params string[] requiredCapabilities)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        RequireString(root, "provider", provider);
        RequireString(root, "contractVersion");
        RequireString(root, "version");
        RequireArray(root, "targetFrameworks");
        RequireArray(root, "operatingSystems");
        RequireArray(root, "rids");
        var capabilities = RequireArray(root, "capabilities")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        foreach (var capability in requiredCapabilities)
            if (!capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"Manifest '{path}' does not declare capability '{capability}'.");
    }

    private static void RequireString(JsonElement root, string name, string? expected = null)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException($"Manifest property '{name}' must be a non-empty string.");
        if (expected is not null && !string.Equals(value.GetString(), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Manifest property '{name}' must be '{expected}'.");
    }

    private static JsonElement RequireArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() == 0)
            throw new InvalidDataException($"Manifest property '{name}' must be a non-empty array.");
        return value;
    }
}
