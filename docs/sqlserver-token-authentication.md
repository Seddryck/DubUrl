---
title: "Access token"
permalink: /sqlserver-token-authentication/
nav_exclude: true
---

# Access token

SQL Server accepts a short-lived Microsoft Entra access token instead of a database password. The token identifies a user or application and must be issued for the SQL resource accepted by that server.

For a person signing in, use [Browser sign-in (OAuth)]({{ '/interactive-oauth/' | relative_url }}) or [Device-code sign-in (OAuth)]({{ '/device-code-authentication/' | relative_url }}). This page covers application-supplied tokens and certificate-based application sign-in.

## Database and operating-system support

{% assign supported = '<span class="auth-supported" role="img" aria-label="Supported" title="Supported">&#10004;</span>' %}
{% assign unsupported = '<span class="auth-unsupported" role="img" aria-label="Not supported" title="Not supported">&#10008;</span>' %}

The operating system is the **client** running DubUrl, not the database server. {{ supported }} means this authentication method is supported through the listed driver route; install the required provider or driver and satisfy the prerequisites in the notes. {{ unsupported }} means this documented method is unavailable for that route. Database names span their catalogued native ADO.NET, ODBC and OLE DB driver rows. Driver-specific sign-in options for other methods are separate from the shared APIs shown here.

<table class="authentication-support">
  <thead>
    <tr><th scope="col">Database</th><th scope="col">Driver route</th><th scope="col">Windows</th><th scope="col">Linux</th><th scope="col">macOS</th></tr>
  </thead>
  <tbody>
    <tr>
      <th scope="rowgroup">Apache Drill</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">CockroachDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">CrateDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">DuckDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Firebird SQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">IBM Db2</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">MariaDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Microsoft Access</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Microsoft Excel</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="3">Microsoft SQL Server</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="3">MySQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Oracle Database</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">OLE DB</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">PostgreSQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">QuestDB</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">SingleStore</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Snowflake</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">SQLite</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Teradata</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup">Text files</th>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Timescale</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
  <tbody>
    <tr>
      <th scope="rowgroup" rowspan="2">Trino</th>
      <th scope="row">ADO.NET</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
    <tr>
      <th scope="row">ODBC</th>
      <td>{{ unsupported }}</td><td>{{ unsupported }}</td><td>{{ unsupported }}</td>
    </tr>
  </tbody>
</table>

**OLE DB is supported only on Windows.** All routes require a compatible provider or driver for the client OS and architecture.

The shared API for this method supports the native ADO.NET routes marked above. ODBC and OLE DB sign-in settings for this method must be configured through the driver separately.

- **Microsoft SQL Server:** Entra-enabled SQL endpoint; token audience and database identity must match.

## Prerequisites

Use an Entra-enabled SQL endpoint, such as Azure SQL or an appropriately configured SQL Server. Provision the Entra user/application in the database and grant its permissions. An ordinary SQL login is not an Entra identity.

For certificate-based application sign-in, register the public certificate on the Entra application and make its private key available only to the application. Configure the tenant authority and SQL resource for the relevant cloud. This is application authentication to Entra, not a TLS client-certificate handshake with SQL Server.

## Library

Use `DubUrl.Authentication.SqlServer`:

```csharp
using DubUrl.Authentication.SqlServer;

var source = new EntraCertificateTokenSource(
    clientId,
    new Uri("https://login.microsoftonline.com/" + tenantId),
    new Uri("https://database.windows.net/"),
    certificate);
var target = new ConnectionUrl("mssql://server.database.windows.net/app")
    .WithAuthentication(SqlServerAuthentication.TokenCallback(source));
await using var connection = await target.OpenAsync(cancellationToken);
```

For a different application token source, implement `ISqlServerTokenSource`. Return a `SqlAuthenticationToken` with the token and its real expiry for the requested authority/resource. Do not put a token, private key, or client secret in the URL.

Reuse a source/strategy for one identity. When changing an application identity or certificate, retire its old pooled connections; an already authenticated database session does not sign in again just because the certificate changes.

## CLI

There is no dedicated CLI option to supply a token callback or select certificate-based application sign-in. Use the Library. The CLI's two OAuth options are user sign-in methods.

See [Microsoft's SqlClient Entra authentication guidance](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-active-directory-authentication).

[CLI authentication]({{ '/cli-authentication/' | relative_url }}) · [Library authentication]({{ '/authentication/' | relative_url }})
