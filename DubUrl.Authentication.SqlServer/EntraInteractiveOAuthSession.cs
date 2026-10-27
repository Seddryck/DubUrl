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
    private InteractiveOAuthOptions? sessionOptions;
    private ConnectionAuthentication? tokenStrategy;
    private bool disposed;
    private IPublicClientApplication Application => application ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);

    public ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, InteractiveOAuthOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (context.ProviderInvariantName != "Microsoft.Data.SqlClient") throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            if (sessionOptions is not null && !SameOptions(sessionOptions, options)) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
            if (application is null)
            {
                sessionOptions = options;
                application = PublicClientApplicationBuilder.Create(options.ClientId).WithAuthority(options.Authority.AbsoluteUri)
                    .WithRedirectUri(options.RedirectUri.AbsoluteUri).Build();
                tokenStrategy = SqlServerAuthentication.TokenCallback(new InteractiveSource(this));
            }
            (tokenStrategy ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed)).Configure(connection, context);
        }
        return ValueTask.CompletedTask;
    }

    public async ValueTask OpenAsync(DbConnection connection, CancellationToken cancellationToken = default)
        => await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

    private async ValueTask<SqlAuthenticationToken> AcquireAsync(SqlAuthenticationParameters request, CancellationToken cancellationToken)
    {
        var configured = sessionOptions ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
        var scope = request.Resource.TrimEnd('/') + "/.default";
        if (!configured.Scopes.Contains(scope, StringComparer.Ordinal)) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        TrustedEntraEndpoint.ValidateAuthority(configured.Authority, request.Authority);
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(configured.AcquisitionTimeoutSeconds));
        var entered = false;
        try
        {
            await acquisition.WaitAsync(budget.Token).ConfigureAwait(false);
            entered = true;
            var accounts = await Application.GetAccountsAsync().ConfigureAwait(false);
            var account = configured.LoginHint is null ? accounts.SingleOrDefault()
                : accounts.FirstOrDefault(a => string.Equals(a.Username, configured.LoginHint, StringComparison.OrdinalIgnoreCase));
            var result = await AcquireCachedOrInteractiveAsync(configured, account, budget.Token).ConfigureAwait(false);
            return new(result.AccessToken, result.ExpiresOn);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) when (budget.IsCancellationRequested) { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationTimeout); }
        catch (ConnectionAuthenticationException) { throw; }
        catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed); }
        finally { if (entered) acquisition.Release(); }
    }

    private async Task<AuthenticationResult> AcquireCachedOrInteractiveAsync(InteractiveOAuthOptions configured, IAccount? account, CancellationToken cancellationToken)
    {
        try { return await Application.AcquireTokenSilent(configured.Scopes, account).ExecuteAsync(cancellationToken).ConfigureAwait(false); }
        catch (MsalUiRequiredException) { return await AcquireInteractiveAsync(configured, cancellationToken).ConfigureAwait(false); }
    }
    private Task<AuthenticationResult> AcquireInteractiveAsync(InteractiveOAuthOptions configured, CancellationToken cancellationToken)
    {
        if (!configured.AllowInteraction || !Environment.UserInteractive) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InteractionRequired);
        // MSAL owns the external-browser authorization-code + PKCE protocol.
        var builder = Application.AcquireTokenInteractive(configured.Scopes).WithUseEmbeddedWebView(false)
            .WithSystemWebViewOptions(new SystemWebViewOptions
            { HtmlMessageSuccess = "Authentication completed. You may close this window.", HtmlMessageError = "Authentication failed. Return to the application." });
        if (configured.LoginHint is not null) builder = builder.WithLoginHint(configured.LoginHint);
        return builder.ExecuteAsync(cancellationToken);
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
