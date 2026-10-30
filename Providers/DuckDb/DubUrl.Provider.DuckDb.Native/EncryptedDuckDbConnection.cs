using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using DuckDB.NET.Data;
using DuckDB.NET.Native;
using DubUrl.Authentication;

namespace DubUrl.Provider.DuckDb.Native;

/// <summary>A native DuckDB connection that unlocks an existing encrypted file on every open.</summary>
public sealed class EncryptedDuckDbConnection : DuckDBConnection, IFileSecretConnection
{
    private const string Catalog = "secured";
    private readonly object lifecycle = new();
    private readonly string path;
    private readonly bool readOnly;
    private FileSecretCredentials? urlCredentials;
    private readonly Action<string>? initializationObserver;
    private IFileSecretCredentialSource? source;
    private AuthenticationContext? context;
    private CancellationTokenSource? openingCancellation;
    private long cycle;
    private int connecting;
    private bool disposed;
    private bool configurationLocked;

    internal EncryptedDuckDbConnection(string connectionString, FileSecretCredentials? credentials, Action<string>? initializationObserver = null)
    {
        var builder = new DuckDBConnectionStringBuilder { ConnectionString = connectionString };
        path = builder.DataSource;
        if (string.IsNullOrWhiteSpace(path) || path.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("md:", StringComparison.OrdinalIgnoreCase) || path.IndexOf('\0') >= 0)
            throw new NotSupportedException("Encrypted DuckDB connections require a local database file.");
        path = Path.GetFullPath(path);
        readOnly = builder.TryGetValue("access_mode", out var mode)
            && string.Equals(Convert.ToString(mode), "READ_ONLY", StringComparison.OrdinalIgnoreCase);
        // Read-only applies to the attached file, not to the bootstrap memory database.
        builder.Remove("access_mode");
        builder.DataSource = ":memory:";
        base.ConnectionString = builder.ConnectionString;
        urlCredentials = credentials;
        this.initializationObserver = initializationObserver;
        configurationLocked = true;
    }

    [AllowNull]
    public override string ConnectionString
    {
        get => base.ConnectionString;
        set
        {
            if (configurationLocked) throw new NotSupportedException("A prepared encrypted connection cannot be reconfigured.");
            base.ConnectionString = value;
        }
    }

    public override string DataSource => path;
    public override string Database => Catalog;
    public override ConnectionState State => Volatile.Read(ref connecting) == 1 ? ConnectionState.Connecting : base.State;

    void IFileSecretConnection.ConfigureFileSecret(IFileSecretCredentialSource credentialSource, AuthenticationContext authenticationContext)
    {
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (State != ConnectionState.Closed || urlCredentials is not null || source is not null)
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
            source = credentialSource;
            context = authenticationContext;
        }
    }

    protected override void OnStateChange(StateChangeEventArgs change)
    {
        if (Volatile.Read(ref connecting) == 0) base.OnStateChange(change);
    }

    public override void Open()
    {
        var lease = StartOpening(CancellationToken.None);
        try
        {
            FileSecretCredentials? credentials;
            if (source is null) credentials = urlCredentials;
            else if (source is ISynchronousFileSecretCredentialSource synchronous)
            {
                try { credentials = synchronous.Resolve(context!); }
                catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
            }
            else throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired);
            CompleteOpening(lease.Cycle, credentials, lease.Cancellation.Token);
        }
        catch { FailOpening(lease.Cycle); throw; }
        finally { EndOpening(lease.Cycle, lease.Cancellation); }
    }

    public override async Task OpenAsync(CancellationToken cancellationToken)
    {
        var lease = StartOpening(cancellationToken);
        try
        {
            FileSecretCredentials? credentials;
            if (source is null) credentials = urlCredentials;
            else
            {
                try { credentials = await source.ResolveAsync(context!, lease.Cancellation.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
            }
            CompleteOpening(lease.Cycle, credentials, lease.Cancellation.Token);
        }
        catch { FailOpening(lease.Cycle); throw; }
        finally { EndOpening(lease.Cycle, lease.Cancellation); }
    }

    private (long Cycle, CancellationTokenSource Cancellation) StartOpening(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        lock (lifecycle)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (State != ConnectionState.Closed) throw new InvalidOperationException("Connection is already open or opening.");
            if (!File.Exists(path)) throw new FileNotFoundException("Encrypted DuckDB connections require an existing database file.");
            openingCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Volatile.Write(ref connecting, 1);
            return (++cycle, openingCancellation);
        }
    }

    private void CompleteOpening(long openingCycle, FileSecretCredentials? credentials, CancellationToken token)
    {
        lock (lifecycle)
        {
            token.ThrowIfCancellationRequested();
            if (disposed || cycle != openingCycle) throw new OperationCanceledException(token);
            if (credentials is null || string.IsNullOrEmpty(credentials.Secret) || credentials.Secret.IndexOf('\0') >= 0)
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
            try
            {
                RequireSupportedVersion(ServerVersion);
                base.Open();
                initializationObserver?.Invoke("opened");
                token.ThrowIfCancellationRequested();
                ExecuteInitialization("ATTACH " + Literal(path) + " AS \"" + Catalog + "\" (ENCRYPTION_KEY ?"
                    + (readOnly ? ", READ_ONLY)" : ")"), token, credentials.Secret);
                initializationObserver?.Invoke("attached");
                ExecuteInitialization("USE \"" + Catalog + "\"", token);
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) { throw; }
            catch (ConnectionAuthenticationException) { throw; }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed); }
            Volatile.Write(ref connecting, 0);
            // The base connection's original event was suppressed. Subscriber commands are now safe.
            base.OnStateChange(new(ConnectionState.Closed, ConnectionState.Open));
        }
    }

    private void ExecuteInitialization(string sql, CancellationToken token, string? key = null)
    {
        token.ThrowIfCancellationRequested();
        var native = NativeConnection;
        using var registration = token.Register(() =>
        {
            try { native.Interrupt(); }
            catch { /* Best effort. The query owns the result and its failure. */ }
        });
        DuckDBResult result = default;
        try
        {
            DuckDBState status;
            if (key is null) status = NativeMethods.Query.DuckDBQuery(native, sql, out result);
            else
            {
                // ATTACH accepts a bound encryption key, but not a bound file path.
                var preparation = NativeMethods.PreparedStatements.DuckDBPrepare(native, sql, out var statement);
                using (statement)
                {
                    token.ThrowIfCancellationRequested();
                    RequireSuccess(preparation);
                    var binding = NativeMethods.PreparedStatements.DuckDBBindVarchar(statement, 1, key);
                    token.ThrowIfCancellationRequested();
                    RequireSuccess(binding);
                    status = NativeMethods.PreparedStatements.DuckDBExecutePrepared(statement, out result);
                }
            }
            token.ThrowIfCancellationRequested();
            RequireSuccess(status);
        }
        finally { NativeMethods.Query.DuckDBDestroyResult(ref result); }
    }

    private static void RequireSuccess(DuckDBState status)
    {
        if (status != DuckDBState.Success)
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
    }

    private void FailOpening(long openingCycle)
    {
        lock (lifecycle)
        {
            if (cycle != openingCycle) return;
            // Hide cleanup events even if a successful-open subscriber itself failed.
            Volatile.Write(ref connecting, 1);
            try { if (base.State == ConnectionState.Open) base.Close(); }
            finally { Volatile.Write(ref connecting, 0); }
        }
    }

    private void EndOpening(long openingCycle, CancellationTokenSource cancellation)
    {
        lock (lifecycle)
        {
            if (cycle == openingCycle) openingCancellation = null;
            cancellation.Dispose();
        }
    }

    public override void Close()
    {
        lock (lifecycle)
        {
            ++cycle;
            var wasConnecting = Volatile.Read(ref connecting) == 1;
            try { openingCancellation?.Cancel(); }
            catch { /* A caller-owned source must not prevent connection cleanup. */ }
            openingCancellation = null;
            try { if (base.State == ConnectionState.Open) base.Close(); }
            finally { if (wasConnecting) Volatile.Write(ref connecting, 0); }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (lifecycle)
            {
                disposed = true;
                Close();
                source = null;
                context = null;
                urlCredentials = null;
            }
        }
        base.Dispose(disposing);
    }

    public override DataTable GetSchema() => GetSchema(DbMetaDataCollectionNames.MetaDataCollections, null);
    public override DataTable GetSchema(string collectionName) => GetSchema(collectionName, null);
    public override DataTable GetSchema(string collectionName, string?[]? restrictionValues)
    {
        if (new[] { "Tables", "Columns", "ForeignKeys", "Indexes" }.Contains(collectionName, StringComparer.OrdinalIgnoreCase))
        {
            restrictionValues = restrictionValues is null || restrictionValues.Length == 0 ? new string?[1] : (string?[])restrictionValues.Clone();
            restrictionValues[0] ??= Catalog;
        }
        return base.GetSchema(collectionName, restrictionValues);
    }

    /// <summary>Duplication cannot preserve this encrypted connection's initialization contract.</summary>
    public new DuckDBConnection Duplicate() => throw new NotSupportedException("Use a new prepared encrypted connection instead of Duplicate().");

    internal static string Literal(string value)
    {
        if (value.IndexOf('\0') >= 0) throw new ArgumentException("SQL literals cannot contain a NUL character.");
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    internal static void RequireSupportedVersion(string engineVersion)
    {
        var numeric = engineVersion.TrimStart('v').Split('-', '+')[0];
        if (!Version.TryParse(numeric, out var version) || version < new Version(1, 4, 0))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedProviderVersion);
    }
}
