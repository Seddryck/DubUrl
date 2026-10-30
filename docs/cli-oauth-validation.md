---
title: OAuth configuration and validation
permalink: /cli-oauth-validation/
nav_exclude: true
---

# OAuth sign-in from the CLI

Select `--oauth browser` or `--oauth device-code` explicitly on `connection check`, `schema deploy`, or `schema inspect`. Parsing, help, capabilities, rendering, and deployment `--dry-run` never start sign-in. No OAuth selection is inferred from configuration, an installed driver, `--yes`, or a previous password login.

| Native provider | Browser authorization code + PKCE | Device code | Required identity setup |
|---|---|---|---|
| SQL Server / Microsoft.Data.SqlClient | Implemented through the existing Entra/MSAL session | Implemented through MSAL; can use a browser on another device | Tenant-specific public-client registration, public-client/device flows enabled as appropriate, registered loopback browser redirect, SQL audience, and an Entra-enabled SQL database with the user authorized |
| Snowflake / Snowflake.Data | Existing LOCAL_APPLICATION native PKCE strategy | Unsupported by this adapter; fails before session creation | Exact account identifier, enabled SNOWFLAKE$LOCAL_APPLICATION integration, allowed role and local browser |
| Other providers or ODBC/OLE DB/ADOMD | Unsupported | Unsupported | Their ordinary QA/password sessions are not OAuth validation |

See [MSAL device-code setup](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/desktop-mobile/device-code-flow) and [Snowflake local applications](https://docs.snowflake.com/en/user-guide/oauth-local-applications). Tokens, authorization codes, device_code values, PKCE verifiers and client secrets must never be supplied in a DubUrl URL. These commands accept public-client configuration, not confidential-client secrets. Install the appropriate approved provider connector as usual; shipping an OAuth adapter does not activate a provider module.

## Examples

SQL Server browser sign-in (replace the GUIDs and endpoint with your provisioned fixture):

```shell
duburl connection check 'mssql://your-server.database.windows.net/app?Connect%20Timeout=180&Pooling=false' --engine --oauth browser --oauth-client-id 11111111-1111-1111-1111-111111111111 --oauth-tenant 22222222-2222-2222-2222-222222222222 --oauth-redirect-uri http://localhost --timeout 180
```

Device code from a terminal without a local browser, preserving JSON stdout:

```shell
duburl connection check --connection sql-dev --engine --oauth device-code --allow-oauth-interaction --oauth-client-id 11111111-1111-1111-1111-111111111111 --oauth-tenant 22222222-2222-2222-2222-222222222222 --timeout 180 --output-format json
```

Only the verification URL, short-lived **user code**, and expiry are deliberately written to stderr. Never merge stderr into stdout when consuming JSON. The SDK's raw device response/message and private device_code are not printed. Device flow has no redirect; supplying `--oauth-redirect-uri` for it is rejected. Both Entra flows first attempt the session's in-memory cache. MSAL handles refresh and expiration; no tokens are persisted by this CLI feature.

Snowflake browser sign-in:

```shell
duburl connection check 'snowflake://myorg-myaccount/app?Pooling=false' --engine --oauth browser --oauth-account myorg-myaccount --oauth-scope session:role:PUBLIC --timeout 180
```

Snowflake defaults to the public `LOCAL_APPLICATION` client, authority `https://<account>.snowflakecomputing.com`, and redirect `http://127.0.0.1:41589/callback/`. Custom client-secret integrations and device code are rejected. Native persistent credential caching stays disabled. A subsequent fresh physical connection can require another browser sign-in. The driver's synchronous browser wait is bounded by the configured timeout, but cannot be interrupted immediately; it is fully awaited before disposal.

The same options apply to schema operations. Inspection additionally requires the provider's existing discovery capability; selecting OAuth does not add that capability:

```shell
duburl schema deploy schema.json --connection sql-dev --oauth browser --oauth-client-id 11111111-1111-1111-1111-111111111111 --oauth-tenant 22222222-2222-2222-2222-222222222222 --dry-run
duburl schema deploy schema.json --connection sql-dev --oauth device-code --allow-oauth-interaction --oauth-client-id 11111111-1111-1111-1111-111111111111 --oauth-tenant 22222222-2222-2222-2222-222222222222
duburl schema inspect --connection sql-dev --basic --output-format json --oauth browser --oauth-client-id 11111111-1111-1111-1111-111111111111 --oauth-tenant 22222222-2222-2222-2222-222222222222
```

The first command renders only. Schema inspection still configures its owned connection for the reader before opening, now through the selected async authentication strategy. Schema deployment still requires its existing destructive-operation confirmation where applicable. `--yes` does not grant permission for OAuth interaction.

## Options and unattended use

Identity defaults may be supplied in `Authentication:OAuth` configuration (environment prefix `DUBURL_`, with `__` between sections). Explicit CLI options override the corresponding setting. Configuration never selects a flow or grants interaction. Keep local identity settings outside source control.

| Option | Configuration key / default |
|---|---|
| `--oauth` | Required explicit `browser` or `device-code`; no configuration default |
| `--oauth-client-id` | `ClientId`; required public-client GUID for Entra, LOCAL_APPLICATION for Snowflake |
| `--oauth-tenant` | `Tenant`; Entra GUID, exclusive with Authority |
| `--oauth-authority` | `Authority`; tenant-specific Microsoft HTTPS authority or exact Snowflake account authority |
| `--oauth-scope` | Repeatable; `Scopes` is space-separated; Entra defaults to `https://database.windows.net/.default`, Snowflake to `session:role:PUBLIC` |
| `--oauth-redirect-uri` | `RedirectUri`; browser-only loopback HTTP URL |
| `--oauth-login-hint` | `LoginHint`; optional account hint, not an impersonation request |
| `--oauth-account` | `Account`; Snowflake only, defaults to URL host |
| `--oauth-timeout` | `TimeoutSeconds`; 1-3600 seconds, default 120 |
| `--allow-oauth-interaction` | Explicit flag only; permits deliberate interaction when stdin/stdout are redirected |

For example, set `DUBURL_Authentication__OAuth__ClientId` and `DUBURL_Authentication__OAuth__Authority` through your existing external environment/configuration mechanism, then still select `--oauth device-code` explicitly. Sovereign-cloud SQL audiences must be supplied with `--oauth-scope`; they are checked against the native SQL token request. Authority must identify a specific tenant; common/organizations/consumers and arbitrary OAuth endpoint URLs are rejected.

Redirected input/output is unattended by default: sign-in stops with `InteractionRequired` before an identity session is created. To send JSON through a pipe while a human signs in, deliberately add `--allow-oauth-interaction` and keep stderr visible. Device code does not require a local browser or Environment.UserInteractive. Browser flows additionally require their SDK's supported interactive environment. There is no password fallback.

`--oauth-timeout` bounds interactive/native opening. A connection check allows that opening budget in addition to its `--timeout` database budget and starts a fresh query budget after opening. A driver's own connection timeout can impose an earlier limit; configure an adequate native Connect Timeout for interactive SQL login. Cancellation is propagated and created connections/sessions are disposed. Ctrl+C/user cancellation returns 130; unsupported/conflicting/invalid settings return 2; connection authentication failures, unavailable interaction and sign-in timeouts return 5. Deployment failures return 4 with safe diagnostics; inspection authentication failures return 5. Normal JSON results never contain token/device-code instruction fields. Malformed OAuth option values are not echoed by parser errors.

## Validation and live acceptance

Three separate levels are retained:

* CLI controlled tests execute the actual parser/application check, deployment and inspection paths through recording connections and injected OAuth sessions. They verify selection, conflicts, unattended policy, JSON/stderr separation, cancellation, time budgets, reader ordering and ownership. These are not live OAuth database logins.
* Library protocol tests execute actual MSAL device-code, denial, cancellation, silent-cache, expired-token refresh and explicit cache-clear/reacquisition through an in-memory HTTP transport. They make no network requests and are not live Entra acceptance. Native closed-configuration tests cover default and isolated installed-connector load contexts.
* Six shared provider QA cases are inherited by all 22 current projects: browser/device availability without sign-in, two competing credential-source guards, supported SQL dry run, and pre-cancellation. The ordinary SQL runner reports InteractionRequired; other adapters reject unsupported flows. These cases never report a password login as OAuth.

`CliOAuthLiveTests` has nine normally discoverable, opt-in cases: login/query, owned schema deployment/query/cleanup, and a fresh non-pooled subsequent connection for Entra browser, Entra device code and Snowflake browser. Entra explicitly clears its MSAL cache for the reacquisition case. Snowflake uses its native fresh-connection path with persistent caching disabled. Expiry-driven refresh is controlled-protocol coverage, not a verified live server/tenant expiry test.

To enable live acceptance on a human-attended, pre-provisioned host, set `DUBURL_OAUTH_LIVE=1` and the appropriate values below outside source control. Live DDL additionally requires `DUBURL_OAUTH_LIVE_SCHEMA=1` and an isolated database/schema where the fixture may create/drop only its UUID-owned table. All live URLs must be free of userinfo/password/token options and include `Pooling=false` so a successful old session cannot substitute for new OAuth opening.

* Entra: `DUBURL_OAUTH_ENTRA_URL`, `DUBURL_OAUTH_ENTRA_CLIENT_ID`, `DUBURL_OAUTH_ENTRA_AUTHORITY`, optional `DUBURL_OAUTH_ENTRA_SCOPES`.
* Snowflake: `DUBURL_OAUTH_SNOWFLAKE_URL`, `DUBURL_OAUTH_SNOWFLAKE_ACCOUNT`, `DUBURL_OAUTH_SNOWFLAKE_ROLE`.

Run `dotnet test DubUrl.Cli.Testing -c Release -f net10.0 --filter TestCategory=OAuthLive` after configuring the host. It requires human sign-in and does not provision tenants, accounts, application registrations, consent, cloud databases, or production credentials. Missing settings/permissions are explicit skips, never successful live evidence. Existing CI is unchanged and supplies no such external OAuth fixtures. **No supported provider/flow has a verified live OAuth database pass in this implementation's current validation; all nine live cases skip without opt-in.** Snowflake device code is unsupported, not a skipped supported flow.
