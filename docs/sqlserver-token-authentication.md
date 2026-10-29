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

The operating system is the **client** running DubUrl, not the database server. {{ supported }} means this authentication method is supported through the listed driver route; install the required provider or driver and satisfy the prerequisites in the notes. {{ unsupported }} means this documented method is unavailable for that route. Each database has a separate row for each catalogued native ADO.NET, ODBC or OLE DB route. Driver-specific sign-in options for other methods are separate from the shared APIs shown here.

| Database | Driver route | Windows | Linux | macOS | CLI | Library | Requirements / reason |
|---|---|---|---|---|---|---|---|
| Apache Drill | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| CockroachDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| CrateDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| DuckDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| DuckDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Firebird SQL | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| IBM Db2 | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| MariaDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| MariaDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Access | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Access | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Microsoft Excel | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Excel | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Microsoft SQL Server | ADO.NET | {{ supported }} | {{ supported }} | {{ supported }} | {{ unsupported }} | {{ supported }} | Entra-enabled SQL endpoint; token audience and database identity must match. |
| Microsoft SQL Server | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft SQL Server | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| MySQL | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| MySQL | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| MySQL | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Oracle Database | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Oracle Database | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| PostgreSQL | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| PostgreSQL | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| QuestDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| QuestDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| SingleStore | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Snowflake | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| SQLite | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Teradata | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Text files | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Timescale | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Timescale | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Trino | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Trino | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |

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
