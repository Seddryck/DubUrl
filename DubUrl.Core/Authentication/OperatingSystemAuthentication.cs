using System.Data.Common;

namespace DubUrl.Authentication;

public abstract partial class ConnectionAuthentication
{
    public static ConnectionAuthentication OperatingSystemIdentity() => new OperatingSystemAuthentication();

    private sealed class OperatingSystemAuthentication : ConnectionAuthentication
    {
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.OperatingSystemIdentity;
        public override void Configure(DbConnection connection, AuthenticationContext context)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            if (connection.GetType().FullName == "Microsoft.Data.SqlClient.SqlConnection")
            {
                if (context.Username is not null) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
                if (!OperatingSystem.IsWindows() && builder.TryGetValue("Data Source", out var source)
                    && (source.ToString() ?? string.Empty).StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase))
                    throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
                builder.Remove("User ID");
                builder.Remove("Password");
                builder["Integrated Security"] = true;
            }
            else if (connection.GetType().FullName == "Npgsql.NpgsqlConnection")
            {
                // Peer authentication is a local Unix socket mechanism. Do not silently use TCP/GSS.
                if (OperatingSystem.IsWindows() || !builder.TryGetValue("Host", out var host)
                    || !(host.ToString() ?? string.Empty).StartsWith('/') || (host.ToString() ?? string.Empty).Contains(','))
                    throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
                builder.Remove("Password");
                builder["Require Auth"] = "None";
                builder["SSL Mode"] = "Disable"; // Unix sockets do not use TLS; pg_hba enforces peer.
            }
            else throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            connection.ConnectionString = builder.ConnectionString;
        }
    }
}
