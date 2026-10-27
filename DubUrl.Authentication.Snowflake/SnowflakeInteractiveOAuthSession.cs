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
            ValidateProvider(connection, context);
            ValidatePublicClient(configured);
            var builder = new DbConnectionStringBuilder { ConnectionString = connection.ConnectionString };
            ValidateAccount(builder, configured);
            ValidateSessionOptions(configured);
            options = configured;
            ApplyNativeOptions(builder, configured);
            connection.ConnectionString = builder.ConnectionString;
        }
        return ValueTask.CompletedTask;
    }
    private static void ValidateProvider(DbConnection connection, AuthenticationContext context)
    {
        if (connection is not SnowflakeDbConnection || context.ProviderInvariantName != "Snowflake.Data")
            throw new ConnectionAuthenticationException("UnsupportedAuthentication");
    }
    private static void ValidatePublicClient(InteractiveOAuthOptions configured)
    {
        if (configured.ClientId != "LOCAL_APPLICATION" || configured.RedirectUri.Host != "127.0.0.1"
            || !configured.Authority.Host.EndsWith(".snowflakecomputing.com", StringComparison.OrdinalIgnoreCase))
            throw new ConnectionAuthenticationException("UnsupportedAuthentication");
        if (!configured.AllowInteraction || !Environment.UserInteractive) throw new ConnectionAuthenticationException("InteractionRequired");
    }
    private void ValidateAccount(DbConnectionStringBuilder builder, InteractiveOAuthOptions configured)
    {
        if (!builder.TryGetValue("ACCOUNT", out var account) || !string.Equals(account.ToString(), trustedAccount, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(configured.Authority.Host, trustedAccount + ".snowflakecomputing.com", StringComparison.OrdinalIgnoreCase))
            throw new ConnectionAuthenticationException("CredentialConflict");
    }
    private void ValidateSessionOptions(InteractiveOAuthOptions configured)
    {
        if (options is null) return;
        if (options.Authority != configured.Authority || options.RedirectUri != configured.RedirectUri
            || options.LoginHint != configured.LoginHint || !options.Scopes.SequenceEqual(configured.Scopes))
            throw new ConnectionAuthenticationException("CredentialConflict");
    }
    private void ApplyNativeOptions(DbConnectionStringBuilder builder, InteractiveOAuthOptions configured)
    {
        builder.Remove("PASSWORD");
        builder["HOST"] = configured.Authority.Host;
        builder["SCHEME"] = "https";
        builder["AUTHENTICATOR"] = "oauth_authorization_code";
        // Documented public-client constants, not a reusable private secret.
        builder["OAUTHCLIENTID"] = "LOCAL_APPLICATION";
        builder["OAUTHCLIENTSECRET"] = "LOCAL_APPLICATION";
        builder["OAUTHAUTHORIZATIONURL"] = new Uri(configured.Authority, "/oauth/authorize").AbsoluteUri;
        builder["OAUTHTOKENREQUESTURL"] = new Uri(configured.Authority, "/oauth/token-request").AbsoluteUri;
        builder["OAUTHREDIRECTURI"] = configured.RedirectUri.AbsoluteUri;
        builder["OAUTHSCOPE"] = string.Join(' ', configured.Scopes);
        builder["BROWSER_RESPONSE_TIMEOUT"] = configured.AcquisitionTimeoutSeconds;
        builder["CLIENT_STORE_TEMPORARY_CREDENTIAL"] = useProtectedNativeCache;
        if (configured.LoginHint is not null) builder["USER"] = configured.LoginHint;
    }
    public async ValueTask OpenAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        await opening.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The native browser wait is bounded but not cancellation-aware. Await before disposal.
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
