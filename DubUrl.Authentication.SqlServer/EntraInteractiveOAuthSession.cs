using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client;

namespace DubUrl.Authentication.SqlServer;

/// <summary>MSAL system-browser authorization-code/PKCE session with an in-memory cache. Caller owns lifetime.</summary>
public sealed class EntraInteractiveOAuthSession : IInteractiveOAuthSession, IDisposable
{
    private readonly SemaphoreSlim acquisition = new(1, 1);
    private readonly object sync = new();
    private IPublicClientApplication? application;
    private InteractiveOAuthOptions? options;
    private ConnectionAuthentication? tokenStrategy;
    private bool disposed;

    public ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, InteractiveOAuthOptions configured, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (context.ProviderInvariantName != "Microsoft.Data.SqlClient") throw new ConnectionAuthenticationException("UnsupportedAuthentication");
            if (options is not null && !SameOptions(options, configured)) throw new ConnectionAuthenticationException("CredentialConflict");
            if (application is null)
            {
                options = configured;
                application = PublicClientApplicationBuilder.Create(configured.ClientId).WithAuthority(configured.Authority.AbsoluteUri)
                    .WithRedirectUri(configured.RedirectUri.AbsoluteUri).Build();
                tokenStrategy = SqlServerAuthentication.TokenCallback(new InteractiveSource(this));
            }
            tokenStrategy!.Configure(connection, context);
        }
        return ValueTask.CompletedTask;
    }

    public async ValueTask OpenAsync(DbConnection connection, CancellationToken cancellationToken = default)
        => await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    private async ValueTask<SqlAuthenticationToken> AcquireAsync(SqlAuthenticationParameters request, CancellationToken cancellationToken)
    {
        var configured = options ?? throw new ConnectionAuthenticationException("AuthenticationFailed");
        var scope = request.Resource.TrimEnd('/') + "/.default";
        if (!configured.Scopes.Contains(scope, StringComparer.Ordinal)) throw new ConnectionAuthenticationException("CredentialConflict");
        TrustedEntraEndpoint.ValidateAuthority(configured.Authority, request.Authority);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(configured.AcquisitionTimeoutSeconds));
        var entered = false;
        try
        {
            await acquisition.WaitAsync(budget.Token).ConfigureAwait(false);
            entered = true;
            var accounts = await application!.GetAccountsAsync().ConfigureAwait(false);
            var account = configured.LoginHint is null ? accounts.SingleOrDefault()
                : accounts.FirstOrDefault(a => string.Equals(a.Username, configured.LoginHint, StringComparison.OrdinalIgnoreCase));
            AuthenticationResult result;
            try { result = await application.AcquireTokenSilent(configured.Scopes, account).ExecuteAsync(budget.Token).ConfigureAwait(false); }
            catch (MsalUiRequiredException)
            {
                if (!configured.AllowInteraction || !Environment.UserInteractive)
                    throw new ConnectionAuthenticationException("InteractionRequired");
                // MSAL's external system browser path uses auth-code + PKCE; no broker, embedded
                // password collection, client secret, device code or custom crypto/token exchange.
                var builder = application.AcquireTokenInteractive(configured.Scopes).WithUseEmbeddedWebView(false)
                    .WithSystemWebViewOptions(new SystemWebViewOptions
                    { HtmlMessageSuccess = "Authentication completed. You may close this window.", HtmlMessageError = "Authentication failed. Return to the application." });
                if (configured.LoginHint is not null) builder = builder.WithLoginHint(configured.LoginHint);
                result = await builder.ExecuteAsync(budget.Token).ConfigureAwait(false);
            }
            return new(result.AccessToken, result.ExpiresOn);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (budget.IsCancellationRequested) { throw new ConnectionAuthenticationException("AuthenticationTimeout"); }
        catch (ConnectionAuthenticationException) { throw; }
        catch { throw new ConnectionAuthenticationException("AuthenticationFailed"); }
        finally { if (entered) acquisition.Release(); }
    }

    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await acquisition.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (application is not null)
                foreach (var account in await application.GetAccountsAsync().ConfigureAwait(false))
                    await application.RemoveAsync(account).ConfigureAwait(false);
        }
        finally { acquisition.Release(); }
    }
    private static bool SameOptions(InteractiveOAuthOptions left, InteractiveOAuthOptions right)
        => left.ClientId == right.ClientId && left.Authority == right.Authority && left.RedirectUri == right.RedirectUri
            && left.LoginHint == right.LoginHint && left.AllowInteraction == right.AllowInteraction
            && left.AcquisitionTimeoutSeconds == right.AcquisitionTimeoutSeconds && left.Scopes.SequenceEqual(right.Scopes);
    public void Dispose() { lock (sync) { disposed = true; acquisition.Dispose(); } }
    public override string ToString() => "EntraInteractiveOAuthSession [REDACTED]";
    private sealed class InteractiveSource(EntraInteractiveOAuthSession owner) : ISqlServerTokenSource
    {
        public ValueTask<SqlAuthenticationToken> GetTokenAsync(SqlAuthenticationParameters request, CancellationToken cancellationToken = default)
            => owner.AcquireAsync(request, cancellationToken);
    }
}
