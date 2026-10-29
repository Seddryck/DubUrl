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
        var kerberosModes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var option in uri.Options)
        {
            var key = Normalize(option.Key);
            if (strategy.Kind == ConnectionAuthenticationKind.OperatingSystemIdentity && key is "kerberosauthmode" or "authenticationkerberosmode"
                && (!kerberosModes.Add(key) || !IsCompatibleKerberosMode(uri, key, option.Value)))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
            ValidateOption(strategy.Kind, uri, key, option.Value);
        }
    }

    private static bool IsCompatibleKerberosMode(ConnectionUri uri, string key, string value)
        => uri.Scheme.Parts.Any(MySqlOperatingSystemIdentityMappings.IsMySql)
            && (value.Equals("SSPI", StringComparison.OrdinalIgnoreCase) || value.Equals("GSSAPI", StringComparison.OrdinalIgnoreCase)
                || (key == "kerberosauthmode" && value.Equals("AUTO", StringComparison.OrdinalIgnoreCase)));

    private static void ValidateOption(ConnectionAuthenticationKind kind, ConnectionUri uri, string key, string value)
    {
        if (IsCompatibleOperatingSystemOption(kind, uri, key, value)) return;
        if (kind == ConnectionAuthenticationKind.OperatingSystemIdentity && IsOpaqueOrIdentityOption(key))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        if (IsCredentialOption(key) || (kind == ConnectionAuthenticationKind.FileSecret && key is "extendedproperties" or "jetoledbsystemdatabase"))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
    }

    private static bool RejectsUsername(ConnectionAuthenticationKind kind)
        => kind is ConnectionAuthenticationKind.NativeToken or ConnectionAuthenticationKind.InteractiveOAuth or ConnectionAuthenticationKind.FileSecret;
    private static bool IsCompatibleOperatingSystemOption(ConnectionAuthenticationKind kind, ConnectionUri uri, string key, string value)
    {
        if (kind != ConnectionAuthenticationKind.OperatingSystemIdentity) return false;
        if (key is "integratedsecurity" or "trustedconnection") return value.ToLowerInvariant() is "true" or "sspi" or "yes";
        if (key is "defaultauth" or "defaultauthenticationplugin")
            return uri.Scheme.Parts.Any(MySqlOperatingSystemIdentityMappings.IsMySql)
                && value.Equals("authentication_kerberos_client", StringComparison.OrdinalIgnoreCase);
        return key == "authentication" && uri.Scheme.Parts.Contains("db2", StringComparer.OrdinalIgnoreCase)
            && value.Equals("KERBEROS", StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsOpaqueOrIdentityOption(string key)
        => key is "pqopt" or "d5" or "connsettings" or "a6" or "extendedproperties" or "providerstring"
            or "dsn" or "filedsn" or "savefile" or "authenticationtype" or "authmech" or "authenticationmechanism"
            or "authenticationstring" or "defaultauth" or "defaultauthenticationplugin" or "osauthent"
            or "krbusername" or "kerberosusername" or "krbkeytab" or "keytab" or "keytabfilepath"
            or "delegationuid" or "delegatekerberoscredentials" or "useexistingkerberoscredentials"
            or "useexistingkrbcreds" or "delegatekrbcreds" or "kerberoskeytab" or "kinittype"
            or "extracredentials" or "krbkeytabfilepath" or "krbcachefilepath" or "kerberosprincipal"
            or "openidtokenfile" or "ociconfigfile" or "ociconfigprofile" or "enablecleartextplugin"
            or "password1" or "password2" or "password3" or "pwd1" or "pwd2" or "pwd3";
    private static bool IsCredentialOption(string key)
        => CredentialOptions.Contains(key) || key == "encryptionkey" || key.StartsWith("oauth", StringComparison.Ordinal)
            || key.EndsWith("password", StringComparison.Ordinal) || key.EndsWith("token", StringComparison.Ordinal)
            || key.EndsWith("secret", StringComparison.Ordinal);

    internal static ConnectionUri Apply(ConnectionUri uri, ConnectionAuthentication strategy, UsernamePasswordCredentials? credentials, AuthenticationContext context)
    {
        if (strategy.Kind == ConnectionAuthenticationKind.OperatingSystemIdentity)
            uri = MySqlOperatingSystemIdentityMappings.PrepareLocation(uri, context);
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
}
