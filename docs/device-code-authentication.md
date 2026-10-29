---
title: "Device-code sign-in (OAuth)"
permalink: /device-code-authentication/
nav_exclude: true
---

# Device-code sign-in (OAuth)

A user opens a verification URL in a browser, enters a short-lived user code, and signs in to Microsoft Entra. The application waiting in the terminal then connects to SQL Server as that user. The browser can be on a different device; the client running DubUrl does not need a local browser.

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
| Microsoft SQL Server | Yes | Yes | Yes | Yes | Yes | Entra public client permitting device-code sign-in; browser may be elsewhere. |
| MySQL | No | No | No | No | No | No supported native route for this method. |
| Oracle Database | No | No | No | No | No | No supported native route for this method. |
| PostgreSQL | No | No | No | No | No | No supported native route for this method. |
| Power BI Desktop | No | No | No | No | No | No supported native route for this method. |
| Power BI Premium | No | No | No | No | No | No supported native route for this method. |
| QuestDB | No | No | No | No | No | No supported native route for this method. |
| SingleStore | No | No | No | No | No | No supported native route for this method. |
| Snowflake | No | No | No | No | No | Snowflake's supplied OAuth session offers browser sign-in only. |
| SQL Server Analysis Services — Multidimensional | No | No | No | No | No | No supported native route for this method. |
| SQL Server Analysis Services — Tabular | No | No | No | No | No | No supported native route for this method. |
| SQLite | No | No | No | No | No | No supported native route for this method. |
| Teradata | No | No | No | No | No | No supported native route for this method. |
| Text files | No | No | No | No | No | No supported native route for this method. |
| Timescale | No | No | No | No | No | No supported native route for this method. |
| Trino | No | No | No | No | No | No supported native route for this method. |

## Prerequisites

Use an Entra-enabled SQL endpoint and grant the user database access. Register an Entra public-client application that permits device-code sign-in. The tenant's policies must allow this flow. Use the tenant authority and SQL scope for the appropriate cloud.

This remains a human sign-in method. It is not an unattended service-account login and does not bypass MFA or conditional access. Device code has no redirect URI.

## CLI

```shell
duburl connection check --connection sql-dev --engine --oauth device-code --oauth-client-id 11111111-1111-1111-1111-111111111111 --oauth-tenant 22222222-2222-2222-2222-222222222222
```

Replace the GUIDs with your public-client registration and tenant. Follow the verification URL and enter the displayed user code before it expires.

The verification URL, user code, and expiry are written to stderr. To consume JSON stdout while a human signs in, keep stderr visible and explicitly permit interaction:

```shell
duburl connection check --connection sql-dev --engine --oauth device-code --allow-oauth-interaction --oauth-client-id 11111111-1111-1111-1111-111111111111 --oauth-tenant 22222222-2222-2222-2222-222222222222 --output-format json
```

The same flags apply to `schema deploy` and `schema inspect`. Do not supply `--oauth-redirect-uri`, a password, or another credential source. For a tenant-specific sovereign-cloud authority use `--oauth-authority` instead of `--oauth-tenant`, and set the correct `--oauth-scope`. The default SQL scope is `https://database.windows.net/.default`.

Optional defaults use `Authentication:OAuth` configuration or environment variables such as `DUBURL_Authentication__OAuth__ClientId`, `...__Authority`, `...__Scopes`, and `...__TimeoutSeconds`. Scopes are space-separated. Select `--oauth device-code` explicitly; configuration does not grant permission for interactive sign-in. `--oauth-timeout` sets the sign-in budget (default 120 seconds, range 1–3600).

## Library

Add `DubUrl.Authentication.SqlServer`. Provide a callback that shows the verification URL and user code to the user:

```csharp
using var session = new EntraInteractiveOAuthSession(
    useDeviceCode: true,
    deviceCodePrompt: (verificationUrl, userCode, expiresAt, token) =>
    {
        Console.Error.WriteLine(
            $"Open {verificationUrl} and enter {userCode} before {expiresAt:u}.");
        return Task.CompletedTask;
    });
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

Set the required `RedirectUri` option to `http://localhost`; device-code sign-in does not use a browser callback. Keep the session alive until active opens finish. Show the code only to the person who requested sign-in. A still-valid signed-in session may be used without displaying another code.

Snowflake's supplied session does not offer device-code sign-in; use [Browser sign-in (OAuth)]({{ '/interactive-oauth/' | relative_url }}).

See [Microsoft's device-code guidance](https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/desktop-mobile/device-code-flow).

[CLI authentication]({{ '/cli-authentication/' | relative_url }}) · [Library authentication]({{ '/authentication/' | relative_url }})
