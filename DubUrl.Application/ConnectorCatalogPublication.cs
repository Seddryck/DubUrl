using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DubUrl.Application;

public sealed record SignedConnectorCatalog(byte[] Signature, string KeyId, long Revision, DateTimeOffset? ExpiresAt);

/// <summary>Signs with derived key identity, optional expiry, and a durable automatic per-source revision.</summary>
public static class ConnectorCatalogPublication
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

    public static async Task<SignedConnectorCatalog> SignAsync(byte[] catalog, RSA privateKey, string sourceId,
        DateTimeOffset? expiresAt = null, bool noExpiration = false, string? publisherStatePath = null,
        CancellationToken cancellationToken = default)
    {
        if (!ConnectorCatalogSignature.IsSafeName(sourceId) || (noExpiration && expiresAt is not null)) throw new InvalidDataException();
        DateTimeOffset? expiry = noExpiration ? null : expiresAt ?? DateTimeOffset.UtcNow.Add(DefaultLifetime);
        if (expiry is not null && expiry <= DateTimeOffset.UtcNow) throw new InvalidDataException();
        var keyId = ConnectorCatalogSignature.GetKeyId(privateKey);
        var revision = await ReserveAsync(publisherStatePath ?? DefaultStatePath(), sourceId,
            Math.Max(1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), cancellationToken).ConfigureAwait(false);
        // Revisions are reserved durably before signing. Failed publication may leave gaps; numbers are never reused.
        var signature = ConnectorCatalogSignature.Sign(catalog, privateKey, sourceId, keyId, revision, expiry);
        return new(signature, keyId, revision, expiry);
    }

    internal static async Task<long> ReserveAsync(string rootPath, string sourceId, long clockFloor, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(rootPath);
        var name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceId.ToLowerInvariant())));
        var path = Path.Combine(rootPath, name + ".json");
        await using var stateLock = await ConnectorCatalogRevisionStore.AcquireLockAsync(path + ".lock", cancellationToken).ConfigureAwait(false);
        long previous = 0;
        if (File.Exists(path))
        {
            var state = JsonSerializer.Deserialize<PublisherState>(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidDataException();
            if (state.SchemaVersion != 1 || state.Purpose != "catalog-publisher" || state.Revision < 1) throw new InvalidDataException();
            previous = state.Revision;
        }
        if (previous == long.MaxValue || clockFloor < 1) throw new InvalidDataException();
        var next = Math.Max(previous + 1, clockFloor);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, new PublisherState(1, "catalog-publisher", next),
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
            return next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string DefaultStatePath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(local)) local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return Path.Combine(local, "DubUrl", "catalog-publication-revisions");
    }

    private sealed record PublisherState(int SchemaVersion, string Purpose, long Revision);
}
