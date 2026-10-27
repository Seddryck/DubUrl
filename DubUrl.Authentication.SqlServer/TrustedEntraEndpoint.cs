namespace DubUrl.Authentication.SqlServer;

internal static class TrustedEntraEndpoint
{
    internal static void ValidateAuthority(Uri configured, string? requested)
    {
        if (string.IsNullOrEmpty(requested)) return;
        if (!Uri.TryCreate(requested, UriKind.Absolute, out var candidate) || candidate.Scheme != "https"
            || !candidate.IsDefaultPort || candidate.UserInfo.Length != 0 || candidate.Query.Length != 0 || candidate.Fragment.Length != 0)
            throw new ConnectionAuthenticationException("CredentialConflict");
        var sameHost = string.Equals(candidate.Host, configured.Host, StringComparison.OrdinalIgnoreCase);
        var publicCloudAlias = candidate.Host is "login.windows.net" or "login.microsoftonline.com"
            && configured.Host is "login.windows.net" or "login.microsoftonline.com";
        var configuredTenant = configured.AbsolutePath.Trim('/');
        var requestedPath = candidate.AbsolutePath.Trim('/');
        if ((!sameHost && !publicCloudAlias) || (requestedPath != configuredTenant
            && requestedPath != configuredTenant + "/oauth2/authorize" && requestedPath != configuredTenant + "/oauth2/v2.0/authorize"))
            throw new ConnectionAuthenticationException("CredentialConflict");
    }
}
