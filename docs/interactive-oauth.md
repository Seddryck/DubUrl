---
title: "Browser sign-in (OAuth)"
permalink: /interactive-oauth/
nav_exclude: true
---

# Browser sign-in (OAuth)

A user signs in to the identity provider in the local system browser. The application receives authorization through an authorization-code flow with PKCE, then connects to the database as that user. MFA and conditional-access requirements remain controlled by the identity provider.

## Database and operating-system support

The operating system is the **client** running DubUrl, not the database server. **Yes** requires the native provider and the database setup described below. **Conditional** names an extra platform or driver requirement. **No** means this documented DubUrl method is unavailable; it does not claim that the database itself lacks that authentication mechanism. ODBC, OLE DB, ADOMD, and ADBC are outside these shared authentication methods; their driver-specific sign-in settings are separate.

| Database | Windows | Linux | macOS | CLI | Library | Requirements / reason |
|---|---|---|---|---|---|---|
| Apache Drill | No | No | No | No | No | No supported native route for this method. |
| Azure Analysis Services | No | No | No | No | No | No supported native route for this method. |
| CockroachDB | No | No | No | No | No | No supported native route for this method. |
| CrateDB | No | No | No | No | No | No supported native route for this method. |
| DuckDB | No | No | No | No | No | No supported native route for this method. |
| Firebird SQL | No | No | No | No | No | No supported native route for this method. |
| IBM Db2 | No | No | No | No | No | No supported native route for this method. |
| MariaDB | No | No | No | No | No | No supported native route for this method. |
| Microsoft Access | No | No | No | No | No | No supported native route for this method. |
| Microsoft Excel | No | No | No | No | No | No supported native route for this method. |
| Microsoft SQL Server | Yes | Yes | Yes | Yes | Yes | Entra public client, local browser, and registered loopback redirect. |
| MySQL | No | No | No | No | No | No supported native route for this method. |
| Oracle Database | No | No | No | No | No | No supported native route for this method. |
| PostgreSQL | No | No | No | No | No | No supported native route for this method. |
| Power BI Desktop | No | No | No | No | No | No supported native route for this method. |
| Power BI Premium | No | No | No | No | No | No supported native route for this method. |
| QuestDB | No | No | No | No | No | No supported native route for this method. |
| SingleStore | No | No | No | No | No | No supported native route for this method. |
| Snowflake | Yes | Yes | Yes | Yes | Yes | Enabled SNOWFLAKE$LOCAL_APPLICATION integration, permitted role, local browser. |
| SQL Server Analysis Services — Multidimensional | No | No | No | No | No | No supported native route for this method. |
| SQL Server Analysis Services — Tabular | No | No | No | No | No | No supported native route for this method. |
| SQLite | No | No | No | No | No | No supported native route for this method. |
| Teradata | No | No | No | No | No | No supported native route for this method. |
| Text files | No | No | No | No | No | No supported native route for this method. |
| Timescale | No | No | No | No | No | No supported native route for this method. |
| Trino | No | No | No | No | No | No supported native route for this method. |

## SQL Server / Microsoft Entra

Use an Entra-enabled SQL endpoint and provision the user in the database. Register an Entra public-client application in the correct tenant, with a loopback browser redirect such as `http://localhost`. Grant the required consent and use the SQL audience for your cloud. The public Azure SQL scope is `https://database.windows.net/.default`.

### CLI

The named connection contains the SQL location without a username or password:

```shell
duburl connection check --connection sql-dev --engine --oauth browser --oauth-client-id 11111111-1111-1111-1111-111111111111 --oauth-tenant 22222222-2222-2222-2222-222222222222 --oauth-redirect-uri http://localhost
```

Replace the example GUIDs with the application's client ID and tenant ID. Use `--oauth-authority` instead of `--oauth-tenant` for a tenant-specific Microsoft authority, including a sovereign cloud. Use `--oauth-scope` for that cloud's SQL audience.

### Library

Add `DubUrl.Authentication.SqlServer` and import `DubUrl.Authentication.SqlServer` and `DubUrl.Authentication`:

```csharp
using var session = new EntraInteractiveOAuthSession();
var method = ConnectionAuthentication.OAuthInteractive(new InteractiveOAuthOptions
{
    ClientId = publicClientId,
    Authority = new Uri("https://login.microsoftonline.com/" + tenantId),
    RedirectUri = new Uri("http://localhost"),
    Scopes = ["https://database.windows.net/.default"],
    Session = session,
    AllowInteraction = true
});
await using var connection = await new ConnectionUrl(sqlLocation)
    .WithAuthentication(method).OpenAsync(cancellationToken);
```

## Snowflake

Use the account's enabled `SNOWFLAKE$LOCAL_APPLICATION` security integration and a user permitted to assume the requested role. Sign-in runs on the user's computer with a local browser and loopback redirect. This method uses the built-in public client; it does not select a custom client-secret integration.

### CLI

```shell
duburl connection check --connection snowflake-dev --engine --oauth browser --oauth-account myorg-myaccount --oauth-scope session:role:PUBLIC
```

The account defaults to the URL host; `--oauth-account` must identify the same account. The defaults are client `LOCAL_APPLICATION`, authority `https://<account>.snowflakecomputing.com`, and redirect `http://127.0.0.1:41589/callback/`.

### Library

Add `DubUrl.Authentication.Snowflake` and import `DubUrl.Authentication.Snowflake` and `DubUrl.Authentication`:

```csharp
using var session = new SnowflakeInteractiveOAuthSession(account);
var method = ConnectionAuthentication.OAuthInteractive(new InteractiveOAuthOptions
{
    ClientId = "LOCAL_APPLICATION",
    Authority = new Uri("https://" + account + ".snowflakecomputing.com"),
    RedirectUri = new Uri("http://127.0.0.1:41589/callback/"),
    Scopes = ["session:role:PUBLIC"],
    Session = session,
    AllowInteraction = true
});
await using var connection = await new ConnectionUrl(snowflakeLocation)
    .WithAuthentication(method).OpenAsync(cancellationToken);
```

A fresh Snowflake connection can require another browser sign-in. The native browser wait is bounded by the sign-in timeout, but cannot be cancelled immediately.

## Interaction and settings

Use the same CLI options on `schema deploy` and `schema inspect`. Browser sign-in needs a browser on the client and an available loopback port. For a human-operated command with redirected input/output, explicitly add `--allow-oauth-interaction`; `--yes` does not enable sign-in. For a host without a local browser, use [Device-code sign-in (OAuth)]({{ '/device-code-authentication/' | relative_url }}) where supported.

`--oauth-timeout <seconds>` sets the sign-in budget (default 120, range 1–3600). For connection checks, `--timeout` separately governs the database operation; allow enough native connection time for sign-in. Do not combine OAuth with URL credentials, `--username`, `--password-prompt`, or `--credential-store`.

Optional identity defaults use `Authentication:OAuth` configuration. Environment variables use `DUBURL_Authentication__OAuth__` followed by `ClientId`, `Tenant` or `Authority`, `RedirectUri`, `Scopes` (space-separated), `LoginHint`, `Account`, or `TimeoutSeconds`. CLI options override their matching setting. `--oauth-login-hint` suggests a user account; it does not change database authorization. Always select `--oauth browser` explicitly.

In the Library, keep the session alive until active opens finish and set `AllowInteraction = true` when user sign-in is allowed. Use async opening. A previously signed-in session may continue without another browser prompt; signing out of the browser does not close existing database sessions.

See [Microsoft public-client browser sign-in](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/desktop-mobile/acquiring-tokens-interactively) and [Snowflake OAuth for local applications](https://docs.snowflake.com/en/user-guide/oauth-local-applications).

[CLI authentication]({{ '/cli-authentication/' | relative_url }}) · [Library authentication]({{ '/authentication/' | relative_url }})
