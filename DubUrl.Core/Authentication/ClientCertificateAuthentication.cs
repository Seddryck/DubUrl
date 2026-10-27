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
                ? sync.Resolve(context) : throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired);
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
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        }
        private void ValidateCertificate()
        {
            try { if (!certificate.HasPrivateKey) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InvalidCertificate); }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InvalidCertificate); }
        }
        public override void Configure(DbConnection connection, AuthenticationContext context)
        {
            ValidateCertificate();
            var npgsql = ValidateConnectionType(connection.GetType());
            if (npgsql && !IsPostgreSqlOrCockroach(context.Scheme))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            ConfigureTls(connection, npgsql);
            InstallCertificateCallback(connection);
        }
        private static bool ValidateConnectionType(Type type)
        {
            if (type.FullName == "Npgsql.NpgsqlConnection") return true;
            if (type.FullName is "MySqlConnector.MySqlConnection" or "SingleStoreConnector.SingleStoreConnection") return false;
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        }
        private static void ConfigureTls(DbConnection connection, bool npgsql)
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            foreach (var key in builder.Keys.Cast<string>())
            {
                var normalized = new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                if (normalized == "sslmode" && !string.Equals(builder[key].ToString(), "VerifyFull", StringComparison.OrdinalIgnoreCase))
                    throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
            }
            builder["SSL Mode"] = "VerifyFull";
            if (npgsql) builder["Require Auth"] = "None";
            connection.ConnectionString = builder.ConnectionString;
        }
        private void InstallCertificateCallback(DbConnection connection)
        {
            var property = connection.GetType().GetProperty("ProvideClientCertificatesCallback")
                ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            var invoke = property.PropertyType.GetMethod("Invoke") ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            var methodName = GetCallbackMethodName(invoke.ReturnType);
            // Bind a public method of this private strategy type. No private accessibility is bypassed.
            var method = GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            property.SetValue(connection, Delegate.CreateDelegate(property.PropertyType, this, method));
        }
        private static string GetCallbackMethodName(Type returnType)
        {
            if (returnType == typeof(void)) return nameof(AddCertificate);
            if (returnType == typeof(ValueTask)) return nameof(AddCertificateValueTask);
            if (returnType == typeof(Task)) return nameof(AddCertificateTask);
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        }
        private static bool IsPostgreSqlOrCockroach(string scheme)
            => scheme is "postgres" or "postgresql" or "pgsql" or "pg" or "pgx"
                or "cr" or "cockroach" or "cockroachdb" or "crdb" or "cdb";
        public void AddCertificate(X509CertificateCollection certificates)
        { ValidateCertificate(); certificates.Clear(); certificates.Add(certificate); }
        public ValueTask AddCertificateValueTask(X509CertificateCollection certificates)
        { AddCertificate(certificates); return ValueTask.CompletedTask; }
        public Task AddCertificateTask(X509CertificateCollection certificates)
        { AddCertificate(certificates); return Task.CompletedTask; }
    }
}
