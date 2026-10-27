using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace DubUrl.Authentication;

public sealed class ClientCertificateAuthenticationOptions
{
    public IUsernamePasswordCredentialSource? SupplementalCredentials { get; init; }
}

public abstract partial class ConnectionAuthentication
{
    public static ConnectionAuthentication ClientCertificate(X509Certificate2 certificate)
        => ClientCertificate(certificate, new());
    public static ConnectionAuthentication ClientCertificate(X509Certificate2 certificate, ClientCertificateAuthenticationOptions options)
        => new CertificateAuthentication(certificate ?? throw new ArgumentNullException(nameof(certificate)), options ?? throw new ArgumentNullException(nameof(options)));

    // The native drivers are optional. Bind only the three known, version-tested hooks, without
    // loading a provider/identity SDK into core or exporting a certificate's private key.
    private sealed class CertificateAuthentication(X509Certificate2 certificate, ClientCertificateAuthenticationOptions options) : ConnectionAuthentication
    {
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.ClientCertificate;
        public override UsernamePasswordCredentials? Resolve(AuthenticationContext context)
        {
            ValidateCertificate();
            if (options.SupplementalCredentials is null) return null;
            ValidateSupplement(context);
            return options.SupplementalCredentials is ISynchronousUsernamePasswordCredentialSource sync
                ? sync.Resolve(context) : throw new ConnectionAuthenticationException("SynchronousCredentialsRequired");
        }
        public override async ValueTask<UsernamePasswordCredentials?> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateCertificate();
            if (options.SupplementalCredentials is null) return null;
            ValidateSupplement(context);
            return await options.SupplementalCredentials.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        }
        private static void ValidateSupplement(AuthenticationContext context)
        {
            if (context.ProviderInvariantName is not ("MySqlConnector" or "SingleStoreConnector"))
                throw new ConnectionAuthenticationException("UnsupportedAuthentication");
        }
        private void ValidateCertificate()
        {
            try { if (!certificate.HasPrivateKey) throw new ConnectionAuthenticationException("InvalidCertificate"); }
            catch { throw new ConnectionAuthenticationException("InvalidCertificate"); }
        }
        public override void Configure(DbConnection connection, AuthenticationContext context)
        {
            ValidateCertificate();
            var npgsql = ValidateConnectionType(connection.GetType());
            if (npgsql && !IsPostgreSqlOrCockroach(context.Scheme))
                throw new ConnectionAuthenticationException("UnsupportedAuthentication");
            ConfigureTls(connection, npgsql);
            InstallCertificateCallback(connection);
        }
        private static bool ValidateConnectionType(Type type)
        {
            if (type.FullName == "Npgsql.NpgsqlConnection") return true;
            if (type.FullName is "MySqlConnector.MySqlConnection" or "SingleStoreConnector.SingleStoreConnection") return false;
            throw new ConnectionAuthenticationException("UnsupportedAuthentication");
        }
        private static void ConfigureTls(DbConnection connection, bool npgsql)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            foreach (var key in builder.Keys.Cast<string>())
            {
                var normalized = new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                if (normalized == "sslmode" && !string.Equals(builder[key].ToString(), "VerifyFull", StringComparison.OrdinalIgnoreCase))
                    throw new ConnectionAuthenticationException("CredentialConflict");
            }
            builder["SSL Mode"] = "VerifyFull";
            if (npgsql) builder["Require Auth"] = "None";
            connection.ConnectionString = builder.ConnectionString;
        }
        private void InstallCertificateCallback(DbConnection connection)
        {
            var property = connection.GetType().GetProperty("ProvideClientCertificatesCallback")
                ?? throw new ConnectionAuthenticationException("UnsupportedAuthentication");
            var returnType = property.PropertyType.GetMethod("Invoke")!.ReturnType;
            var methodName = returnType == typeof(void) ? nameof(AddCertificate)
                : returnType == typeof(ValueTask) ? nameof(AddCertificateValueTask)
                : returnType == typeof(Task) ? nameof(AddCertificateTask) : null;
            if (methodName is null) throw new ConnectionAuthenticationException("UnsupportedAuthentication");
            var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            property.SetValue(connection, Delegate.CreateDelegate(property.PropertyType, this, method));
        }
        private static bool IsPostgreSqlOrCockroach(string scheme)
            => scheme is "postgres" or "postgresql" or "pgsql" or "pg" or "pgx"
                or "cr" or "cockroach" or "cockroachdb" or "crdb" or "cdb";
        private void AddCertificate(X509CertificateCollection certificates)
        { ValidateCertificate(); certificates.Clear(); certificates.Add(certificate); }
        private ValueTask AddCertificateValueTask(X509CertificateCollection certificates)
        { AddCertificate(certificates); return ValueTask.CompletedTask; }
        private Task AddCertificateTask(X509CertificateCollection certificates)
        { AddCertificate(certificates); return Task.CompletedTask; }
    }
}
