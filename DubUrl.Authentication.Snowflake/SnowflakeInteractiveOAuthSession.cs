using System.Data.Common;
using Snowflake.Data.Client;

namespace DubUrl.Authentication.Snowflake;

/// <summary>Explicit native Snowflake LOCAL_APPLICATION PKCE flow. Caller owns session lifetime.</summary>
public sealed class SnowflakeInteractiveOAuthSession(string trustedAccount, bool useProtectedNativeCache = false) : IInteractiveOAuthSession, IDisposable
{
    private readonly SemaphoreSlim opening = new(1, 1);
    private readonly object sync = new();
    private InteractiveOAuthOptions? options;
    private bool disposed;

    public ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, InteractiveOAuthOptions configured, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (connection is not SnowflakeDbConnection || context.ProviderInvariantName != "Snowflake.Data")
                throw new ConnectionAuthenticationException("UnsupportedAuthentication");
            if (configured.ClientId != "LOCAL_APPLICATION" || configured.RedirectUri.Host != "127.0.0.1"
                || !configured.Authority.Host.EndsWith(".snowflakecomputing.com", StringComparison.OrdinalIgnoreCase))
                throw new ConnectionAuthenticationException("UnsupportedAuthentication");
            if (!configured.AllowInteraction || !Environment.UserInteractive)
                throw new ConnectionAuthenticationException("InteractionRequired");
            var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            if (!builder.TryGetValue("ACCOUNT", out var account) || !string.Equals(account.ToString(), trustedAccount, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(configured.Authority.Host, trustedAccount + ".snowflakecomputing.com", StringComparison.OrdinalIgnoreCase))
                throw new ConnectionAuthenticationException("CredentialConflict");
            if (options is not null && (options.Authority != configured.Authority || options.RedirectUri != configured.RedirectUri
                || options.LoginHint != configured.LoginHint || !options.Scopes.SequenceEqual(configured.Scopes)))
                throw new ConnectionAuthenticationException("CredentialConflict");
            options = configured;
            builder.Remove("PASSWORD");
            builder["HOST"] = configured.Authority.Host;
            builder["SCHEME"] = "https";
            builder["AUTHENTICATOR"] = "oauth_authorization_code";
            // These are Snowflake's documented public-client constants, not a reusable private secret.
            builder["OAUTHCLIENTID"] = "LOCAL_APPLICATION";
            builder["OAUTHCLIENTSECRET"] = "LOCAL_APPLICATION";
            builder["OAUTHAUTHORIZATIONURL"] = new Uri(configured.Authority, "/oauth/authorize").AbsoluteUri;
            builder["OAUTHTOKENREQUESTURL"] = new Uri(configured.Authority, "/oauth/token-request").AbsoluteUri;
            builder["OAUTHREDIRECTURI"] = configured.RedirectUri.AbsoluteUri;
            builder["OAUTHSCOPE"] = string.Join(' ', configured.Scopes);
            builder["BROWSER_RESPONSE_TIMEOUT"] = configured.AcquisitionTimeoutSeconds;
            builder["CLIENT_STORE_TEMPORARY_CREDENTIAL"] = useProtectedNativeCache;
            if (configured.LoginHint is not null) builder["USER"] = configured.LoginHint;
            connection.ConnectionString = builder.ConnectionString;
        }
        return ValueTask.CompletedTask;
    }

    public async ValueTask OpenAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await opening.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Driver 6.3's browser wait is synchronous and bounded by BROWSER_RESPONSE_TIMEOUT.
            // Keep it off the caller's UI thread and await it fully before any resource disposal.
            await Task.Run(() => connection.OpenAsync(cancellationToken), CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { throw new ConnectionAuthenticationException("AuthenticationFailed"); }
        finally { opening.Release(); }
    }

    public void Dispose() { lock (sync) { disposed = true; opening.Dispose(); } }
    public override string ToString() => "SnowflakeInteractiveOAuthSession [REDACTED]";
}
