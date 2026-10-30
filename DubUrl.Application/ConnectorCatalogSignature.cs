using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DubUrl.Application;

/// <summary>A detached v1 RSA-PSS/SHA-256 signature. Expiry is UTC Unix seconds, or zero for no expiration.</summary>
public sealed record ConnectorCatalogSignature(
    int SchemaVersion, string SourceId, string KeyId, long Revision, long ExpiresAt, string Signature)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Signs exact catalog bytes and freshness metadata. The caller owns and protects the private key.</summary>
    public static byte[] Sign(byte[] catalog, RSA privateKey, string sourceId, string keyId, long revision, DateTimeOffset? expiresAt)
    {
        var document = new ConnectorCatalogSignature(1, sourceId.ToLowerInvariant(), keyId, revision, expiresAt?.ToUnixTimeSeconds() ?? 0, "");
        document.Validate();
        ValidateKeySize(privateKey);
        var signature = privateKey.SignData(document.Payload(catalog), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return JsonSerializer.SerializeToUtf8Bytes(document with { Signature = Convert.ToBase64String(signature) }, JsonOptions);
    }

    /// <summary>Stable identifier derived from the SHA-256 fingerprint of the public SubjectPublicKeyInfo.</summary>
    public static string GetKeyId(RSA key)
    {
        ValidateKeySize(key);
        return "sha256-" + Convert.ToHexString(SHA256.HashData(key.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    }

    internal static ConnectorCatalogSignature Read(byte[] bytes)
    {
        if (bytes.Length > 16384) throw new InvalidDataException();
        var document = JsonSerializer.Deserialize<ConnectorCatalogSignature>(bytes, JsonOptions) ?? throw new InvalidDataException();
        document.Validate();
        return document;
    }

    internal byte[] Payload(byte[] catalog)
    {
        // Fixed framing authenticates all metadata and the original bytes, without JSON canonicalization.
        var header = Encoding.UTF8.GetBytes("DubUrl.ConnectorCatalog/v1\n" + SourceId + "\n" + KeyId + "\n"
            + Revision.ToString(CultureInfo.InvariantCulture) + "\n" + ExpiresAt.ToString(CultureInfo.InvariantCulture) + "\n");
        var payload = new byte[header.Length + catalog.Length];
        header.CopyTo(payload, 0);
        catalog.CopyTo(payload, header.Length);
        return payload;
    }

    internal static bool IsSafeName(string? value)
        => value is not null && Regex.IsMatch(value, @"\A[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}\z");

    internal static void ImportPublicKey(RSA key, string pem)
    {
        var text = pem.Trim();
        var fields = PemEncoding.Find(text);
        if (text[fields.Label] != "PUBLIC KEY" || text[fields.Location] != text)
            throw new InvalidDataException();
        var bytes = Convert.FromBase64String(text[fields.Base64Data]);
        key.ImportSubjectPublicKeyInfo(bytes, out var consumed);
        if (consumed != bytes.Length) throw new InvalidDataException();
        ValidateKeySize(key);
    }

    private void Validate()
    {
        if (SchemaVersion != 1 || !IsSafeName(SourceId) || SourceId != SourceId.ToLowerInvariant()
            || !IsSafeName(KeyId) || Revision < 1 || ExpiresAt < 0)
            throw new InvalidDataException();
    }

    private static void ValidateKeySize(RSA key)
    {
        if (key.KeySize is < 2048 or > 8192) throw new InvalidDataException();
    }
}
