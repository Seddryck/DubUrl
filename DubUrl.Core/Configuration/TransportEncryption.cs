using System.Data.Common;
using System.Text.RegularExpressions;

namespace DubUrl;

/// <summary>Driver-specific TLS mappings; DSNs and unknown drivers cannot establish a verifiable contract.</summary>
internal static class TransportEncryption
{
    internal static void Apply(DbConnectionStringBuilder builder, ConnectionEncryption encryption, bool odbc)
    {
        var supplied = new DbConnectionStringBuilder(odbc) { ConnectionString = builder.ConnectionString };
        if (supplied.ContainsKey("DSN") || supplied.ContainsKey("FileDSN")) Unsupported();
        var driver = supplied.TryGetValue(odbc ? "Driver" : "Provider", out var value) ? value.ToString()!.Trim('{', '}') : "";
        if (supplied.Keys.Cast<string>().Any(key => Normalize(key) is "servercertificate" or "hostnameincertificate" or "tlspeerfp" or "tlspeerfplist")) Conflict();
        if ((odbc && Regex.IsMatch(driver, @"^ODBC Driver (?:1[89]|[2-9]\d) for SQL Server$", RegexOptions.None, TimeSpan.FromSeconds(1)))
            || (!odbc && driver is "MSOLEDBSQL" or "MSOLEDBSQL19"))
        {
            if (!odbc && !OperatingSystem.IsWindows()) Unsupported();
            if (encryption.CaPath is not null)
                throw new NotSupportedException("The SQL Server driver requires external system CA trust; a pinning certificate is not a CA trust file.");
            Require(supplied, odbc ? "Encrypt" : "Use Encryption for Data", "yes", "true", "mandatory", "strict");
            Require(supplied, "TrustServerCertificate", "no", "false");
            Require(supplied, "Trust Server Certificate", "no", "false");
            var keyword = odbc ? "Encrypt" : "Use Encryption for Data";
            if (!supplied.ContainsKey(keyword)) builder[keyword] = odbc ? "yes" : driver == "MSOLEDBSQL19" ? "Mandatory" : "True";
            builder[odbc ? "TrustServerCertificate" : "Trust Server Certificate"] = odbc ? "no" : "false";
            return;
        }
        if (odbc && Regex.IsMatch(driver, @"^PostgreSQL (Unicode|ANSI)(\(x64\))?$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            Require(supplied, "SSLmode", "verify-full");
            // Pqopt can override libpq settings, so arbitrary preexisting option strings are rejected.
            if (supplied.ContainsKey("Pqopt")) Conflict();
            builder["SSLmode"] = "verify-full";
            builder["Pqopt"] = "gssencmode=disable sslrootcert=" + Quote(encryption.CaPath ?? "system");
            return;
        }
        if (odbc && Regex.IsMatch(driver, @"^MySQL ODBC (?:[89]|[1-9]\d+)\.\d+(?:\.\d+)? (Unicode |ANSI )?Driver$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            Require(supplied, "SSLVERIFY", "1");
            Require(supplied, "SSLMODE", "VERIFY_IDENTITY");
            builder["SSLMODE"] = "VERIFY_IDENTITY";
            if (encryption.CaPath is not null) { Require(supplied, "SSLCA", encryption.CaPath); builder["SSLCA"] = encryption.CaPath; }
            return;
        }
        if (odbc && Regex.IsMatch(driver, @"^MariaDB ODBC 3\.[1-9]\d*(?:\.\d+)? Driver$", RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            Require(supplied, "SSLVERIFY", "1");
            Require(supplied, "FORCETLS", "1");
            builder["SSLVERIFY"] = 1;
            builder["FORCETLS"] = 1;
            if (encryption.CaPath is not null) { Require(supplied, "SSLCA", encryption.CaPath); builder["SSLCA"] = encryption.CaPath; }
            return;
        }
        Unsupported();
    }

    private static string Quote(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
    private static void Require(DbConnectionStringBuilder builder, string key, params string[] values)
    {
        foreach (string suppliedKey in builder.Keys)
            if (Normalize(suppliedKey) == Normalize(key) && !(Normalize(key) == "sslca"
                ? values.Any(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(builder[suppliedKey].ToString()!),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                : values.Contains(builder[suppliedKey].ToString(), StringComparer.OrdinalIgnoreCase))) Conflict();
    }
    private static string Normalize(string key) => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static void Conflict() => throw new ArgumentException("Explicit TLS conflicts with a native driver verification option.");
    private static void Unsupported() => throw new NotSupportedException("Verified TLS is unsupported for the selected transport driver; use a documented native route or supported driver.");
}
