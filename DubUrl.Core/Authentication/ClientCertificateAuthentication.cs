using System.Data.Common;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;

namespace DubUrl.Authentication;

public sealed class ClientCertificateAuthenticationOptions
{
    public IUsernamePasswordCredentialSource? SupplementalCredentials { get; init; }
}

public abstract partial class ConnectionAuthentication
{
    public static ConnectionAuthentication ClientCertificate(CertificateOptions certificateOptions, string? caPath = null)
        => new CertificateAuthentication(certificateOptions ?? throw new ArgumentNullException(nameof(certificateOptions)), caPath);

    [Obsolete("Use ClientCertificate(new X509CertificateOptions(certificate)) instead.")]
    public static ConnectionAuthentication ClientCertificate(X509Certificate2 certificate)
        => ClientCertificate(new X509CertificateOptions(certificate));
    [Obsolete("Move SupplementalCredentials to X509CertificateOptions and use the CertificateOptions overload.")]
    public static ConnectionAuthentication ClientCertificate(X509Certificate2 certificate, ClientCertificateAuthenticationOptions options)
        => ClientCertificate(new X509CertificateOptions(certificate) { SupplementalCredentials = (options ?? throw new ArgumentNullException(nameof(options))).SupplementalCredentials });

    private sealed class CertificateAuthentication(CertificateOptions options, string? caPath) : ConnectionAuthentication
    {
        private const string ConnectorNet = "MySql.Data";
        private const string Odbc = "System.Data.Odbc";
        private static readonly TimeSpan DriverMatchTimeout = TimeSpan.FromSeconds(1);
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.ClientCertificate;
        internal override string? ServerCaPath => caPath;
        internal override void ValidateConnectionConfiguration(ConnectionEncryption? encryption, ConnectionPoolingOptions? pooling, AuthenticationContext context)
        {
            if (context.ProviderInvariantName != ConnectorNet) return;
            if (options is Pkcs12CertificateOptions && encryption?.CaPath is not null) Unsupported();
            if (pooling?.Enabled == true) Unsupported(); // File credentials cannot safely isolate a Connector/NET pool.
        }
        public override void ValidateContext(AuthenticationContext context)
        {
            options.ValidateSelection();
            var family = OperatingSystemIdentityMappings.Family(context);
            if (!SupportsProvider(context.ProviderInvariantName, family)) Unsupported();
            if (string.IsNullOrWhiteSpace(context.Username))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
            ValidateSupplement(context, family);
            ValidateFileOptions(context.ProviderInvariantName);
        }
        private static bool SupportsProvider(string provider, string family) => provider switch
        {
            "Npgsql" => IsPostgres(family),
            "MySqlConnector" or ConnectorNet => IsMySql(family),
            "SingleStoreConnector" => IsSingleStore(family),
            Odbc => IsPostgres(family) || IsMySql(family),
            _ => false
        };
        private void ValidateSupplement(AuthenticationContext context, string family)
        {
            if (options.SupplementalCredentials is not null && !IsMySql(family)
                && context.ProviderInvariantName != "SingleStoreConnector") Unsupported();
        }
        private void ValidateFileOptions(string provider)
        {
            if (provider is not (ConnectorNet or Odbc)) return;
            if (options is X509CertificateOptions { IsUnencryptedPem: true }) return;
            // Connector/NET's CertificateFile setting cannot represent both a bundle and a CA.
            if (provider != ConnectorNet || options is not Pkcs12CertificateOptions || caPath is not null) Unsupported();
        }
        internal override void ValidateMappedConnection(string connectionString, AuthenticationContext context)
        {
            var builder = new DbConnectionStringBuilder(true) { ConnectionString = connectionString };
            if (builder.ContainsKey("DSN") || builder.ContainsKey("FileDSN")) Unsupported();
            var driver = builder.TryGetValue("Driver", out var value) ? (value.ToString() ?? "").Trim('{', '}') : "";
            var family = OperatingSystemIdentityMappings.Family(context);
            if (!(IsPostgres(family) && IsPostgresDriver(driver))
                && !(IsMySql(family) && IsMySqlDriver(driver))
                && !(family is "maria" or "mariadb" && IsMariaDbDriver(driver))) Unsupported();
        }
        private static bool IsPostgresDriver(string driver) => Regex.IsMatch(driver, @"^PostgreSQL (Unicode|ANSI)(\(x64\))?$", RegexOptions.None, DriverMatchTimeout);
        private static bool IsMySqlDriver(string driver) => Regex.IsMatch(driver, @"^MySQL ODBC (?:[89]|[1-9]\d+)\.\d+(?:\.\d+)? (Unicode |ANSI )?Driver$", RegexOptions.None, DriverMatchTimeout);
        private static bool IsMariaDbDriver(string driver) => Regex.IsMatch(driver, @"^MariaDB ODBC 3\.[1-9]\d*(?:\.\d+)? Driver$", RegexOptions.None, DriverMatchTimeout);
        public override UsernamePasswordCredentials? Resolve(AuthenticationContext context)
        {
            ValidateContext(context);
            ValidateCa();
            _ = options.Get(context);
            if (options.SupplementalCredentials is null) return null;
            return options.SupplementalCredentials is ISynchronousUsernamePasswordCredentialSource sync
                ? sync.Resolve(context) : throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired);
        }
        public override async ValueTask<UsernamePasswordCredentials?> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateContext(context);
            ValidateCa();
            _ = await options.GetAsync(context, cancellationToken).ConfigureAwait(false);
            return options.SupplementalCredentials is null ? null
                : await options.SupplementalCredentials.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        }
        private void ValidateCa()
        {
            if (caPath is null) return;
            var certificates = new X509Certificate2Collection();
            try
            {
                certificates.ImportFromPemFile(caPath);
                if (certificates.Count == 0) throw new InvalidOperationException();
            }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InvalidCertificate); }
            finally { foreach (var item in certificates) item.Dispose(); }
        }
        public override void Configure(DbConnection connection, AuthenticationContext context)
        {
            ValidateContext(context);
            if (context.ProviderInvariantName is Odbc or ConnectorNet)
            {
                ConfigureFileRoute(connection, context);
                return;
            }
            var npgsql = connection.GetType().FullName == "Npgsql.NpgsqlConnection";
            if (!npgsql && connection.GetType().FullName is not ("MySqlConnector.MySqlConnection" or "SingleStoreConnector.SingleStoreConnection")) Unsupported();
            var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            ValidateTlsMode(builder);
            builder["SSL Mode"] = "VerifyFull";
            if (caPath is not null) builder[npgsql ? "Root Certificate" : "SSL CA"] = Path.GetFullPath(caPath);
            if (npgsql)
            {
                builder["Require Auth"] = "None";
                builder["GSS Encryption Mode"] = "Disable";
            }
            IsolatePool(builder);
            connection.ConnectionString = builder.ConnectionString;
            options.RegisterPool(connection);
            InstallCallback(connection, options.Get(context));
        }
        private static void ValidateTlsMode(DbConnectionStringBuilder builder)
        {
            foreach (var key in builder.Keys.Cast<string>())
            {
                var normalized = new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                if (normalized == "sslmode" && !string.Equals(builder[key].ToString(), "VerifyFull", StringComparison.OrdinalIgnoreCase)) Conflict();
            }
        }
        private void ConfigureFileRoute(DbConnection connection, AuthenticationContext context)
        {
            var odbc = context.ProviderInvariantName == Odbc;
            if (connection.GetType().FullName != (odbc ? "System.Data.Odbc.OdbcConnection" : "MySql.Data.MySqlClient.MySqlConnection")) Unsupported();
            var builder = new DbConnectionStringBuilder(odbc) { ConnectionString = connection.ConnectionString };
            if (odbc)
            {
                ValidateMappedConnection(connection.ConnectionString, context);
                ConfigureOdbc(builder, context);
            }
            else ConfigureConnectorNet(builder);
            connection.ConnectionString = builder.ConnectionString;
            if (!odbc) options.RegisterPool(connection);
        }
        private void ConfigureConnectorNet(DbConnectionStringBuilder builder)
        {
            builder["SSL Mode"] = "VerifyFull";
            builder["Pooling"] = false; // Connector/NET has no application-name pool discriminator.
            if (options is Pkcs12CertificateOptions bundle)
            {
                builder["CertificateFile"] = bundle.BundlePath;
                builder["CertificatePassword"] = options.ResolvedPassword ?? "";
                return;
            }
            var pem = (X509CertificateOptions)options;
            builder["SslCert"] = pem.CertificatePath;
            builder["SslKey"] = pem.KeyPath;
            if (caPath is not null) builder["SslCa"] = Path.GetFullPath(caPath);
        }
        private void ConfigureOdbc(DbConnectionStringBuilder builder, AuthenticationContext context)
        {
            var family = OperatingSystemIdentityMappings.Family(context);
            var pem = (X509CertificateOptions)options;
            if (IsPostgres(family))
            {
                ConfigurePostgresOdbc(builder, pem);
                return;
            }
            var driver = builder["Driver"].ToString()?.Trim('{', '}') ?? "";
            if (IsMySqlDriver(driver)) builder["SSLMODE"] = "VERIFY_IDENTITY";
            else
            {
                builder["SSLVERIFY"] = 1;
                builder["FORCETLS"] = 1;
            }
            builder["SSLCERT"] = pem.CertificatePath;
            builder["SSLKEY"] = pem.KeyPath;
            if (caPath is not null) builder["SSLCA"] = Path.GetFullPath(caPath);
        }
        private void ConfigurePostgresOdbc(DbConnectionStringBuilder builder, X509CertificateOptions pem)
        {
            // libpq >=16 enforces certificate-only authentication and system trust.
            builder["SSLmode"] = "verify-full";
            var trustOptions = builder.TryGetValue("Pqopt", out var configuredTrust) ? configuredTrust.ToString()!.Trim('{', '}')
                : "gssencmode=disable sslrootcert=" + QuoteLibpq(caPath is null ? "system" : Path.GetFullPath(caPath));
            builder["Pqopt"] = trustOptions + " require_auth=none sslcertmode=require sslcert=" + QuoteLibpq(pem.CertificatePath ?? "")
                + " sslkey=" + QuoteLibpq(pem.KeyPath ?? "");
        }
        private void IsolatePool(DbConnectionStringBuilder builder)
        {
            var application = builder.TryGetValue("Application Name", out var value) ? value.ToString() : "";
            builder["Application Name"] = string.IsNullOrEmpty(application) ? options.PoolIdentity : application + ";" + options.PoolIdentity;
        }
        private static string QuoteLibpq(string value) => "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        private static bool IsMySql(string family) => family is "mysql" or "my" or "maria" or "mariadb";
        private static void InstallCallback(DbConnection connection, X509Certificate2 certificate)
        {
            var property = connection.GetType().GetProperty("ProvideClientCertificatesCallback")
                ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            var returnType = property.PropertyType.GetMethod("Invoke")?.ReturnType;
            var name = CallbackMethodName(returnType);
            var callback = new CertificateCallback(certificate);
            property.SetValue(connection, Delegate.CreateDelegate(property.PropertyType, callback,
                typeof(CertificateCallback).GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
                    ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication)));
        }
        private static string CallbackMethodName(Type? returnType)
        {
            if (returnType == typeof(void)) return nameof(CertificateCallback.Add);
            if (returnType == typeof(ValueTask)) return nameof(CertificateCallback.AddValueTask);
            if (returnType == typeof(Task)) return nameof(CertificateCallback.AddTask);
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        }
        private static bool IsSingleStore(string family)
            => family is "sg" or "sgs" or "single" or "singlestore" or "singlestoredb";
        private static bool IsPostgres(string scheme)
            => scheme is "postgres" or "postgresql" or "pgsql" or "pg" or "pgx" or "ts" or "timescale"
                or "cr" or "cockroach" or "cockroachdb" or "crdb" or "cdb";
        private static void Unsupported() => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        private static void Conflict() => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
    }
    private sealed class CertificateCallback(X509Certificate2 certificate)
    {
        public void Add(X509CertificateCollection certificates) { certificates.Clear(); certificates.Add(certificate); }
        public ValueTask AddValueTask(X509CertificateCollection certificates) { Add(certificates); return ValueTask.CompletedTask; }
        public Task AddTask(X509CertificateCollection certificates) { Add(certificates); return Task.CompletedTask; }
    }
}
