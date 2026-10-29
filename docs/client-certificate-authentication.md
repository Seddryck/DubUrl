---
title: "Client certificate"
permalink: /client-certificate-authentication/
nav_exclude: true
---

# Client certificate

The client presents a certificate and proves possession of its private key during the database's TLS handshake. The database authorizes the certificate identity, or requires a certificate in addition to an account password.

This differs from [Access token]({{ '/sqlserver-token-authentication/' | relative_url }}): an Entra application certificate obtains a token from Entra; SQL Server does not receive that certificate as a TLS client identity.

## Database and operating-system support

The operating system is the **client** running DubUrl, not the database server. **Yes** requires the native provider and the database setup described below. **Conditional** names an extra platform or driver requirement. **No** means this documented DubUrl method is unavailable; it does not claim that the database itself lacks that authentication mechanism. ODBC, OLE DB, ADOMD, and ADBC are outside these shared authentication methods; their driver-specific sign-in settings are separate.

| Database | Windows | Linux | macOS | CLI | Library | Requirements / reason |
|---|---|---|---|---|---|---|
| Apache Drill | No | No | No | No | No | No supported native route for this method. |
| Azure Analysis Services | No | No | No | No | No | No supported native route for this method. |
| CockroachDB | Yes | Yes | Yes | No | Yes | Npgsql; secure cluster with a certificate for the database user. |
| CrateDB | No | No | No | No | No | No supported native route for this method. |
| DuckDB | No | No | No | No | No | No supported native route for this method. |
| Firebird SQL | No | No | No | No | No | No supported native route for this method. |
| IBM Db2 | No | No | No | No | No | No supported native route for this method. |
| MariaDB | Yes | Yes | Yes | No | Yes | MySqlConnector; account certificate restrictions, plus password if required. |
| Microsoft Access | No | No | No | No | No | No supported native route for this method. |
| Microsoft Excel | No | No | No | No | No | No supported native route for this method. |
| Microsoft SQL Server | No | No | No | No | No | No supported native route for this method. |
| MySQL | Yes | Yes | Yes | No | Yes | MySqlConnector; account certificate restrictions, plus password if required. |
| Oracle Database | No | No | No | No | No | No supported native route for this method. |
| PostgreSQL | Yes | Yes | Yes | No | Yes | Npgsql; server certificate authentication maps certificate identity to a role. |
| Power BI Desktop | No | No | No | No | No | No supported native route for this method. |
| Power BI Premium | No | No | No | No | No | No supported native route for this method. |
| QuestDB | No | No | No | No | No | No supported native route for this method. |
| SingleStore | Yes | Yes | Yes | No | Yes | SingleStoreConnector; deployment/account must require the client certificate. |
| Snowflake | No | No | No | No | No | No supported native route for this method. |
| SQL Server Analysis Services — Multidimensional | No | No | No | No | No | No supported native route for this method. |
| SQL Server Analysis Services — Tabular | No | No | No | No | No | No supported native route for this method. |
| SQLite | No | No | No | No | No | No supported native route for this method. |
| Teradata | No | No | No | No | No | No supported native route for this method. |
| Text files | No | No | No | No | No | No supported native route for this method. |
| Timescale | No | No | No | No | No | No supported native route for this method. |
| Trino | No | No | No | No | No | No supported native route for this method. |

## Prerequisites

Obtain a client certificate with its private key and the server's trusted CA certificate. Configure the database to require and authorize that client certificate. The certificate identity must satisfy its role/account rules:

- PostgreSQL: a `cert` authentication rule and certificate-to-role mapping.
- CockroachDB: a secure cluster and a user certificate for the requested account.
- MySQL and MariaDB: account restrictions such as `REQUIRE X509`, `SUBJECT`, or `ISSUER`.
- SingleStore: a deployment and account configured to require client certificates.

The client must also verify the server's certificate and hostname with `VerifyFull`. Configure `Root Certificate` for Npgsql or `SSL CA` for the MySQL-family driver. A private-key decryption password is separate from a database password.

## Library

Pass an application-loaded `X509Certificate2` containing the private key:

```csharp
var location = "postgres://alice@db.example.com/app?Root%20Certificate="
    + Uri.EscapeDataString(caPath);
var target = new ConnectionUrl(location)
    .WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate));
await using var connection = await target.OpenAsync(cancellationToken);
```

For MySQL, MariaDB, or SingleStore accounts that also require a password, supply it explicitly:

```csharp
var method = ConnectionAuthentication.ClientCertificate(certificate,
    new ClientCertificateAuthenticationOptions
    {
        SupplementalCredentials = databaseCredentials
    });
var target = new ConnectionUrl(databaseLocation).WithAuthentication(method);
await using var connection = await target.OpenAsync(cancellationToken);
```

Here `databaseCredentials` is an `IUsernamePasswordCredentialSource`. PostgreSQL and CockroachDB do not accept this password supplement through this method. Keep the certificate and private key available for future connections. After replacing a certificate, retire existing pooled connections before requiring all sessions to use the new identity.

## CLI

There is no dedicated CLI selection for this method. Use the Library; `--credential-store` supplies a database password, not a client certificate.

See [Npgsql TLS and certificate settings](https://www.npgsql.org/doc/security.html) and [MySqlConnector connection options](https://mysqlconnector.net/connection-options/).

[CLI authentication]({{ '/cli-authentication/' | relative_url }}) · [Library authentication]({{ '/authentication/' | relative_url }})
