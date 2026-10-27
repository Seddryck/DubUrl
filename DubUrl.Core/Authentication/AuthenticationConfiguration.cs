using DubUrl.Parsing;

namespace DubUrl.Authentication;

internal static class AuthenticationConfiguration
{
    private static readonly HashSet<string> CredentialOptions = new((
        "uid userid user username pwd passwd password authentication authenticator integratedsecurity trustedconnection sspi " +
        "passfile requireauth sslcert sslclientcertificate clientcertificatekey sslclientkey accesstoken token clientsecret clientid " +
        "clientcertificate sslcertificate sslkey sslpassword certificatefile certificatepassword certificatestorelocation " +
        "certificatethumbprint certificate privatekey privatekeyfile clientstoretemporarycredential").Split(' '), StringComparer.Ordinal);

    private static string Normalize(string key) => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    internal static void Validate(ConnectionUri uri, ConnectionAuthentication strategy)
    {
        if (uri.HasPassword) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        if (RejectsUsername(strategy.Kind) && uri.HasUsername) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        foreach (var option in uri.Options)
        {
            var key = Normalize(option.Key);
            if (IsCompatibleOperatingSystemOption(strategy.Kind, key, option.Value)) continue;
            if (IsCredentialOption(key)) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        }
    }

    private static bool RejectsUsername(ConnectionAuthenticationKind kind)
        => kind is ConnectionAuthenticationKind.NativeToken or ConnectionAuthenticationKind.InteractiveOAuth;
    private static bool IsCompatibleOperatingSystemOption(ConnectionAuthenticationKind kind, string key, string value)
        => kind == ConnectionAuthenticationKind.OperatingSystemIdentity
            && key is "integratedsecurity" or "trustedconnection"
            && value.ToLowerInvariant() is "true" or "sspi" or "yes";
    private static bool IsCredentialOption(string key)
        => CredentialOptions.Contains(key) || key.StartsWith("oauth", StringComparison.Ordinal)
            || key.EndsWith("password", StringComparison.Ordinal) || key.EndsWith("token", StringComparison.Ordinal)
            || key.EndsWith("secret", StringComparison.Ordinal);

    internal static ConnectionUri Apply(ConnectionUri uri, ConnectionAuthentication strategy, UsernamePasswordCredentials? credentials)
    {
        if (credentials is null)
        {
            if (strategy.Kind == ConnectionAuthenticationKind.UsernamePassword) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
            return CopyWithCredentials(uri, uri.Username, "", uri.HasUsername, false);
        }
        ValidateCredentials(uri, credentials);
        var password = credentials.Password ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
        return CopyWithCredentials(uri, credentials.Username ?? uri.Username, password, credentials.Username is not null || uri.HasUsername, true);
    }

    private static void ValidateCredentials(ConnectionUri uri, UsernamePasswordCredentials credentials)
    {
        if (uri.HasUsername && credentials.Username is not null && uri.Username != credentials.Username)
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        if (credentials.Password is null) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
        if (credentials.Username is null && !uri.HasUsername) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
    }

    private static ConnectionUri CopyWithCredentials(ConnectionUri uri, string username, string password, bool hasUsername, bool hasPassword)
        => new()
        {
            Scheme = uri.Scheme, Host = uri.Host, Port = uri.Port, Segments = uri.Segments, Options = uri.Options,
            Username = username, Password = password, HasUsername = hasUsername, HasPassword = hasPassword
        };

    internal static void ValidatePasswordProvider(string provider)
    {
        if (provider is not ("Microsoft.Data.SqlClient" or "Npgsql" or "MySqlConnector" or "MySql.Data.MySqlClient"
            or "MySql.Data" or "SingleStoreConnector" or "Oracle.ManagedDataAccess" or "FirebirdSql.Data.FirebirdClient"
            or "Snowflake.Data" or "IBM.Data.Db2" or "Teradata.Client" or "Teradata.Client.Provider"))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
    }
}
