# SQL Server native tokens and certificate-backed Entra

The optional `DubUrl.Authentication.SqlServer` package uses Microsoft.Data.SqlClient
7.1.1 and MSAL 4.83.3, outside core. Applications can implement
`ISqlServerTokenSource` with the native `SqlAuthenticationParameters` request and
`SqlAuthenticationToken` result, or use the included certificate adapter:

```csharp
using DubUrl.Authentication.SqlServer;
// Load a certificate/private key using application-owned configuration, not a URL.
var source = new EntraCertificateTokenSource(clientId,
    new Uri("https://login.microsoftonline.com/" + tenantId),
    new Uri("https://database.windows.net/"), certificate);
var strategy = SqlServerAuthentication.TokenCallback(source);
var target = new ConnectionUrl("mssql://server.database.windows.net/app")
    .WithAuthentication(strategy);
await using var connection = await target.OpenAsync(cancellationToken);
```

The adapter authenticates an application to Entra with its certificate; SQL receives
an access token, not that certificate in a TLS client-authentication handshake.
Register the application's certificate in Entra and provision its database identity
and permissions. Use an Entra-enabled Azure SQL/Managed Instance or supported SQL
Server endpoint. LocalDB and ordinary SQL containers do not exercise this flow.

The configured authority and SQL resource are explicit trusted HTTPS values.
Callback resource/authority must match them; no credential is forwarded to an
arbitrary destination from the server. Sovereign clouds require their corresponding
trusted authority and SQL resource. The adapter maps the SQL audience to
`<resource-without-trailing-slash>/.default`, and propagates MSAL's actual expiration.
If a server advertises a different trusted authority spelling, configure that exact
authority explicitly rather than accepting arbitrary aliases.

Configuration and Parse never acquire a token. One delegate instance is retained
per strategy; reuse the same strategy and source for connections sharing an identity
and pool. SqlClient controls callback timing and pooled authentication renewal;
MSAL owns token caching. There is no additional retry/cache loop. Acquisition is
async through the native callback even if a caller uses native synchronous opening;
DubUrl does not block on an async credential source itself. Use OpenAsync in UI code.
Caller owns certificates, sources, MSAL/application objects and returned connections;
keep certificates usable while the source may need new tokens. No private key is
exported, and failed acquisition never falls back to another authentication mode.

Do not switch identities behind a shared strategy. For certificate rotation, build a
new source/strategy, drain old work and clear the affected native pools deliberately;
cached tokens/pooled sessions are not proof that a new certificate was used.
Normal server certificate/hostname verification is enforced independently.
Source/provider failures are sanitized; caller-controlled native token objects still
contain sensitive access tokens and must never be logged.

Deterministic tests verify callback timing/identity, expiration, cancellation, safe
failures and authority/audience validation. Live Entra authentication, certificate
expiry/rotation and pooled refresh require an explicitly provisioned endpoint and
application certificate; these were not run. For live checks, disable pooling and
use a fresh source/cache for negative certificate tests, then separately test pooled
reuse. Do not interpret cached-token success as certificate validation.

The [provider token QA inventory](token-authentication-qa.md) distinguishes actual native configuration/negative I/O from opt-in Entra login, pool reuse and certificate tests. It documents every unsupported QA and the external live prerequisites. Existing CI is unchanged; ordinary SQL containers do not establish successful token authentication.

Native behavior: [SqlClient callback and pooling contract](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-active-directory-authentication).
