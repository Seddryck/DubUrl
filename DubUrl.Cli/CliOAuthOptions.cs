using System.CommandLine;
using System.Data.Common;
using System.Globalization;
using DubUrl.Authentication;
using DubUrl.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

public sealed record CliOAuthRequest(string Provider, string Flow, string ClientId, Uri Authority, Uri RedirectUri,
    IReadOnlyList<string> Scopes, string? LoginHint, int TimeoutSeconds, string? Account)
{
    public override string ToString() => "CliOAuthRequest [REDACTED]";
}

/// <summary>Creates an owned session for the actual driver's load context; creation must not sign in.</summary>
public interface ICliOAuthSessionFactory
{
    IInteractiveOAuthSession Create(DbConnection connection, CliOAuthRequest request, ICliConsole console);
}

internal sealed class CliOAuthInputException(string message) : Exception(message);

internal sealed class CliOAuthOptions
{
    internal int AcquisitionTimeoutSeconds { get; private set; }
    private readonly Option<string?> flow = new("--oauth") { Description = "Explicit OAuth sign-in: browser or device-code (Entra only)." };
    private readonly Option<string?> clientId = new("--oauth-client-id") { Description = "Registered public-client application ID; never a client secret." };
    private readonly Option<string?> tenant = new("--oauth-tenant") { Description = "Entra tenant ID; exclusive with --oauth-authority." };
    private readonly Option<string?> authority = new("--oauth-authority") { Description = "Trusted HTTPS tenant authority (or exact Snowflake account authority)." };
    private readonly Option<string[]> scopes = new("--oauth-scope") { Description = "OAuth scope; repeat for each scope.", AllowMultipleArgumentsPerToken = false };
    private readonly Option<string?> redirect = new("--oauth-redirect-uri") { Description = "Registered loopback HTTP redirect for browser sign-in." };
    private readonly Option<string?> loginHint = new("--oauth-login-hint") { Description = "Optional account hint; does not change the process identity." };
    private readonly Option<string?> account = new("--oauth-account") { Description = "Trusted Snowflake account identifier; defaults to the target host." };
    private readonly Option<int?> timeout = new("--oauth-timeout") { Description = "Sign-in budget in seconds (1-3600, default 120), separate from --timeout." };
    private readonly Option<bool> interaction = new("--allow-oauth-interaction") { Description = "Deliberately allow sign-in and stderr device instructions with redirected input/output. Never implied by --yes." };
    private Option[] Options => [flow, clientId, tenant, authority, scopes, redirect, loginHint, account, timeout, interaction];
    internal void AddTo(Command command) { foreach (var option in Options) command.Options.Add(option); }
    internal bool IsSelected(ParseResult result)
    {
        if (Options.Where(option => option != scopes).Any(option => result.GetResult(option)?.IdentifierTokenCount > 1))
            throw new CliOAuthInputException("Specify each OAuth selection/configuration option only once.");
        var selected = result.GetValue(flow) is not null;
        if (!selected && Options.Skip(1).Any(option => result.GetResult(option) is { Implicit: false }))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        return selected;
    }
    internal ConnectionUrl Configure(IServiceProvider services, ParseResult parsed, ConnectionUrl target, out IDisposable lifetime)
    {
        var uri = ConnectionUri.Parse(target.Url);
        var registry = services.GetRequiredService<DubUrl.Mapping.ISchemeRegistry>();
        var provider = registry.GetMapper(uri.Scheme).GetProviderName();
        var selectedFlow = parsed.GetValue(flow);
        if (selectedFlow is not ("browser" or "device-code") || provider is not ("Microsoft.Data.SqlClient" or "Snowflake.Data"))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        if (provider == "Snowflake.Data" && selectedFlow != "browser")
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        var settings = services.GetRequiredService<IConfiguration>().GetSection("Authentication:OAuth");
        string? Value(Option<string?> option, string key) => parsed.GetValue(option) ?? settings[key];
        var budget = parsed.GetValue(timeout) ?? ReadTimeout(settings["TimeoutSeconds"]);
        if (budget is < 1 or > 3600) throw new CliOAuthInputException("--oauth-timeout must be between 1 and 3600 seconds.");
        var request = CreateRequest(provider, selectedFlow, uri, Value, settings, parsed, budget);
        AcquisitionTimeoutSeconds = budget;
        var session = new DeferredSession(services.GetRequiredService<ICliOAuthSessionFactory>(), request,
            services.GetRequiredService<ICliConsole>(), parsed.GetValue(interaction));
        lifetime = session;
        try
        {
            return target.WithAuthentication(ConnectionAuthentication.OAuthInteractive(new()
            {
                ClientId = request.ClientId, Authority = request.Authority, RedirectUri = request.RedirectUri,
                Scopes = request.Scopes, LoginHint = request.LoginHint, AcquisitionTimeoutSeconds = request.TimeoutSeconds,
                AllowInteraction = true, Session = session
            }));
        }
        catch (ArgumentException) { session.Dispose(); throw new CliOAuthInputException("OAuth requires a trusted HTTPS authority, loopback HTTP redirect and nonempty scopes."); }
    }
    private CliOAuthRequest CreateRequest(string provider, string selectedFlow, ConnectionUri uri,
        Func<Option<string?>, string, string?> value, IConfiguration settings, ParseResult parsed, int budget)
    {
        var isSql = provider == "Microsoft.Data.SqlClient";
        var identity = isSql ? ReadEntraIdentity(value) : ReadSnowflakeIdentity(uri, value);
        var callback = ReadRedirect(isSql, selectedFlow, value(redirect, "RedirectUri"));
        var selectedScopes = ReadScopes(isSql, parsed.GetValue(scopes) ?? [], settings["Scopes"]);
        if (!isSql && identity.Client != "LOCAL_APPLICATION") throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        return new(provider, selectedFlow, identity.Client, identity.Authority, callback, Array.AsReadOnly(selectedScopes.Distinct(StringComparer.Ordinal).ToArray()),
            value(loginHint, "LoginHint"), budget, identity.Account);
    }
    private static Uri ReadRedirect(bool isSql, string flow, string? requestedRedirect)
    {
        if (flow == "device-code" && requestedRedirect is not null)
            throw new CliOAuthInputException("Device-code flow does not use a redirect URI; omit --oauth-redirect-uri.");
        if (!Uri.TryCreate(requestedRedirect ?? (isSql ? "http://localhost" : "http://127.0.0.1:41589/callback/"), UriKind.Absolute, out var callback))
            throw new CliOAuthInputException("A valid loopback HTTP OAuth redirect is required.");
        if (!isSql && callback.Host != "127.0.0.1") throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        return callback;
    }
    private static string[] ReadScopes(bool isSql, string[] selectedScopes, string? configured)
    {
        if (selectedScopes.Length == 0) selectedScopes = configured?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            ?? (isSql ? ["https://database.windows.net/.default"] : ["session:role:PUBLIC"]);
        if (selectedScopes.Any(scope => string.IsNullOrWhiteSpace(scope) || scope.Any(char.IsControl) || scope.Any(char.IsWhiteSpace)))
            throw new CliOAuthInputException("Each --oauth-scope must be a nonempty scope without whitespace.");
        if (isSql && (selectedScopes.Length != 1 || !IsSqlScope(selectedScopes[0])))
            throw new CliOAuthInputException("Entra SQL OAuth requires one HTTPS database /.default scope.");
        return selectedScopes;
    }
    private sealed record Identity(string Client, Uri Authority, string? Account);
    private Identity ReadEntraIdentity(Func<Option<string?>, string, string?> value)
    {
        if (value(account, "Account") is not null) throw new CliOAuthInputException("--oauth-account applies only to Snowflake.");
        var selectedTenant = value(tenant, "Tenant");
        var selectedAuthority = value(authority, "Authority");
        if (selectedTenant is not null && selectedAuthority is not null) throw new CliOAuthInputException("Choose either --oauth-tenant or --oauth-authority.");
        if (selectedTenant is not null && !Guid.TryParse(selectedTenant, out _)) throw new CliOAuthInputException("Use an Entra tenant GUID or a full tenant --oauth-authority.");
        var selectedClient = value(clientId, "ClientId");
        if (!Guid.TryParse(selectedClient, out var id) || id == Guid.Empty) throw new CliOAuthInputException("Entra OAuth requires a registered public-client GUID in --oauth-client-id.");
        selectedAuthority ??= selectedTenant is null ? null : "https://login.microsoftonline.com/" + selectedTenant;
        return new(selectedClient!, ReadAuthority(selectedAuthority, true, null), null);
    }
    private Identity ReadSnowflakeIdentity(ConnectionUri uri, Func<Option<string?>, string, string?> value)
    {
        if (value(tenant, "Tenant") is not null) throw new CliOAuthInputException("--oauth-tenant applies only to Entra.");
        var selectedAccount = value(account, "Account") ?? uri.Host;
        var selectedAuthority = value(authority, "Authority") ?? "https://" + selectedAccount + ".snowflakecomputing.com";
        return new(value(clientId, "ClientId") ?? "LOCAL_APPLICATION", ReadAuthority(selectedAuthority, false, selectedAccount), selectedAccount);
    }
    private static bool IsSqlScope(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var scope) && scope.Scheme == "https" && scope.IsDefaultPort
            && scope.UserInfo.Length == 0 && scope.Query.Length == 0 && scope.Fragment.Length == 0 && scope.AbsolutePath == "/.default";
    private static int ReadTimeout(string? value)
        => value is null ? 120 : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed
            : throw new CliOAuthInputException("OAuth TimeoutSeconds must be an integer between 1 and 3600.");
    private static Uri ReadAuthority(string? value, bool sql, string? account)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) || !IsPlainHttps(endpoint))
            throw new CliOAuthInputException("OAuth requires a trusted HTTPS authority without credentials, query or fragment.");
        if (sql)
        {
            if (!IsTenantAuthority(endpoint))
                throw new CliOAuthInputException("Entra OAuth requires a tenant-specific Microsoft authority.");
        }
        else if (!IsSnowflakeAuthority(endpoint, account))
            throw new CliOAuthInputException("The Snowflake authority must match its trusted account identifier exactly.");
        return endpoint;
    }
    private static bool IsPlainHttps(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
    private static bool IsTenantAuthority(Uri endpoint)
        => (endpoint.Host is "login.microsoftonline.com" or "login.microsoftonline.us" or "login.chinacloudapi.cn")
            && endpoint.Segments.Length == 2 && endpoint.AbsolutePath.Trim('/') is not ("common" or "organizations" or "consumers");
    private static bool IsSnowflakeAuthority(Uri endpoint, string? account)
        => !string.IsNullOrWhiteSpace(account) && account.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
            && string.Equals(endpoint.Host, account + ".snowflakecomputing.com", StringComparison.OrdinalIgnoreCase) && endpoint.AbsolutePath == "/";

    private sealed class DeferredSession(ICliOAuthSessionFactory factory, CliOAuthRequest request, ICliConsole console, bool allowRedirected) : IInteractiveOAuthSession, IDisposable
    {
        private IInteractiveOAuthSession? session;
        public ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, InteractiveOAuthOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!console.IsInteractive && !allowRedirected) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InteractionRequired);
            try { session ??= factory.Create(connection, request, console); }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication); }
            return session.ConfigureAsync(connection, context, options, cancellationToken);
        }
        public async ValueTask OpenAsync(DbConnection connection, CancellationToken cancellationToken = default)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));
            try { await Required.OpenAsync(connection, budget.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (OperationCanceledException) when (budget.IsCancellationRequested) { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationTimeout); }
            catch (OperationCanceledException) { throw; }
            catch (ConnectionAuthenticationException) { throw; }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed); }
        }
        private IInteractiveOAuthSession Required => session ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
        public void Dispose()
        {
            if (session is IDisposable disposable) disposable.Dispose();
            else if (session is IAsyncDisposable asynchronous) asynchronous.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
