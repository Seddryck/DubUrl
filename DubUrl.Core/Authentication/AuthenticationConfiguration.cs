using DubUrl.Parsing;

namespace DubUrl.Authentication;

internal static class AuthenticationConfiguration
{
    // Normalize aliases before comparing. Fail closed for modes/credential-bearing option aliases.
    private static string Normalize(string key) => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    internal static void Validate(ConnectionUri uri, ConnectionAuthentication strategy)
    {
        if (uri.HasPassword) throw new ConnectionAuthenticationException("CredentialConflict");
        if (strategy.Kind is ConnectionAuthenticationKind.NativeToken or ConnectionAuthenticationKind.InteractiveOAuth && uri.HasUsername)
            throw new ConnectionAuthenticationException("CredentialConflict");
        foreach (var option in uri.Options)
        {
            var key = Normalize(option.Key);
            if (strategy.Kind == ConnectionAuthenticationKind.OperatingSystemIdentity
                && key is "integratedsecurity" or "trustedconnection"
                && option.Value.ToLowerInvariant() is "true" or "sspi" or "yes") continue;
            if (key is "uid" or "userid" or "user" or "username" or "pwd" or "passwd" or "password"
                or "authentication" or "authenticator" or "integratedsecurity" or "trustedconnection" or "sspi"
                or "passfile" or "requireauth" or "sslcert" or "sslclientcertificate" or "clientcertificatekey" or "sslclientkey"
                or "accesstoken" or "token" or "clientsecret" or "clientid" or "clientcertificate" or "sslcertificate"
                or "sslkey" or "sslpassword" or "certificatefile" or "certificatepassword" or "certificatestorelocation"
                or "certificatethumbprint" or "certificate" or "privatekey" or "privatekeyfile"
                || key.StartsWith("oauth", StringComparison.Ordinal) || key == "clientstoretemporarycredential"
                || key.EndsWith("password", StringComparison.Ordinal) || key.EndsWith("token", StringComparison.Ordinal)
                || key.EndsWith("secret", StringComparison.Ordinal))
                throw new ConnectionAuthenticationException("CredentialConflict");
        }
    }

    internal static ConnectionUri Apply(ConnectionUri uri, ConnectionAuthentication strategy, UsernamePasswordCredentials? credentials)
    {
        if (credentials is not null)
        {
            if (uri.HasUsername && credentials.Username is not null && uri.Username != credentials.Username)
                throw new ConnectionAuthenticationException("CredentialConflict");
            if (credentials.Password is null || (credentials.Username is null && !uri.HasUsername))
                throw new ConnectionAuthenticationException("MissingCredentials");
        }
        else if (strategy.Kind == ConnectionAuthenticationKind.UsernamePassword)
            throw new ConnectionAuthenticationException("MissingCredentials");
        return new ConnectionUri
        {
            Scheme = uri.Scheme, Host = uri.Host, Port = uri.Port, Segments = uri.Segments, Options = uri.Options,
            Username = credentials?.Username ?? uri.Username, Password = credentials?.Password ?? "",
            HasUsername = credentials?.Username is not null || uri.HasUsername, HasPassword = credentials?.Password is not null
        };
    }

    internal static void ValidatePasswordProvider(string provider)
    {
        if (provider is not ("Microsoft.Data.SqlClient" or "Npgsql" or "MySqlConnector" or "MySql.Data.MySqlClient"
            or "MySql.Data" or "SingleStoreConnector" or "Oracle.ManagedDataAccess" or "FirebirdSql.Data.FirebirdClient"
            or "Snowflake.Data" or "IBM.Data.Db2" or "Teradata.Client" or "Teradata.Client.Provider"))
            throw new ConnectionAuthenticationException("UnsupportedAuthentication");
    }
}
