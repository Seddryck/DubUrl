---
title: "Access token"
permalink: /sqlserver-token-authentication/
nav_exclude: true
---

# Access token

SQL Server accepts a short-lived Microsoft Entra access token instead of a database password. The token identifies a user or application and must be issued for the SQL resource accepted by that server.

For a person signing in, use [Browser sign-in (OAuth)]({{ '/interactive-oauth/' | relative_url }}) or [Device-code sign-in (OAuth)]({{ '/device-code-authentication/' | relative_url }}). This page covers application-supplied tokens and certificate-based application sign-in.

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
| Microsoft SQL Server | Yes | Yes | Yes | No | Yes | Entra-enabled SQL endpoint; token audience and database identity must match. |
| MySQL | No | No | No | No | No | No supported native route for this method. |
| Oracle Database | No | No | No | No | No | No supported native route for this method. |
| PostgreSQL | No | No | No | No | No | No supported native route for this method. |
| Power BI Desktop | No | No | No | No | No | No supported native route for this method. |
| Power BI Premium | No | No | No | No | No | No supported native route for this method. |
| QuestDB | No | No | No | No | No | No supported native route for this method. |
| SingleStore | No | No | No | No | No | No supported native route for this method. |
| Snowflake | No | No | No | No | No | No supported native route for this method. |
| SQL Server Analysis Services — Multidimensional | No | No | No | No | No | No supported native route for this method. |
| SQL Server Analysis Services — Tabular | No | No | No | No | No | No supported native route for this method. |
| SQLite | No | No | No | No | No | No supported native route for this method. |
| Teradata | No | No | No | No | No | No supported native route for this method. |
| Text files | No | No | No | No | No | No supported native route for this method. |
| Timescale | No | No | No | No | No | No supported native route for this method. |
| Trino | No | No | No | No | No | No supported native route for this method. |

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
