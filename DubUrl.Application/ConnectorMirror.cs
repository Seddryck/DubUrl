using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Core.ProviderPacks;

namespace DubUrl.Application;

public sealed record ConnectorMirrorSelection(string Id, string Version);

/// <summary>Prepares a new, unpublished directory of immutable archives and a replacement catalog.</summary>
/// <remarks>The caller must approve the upstream catalog and owns the HTTP client, including authentication.
/// No catalog or archive is published to a server by this operation.</remarks>
public sealed class ConnectorMirror(HttpClient httpClient)
{
    public async Task PrepareAsync(
        ProviderPackCatalog approvedCatalog,
        IEnumerable<ConnectorMirrorSelection> selections,
        Uri archiveBaseUri,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approvedCatalog);
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(archiveBaseUri);
        if (!archiveBaseUri.IsAbsoluteUri || archiveBaseUri.Scheme != Uri.UriSchemeHttps
            || archiveBaseUri.UserInfo.Length != 0 || archiveBaseUri.Query.Length != 0 || archiveBaseUri.Fragment.Length != 0)
            throw new ConnectorOperationException("mirror-url-invalid", "The mirror requires an absolute HTTPS base URL without credentials, query, or fragment.");

        var entries = new List<ConnectorDefinition>();
        foreach (var selection in selections)
        {
            if (!approvedCatalog.TryGet(selection.Id, out var entry) || entry is null || entry.Version != selection.Version)
                throw new ConnectorOperationException("mirror-selection-invalid", "Every selection must identify an approved connector and its exact pinned version.");
            if (!entries.Any(existing => existing.Provider.Equals(entry.Provider, StringComparison.OrdinalIgnoreCase)))
                entries.Add(entry);
        }
        if (entries.Count == 0)
            throw new ConnectorOperationException("mirror-selection-invalid", "Select at least one pinned connector.");

        // Validate distribution metadata even when the host constructed the catalog in memory.
        ValidateCatalog(entries);
        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        var staging = destination + "." + Guid.NewGuid().ToString("N") + ".staging";
        try
        {
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new ConnectorOperationException("mirror-output-exists", "Prepare each catalog snapshot in a new output directory.");
            Directory.CreateDirectory(staging);
            var mirrored = new List<ConnectorDefinition>();
            var baseUri = new Uri(archiveBaseUri.AbsoluteUri.TrimEnd('/') + "/");
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = entry.Sha256!.ToLowerInvariant() + ".zip";
                var path = Path.Combine(staging, name);
                if (!File.Exists(path))
                {
                    await using var source = await httpClient.GetStreamAsync(entry.DownloadUri, cancellationToken).ConfigureAwait(false);
                    await using (var target = File.Create(path))
                        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                    await using var archive = File.OpenRead(path);
                    var digest = Convert.ToHexString(await SHA256.HashDataAsync(archive, cancellationToken).ConfigureAwait(false));
                    if (!digest.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new ConnectorOperationException("pack-integrity", "An upstream connector archive does not match its approved SHA-256 digest.");
                }
                mirrored.Add(entry with { DownloadUri = new Uri(baseUri, name) });
            }
            ValidateCatalog(mirrored);
            await using (var catalog = File.Create(Path.Combine(staging, "connector-catalog.json")))
                await JsonSerializer.SerializeAsync(catalog,
                    new ProviderPackCatalogDocument(ProviderPackCatalogDocument.CurrentSchemaVersion, mirrored),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new ConnectorOperationCanceledException(cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new ConnectorOperationException("mirror-download-failed", "An approved upstream archive could not be downloaded.");
        }
        catch (OperationCanceledException)
        {
            throw new ConnectorOperationException("mirror-download-failed", "An upstream archive download timed out.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ConnectorOperationException("mirror-write-failed", "The mirror snapshot could not be written.");
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static void ValidateCatalog(IReadOnlyList<ConnectorDefinition> entries)
    {
        try
        {
            using var buffer = new MemoryStream();
            JsonSerializer.Serialize(buffer, new ProviderPackCatalogDocument(ProviderPackCatalogDocument.CurrentSchemaVersion, entries),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            buffer.Position = 0;
            ProviderPackCatalog.Read(buffer);
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException or JsonException)
        {
            throw new ConnectorOperationException("connector-catalog-invalid", "The approved connector catalog has invalid distribution metadata.");
        }
    }
}
