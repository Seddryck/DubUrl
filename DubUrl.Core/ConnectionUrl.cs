using DubUrl.Extensions;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Authentication;

namespace DubUrl;

public class ConnectionUrl : BaseConnectionUrl
{
    private ConnectionAuthentication? authentication;
    private ConnectionAuthentication RequiredAuthentication => authentication ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
    internal bool HasAuthentication => authentication is not null;
    public override IDialect Dialect => HasAuthentication ? Registry.GetMapper(Parser.Parse(Url).Scheme).GetDialect() : base.Dialect;
    public override IConnectivity Connectivity => HasAuthentication ? Registry.GetMapper(Parser.Parse(Url).Scheme).GetConnectivity() : base.Connectivity;
    public override IParametrizer Parametrizer => HasAuthentication ? Registry.GetMapper(Parser.Parse(Url).Scheme).GetParametrizer() : base.Parametrizer;
    public override DubUrl.BulkCopy.IBulkCopyProvider? BulkCopyProvider
    {
        get
        {
            if (!HasAuthentication) return base.BulkCopyProvider;
            return TryGetCapability<DubUrl.BulkCopy.IBulkCopyProvider>(out var provider) ? provider : null;
        }
    }

    public ConnectionUrl WithAuthentication(ConnectionAuthentication strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        return new ConnectionUrl(Url, Parser, Registry) { authentication = strategy };
    }

    /// <summary>Validates strategy conflicts without retrieving credentials or creating a connection.</summary>
    public void ValidateAuthentication()
    {
        if (authentication is not null)
            AuthenticationConfiguration.Validate(Parser.Parse(Url), authentication);
    }

    public ConnectionUrl(string url)
        : this(url, DefaultRegistry.Get()) { }

    public ConnectionUrl(string url, ISchemeRegistry registry)
        : this(url, new Parser(), registry) { }

    internal ConnectionUrl(string url, IParser parser, ISchemeRegistry registry)
        : base(url, parser, registry) { }

    public virtual IDbConnection Connect()
    {
        if (authentication is not null) return ConnectAuthenticated();
        var connection = GetProviderFactory().CreateConnection() ?? throw new ArgumentNullException();
        try
        {
            connection.ConnectionString = Result.ConnectionString;
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public virtual IDbConnection Open()
    {
        var connection = Connect();
        try
        {
            if (authentication is not null) authentication.OpenConnection((DbConnection)connection);
            else connection.Open();
            return connection;
        }
        catch (Exception exception)
        {
            connection.Dispose();
            if (authentication is not null && exception is not OperationCanceledException)
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
            throw;
        }
    }

    public virtual async ValueTask<DbConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (authentication is null) return (DbConnection)Connect();
        DbConnection? connection = null;
        try
        {
            var (uri, context) = PrepareAuthentication();
            UsernamePasswordCredentials? credentials;
            try { credentials = await authentication.ResolveAsync(context, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (ConnectionAuthenticationException) { throw; }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
            cancellationToken.ThrowIfCancellationRequested();
            connection = CreateAuthenticatedConnection(uri, credentials);
            await authentication.ConfigureAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (Exception exception)
        {
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            if (exception is OperationCanceledException or ConnectionAuthenticationException) throw;
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
        }
    }

    public virtual async ValueTask<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await OpenPreparedAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            if (authentication is not null && exception is not OperationCanceledException)
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
            throw;
        }
    }

    internal async ValueTask OpenPreparedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (authentication is null) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        else
        {
            try { await authentication.OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (ConnectionAuthenticationException) { throw; }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed); }
        }
    }
    private DbConnection ConnectAuthenticated()
    {
        DbConnection? connection = null;
        try
        {
            var (uri, context) = PrepareAuthentication();
            UsernamePasswordCredentials? credentials;
            try { credentials = RequiredAuthentication.Resolve(context); }
            catch (ConnectionAuthenticationException) { throw; }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
            connection = CreateAuthenticatedConnection(uri, credentials);
            RequiredAuthentication.Configure(connection, context);
            return connection;
        }
        catch (Exception exception)
        {
            connection?.Dispose();
            if (exception is ConnectionAuthenticationException) throw;
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
        }
    }

    private (ConnectionUri Uri, AuthenticationContext Context) PrepareAuthentication()
    {
        var uri = Parser.Parse(Url);
        AuthenticationConfiguration.Validate(uri, RequiredAuthentication);
        var provider = Registry.GetMapper(uri.Scheme).GetProviderName();
        if (RequiredAuthentication.Kind == ConnectionAuthenticationKind.UsernamePassword)
            AuthenticationConfiguration.ValidatePasswordProvider(provider);
        return (uri, new(provider, uri.Scheme.Canonical, uri.Host, uri.Port == 0 ? null : uri.Port,
            uri.Segments.LastOrDefault(), uri.HasUsername ? uri.Username : null));
    }

    private DbConnection CreateAuthenticatedConnection(ConnectionUri uri, UsernamePasswordCredentials? credentials)
    {
        // This transient structured mapping is deliberately never stored in BaseConnectionUrl.Result.
        var resolved = Registry.Resolve(AuthenticationConfiguration.Apply(uri, RequiredAuthentication, credentials));
        var connection = resolved.ProviderFactory.CreateConnection() ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        try
        {
            connection.ConnectionString = resolved.ConnectionString;
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }
}
