using System.Data.Common;
using static DubUrl.TlsOptionValidation;

namespace DubUrl;

internal static class NativeEncryption
{
    private const string Npgsql = "Npgsql";
    private const string ConnectorNet = "ConnectorNet";

    internal static void Apply(string? mapping, DbConnectionStringBuilder builder, ConnectionEncryption encryption)
    {
        // Only explicit options are inspected; native getters can also expose defaults.
        var supplied = new DbConnectionStringBuilder { ConnectionString = builder.ConnectionString };
        switch (mapping)
        {
            case "Teradata": ApplyTeradata(builder, supplied, encryption); break;
            case "Trino": ApplyTrino(builder, supplied, encryption); break;
            case "SqlClient": ApplySqlClient(builder, supplied, encryption); break;
            case "Odbc": TransportEncryption.Apply(builder, encryption, true); break;
            case "OleDb": TransportEncryption.Apply(builder, encryption, false); break;
            case Npgsql:
            case "MySql":
            case ConnectorNet: ApplyVerifyFull(mapping, builder, supplied, encryption); break;
            default: throw new NotSupportedException("Unknown TLS mapping.");
        }
    }

    private static void ApplyTeradata(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, ConnectionEncryption encryption)
    {
        Require(supplied, "SslMode", "Verify-Full", "VerifyFull");
        builder["SslMode"] = "Verify-Full";
        ApplyCa(builder, supplied, "SslCA", encryption.CaPath);
    }

    private static void ApplyTrino(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, ConnectionEncryption encryption)
    {
        if (encryption.CaPath is not null)
            throw new NotSupportedException("NReco Trino requires external system trust; custom CA files are unsupported.");
        Require(supplied, "UseSsl", "True");
        builder["UseSsl"] = true;
    }

    private static void ApplySqlClient(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, ConnectionEncryption encryption)
    {
        if (encryption.CaPath is not null)
            throw new NotSupportedException("SqlClient cannot use a custom CA file. Configure external system trust; ServerCertificate is certificate pinning, not CA trust.");
        Require(supplied, "Encrypt", "True", "Mandatory", "Strict");
        Require(supplied, "TrustServerCertificate", "False");
        RejectExplicit(supplied, "ServerCertificate", "HostNameInCertificate");
        if (!supplied.Keys.Cast<string>().Any(k => Normalize(k) == "encrypt")) builder["Encrypt"] = "Mandatory";
        builder["Trust Server Certificate"] = false;
    }

    private static void ApplyVerifyFull(string mapping, DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, ConnectionEncryption encryption)
    {
        if (mapping == Npgsql) ConfigurePostgresTransport(builder, supplied);
        else ValidateMySqlTransport(supplied);
        Require(supplied, "SSLMode", "VerifyFull");
        RejectExplicit(supplied, "TrustServerCertificate", "TrustServerCertificateCallback");
        builder["SSL Mode"] = "VerifyFull";
        var caKeyword = mapping switch { Npgsql => "Root Certificate", ConnectorNet => "SslCa", _ => "SSL CA" };
        ApplyCa(builder, supplied, caKeyword, encryption.CaPath);
    }

    private static void ConfigurePostgresTransport(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied)
    {
        if (supplied.TryGetValue("Host", out var host) && Convert.ToString(host)?.StartsWith('/') == true)
            throw new NotSupportedException("TLS cannot be applied to a Unix-domain socket.");
        Require(supplied, "GSSEncryptionMode", "Disable");
        builder["GSS Encryption Mode"] = "Disable";
    }

    private static void ValidateMySqlTransport(DbConnectionStringBuilder supplied)
    {
        foreach (string key in supplied.Keys)
        {
            var normalized = Normalize(key);
            var value = Convert.ToString(supplied[key]) ?? "";
            if (normalized is "connectionprotocol" or "protocol" && !new[] { "socket", "tcp" }.Contains(value, StringComparer.OrdinalIgnoreCase))
                throw new NotSupportedException("TLS requires a TCP transport.");
            if (normalized is "server" or "host" && value.StartsWith('/'))
                throw new NotSupportedException("TLS requires a TCP transport.");
        }
    }

    private static void ApplyCa(DbConnectionStringBuilder builder, DbConnectionStringBuilder supplied, string keyword, string? path)
    {
        if (path is null) return;
        Require(supplied, keyword, path);
        builder[keyword] = path;
    }
}
