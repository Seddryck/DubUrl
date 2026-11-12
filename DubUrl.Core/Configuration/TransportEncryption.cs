using System.Data.Common;
using System.Text.RegularExpressions;
using static DubUrl.TlsOptionValidation;

namespace DubUrl;

/// <summary>Driver-specific TLS mappings; DSNs and unknown drivers cannot establish a verifiable contract.</summary>
internal static class TransportEncryption
{
    private const string CaKeyword = "SSLCA";
    private enum DriverKind { Unsupported, SqlServer, Postgres, MySql, MariaDb, SparkOrHive, Databricks, Trino }

    internal static void Apply(DbConnectionStringBuilder builder, ConnectionEncryption encryption, bool odbc)
    {
        var supplied = new DbConnectionStringBuilder(odbc) { ConnectionString = builder.ConnectionString };
        if (supplied.ContainsKey("DSN") || supplied.ContainsKey("FileDSN")) Unsupported();
        var driverKeyword = odbc ? "Driver" : "Provider";
        var driver = supplied.TryGetValue(driverKeyword, out var value) ? (Convert.ToString(value) ?? "").Trim('{', '}') : "";
        RejectExplicit(supplied, "ServerCertificate", "HostnameInCertificate", "TlsPeerFp", "TlsPeerFpList");
        switch (IdentifyDriver(driver, odbc))
        {
            case DriverKind.SqlServer: ApplySqlServer(builder, supplied, encryption, odbc, driver); break;
            case DriverKind.Postgres: ApplyPostgres(builder, supplied, encryption); break;
            case DriverKind.MySql: ApplyMySql(builder, supplied, encryption); break;
            case DriverKind.MariaDb: ApplyMariaDb(builder, supplied, encryption); break;
            case DriverKind.SparkOrHive:
                ApplySimba(builder, supplied, encryption, "AllowSelfSignedServerCert", "CAIssuedCertNamesMismatch");
                break;
            case DriverKind.Databricks:
                ApplySimba(builder, supplied, encryption, "AllowSelfSignedCert", "AllowHostNameCNMismatch");
                break;
            case DriverKind.Trino:
                ApplySimba(builder, supplied, encryption, "AllowSelfSignedServerCert", "AllowHostNameCNMismatch");
                RequireAndSet(builder, supplied, "AllowHTTPRedirect", "0");
                break;
            default: Unsupported(); break;
        }
    }

    private static DriverKind IdentifyDriver(string driver, bool odbc)
    {
        if (!odbc) return driver is "MSOLEDBSQL" or "MSOLEDBSQL19" ? DriverKind.SqlServer : DriverKind.Unsupported;
        if (Matches(driver, @"^ODBC Driver (?:1[89]|[2-9]\d) for SQL Server$")) return DriverKind.SqlServer;
        if (Matches(driver, @"^PostgreSQL (Unicode|ANSI)(\(x64\))?$")) return DriverKind.Postgres;
        if (Matches(driver, @"^MySQL ODBC (?:[89]|[1-9]\d+)\.\d+(?:\.\d+)? (Unicode |ANSI )?Driver$")) return DriverKind.MySql;
        if (Matches(driver, @"^MariaDB ODBC 3\.[1-9]\d*(?:\.\d+)? Driver$")) return DriverKind.MariaDb;
        return driver.ToUpperInvariant() switch
        {
            "SIMBA SPARK ODBC DRIVER" or "CLOUDERA ODBC DRIVER FOR APACHE HIVE" => DriverKind.SparkOrHive,
            "DATABRICKS ODBC DRIVER" => DriverKind.Databricks,
            "SIMBA TRINO ODBC DRIVER" => DriverKind.Trino,
            _ => DriverKind.Unsupported
        };
    }

    private static bool Matches(string driver, string pattern)
        => Regex.IsMatch(driver, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));

    private static void ApplySqlServer(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied,
        ConnectionEncryption encryption, bool odbc, string driver)
    {
        if (!odbc && !OperatingSystem.IsWindows()) Unsupported();
        if (encryption.CaPath is not null)
            throw new NotSupportedException("The SQL Server driver requires external system CA trust; a pinning certificate is not a CA trust file.");
        var keyword = odbc ? "Encrypt" : "Use Encryption for Data";
        Require(supplied, keyword, "yes", "true", "mandatory", "strict");
        Require(supplied, "TrustServerCertificate", "no", "false");
        Require(supplied, "Trust Server Certificate", "no", "false");
        if (!supplied.ContainsKey(keyword)) builder[keyword] = SqlServerEncryptionValue(odbc, driver);
        builder[odbc ? "TrustServerCertificate" : "Trust Server Certificate"] = odbc ? "no" : "false";
    }

    private static string SqlServerEncryptionValue(bool odbc, string driver)
        => (odbc, driver) switch { (true, _) => "yes", (_, "MSOLEDBSQL19") => "Mandatory", _ => "True" };

    private static void ApplyPostgres(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, ConnectionEncryption encryption)
    {
        Require(supplied, "SSLmode", "verify-full");
        // Pqopt can override libpq settings, so arbitrary preexisting option strings are rejected.
        if (supplied.ContainsKey("Pqopt")) throw new ArgumentException("Explicit TLS conflicts with native libpq options.");
        builder["SSLmode"] = "verify-full";
        builder["Pqopt"] = "gssencmode=disable sslrootcert=" + Quote(encryption.CaPath ?? "system");
    }

    private static void ApplyMySql(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, ConnectionEncryption encryption)
    {
        Require(supplied, "SSLVERIFY", "1");
        Require(supplied, "SSLMODE", "VERIFY_IDENTITY");
        builder["SSLMODE"] = "VERIFY_IDENTITY";
        ApplyCa(builder, supplied, encryption.CaPath);
    }

    private static void ApplyMariaDb(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, ConnectionEncryption encryption)
    {
        Require(supplied, "SSLVERIFY", "1");
        Require(supplied, "FORCETLS", "1");
        builder["SSLVERIFY"] = 1;
        builder["FORCETLS"] = 1;
        ApplyCa(builder, supplied, encryption.CaPath);
    }

    private static void ApplySimba(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied,
        ConnectionEncryption encryption, string selfSigned, string hostMismatch)
    {
        RequireAndSet(builder, supplied, "SSL", "1");
        // Databricks also recognizes the older spelling of its hostname override.
        Require(supplied, "CAIssuedCertNamesMismatch", "0");
        Require(supplied, "AllowHostNameCNMismatch", "0");
        RequireAndSet(builder, supplied, selfSigned, "0");
        RequireAndSet(builder, supplied, hostMismatch, "0");
        if (encryption.CaPath is null && !supplied.ContainsKey("TrustedCerts")) return;
        RequireAndSet(builder, supplied, "UseSystemTrustStore", "0");
        if (encryption.CaPath is not string path) return;
        Require(supplied, "TrustedCerts", path);
        builder["TrustedCerts"] = path;
    }

    private static void RequireAndSet(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied,
        string keyword, string value)
    {
        Require(supplied, keyword, value);
        builder[keyword] = value;
    }

    private static void ApplyCa(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, string? path)
    {
        if (path is null) return;
        Require(supplied, CaKeyword, path);
        builder[CaKeyword] = path;
    }

    private static string Quote(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    private static void Unsupported() => throw new NotSupportedException("Verified TLS is unsupported for the selected transport driver; use a documented native route or supported driver.");
}
