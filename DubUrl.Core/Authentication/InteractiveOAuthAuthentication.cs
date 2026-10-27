using System.Data.Common;

namespace DubUrl.Authentication;

/// <summary>Caller-owned provider session, including browser/cache lifetime and native opening coordination.</summary>
public interface IInteractiveOAuthSession
{
    ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, InteractiveOAuthOptions options, CancellationToken cancellationToken = default);
    ValueTask OpenAsync(DbConnection connection, CancellationToken cancellationToken = default);
}

public sealed class InteractiveOAuthOptions
{
    public required string ClientId { get; init; }
    public required Uri Authority { get; init; }
    public required Uri RedirectUri { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public required IInteractiveOAuthSession Session { get; init; }
    public string? LoginHint { get; init; }
    public bool AllowInteraction { get; init; }
    public int AcquisitionTimeoutSeconds { get; init; } = 120;
    public override string ToString() => "InteractiveOAuthOptions [REDACTED]";
}

public abstract partial class ConnectionAuthentication
{
    public virtual void OpenConnection(DbConnection connection) => connection.Open();
    public virtual async ValueTask OpenConnectionAsync(DbConnection connection, CancellationToken cancellationToken = default)
        => await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    public static ConnectionAuthentication OAuthInteractive(InteractiveOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientId);
        ArgumentNullException.ThrowIfNull(options.Session);
        if (!options.Authority.IsAbsoluteUri || options.Authority.Scheme != "https" || !options.Authority.IsDefaultPort
            || options.Authority.UserInfo.Length != 0 || options.Authority.Query.Length != 0 || options.Authority.Fragment.Length != 0
            || !options.RedirectUri.IsAbsoluteUri || !options.RedirectUri.IsLoopback || options.RedirectUri.Scheme != "http"
            || options.RedirectUri.UserInfo.Length != 0 || options.RedirectUri.Query.Length != 0 || options.RedirectUri.Fragment.Length != 0
            || options.AcquisitionTimeoutSeconds <= 0 || options.Scopes.Count == 0 || options.Scopes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Trusted HTTPS authority, registered loopback HTTP redirect, scopes and a positive acquisition timeout are required.");
        // Freeze the list; callers cannot change the identity/scope of a session through a mutable collection.
        var snapshot = new InteractiveOAuthOptions
        {
            ClientId = options.ClientId, Authority = options.Authority, RedirectUri = options.RedirectUri,
            Scopes = Array.AsReadOnly(options.Scopes.ToArray()), Session = options.Session, LoginHint = options.LoginHint,
            AllowInteraction = options.AllowInteraction, AcquisitionTimeoutSeconds = options.AcquisitionTimeoutSeconds
        };
        return new InteractiveAuthentication(snapshot);
    }

    private sealed class InteractiveAuthentication(InteractiveOAuthOptions options) : ConnectionAuthentication
    {
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.InteractiveOAuth;
        public override void Configure(DbConnection connection, AuthenticationContext context)
            => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired);
        public override ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, CancellationToken cancellationToken = default)
            => options.Session.ConfigureAsync(connection, context, options, cancellationToken);
        public override ValueTask OpenConnectionAsync(DbConnection connection, CancellationToken cancellationToken = default)
            => options.Session.OpenAsync(connection, cancellationToken);
    }
}
