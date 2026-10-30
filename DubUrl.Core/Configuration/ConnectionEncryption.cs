namespace DubUrl;

/// <summary>Required TLS with certificate-chain and server-hostname verification.</summary>
public sealed record ConnectionEncryption
{
    public string? CaPath { get; }
    private ConnectionEncryption(string? caPath) => CaPath = caPath;

    /// <summary>Uses normal driver trust unless a custom PEM CA file is supplied.</summary>
    public static ConnectionEncryption Tls(string? caPath = null)
    {
        if (caPath is not null) ArgumentException.ThrowIfNullOrWhiteSpace(caPath);
        return new(caPath is null ? null : Path.GetFullPath(caPath));
    }
}
