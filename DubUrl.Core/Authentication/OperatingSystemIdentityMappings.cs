using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace DubUrl.Authentication;

// Native packages remain optional. Match the registry provider and database family, then
// the actual mapped ODBC/OLE DB driver. Never substitute a different driver.
internal static class OperatingSystemIdentityMappings
{
    private const string IntegratedSecurity = "Integrated Security";
    internal static string Family(AuthenticationContext context)
        => context.Scheme.Split('+').FirstOrDefault(part => part is not ("odbc" or "oledb")) ?? "";
    private static bool IsPostgres(string family)
        => family is "pg" or "pgx" or "pgsql" or "postgres" or "postgresql" or "ts" or "timescale" or "cr" or "cockroach" or "cockroachdb" or "crdb" or "cdb";
    private static bool IsSqlServer(string family)
        => family is "mssql" or "ms" or "sqlserver" or "mssqlserver";
    [DoesNotReturn]
    private static void Unsupported() => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
    private static void Conflict() => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
    internal static void ValidateProvider(AuthenticationContext context)
    {
        var family = Family(context);
        if (!SupportsProvider(context.ProviderInvariantName, family)) Unsupported();
        if (context.ProviderInvariantName is "MySqlConnector" or "SingleStoreConnector"
            && string.IsNullOrWhiteSpace(context.Username))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
    }

    private static bool SupportsProvider(string provider, string family)
        => provider switch
        {
            "Microsoft.Data.SqlClient" => IsSqlServer(family),
            "Npgsql" => IsPostgres(family),
            "MySqlConnector" => MySqlOperatingSystemIdentityMappings.IsMariaDb(family),
            "MySql.Data" => SupportsConnectorNet(family),
            "SingleStoreConnector" => family is "sg" or "sgs" or "single" or "singlestore" or "singlestoredb",
            _ => SupportsAdditionalProvider(provider, family)
        };

    private static bool SupportsAdditionalProvider(string provider, string family)
        => provider switch
        {
            "IBM.Data.Db2" => family is "db2",
            "Oracle.ManagedDataAccess" or "Oracle.DataAccess" => family is "oracle" or "ora" or "or",
            "Teradata.Client" => family is "teradata" or "td" or "tera" or "teradatasql",
            "FirebirdSql.Data.FirebirdClient" => OperatingSystem.IsWindows() && family is "firebird" or "firebirdsql" or "fb",
            "System.Data.Odbc" => SupportsOdbc(family),
            "System.Data.OleDb" => OperatingSystem.IsWindows() && (IsSqlServer(family) || family is "oracle" or "ora" or "or"),
            _ => false
        };

    private static bool SupportsConnectorNet(string family)
        => MySqlOperatingSystemIdentityMappings.IsMySql(family) && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux());

    private static bool IsMySqlFamily(string family)
        => MySqlOperatingSystemIdentityMappings.IsMariaDb(family) || MySqlOperatingSystemIdentityMappings.IsMySql(family);

    private static bool SupportsOdbc(string family)
        => IsSqlServer(family) || IsPostgres(family) || family is "trino" or "tr" or "drill" || IsMySqlFamily(family);

    internal static void Configure(DbConnectionStringBuilder builder, AuthenticationContext context)
    {
        switch (context.ProviderInvariantName)
        {
            case "Microsoft.Data.SqlClient": ConfigureSqlServer(builder, context); break;
            case "Npgsql": ConfigurePostgres(builder); break;
            case "MySqlConnector":
            case "SingleStoreConnector":
                RemoveCredentials(builder, false); // Role hint selects a server account, not another OS credential.
                break;
            case "MySql.Data": MySqlOperatingSystemIdentityMappings.ConfigureConnectorNet(builder); break;
            case "IBM.Data.Db2":
                Require(builder, "TargetPrincipal");
                RejectUser(context);
                builder["Authentication"] = "KERBEROS";
                RemoveCredentials(builder, true);
                break;
            case "Oracle.ManagedDataAccess":
            case "Oracle.DataAccess":
                RejectUser(context);
                RemoveCredentials(builder, true);
                builder["User Id"] = "/";
                break;
            case "Teradata.Client":
                RejectUser(context);
                RemoveCredentials(builder, true);
                builder[IntegratedSecurity] = true;
                builder["Authentication Mechanism"] = "SPNEGO";
                break;
            case "FirebirdSql.Data.FirebirdClient":
                RejectUser(context);
                RemoveCredentials(builder, true);
                builder[IntegratedSecurity] = true;
                break;
            case "System.Data.Odbc": ConfigureOdbc(builder, context); break;
            case "System.Data.OleDb": ConfigureOleDb(builder, context); break;
            default: Unsupported(); break;
        }
    }

    private static void ConfigureSqlServer(DbConnectionStringBuilder builder, AuthenticationContext context)
    {
        RejectUser(context);
        if (!OperatingSystem.IsWindows() && Get(builder, "Data Source").StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase)) Unsupported();
        RemoveCredentials(builder, true);
        builder.Remove("Trusted_Connection");
        builder[IntegratedSecurity] = true;
    }

    private static void ConfigurePostgres(DbConnectionStringBuilder builder)
    {
        var host = Get(builder, "Host");
        if (string.IsNullOrWhiteSpace(host)) Unsupported();
        var hosts = host.Split(',');
        var socket = hosts.Any(value => value.Trim().StartsWith('/'));
        if (socket && (OperatingSystem.IsWindows() || hosts.Length != 1)) Unsupported();
        RemoveCredentials(builder, false);
        builder.Remove(IntegratedSecurity);
        builder.Remove("Trusted_Connection");
        builder["Require Auth"] = socket ? "None" : "GSS, SSPI";
        if (socket) builder["SSL Mode"] = "Disable";
    }

    private static void ConfigureOdbc(DbConnectionStringBuilder builder, AuthenticationContext context)
    {
        // DSNs/opaque driver paths do not establish the vendor/version contract safely.
        if (builder.ContainsKey("DSN") || builder.ContainsKey("FileDSN")) Unsupported();
        var driver = Get(builder, "Driver").Trim('{', '}');
        if (IsMySqlFamily(Family(context)))
        {
            MySqlOperatingSystemIdentityMappings.ConfigureOdbc(builder, context, driver);
            return;
        }
        ConfigureOdbcDriver(builder, context, driver);
    }

    private static void ConfigureOdbcDriver(DbConnectionStringBuilder builder, AuthenticationContext context, string driver)
    {
        if (IsSqlServer(Family(context)) && driver is "ODBC Driver 17 for SQL Server" or "ODBC Driver 18 for SQL Server")
        {
            RejectUser(context);
            RemoveCredentials(builder, true);
            builder.Remove(IntegratedSecurity);
            builder["Trusted_Connection"] = "yes";
        }
        else if (IsPostgres(Family(context)) && IsPostgresDriver(driver))
        {
            ConfigurePostgresOdbcIdentity(builder);
        }
        else if (Family(context) is "trino" or "tr" && driver == "Simba Trino ODBC Driver")
        {
            RemoveCredentials(builder, false); // UID may be a database-role hint, not an OS login.
            builder["AuthenticationType"] = "Kerberos Authentication";
            builder["UseExistingKrbCreds"] = 1;
            builder["DelegateKrbCreds"] = 0;
            builder["SSL"] = 1;
        }
        else if (Family(context) == "drill" && driver is "MapR Drill ODBC Driver" or "HPE Drill ODBC Driver")
            ConfigureDrill(builder, context);
        else Unsupported();
    }

    private static void ConfigurePostgresOdbcIdentity(DbConnectionStringBuilder builder)
    {
        // libpq >=16 supports require_auth; older versions fail on this parameter.
        const string requiredAuth = "require_auth=gss,sspi";
        RemoveCredentials(builder, false);
        var options = Get(builder, "Pqopt").Trim('{', '}');
        if (!options.EndsWith(requiredAuth, StringComparison.Ordinal))
            options = options.Length == 0 ? requiredAuth : options + " " + requiredAuth;
        builder["Pqopt"] = "{" + options + "}";
    }

    private static bool IsPostgresDriver(string driver)
        => driver.StartsWith("PostgreSQL Unicode", StringComparison.Ordinal) || driver.StartsWith("PostgreSQL ANSI", StringComparison.Ordinal);

    private static void ConfigureDrill(DbConnectionStringBuilder builder, AuthenticationContext context)
    {
        RejectUser(context);
        RemoveCredentials(builder, true);
        Require(builder, "KrbServiceHost");
        builder["AuthenticationType"] = "Kerberos";
        if (!builder.ContainsKey("KrbServiceName")) builder["KrbServiceName"] = "drill";
        if (!OperatingSystem.IsWindows() && builder.ContainsKey("UseOnlySSPI")) Unsupported();
    }

    private static void ConfigureOleDb(DbConnectionStringBuilder builder, AuthenticationContext context)
    {
        RejectUser(context);
        var provider = Get(builder, "Provider");
        RemoveCredentials(builder, true);
        if (IsSqlServer(Family(context)) && provider is "MSOLEDBSQL" or "MSOLEDBSQL19")
            builder[IntegratedSecurity] = "SSPI";
        else if (Family(context) is "oracle" or "ora" or "or" && provider == "OraOLEDB.Oracle")
        {
            builder["OSAuthent"] = 1;
            builder["User ID"] = "/";
            builder["PwdChgDlg"] = 0;
        }
        else Unsupported();
    }

    private static void RejectUser(AuthenticationContext context) { if (context.Username is not null) Conflict(); }
    private static string Get(DbConnectionStringBuilder builder, string key)
        => builder.TryGetValue(key, out var value) ? value.ToString() ?? "" : "";
    private static void Require(DbConnectionStringBuilder builder, string key)
    {
        if (string.IsNullOrWhiteSpace(Get(builder, key)))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
    }
    private static void RemoveCredentials(DbConnectionStringBuilder builder, bool removeUser)
    {
        foreach (var key in new[] { "Password", "Pwd", "Passwd" }) builder.Remove(key);
        if (removeUser) foreach (var key in new[] { "User ID", "UserID", "Uid", "User", "Username" }) builder.Remove(key);
    }
}
