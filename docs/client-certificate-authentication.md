---
title: "Client certificate"
permalink: /client-certificate-authentication/
nav_exclude: true
---

# Client certificate

The client presents a certificate and proves possession of its private key during the database's TLS handshake. The database authorizes the certificate identity, or requires a certificate in addition to an account password.

This differs from [Access token]({{ '/sqlserver-token-authentication/' | relative_url }}): an Entra application certificate obtains a token from Entra; SQL Server does not receive that certificate as a TLS client identity.

## Database and operating-system support

{% assign supported = '<span class="auth-supported" role="img" aria-label="Supported" title="Supported">&#10004;</span>' %}
{% assign unsupported = '<span class="auth-unsupported" role="img" aria-label="Not supported" title="Not supported">&#10008;</span>' %}

The operating system is the **client** running DubUrl, not the database server. {{ supported }} means this authentication method is supported through the listed driver route; install the required provider or driver and satisfy the prerequisites in the notes. {{ unsupported }} means this documented method is unavailable for that route. Each database has a separate row for each catalogued native ADO.NET, ODBC or OLE DB route. Driver-specific sign-in options for other methods are separate from the shared APIs shown here.

| Database | Driver route | Windows | Linux | macOS | CLI | Library | Requirements / reason |
|---|---|---|---|---|---|---|---|
| Apache Drill | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| CockroachDB | ADO.NET | {{ supported }} | {{ supported }} | {{ supported }} | {{ unsupported }} | {{ supported }} | Npgsql; secure cluster with a certificate for the database user. |
| CrateDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| DuckDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| DuckDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Firebird SQL | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| IBM Db2 | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| MariaDB | ADO.NET | {{ supported }} | {{ supported }} | {{ supported }} | {{ unsupported }} | {{ supported }} | MySqlConnector; account certificate restrictions, plus password if required. |
| MariaDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Access | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Access | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Microsoft Excel | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Excel | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Microsoft SQL Server | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Microsoft SQL Server | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft SQL Server | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| MySQL | ADO.NET | {{ supported }} | {{ supported }} | {{ supported }} | {{ unsupported }} | {{ supported }} | MySqlConnector; account certificate restrictions, plus password if required. |
| MySQL | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| MySQL | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Oracle Database | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Oracle Database | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| PostgreSQL | ADO.NET | {{ supported }} | {{ supported }} | {{ supported }} | {{ unsupported }} | {{ supported }} | Npgsql; server certificate authentication maps certificate identity to a role. |
| PostgreSQL | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| QuestDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| QuestDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| SingleStore | ADO.NET | {{ supported }} | {{ supported }} | {{ supported }} | {{ unsupported }} | {{ supported }} | SingleStoreConnector; deployment/account must require the client certificate. |
| Snowflake | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| SQLite | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Teradata | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Text files | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Timescale | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Timescale | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Trino | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No supported native route for this method. |
| Trino | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |

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
