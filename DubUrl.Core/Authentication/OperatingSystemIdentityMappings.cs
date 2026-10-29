using System.Data.Common;

namespace DubUrl.Authentication;

// Native packages remain optional. Match the registry provider and database family, then
// the actual mapped ODBC/OLE DB driver. Never substitute a different driver.
internal static class OperatingSystemIdentityMappings
{
    internal static string Family(AuthenticationContext context)
        => context.Scheme.Split('+').FirstOrDefault(part => part is not ("odbc" or "oledb")) ?? "";
    private static bool IsPostgres(string family)
        => family is "pg" or "pgx" or "pgsql" or "postgres" or "postgresql" or "ts" or "timescale" or "cr" or "cockroach" or "cockroachdb" or "crdb" or "cdb";
    private static bool IsSqlServer(string family)
        => family is "mssql" or "ms" or "sqlserver" or "mssqlserver";
    private static void Unsupported() => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
    private static void Conflict() => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
    internal static void ValidateProvider(AuthenticationContext context)
    {
        var family = Family(context);
        var supported = context.ProviderInvariantName switch
        {
            "Microsoft.Data.SqlClient" => IsSqlServer(family),
            "Npgsql" => IsPostgres(family),
            "IBM.Data.Db2" => family is "db2",
            "Oracle.ManagedDataAccess" or "Oracle.DataAccess" => family is "oracle" or "ora" or "or",
            "Teradata.Client" => family is "teradata" or "td" or "tera" or "teradatasql",
            "FirebirdSql.Data.FirebirdClient" => OperatingSystem.IsWindows() && family is "firebird" or "firebirdsql" or "fb",
            "System.Data.Odbc" => IsSqlServer(family) || IsPostgres(family) || family is "trino" or "tr" or "drill",
            "System.Data.OleDb" => OperatingSystem.IsWindows() && (IsSqlServer(family) || family is "oracle" or "ora" or "or"),
            // Plugin negotiation alone does not enforce an OS-only authentication mechanism.
            _ => false
        };
        if (!supported) Unsupported();
    }

    internal static void Configure(DbConnectionStringBuilder builder, AuthenticationContext context)
    {
        switch (context.ProviderInvariantName)
        {
            case "Microsoft.Data.SqlClient": ConfigureSqlServer(builder, context); break;
            case "Npgsql": ConfigurePostgres(builder); break;
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
                builder["Integrated Security"] = true;
                builder["Authentication Mechanism"] = "SPNEGO";
                break;
            case "FirebirdSql.Data.FirebirdClient":
                RejectUser(context);
                RemoveCredentials(builder, true);
                builder["Integrated Security"] = true;
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
        builder["Integrated Security"] = true;
    }

    private static void ConfigurePostgres(DbConnectionStringBuilder builder)
    {
        var host = Get(builder, "Host");
        if (string.IsNullOrWhiteSpace(host)) Unsupported();
        var hosts = host.Split(',');
        var socket = hosts.Any(value => value.Trim().StartsWith('/'));
        if (socket && (OperatingSystem.IsWindows() || hosts.Length != 1)) Unsupported();
        RemoveCredentials(builder, false);
        builder.Remove("Integrated Security");
        builder.Remove("Trusted_Connection");
        builder["Require Auth"] = socket ? "None" : "GSS, SSPI";
        if (socket) builder["SSL Mode"] = "Disable";
    }

    private static void ConfigureOdbc(DbConnectionStringBuilder builder, AuthenticationContext context)
    {
        // DSNs/opaque driver paths do not establish the vendor/version contract safely.
        if (builder.ContainsKey("DSN") || builder.ContainsKey("FileDSN")) Unsupported();
        var driver = Get(builder, "Driver").Trim('{', '}');
        if (IsSqlServer(Family(context)) && driver is "ODBC Driver 17 for SQL Server" or "ODBC Driver 18 for SQL Server")
        {
            RejectUser(context);
            RemoveCredentials(builder, true);
            builder.Remove("Integrated Security");
            builder["Trusted_Connection"] = "yes";
        }
        else if (IsPostgres(Family(context)) && (driver.StartsWith("PostgreSQL Unicode", StringComparison.Ordinal)
            || driver.StartsWith("PostgreSQL ANSI", StringComparison.Ordinal)))
        {
            // libpq >=16 supports require_auth; older versions fail on this parameter.
            RemoveCredentials(builder, false);
            builder["Pqopt"] = "{require_auth=gss,sspi}";
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
        {
            RejectUser(context);
            RemoveCredentials(builder, true);
            Require(builder, "KrbServiceHost");
            builder["AuthenticationType"] = "Kerberos";
            if (!builder.ContainsKey("KrbServiceName")) builder["KrbServiceName"] = "drill";
            if (!OperatingSystem.IsWindows() && builder.ContainsKey("UseOnlySSPI")) Unsupported();
        }
        else Unsupported();
    }

    private static void ConfigureOleDb(DbConnectionStringBuilder builder, AuthenticationContext context)
    {
        RejectUser(context);
        var provider = Get(builder, "Provider");
        RemoveCredentials(builder, true);
        if (IsSqlServer(Family(context)) && provider is "MSOLEDBSQL" or "MSOLEDBSQL19")
            builder["Integrated Security"] = "SSPI";
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

