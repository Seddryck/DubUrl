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
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td><td>{{ unsupported }}</td><td>{{ supported }}</td>
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
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td><td>{{ unsupported }}</td><td>{{ supported }}</td>
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
      <th scope="rowgroup" rowspan="3">MySQL</th>
      <th scope="row">ADO.NET</th>
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td><td>{{ unsupported }}</td><td>{{ supported }}</td>
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
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td><td>{{ unsupported }}</td><td>{{ supported }}</td>
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
      <td>{{ supported }}</td><td>{{ supported }}</td><td>{{ supported }}</td><td>{{ unsupported }}</td><td>{{ supported }}</td>
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

- **CockroachDB:** Npgsql; secure cluster with a certificate for the database user.
- **MariaDB:** MySqlConnector; account certificate restrictions, plus password if required.
- **MySQL:** MySqlConnector; account certificate restrictions, plus password if required.
- **PostgreSQL:** Npgsql; server certificate authentication maps certificate identity to a role.
- **SingleStore:** SingleStoreConnector; deployment/account must require the client certificate.

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
