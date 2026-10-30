using System.Security.Cryptography.X509Certificates;
using System.Data.Common;
using System.Reflection;

namespace DubUrl.Authentication;

/// <summary>Owns file-loaded certificates. Dispose only after all connections using these options
/// (including connections that may be reopened) have been disposed. Caller-supplied certificates remain caller-owned.</summary>
public abstract class CertificateOptions : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private X509Certificate2? loaded;
    internal string PoolIdentity { get; } = "DubUrlCertificate_" + Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, DbConnection> pools = new();
    internal void RegisterPool(DbConnection connection)
    {
        var identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(connection.ConnectionString)));
        gate.Wait();
        try { CheckDisposed(); pools.TryAdd(identity, connection); }
        finally { gate.Release(); }
    }
    private bool disposed;
    internal string? ResolvedPassword { get; private set; }
    public IUsernamePasswordCredentialSource? SupplementalCredentials { get; init; }
    /// <summary>Only Password is used; Username is ignored. Separate from database credentials.</summary>
    public IUsernamePasswordCredentialSource? DecryptionPasswordSource { get; init; }
    protected abstract X509Certificate2 Load(string? password);
    protected virtual bool OwnsCertificate => true;
    protected virtual string? Password => null;
    internal void ValidateSelection()
    {
        if (Password is not null && DecryptionPasswordSource is not null)
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
    }
    private string? RequirePassword(string? value)
        => DecryptionPasswordSource is not null && value is null
            ? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials) : value;
    internal X509Certificate2 Get(AuthenticationContext context)
    {
        gate.Wait();
        try
        {
            CheckDisposed();
            if (loaded is not null) return loaded;
            var password = DecryptionPasswordSource is null ? Password
                : DecryptionPasswordSource is ISynchronousUsernamePasswordCredentialSource sync ? sync.Resolve(context).Password
                : throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired);
            return LoadChecked(RequirePassword(password));
        }
        finally { gate.Release(); }
    }
    internal async ValueTask<X509Certificate2> GetAsync(AuthenticationContext context, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            CheckDisposed();
            if (loaded is not null) return loaded;
            var password = DecryptionPasswordSource is null ? Password
                : (await DecryptionPasswordSource.ResolveAsync(context, token).ConfigureAwait(false)).Password;
            token.ThrowIfCancellationRequested();
            return LoadChecked(RequirePassword(password));
        }
        finally { gate.Release(); }
    }
    private void CheckDisposed()
    {
        if (disposed) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InvalidCertificate);
    }
    private X509Certificate2 LoadChecked(string? password)
    {
        X509Certificate2? certificate = null;
        try
        {
            certificate = Load(password);
            if (!certificate.HasPrivateKey) throw new InvalidOperationException();
            ResolvedPassword = password;
            return loaded = certificate;
        }
        catch
        {
            if (OwnsCertificate) certificate?.Dispose();
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InvalidCertificate);
        }
    }
    public void Dispose()
    {
        gate.Wait();
        try
        {
            if (disposed) return;
            disposed = true;
            try
            {
                foreach (var connection in pools.Values)
                {
                    var type = connection.GetType();
                    type.GetMethod("ClearPool", BindingFlags.Public | BindingFlags.Static, null, [type], null)?.Invoke(null, [connection]);
                }
            }
            finally
            {
                pools.Clear();
                if (OwnsCertificate) loaded?.Dispose();
                loaded = null; ResolvedPassword = null;
            }
        }
        finally { gate.Release(); }
        GC.SuppressFinalize(this);
    }
    public sealed override string ToString() => "CertificateOptions [REDACTED]";
}

public sealed class X509CertificateOptions : CertificateOptions
{
    private readonly X509Certificate2? certificate;
    internal string? CertificatePath { get; }
    internal string? KeyPath { get; }
    private readonly string? keyPassword;
    public X509CertificateOptions(X509Certificate2 certificate)
        => this.certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
    public X509CertificateOptions(string certificatePath, string keyPath, string? keyPassword = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        CertificatePath = Path.GetFullPath(certificatePath); KeyPath = Path.GetFullPath(keyPath); this.keyPassword = keyPassword;
    }
    internal bool IsUnencryptedPem => CertificatePath is not null && keyPassword is null && DecryptionPasswordSource is null;
    protected override bool OwnsCertificate => certificate is null;
    protected override string? Password => keyPassword;
    protected override X509Certificate2 Load(string? password)
    {
        if (certificate is not null) return certificate;
        using var pem = password is null ? X509Certificate2.CreateFromPemFile(CertificatePath!, KeyPath)
            : X509Certificate2.CreateFromEncryptedPemFile(CertificatePath!, password, KeyPath);
        // Reimport so Windows TLS can use the private key; never persist it to a machine/user key store.
#pragma warning disable SYSLIB0057
        return new X509Certificate2(pem.Export(X509ContentType.Pkcs12), (string?)null, X509KeyStorageFlags.EphemeralKeySet);
#pragma warning restore SYSLIB0057
    }
}

public sealed class Pkcs12CertificateOptions : CertificateOptions
{
    internal string BundlePath { get; }
    private readonly string? bundlePassword;
    public Pkcs12CertificateOptions(string path, string? bundlePassword = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        BundlePath = Path.GetFullPath(path); this.bundlePassword = bundlePassword;
    }
    protected override string? Password => bundlePassword;
#pragma warning disable SYSLIB0057
    protected override X509Certificate2 Load(string? password)
        => new(BundlePath, password, X509KeyStorageFlags.EphemeralKeySet);
#pragma warning restore SYSLIB0057
}
