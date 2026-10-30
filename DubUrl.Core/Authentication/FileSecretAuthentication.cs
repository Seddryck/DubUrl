using System.Data;
using System.Data.Common;
using System.Diagnostics;

namespace DubUrl.Authentication;

/// <summary>A file-unlocking secret, distinct from a server account's username/password.</summary>
[DebuggerDisplay("FileSecretCredentials [REDACTED]")]
public sealed class FileSecretCredentials(string secret)
{
    public string Secret { get; } = secret;
    public override string ToString() => "FileSecretCredentials [REDACTED]";
}

/// <summary>The caller owns this source. Other file-based adapters may use the same contract for encryption keys.</summary>
public interface IFileSecretCredentialSource
{
    ValueTask<FileSecretCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default);
}

public interface ISynchronousFileSecretCredentialSource : IFileSecretCredentialSource
{
    FileSecretCredentials Resolve(AuthenticationContext context);
}

public abstract partial class ConnectionAuthentication
{
    /// <summary>Unlocks an Access database through Windows ACE OLE DB. No database username is used.</summary>
    public static ConnectionAuthentication FilePassword(string password)
        => FilePassword(new FixedFileSecretSource(new(password)));

    public static ConnectionAuthentication FilePassword(IFileSecretCredentialSource source)
        => new FilePasswordAuthentication(source ?? throw new ArgumentNullException(nameof(source)));

    private sealed class FixedFileSecretSource(FileSecretCredentials credentials) : ISynchronousFileSecretCredentialSource
    {
        public FileSecretCredentials Resolve(AuthenticationContext context) => credentials;
        public ValueTask<FileSecretCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return new(credentials); }
        public override string ToString() => "FixedFileSecretSource [REDACTED]";
    }

    private sealed class FilePasswordAuthentication(IFileSecretCredentialSource source) : ConnectionAuthentication
    {
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.FileSecret;

        // Preflight runs before native mapping/creation, without acquiring a secret.
        public override UsernamePasswordCredentials? Resolve(AuthenticationContext context)
        {
            ValidateProvider(context);
            return null;
        }

        private static void ValidateProvider(AuthenticationContext context)
        {
            var parts = context.Scheme.Split('+');
            if (!OperatingSystem.IsWindows() || context.ProviderInvariantName != "System.Data.OleDb"
                || parts.Length != 2 || !parts.Contains("oledb", StringComparer.Ordinal)
                || !parts.Any(part => part is "accdb" or "access" or "msaccess" or "mdb"))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        }

        public override void Configure(DbConnection connection, AuthenticationContext context)
        {
            var builder = Prepare(connection, context);
            if (source is not ISynchronousFileSecretCredentialSource synchronous)
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired);
            FileSecretCredentials credentials;
            try { credentials = synchronous.Resolve(context); }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
            Apply(connection, builder, credentials);
        }

        public override async ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var builder = Prepare(connection, context);
            FileSecretCredentials credentials;
            try { credentials = await source.ResolveAsync(context, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
            cancellationToken.ThrowIfCancellationRequested();
            Apply(connection, builder, credentials);
        }

        private static DbConnectionStringBuilder Prepare(DbConnection connection, AuthenticationContext context)
        {
            ValidateProvider(context);
            if (connection.GetType().FullName != "System.Data.OleDb.OleDbConnection" || connection.State != ConnectionState.Closed)
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            if (!builder.TryGetValue("Provider", out var provider)
                || !new[] { "Microsoft.ACE.OLEDB.12.0", "Microsoft.ACE.OLEDB.16.0" }.Contains(provider.ToString(), StringComparer.OrdinalIgnoreCase))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            foreach (var key in builder.Keys.Cast<string>())
            {
                var normalized = new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
                if (normalized is "userid" or "password" or "extendedproperties" or "jetoledbsystemdatabase"
                    || normalized.EndsWith("password", StringComparison.Ordinal))
                    throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
            }
            return builder;
        }

        private static void Apply(DbConnection connection, DbConnectionStringBuilder builder, FileSecretCredentials? credentials)
        {
            if (credentials is null || string.IsNullOrEmpty(credentials.Secret))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
            builder["Jet OLEDB:Database Password"] = credentials.Secret;
            connection.ConnectionString = builder.ConnectionString;
        }
    }
}
