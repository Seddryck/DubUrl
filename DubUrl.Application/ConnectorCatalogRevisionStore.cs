using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DubUrl.Application;

/// <summary>Per-source high-water marks. The deployment must protect this directory and retain it across key rotation.</summary>
internal sealed class ConnectorCatalogRevisionStore(string rootPath)
{
    public Task CheckAsync(string sourceId, long revision, string payloadSha256, CancellationToken cancellationToken)
        => ProcessAsync(sourceId, revision, payloadSha256, false, cancellationToken);

    public Task AcceptAsync(string sourceId, long revision, string payloadSha256, CancellationToken cancellationToken)
        => ProcessAsync(sourceId, revision, payloadSha256, true, cancellationToken);

    private async Task ProcessAsync(string sourceId, long revision, string payloadSha256, bool persist, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(rootPath);
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceId.ToLowerInvariant())));
        var path = Path.Combine(rootPath, name + ".json");
        await using var storeLock = await AcquireLockAsync(path + ".lock", cancellationToken).ConfigureAwait(false);
        if (File.Exists(path))
        {
            var state = JsonSerializer.Deserialize<RevisionState>(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidDataException();
            if (state.SchemaVersion != 1 || state.Revision < 1 || state.PayloadSha256 is not { Length: 64 }
                || !state.PayloadSha256.All(Uri.IsHexDigit))
                throw new InvalidDataException();
            if (revision < state.Revision || (revision == state.Revision && payloadSha256 != state.PayloadSha256))
                throw new InvalidDataException();
            if (revision == state.Revision) return;
        }
        if (!persist) return;

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, new RevisionState(1, revision, payloadSha256),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task<FileStream> AcquireLockAsync(string path, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private sealed record RevisionState(int SchemaVersion, long Revision, string PayloadSha256);
}
