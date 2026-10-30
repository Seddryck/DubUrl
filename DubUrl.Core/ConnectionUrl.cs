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
        return new ConnectionUrl(Url, Parser, Registry) { authentication = strategy, configuration = configuration, readOnly = readOnly };
    }

    /// <summary>Validates strategy conflicts without retrieving credentials or creating a connection.</summary>
    public void ValidateAuthentication()
    {
        if (authentication is not null)
            _ = PrepareAuthentication();
    }

    /// <summary>Checks required route support without acquiring credentials or opening a connection.</summary>
    public void ValidateConfiguration()
    {
        if (configuration.Length == 0 && !readOnly) return;
        var uri = Parser.Parse(Url);
        ValidateConfigurationSupport(uri);
        if (authentication is null) _ = Result;
    }

    internal int? ConfiguredCommandTimeout
    {
        get
        {
            var requests = configuration.Where(r => r.Option == ConnectionConfigurationOption.CommandTimeout).ToArray();
            return (requests.LastOrDefault(r => !r.Optional) ?? requests.LastOrDefault())?.Value as int?;
        }
    }

    private ConnectionConfigurationRequest[] configuration = [];
    private bool readOnly;
    private IReadOnlyList<SkippedConnectionOption> skippedOptions = Array.Empty<SkippedConnectionOption>();

    /// <summary>Resolves configuration and reports unsupported optional settings without credentials.</summary>
    public IReadOnlyList<SkippedConnectionOption> SkippedOptions
    {
        get { _ = Result; return skippedOptions; }
    }

    public ConnectionUrl WithApplicationName(string applicationName)
        => WithRequest(ConnectionConfigurationOption.ApplicationName, OptionalConnectionOptions.ValidateApplicationName(applicationName));

    public ConnectionUrl WithPooling(ConnectionPoolingOptions options)
        => WithRequest(ConnectionConfigurationOption.Pooling, OptionalConnectionOptions.ValidatePooling(options));

    public ConnectionUrl WithConnectionTimeout(TimeSpan timeout)
        => WithRequest(ConnectionConfigurationOption.ConnectionTimeout, OptionalConnectionOptions.ValidateTimeout(timeout));

    public ConnectionUrl WithCommandTimeout(TimeSpan timeout)
        => WithRequest(ConnectionConfigurationOption.CommandTimeout, OptionalConnectionOptions.ValidateTimeout(timeout));

    public ConnectionUrl WithEncryption(ConnectionEncryption encryption)
        => WithRequest(ConnectionConfigurationOption.Encryption, encryption ?? throw new ArgumentNullException(nameof(encryption)));

    public ConnectionUrl WithTimeZone(string timeZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        // Zone identifiers and numeric offsets only; never interpolate SQL or replace a zone with today's offset.
        if (timeZone.Length > 255 || timeZone.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '-' or '+' or ':' or '.')))
            throw new ArgumentException("Use a named time zone or an offset such as +02:00.", nameof(timeZone));
        if (!char.IsAsciiLetter(timeZone[0]))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(timeZone, @"^[+-]\d{2}:[0-5]\d$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
                || !TimeSpan.TryParseExact(timeZone[1..], @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var offset)
                || offset > (timeZone[0] == '-' ? TimeSpan.FromHours(12) : TimeSpan.FromHours(14)))
                throw new ArgumentException("Offsets must be between -12:00 and +14:00 in signed HH:mm format.", nameof(timeZone));
        }
        return WithRequest(ConnectionConfigurationOption.TimeZone, timeZone);
    }

    /// <summary>Requires native read-only access to an existing primary database file.</summary>
    /// <remarks>Strict and idempotent. Unsupported routes throw before creating a connection.
    /// Temporary objects, attached databases and external side effects follow native engine rules.</remarks>
    public ConnectionUrl WithReadOnly()
        => new(Url, Parser, Registry) { authentication = authentication, configuration = configuration, readOnly = true };

    /// <summary>Skips unsupported settings only. Invalid values and provider failures still throw.</summary>
    public ConnectionUrl WithOptional(Action<OptionalConnectionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new OptionalConnectionOptions();
        configure(options);
        return WithRequests(options.Snapshot());
    }

    private ConnectionUrl WithRequest(ConnectionConfigurationOption option, object value)
        => WithRequests([new(option, value, false)]);

    private ConnectionUrl WithRequests(ConnectionConfigurationRequest[] requests)
        => new(Url, Parser, Registry) { authentication = authentication, configuration = [.. configuration, .. requests], readOnly = readOnly };

    protected override ResolvedConnection ResolveConnection(ConnectionUri connectionUri)
    {
        return ApplyConfiguration(connectionUri, base.ResolveConnection(connectionUri));
    }

    private ResolvedConnection ApplyConfiguration(ConnectionUri connectionUri, ResolvedConnection resolved)
    {
        if (configuration.Length == 0 && !readOnly) return resolved;
        Registry.TryGetCapability<ConnectionConfigurationCapability>(connectionUri.Scheme, out var capability);
        var provider = resolved.ProviderFactory.GetType().FullName ?? resolved.ProviderFactory.GetType().Name;
        var readOnlyCapability = ResolveReadOnlyCapability(connectionUri, provider);
        var applicable = ResolveConfigurationRequests(connectionUri, capability, provider, out var skipped);
        skippedOptions = skipped.AsReadOnly();
        if (applicable.Count == 0 && !readOnly) return resolved;
        var builder = resolved.ProviderFactory.CreateConnectionStringBuilder()
            ?? throw new NotSupportedException($"Provider '{provider}' does not supply a connection-string builder for shared configuration.");
        builder.ConnectionString = resolved.ConnectionString;
        foreach (var request in applicable) capability!.Apply(builder, request);
        capability?.ValidateNative(builder, applicable);
        // Always last: optional/native options cannot silently weaken the required access mode.
        readOnlyCapability?.Apply(builder);
        return resolved with { ConnectionString = builder.ConnectionString };
    }

    private ReadOnlyConnectionCapability? ResolveReadOnlyCapability(ConnectionUri connectionUri, string provider)
    {
        if (!readOnly) return null;
        if (Registry.TryGetCapability<ReadOnlyConnectionCapability>(connectionUri.Scheme, out var capability) && capability is not null)
            return capability;
        throw new NotSupportedException($"ReadOnly is not supported for database route '{connectionUri.Scheme.GetRegistryAlias()}' using provider '{provider}'. A transaction default or replica-routing hint is not native read-only file access.");
    }

    private List<ConnectionConfigurationRequest> ResolveConfigurationRequests(ConnectionUri connectionUri,
        ConnectionConfigurationCapability? capability, string provider, out List<SkippedConnectionOption> skipped)
    {
        skipped = [];
        var applicable = new List<ConnectionConfigurationRequest>();
        foreach (var group in configuration.GroupBy(request => request.Option))
        {
            // An optional call must never weaken a required setting, irrespective of call order.
            var request = group.LastOrDefault(item => !item.Optional) ?? group.Last();
            if (capability?.Supports(request) == true) applicable.Add(request);
            else if (request.Optional) skipped.Add(new(request.Option, provider, "Unsupported by the selected provider route or requested pool-size configuration."));
            else throw new NotSupportedException($"{request.Option} is not supported for database route '{connectionUri.Scheme.GetRegistryAlias()}' using provider '{provider}'.");
        }
        return applicable;
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
            if (readOnly && TryGetCapability<ReadOnlyConnectionCapability>(out var capability))
                capability!.Configure(connection);
            ConfigureSession(connection);
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
            if (authentication is not null && exception is not OperationCanceledException && !IsNativeTimeout(exception))
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
            connection = CreateAuthenticatedConnection(uri, credentials, context);
            await authentication.ConfigureAsync(connection, context, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (Exception exception)
        {
            if (connection is not null) await connection.DisposeAsync().ConfigureAwait(false);
            if (exception is OperationCanceledException or ConnectionAuthenticationException or ArgumentException
                || (exception is NotSupportedException && !readOnly)) throw;
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
        }
    }

    public virtual ValueTask<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        => OpenConfiguredAsync(null, cancellationToken);

    /// <summary>Creates an owned connection, applies caller configuration before opening through the selected strategy, and disposes it on failure.</summary>
    public virtual ValueTask<DbConnection> OpenAsync(Action<DbConnection> configureConnection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configureConnection);
        return OpenConfiguredAsync(configureConnection, cancellationToken);
    }

    private async ValueTask<DbConnection> OpenConfiguredAsync(Action<DbConnection>? configureConnection, CancellationToken cancellationToken)
    {
        var connection = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            configureConnection?.Invoke(connection);
            await OpenPreparedAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (Exception exception)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            if (authentication is not null && exception is not (OperationCanceledException or ConnectionAuthenticationException) && !IsNativeTimeout(exception))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
            throw;
        }
    }

    internal async ValueTask OpenPreparedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        SessionTimeZone.SetCancellation(connection, cancellationToken);
        try
        {
            if (authentication is null) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            else
            {
                try { await authentication.OpenConnectionAsync(connection, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
                catch (ConnectionAuthenticationException) { throw; }
                catch (Exception error) when (IsNativeTimeout(error)) { throw; }
                catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed); }
            }
        }
        finally { SessionTimeZone.SetCancellation(connection, default); }
    }

    private static bool IsNativeTimeout(Exception error)
    {
        if (error is TimeoutException) return true;
        if (error is not DbException) return false;
        for (var inner = error.InnerException; inner is not null; inner = inner.InnerException)
            if (inner is TimeoutException) return true;
        // SqlClient reports native timeout number -2 without a TimeoutException inner cause.
        return error.GetType().FullName == "Microsoft.Data.SqlClient.SqlException"
            && error.GetType().GetProperty("Number")?.GetValue(error) is -2;
    }

    private void ConfigureSession(DbConnection connection)
    {
        if (configuration.Length > 0 && TryGetCapability<ConnectionConfigurationCapability>(out var capability))
            capability!.Configure(connection, configuration);
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
            connection = CreateAuthenticatedConnection(uri, credentials, context);
            RequiredAuthentication.Configure(connection, context);
            return connection;
        }
        catch (Exception exception)
        {
            connection?.Dispose();
            if (exception is ConnectionAuthenticationException or ArgumentException
                || (exception is NotSupportedException && !readOnly)) throw;
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
        }
    }

    private (ConnectionUri Uri, AuthenticationContext Context) PrepareAuthentication()
    {
        var uri = Parser.Parse(Url);
        AuthenticationConfiguration.Validate(uri, RequiredAuthentication);
        ValidateConfigurationSupport(uri);
        var provider = Registry.GetMapper(uri.Scheme).GetProviderName();
        var context = new AuthenticationContext(provider, uri.Scheme.Canonical, uri.Host, uri.Port == 0 ? null : uri.Port,
            uri.Segments.LastOrDefault(), uri.HasUsername ? uri.Username : null);
        RequiredAuthentication.ValidateContext(context);
        RequiredAuthentication.ValidateConnectionConfiguration(
            configuration.LastOrDefault(r => r.Option == ConnectionConfigurationOption.Encryption)?.Value as ConnectionEncryption,
            configuration.LastOrDefault(r => r.Option == ConnectionConfigurationOption.Pooling && !r.Optional)?.Value as ConnectionPoolingOptions,
            context);
        if (configuration.Length > 0)
        {
            // Mapping-only preflight: validate native settings before a password prompt, token
            // acquisition or certificate-file load. These placeholders never reach a connection.
            var placeholder = RequiredAuthentication.Kind == ConnectionAuthenticationKind.UsernamePassword
                ? new UsernamePasswordCredentials(uri.HasUsername ? uri.Username : "configuration-validation", "") : null;
            _ = ApplyConfiguration(uri, Registry.Resolve(AuthenticationConfiguration.Apply(uri, RequiredAuthentication, placeholder, context)));
        }
        if (RequiredAuthentication.Kind == ConnectionAuthenticationKind.ClientCertificate && provider == "System.Data.Odbc")
            RequiredAuthentication.ValidateMappedConnection(Registry.Resolve(uri).ConnectionString, context);
        return (uri, context);
    }

    private void ValidateConfigurationSupport(ConnectionUri uri)
    {
        var encryption = configuration.LastOrDefault(r => r.Option == ConnectionConfigurationOption.Encryption)?.Value as ConnectionEncryption;
        if (authentication?.ServerCaPath is string ca && encryption?.CaPath is string selectedCa
            && !string.Equals(Path.GetFullPath(ca), selectedCa, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Certificate authentication and encryption specify conflicting CA files.");
        // Check policy without mapping an unauthenticated URL: some mappers require
        // credentials or native authentication options supplied by the strategy.
        if (readOnly && !Registry.TryGetCapability<ReadOnlyConnectionCapability>(uri.Scheme, out _))
            throw new NotSupportedException("Native read-only access is unsupported for the selected provider route.");
        if (configuration.Length == 0) return;
        Registry.TryGetCapability<ConnectionConfigurationCapability>(uri.Scheme, out var capability);
        foreach (var group in configuration.GroupBy(request => request.Option))
        {
            var request = group.LastOrDefault(item => !item.Optional) ?? group.Last();
            if (!request.Optional && capability?.Supports(request) != true)
                throw new NotSupportedException("A required connection option is unsupported for the selected provider route.");
        }
    }

    private DbConnection CreateAuthenticatedConnection(ConnectionUri uri, UsernamePasswordCredentials? credentials, AuthenticationContext context)
    {
        // This transient structured mapping is deliberately never stored in BaseConnectionUrl.Result.
        var resolved = ApplyConfiguration(uri, Registry.Resolve(AuthenticationConfiguration.Apply(uri, RequiredAuthentication, credentials, context)));
        var connection = resolved.ProviderFactory.CreateConnection() ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        try
        {
            connection.ConnectionString = resolved.ConnectionString;
            if (readOnly && TryGetCapability<ReadOnlyConnectionCapability>(out var capability))
                capability!.Configure(connection);
            ConfigureSession(connection);
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }
}
