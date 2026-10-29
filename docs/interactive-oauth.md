# Interactive authorization code with PKCE

The common factory is `ConnectionAuthentication.OAuthInteractive(options)`. The
dependency-light options/session contract is in core; optional provider sessions
own identity SDK/native browser integration. No secrets, authorization codes or
PKCE verifiers are added to the location URL or parsed cache. PKCE is mandatory
in both selected native flows; no option disables it.

## SQL Server / Microsoft Entra

```csharp
using var session = new EntraInteractiveOAuthSession();
var strategy = ConnectionAuthentication.OAuthInteractive(new InteractiveOAuthOptions
{
    ClientId = registeredPublicClientId,
    Authority = new Uri("https://login.microsoftonline.com/" + tenantId),
    RedirectUri = new Uri("http://localhost"),
    Scopes = ["https://database.windows.net/.default"],
    Session = session,
    AllowInteraction = true,
    AcquisitionTimeoutSeconds = 120
});
await using var connection = await new ConnectionUrl(sqlLocation)
    .WithAuthentication(strategy).OpenAsync(cancellationToken);
```

The optional SqlServer package uses MSAL 4.83.3's external system-browser
authorization-code/PKCE path, without embedded webviews or a broker. MSAL owns
fresh state/verifiers, response correlation, redirect handling and OIDC validation.
Only database access tokens reach SqlClient 7.1.1's stable native callback. The
configured tenant/audience is checked, permitting the documented public-cloud
login.windows.net/login.microsoftonline.com authority aliases within that tenant.
Register a native public client with the loopback redirect and provision the user
in an Entra-enabled SQL endpoint. MFA/conditional access remains with Entra.
LocalDB/unconfigured SQL containers are not equivalent test targets.

The session keeps an in-memory MSAL cache, serializes acquisition, tries silent
acquisition before interaction, and propagates actual token expiration. Explicit
AllowInteraction is required; otherwise interaction-required failures are safe.
Construction, metadata, Parse and ConnectAsync do not open a browser; acquisition
occurs when native opening invokes the callback. A separate acquisition budget is
linked with caller/provider cancellation. Pools can avoid acquisition entirely.

## Snowflake local applications

```csharp
using var session = new SnowflakeInteractiveOAuthSession(account);
var strategy = ConnectionAuthentication.OAuthInteractive(new InteractiveOAuthOptions
{
    ClientId = "LOCAL_APPLICATION",
    Authority = new Uri("https://" + account + ".snowflakecomputing.com"),
    RedirectUri = new Uri("http://127.0.0.1:41589/callback/"),
    Scopes = ["session:role:PUBLIC"],
    Session = session,
    AllowInteraction = true
});
await using var connection = await new ConnectionUrl(snowflakeLocation)
    .WithAuthentication(strategy).OpenAsync(cancellationToken);
```

Snowflake.Data 6.3.0 (above the documented 4.8 minimum) supplies its native
`oauth_authorization_code` flow with S256 challenge, fresh state and verifier,
state validation and bounded loopback listener lifetime. The built-in public
client uses the documented LOCAL_APPLICATION constants; no application secret is
required. Trusted account and HTTPS endpoints are explicit and must agree with
the selected location. No arbitrary OAuth query endpoints are accepted. Verify
that the account's SNOWFLAKE$LOCAL_APPLICATION integration is enabled and permits
the requested user/role; a token does not grant database/role authorization.

The adapter does not query account integration metadata during configuration.
An unavailable/disabled integration fails through sanitized native authentication;
an administrator must inspect/enable it. Custom public-client integrations are not
advertised by this adapter: the installed driver's custom configuration requires
a client-secret field. Supply a separately verified IInteractiveOAuthSession for
that case rather than silently adopting a confidential-client flow.

Persistent caching is off by default. Driver 6.3 requires its native credential
manager for cross-session access/refresh token caching; opting in via
`useProtectedNativeCache: true` delegates to its supported protected OS backend,
requires a stable LoginHint, and must be reviewed for the host's store availability.
Default native pooling can reuse sessions; without native cache a fresh physical
authentication may require another browser login. The session serializes native
opens to avoid overlapping listeners. There is no invented in-memory refresh layer.

**Native limitation:** this version's browser callback wait is synchronous and does
not accept CancellationToken. It is bounded by AcquisitionTimeoutSeconds, runs off
the caller's UI thread and is fully awaited before connection disposal. Cancellation
is passed through other native operations and observed after that wait; immediate
browser-wait cancellation is not supported. Native browser error-page rendering and
internal diagnostics remain driver-owned; this adapter sanitizes returned exception
chains but cannot promise universal redaction of caller-enabled SDK logging.

## Ownership, failures and validation

Sessions are caller-owned; reuse one session/strategy for one identity/pool and
dispose only after active opens finish. Synchronous Connect/Open reject this
async-first strategy. Unsafe redirects, competing passwords/certificates/tokens,
query OAuth selectors and incompatible native modes fail without fallback. Browser
unavailable, user denial, occupied port, callback timeout, revoked tokens and native
errors become safe failures; cancellation remains separate where the SDK supports it.
Do not enable detailed native logging containing authentication exchanges.

Entra ClearCacheAsync removes local MSAL accounts/tokens. This does not revoke
tokens, terminate browser/identity-provider sessions, close database sessions or
clear provider pools. Snowflake native protected-cache clearing is controlled by
its OS/native tooling, not by deleting repository files. No tokens are persisted
by DubUrl itself and no machine-wide trust/authentication configuration is changed.

Six deterministic tests cover native adapter configuration, callback identity,
unattended interaction, redirect validation, conflicts and lazy behavior. The live
Entra/Snowflake tests are Explicit and were not run (no configured tenant/account or
human sign-in). Native protocol suites are owned by the SDK/driver; this repository
does not yet simulate full PKCE/state exchanges, renewal/concurrent browser behavior
or all failure paths. These and the Snowflake cancellation/cache limitations remain
validation/contract gaps, not completed live evidence.

References: [SqlClient callbacks](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-active-directory-authentication),
[Snowflake local applications](https://docs.snowflake.com/en/user-guide/oauth-local-applications),
[exact Snowflake 6.3 flow](https://github.com/snowflakedb/snowflake-connector-net/blob/v6.3.0/Snowflake.Data/Core/Authenticator/OAuthAuthorizationCodeAuthenticator.cs).
