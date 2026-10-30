using System.IO.Compression;
using System.Text.Json;

namespace DubUrl.Core.ProviderPacks;

/// <summary>Inspects archive structure and metadata without extracting files or loading provider code.</summary>
public static class ProviderPackArchive
{
    public static async Task<ConnectorDefinition> ReadDefinitionAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var files = new HashSet<string>(StringComparer.Ordinal);
        var paths = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000
                || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException();
            var name = entry.FullName.Replace('\\', '/');
            var directory = name.EndsWith('/');
            var path = Normalize(directory ? name[..^1] : name);
            if (!paths.TryAdd(path, directory)) throw new InvalidDataException();
            if (!directory) files.Add(path);
        }
        foreach (var path in paths.Keys)
        {
            var parent = path;
            while (parent.LastIndexOf('/') is var index && index >= 0)
            {
                parent = parent[..index];
                if (paths.TryGetValue(parent, out var directory) && !directory) throw new InvalidDataException();
            }
        }
        var metadata = archive.GetEntry(ConnectorDefinition.FileName) ?? throw new InvalidDataException();
        if (metadata.Length > 1024 * 1024) throw new InvalidDataException();
        ConnectorDefinition definition;
        using (var manifest = metadata.Open())
            definition = await JsonSerializer.DeserializeAsync<ConnectorDefinition>(manifest,
                new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken).ConfigureAwait(false) ?? throw new InvalidDataException();
        if (!definition.HasCompleteMetadata()) throw new InvalidDataException();
        ProviderPackStore.ValidateIdentifier(definition.Provider, nameof(definition.Provider));
        ProviderPackStore.ValidateIdentifier(definition.Version, nameof(definition.Version));
        foreach (var required in ProviderPackStore.RequiredAssemblyPaths(definition))
            if (!files.Contains(Normalize(required))) throw new InvalidDataException();
        // Read all payloads to detect unreadable/truncated compression streams, including non-module assets.
        foreach (var entry in archive.Entries.Where(entry => !entry.FullName.EndsWith('/')))
        {
            using var content = entry.Open();
            await content.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
        }
        return definition;
    }

    private static string Normalize(string path)
    {
        var normalized = ProviderPackStore.ValidateRelativePath(path).Replace('\\', '/');
        foreach (var part in normalized.Split('/'))
        {
            if (part.Any(character => character < 32 || "<>\"|?*".Contains(character))) throw new InvalidDataException();
            var stem = part.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL"
                || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '1' and <= '9'))
                throw new InvalidDataException();
        }
        return normalized;
    }
}
