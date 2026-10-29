using System.Data.Common;
using System.Diagnostics;

namespace DubUrl.Authentication;

public static class AuthenticationDiagnosticCodes
{
    public const string CredentialConflict = nameof(CredentialConflict);
    public const string CredentialResolutionFailed = nameof(CredentialResolutionFailed);
    public const string MissingCredentials = nameof(MissingCredentials);
    public const string UnsupportedAuthentication = nameof(UnsupportedAuthentication);
    public const string SynchronousCredentialsRequired = nameof(SynchronousCredentialsRequired);
    public const string AuthenticationFailed = nameof(AuthenticationFailed);
    public const string InvalidCertificate = nameof(InvalidCertificate);
    public const string InteractionRequired = nameof(InteractionRequired);
    public const string AuthenticationTimeout = nameof(AuthenticationTimeout);
}

public enum ConnectionAuthenticationKind { UsernamePassword, NativeToken, ClientCertificate, OperatingSystemIdentity, InteractiveOAuth, FileSecret }

/// <summary>A single runtime strategy. Applications own strategies, sources and their shared resources.</summary>
[DebuggerDisplay("{ToString(),nq}")]
public abstract partial class ConnectionAuthentication
{
    public abstract ConnectionAuthenticationKind Kind { get; }
    public virtual UsernamePasswordCredentials? Resolve(AuthenticationContext context)
        => null;
    public virtual ValueTask<UsernamePasswordCredentials?> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return new(Resolve(context)); }
    public abstract void Configure(DbConnection connection, AuthenticationContext context);
    public virtual ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); Configure(connection, context); return ValueTask.CompletedTask; }
    public sealed override string ToString() => $"ConnectionAuthentication {{ Kind = {Kind}, Credentials = [REDACTED] }}";

    public static ConnectionAuthentication UsernamePassword(IUsernamePasswordCredentialSource source)
        => new PasswordAuthentication(source ?? throw new ArgumentNullException(nameof(source)));
    public static ConnectionAuthentication UsernamePassword(string username, string password)
        => UsernamePassword(new FixedCredentials(new(username, password)));
    public static ConnectionAuthentication Password(string password)
        => UsernamePassword(new FixedCredentials(new(password: password)));

    private sealed class FixedCredentials(UsernamePasswordCredentials credentials) : ISynchronousUsernamePasswordCredentialSource
    {
        public UsernamePasswordCredentials Resolve(AuthenticationContext context) => credentials;
        public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return new(credentials); }
        public override string ToString() => "FixedCredentials [REDACTED]";
    }
    private sealed class PasswordAuthentication(IUsernamePasswordCredentialSource source) : ConnectionAuthentication
    {
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.UsernamePassword;
        public override UsernamePasswordCredentials Resolve(AuthenticationContext context)
            => source is ISynchronousUsernamePasswordCredentialSource synchronous ? synchronous.Resolve(context)
                : throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired);
        public override async ValueTask<UsernamePasswordCredentials?> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
            => await source.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        public override void Configure(DbConnection connection, AuthenticationContext context) { }
    }
}

[DebuggerDisplay("UsernamePasswordCredentials [REDACTED]")]
public sealed class UsernamePasswordCredentials(string? username = null, string? password = null)
{
    public string? Username { get; } = username;
    public string? Password { get; } = password;
    public override string ToString() => "UsernamePasswordCredentials [REDACTED]";
}

public interface IUsernamePasswordCredentialSource
{
    ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default);
}
public interface ISynchronousUsernamePasswordCredentialSource : IUsernamePasswordCredentialSource
{
    UsernamePasswordCredentials Resolve(AuthenticationContext context);
}

/// <summary>Selected location metadata only. Never includes a URL, query string, password or token.</summary>
public sealed class AuthenticationContext(string providerInvariantName, string scheme, string? host, int? port, string? database, string? username)
{
    public string ProviderInvariantName { get; } = providerInvariantName;
    public string Scheme { get; } = scheme;
    public string? Host { get; } = host;
    public int? Port { get; } = port;
    public string? Database { get; } = database;
    public string? Username { get; } = username;
    public override string ToString() => "AuthenticationContext [REDACTED]";
}

/// <summary>Stable safe failure. Intentionally retains neither an unsafe message nor an inner exception.</summary>
public sealed class ConnectionAuthenticationException : Exception
{
    public string Code { get; }
    public ConnectionAuthenticationException(string code) : base(MessageFor(code)) => Code = KnownCode(code);
    private static string KnownCode(string code) => code switch
    {
        AuthenticationDiagnosticCodes.CredentialConflict or AuthenticationDiagnosticCodes.CredentialResolutionFailed or AuthenticationDiagnosticCodes.MissingCredentials or AuthenticationDiagnosticCodes.UnsupportedAuthentication
            or AuthenticationDiagnosticCodes.SynchronousCredentialsRequired or AuthenticationDiagnosticCodes.AuthenticationFailed or AuthenticationDiagnosticCodes.InvalidCertificate or AuthenticationDiagnosticCodes.InteractionRequired or AuthenticationDiagnosticCodes.AuthenticationTimeout => code,
        _ => AuthenticationDiagnosticCodes.AuthenticationFailed
    };
    private static string MessageFor(string code) => KnownCode(code) switch
    {
        AuthenticationDiagnosticCodes.CredentialConflict => "Competing authentication settings are not allowed.",
        AuthenticationDiagnosticCodes.CredentialResolutionFailed => "The credential source could not be resolved.",
        AuthenticationDiagnosticCodes.MissingCredentials => "The selected strategy requires credentials.",
        AuthenticationDiagnosticCodes.UnsupportedAuthentication => "This authentication strategy is not supported for the selected provider.",
        AuthenticationDiagnosticCodes.SynchronousCredentialsRequired => "This credential source requires an asynchronous connection operation.",
        AuthenticationDiagnosticCodes.InvalidCertificate => "The client certificate is unavailable or has no usable private key.",
        AuthenticationDiagnosticCodes.InteractionRequired => "Explicit user interaction is required for authentication.",
        AuthenticationDiagnosticCodes.AuthenticationTimeout => "The interactive authentication attempt timed out.",
        _ => "Database authentication failed."
    };
}
