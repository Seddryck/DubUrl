---
title: "Device-code sign-in (OAuth)"
permalink: /device-code-authentication/
nav_exclude: true
---

# Device-code sign-in (OAuth)

A user opens a verification URL in a browser, enters a short-lived user code, and signs in to Microsoft Entra. The application waiting in the terminal then connects to SQL Server as that user. The browser can be on a different device; the client running DubUrl does not need a local browser.

## Database and operating-system support

{% assign supported = '<span class="auth-supported" role="img" aria-label="Supported" title="Supported">&#10004;</span>' %}
{% assign unsupported = '<span class="auth-unsupported" role="img" aria-label="Not supported" title="Not supported">&#10008;</span>' %}

The operating system is the **client** running DubUrl, not the database server. {{ supported }} means this authentication method is supported through the listed driver route; install the required provider or driver and satisfy the prerequisites in the notes. {{ unsupported }} means this documented method is unavailable for that route. Database names span their catalogued native ADO.NET, ODBC and OLE DB driver rows. Driver-specific sign-in options for other methods are separate from the shared APIs shown here.

<table class="authentication-support">
  <thead>
    <tr><th scope="col">Database</th><th scope="col">Driver route</th><th scope="col">Windows</th><th scope="col">Linux</th><th scope="col">macOS</th><th scope="col">CLI</th><th scope="col">Library</th></tr>
  </thead>
  <tbody>
    <tr>
      <th scope="rowgroup">Apache Drill</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">CockroachDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">CrateDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">DuckDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Firebird SQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">IBM Db2</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">MariaDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Microsoft Access</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Microsoft Excel</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="3">Microsoft SQL Server</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="3">MySQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Oracle Database</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">PostgreSQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">QuestDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">SingleStore</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Snowflake</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">SQLite</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Teradata</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Text files</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Timescale</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Trino</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
</table>

**OLE DB is supported only on Windows.** All routes require a compatible provider or driver for the client OS and architecture.

The shared API for this method supports the native ADO.NET routes marked above. ODBC and OLE DB sign-in settings for this method must be configured through the driver separately.

- **Microsoft SQL Server:** Entra public client permitting device-code sign-in; browser may be elsewhere.

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
