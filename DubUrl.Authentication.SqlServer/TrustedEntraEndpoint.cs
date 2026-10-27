namespace DubUrl.Authentication.SqlServer;

internal static class TrustedEntraEndpoint
{
    internal static void ValidateAuthority(Uri configured, string? requested)
    {
        if (string.IsNullOrEmpty(requested)) return;
        var candidate = ParseHttps(requested);
        if (!MatchesHost(configured, candidate) || !MatchesTenant(configured, candidate))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
    }
    private static Uri ParseHttps(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate) || candidate.Scheme != "https"
            || !candidate.IsDefaultPort || candidate.UserInfo.Length != 0 || candidate.Query.Length != 0 || candidate.Fragment.Length != 0)
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        return candidate;
    }
    private static bool MatchesHost(Uri configured, Uri candidate)
        => string.Equals(candidate.Host, configured.Host, StringComparison.OrdinalIgnoreCase)
            || (IsPublicCloudHost(candidate.Host) && IsPublicCloudHost(configured.Host));
    private static bool IsPublicCloudHost(string host) => host is "login.windows.net" or "login.microsoftonline.com";
    private static bool MatchesTenant(Uri configured, Uri candidate)
    {
        var tenant = configured.AbsolutePath.Trim('/');
        var path = candidate.AbsolutePath.Trim('/');
        return path == tenant || path == tenant + "/oauth2/authorize" || path == tenant + "/oauth2/v2.0/authorize";
    }
}
