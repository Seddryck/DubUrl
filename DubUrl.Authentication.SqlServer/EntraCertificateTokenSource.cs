using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;

namespace DubUrl.Authentication.SqlServer;

/// <summary>Application-owned MSAL confidential client with explicit trusted authority and SQL audience.</summary>
public sealed class EntraCertificateTokenSource : ISqlServerTokenSource
{
    private readonly IConfidentialClientApplication application;
    private readonly Uri authority;
    private readonly string resource;

    public EntraCertificateTokenSource(string clientId, Uri authority, Uri sqlResource, X509Certificate2 certificate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ValidateHttps(authority);
        ValidateHttps(sqlResource);
        ArgumentNullException.ThrowIfNull(certificate);
        try { if (!certificate.HasPrivateKey) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InvalidCertificate); }
        catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InvalidCertificate); }
        this.authority = authority;
        resource = sqlResource.AbsoluteUri.TrimEnd('/');
        application = ConfidentialClientApplicationBuilder.Create(clientId)
            .WithAuthority(authority.AbsoluteUri).WithCertificate(certificate).Build();
    }

    public async ValueTask<SqlAuthenticationToken> GetTokenAsync(SqlAuthenticationParameters request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRequest(request.Resource, request.Authority);
        try
        {
            var result = await application.AcquireTokenForClient([resource + "/.default"])
                .ExecuteAsync(cancellationToken).ConfigureAwait(false);
            return new(result.AccessToken, result.ExpiresOn);
        }
        catch (OperationCanceledException) { throw; }
        catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed); }
    }

    public void ValidateRequest(string requestedResource, string? requestedAuthority)
    {
        if (requestedResource.TrimEnd('/') != resource)
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        TrustedEntraEndpoint.ValidateAuthority(authority, requestedAuthority);
    }

    private static void ValidateHttps(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Explicit trusted HTTPS authority and SQL audience are required.");
    }
    public override string ToString() => "EntraCertificateTokenSource [REDACTED]";
}
