using System.Data.Common;
using System.Text.RegularExpressions;
using DubUrl.Parsing;

namespace DubUrl.Authentication;

internal static class MySqlOperatingSystemIdentityMappings
{
    internal static bool IsMariaDb(string family) => family is "maria" or "mariadb";
    internal static bool IsMySql(string family) => family is "mysql" or "my";

    internal static ConnectionUri PrepareLocation(ConnectionUri uri, AuthenticationContext context)
        => context.ProviderInvariantName == "MySql.Data"
            ? uri.WithOption("DefaultAuthenticationPlugin", "authentication_kerberos_client") : uri;

    internal static void ConfigureConnectorNet(DbConnectionStringBuilder builder)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) Unsupported();
        RemovePasswords(builder);
        // The legacy mapper defaults a userless URL to Windows integrated authentication.
        // Connector.NET's explicit Kerberos plugin is a different, cross-platform route.
        builder.Remove("Integrated Security");
        builder.Remove("IntegratedSecurity");
        builder.Remove("Trusted_Connection");
        builder["DefaultAuthenticationPlugin"] = "authentication_kerberos_client";
        ConfigureMode(builder, "KerberosAuthMode", windowsOnlyOption: true);
    }

    internal static void ConfigureOdbc(DbConnectionStringBuilder builder, AuthenticationContext context, string driver)
    {
        RemovePasswords(builder);
        if (IsMariaDb(OperatingSystemIdentityMappings.Family(context)))
        {
            if (!MatchesDriver(driver, @"^MariaDB ODBC (\d+(?:\.\d+){1,2}) Driver$", new Version(3, 1))) Unsupported();
            if (string.IsNullOrWhiteSpace(context.Username))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
            // MariaDB ODBC selects auth_gssapi_client from the server's gssapi-only account.
            // It has no ServerSPN keyword; PLUGIN_DIR (where required) remains caller configuration.
            if (builder.ContainsKey("ServerSPN") || builder.ContainsKey("Server SPN")) Unsupported();
        }
        else
        {
            // Display names usually omit the patch. 8.0 installations must be >=8.0.32;
            // documentation states this requirement rather than claiming a patch was discovered.
            if ((!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux()) || !IsSupportedMySqlDriver(driver)) Unsupported();
            builder["DEFAULT_AUTH"] = "authentication_kerberos_client";
            ConfigureMode(builder, "authentication-kerberos-mode", windowsOnlyOption: false);
        }
    }

    private static bool MatchesDriver(string driver, string pattern, Version minimum)
    {
        var match = Regex.Match(driver, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version) && version >= minimum;
    }

    private static bool IsSupportedMySqlDriver(string driver)
    {
        var match = Regex.Match(driver, @"^MySQL ODBC (\d+(?:\.\d+){1,2}) (?:ANSI|Unicode) Driver$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version) || version < new Version(8, 0)) return false;
        return version.Major != 8 || version.Minor != 0 || version.Build < 0 || version.Build >= 32;
    }

    private static void ConfigureMode(DbConnectionStringBuilder builder, string key, bool windowsOnlyOption)
    {
        var aliases = builder.Keys.Cast<string>().Where(candidate => Normalize(candidate) == Normalize(key)).ToArray();
        if (aliases.Length > 1) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        if (aliases.Length == 1)
        {
            var mode = ValidateMode(builder[aliases[0]].ToString() ?? "", windowsOnlyOption);
            builder.Remove(aliases[0]);
            builder[key] = mode;
        }
        else if (OperatingSystem.IsWindows()) builder[key] = "SSPI";
        else if (!windowsOnlyOption) builder[key] = "GSSAPI";
    }

    private static string ValidateMode(string mode, bool windowsOnlyOption)
    {
        if (!mode.Equals("SSPI", StringComparison.OrdinalIgnoreCase) && !mode.Equals("GSSAPI", StringComparison.OrdinalIgnoreCase)
            && !(windowsOnlyOption && mode.Equals("AUTO", StringComparison.OrdinalIgnoreCase)))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        if (!OperatingSystem.IsWindows() && (windowsOnlyOption || mode.Equals("SSPI", StringComparison.OrdinalIgnoreCase))) Unsupported();
        return mode.ToUpperInvariant();
    }

    private static string Normalize(string key) => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static void RemovePasswords(DbConnectionStringBuilder builder)
    {
        foreach (var key in new[] { "Password", "Pwd", "Passwd", "Password1", "Password2", "Password3", "PWD1", "PWD2", "PWD3" }) builder.Remove(key);
    }
    private static void Unsupported() => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
}
