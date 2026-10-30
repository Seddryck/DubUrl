---
title: "Client certificate"
permalink: /client-certificate-authentication/
nav_exclude: true
---

# Client certificate

The client certificate and matching private key establish client identity in the TLS handshake. The database must authorize that identity; some accounts also require a database password. A PKCS#12 bundle (`.pfx` or `.p12`) can hold both the certificate and private key. PEM uses separate files.

This differs from [Access token]({{ '/sqlserver-token-authentication/' | relative_url }}): an Entra application certificate obtains a token; it is not a database TLS client identity.

## Library

The single options-based entry point is `ConnectionAuthentication.ClientCertificate(CertificateOptions certificateOptions, string? caPath = null)`. It configures the generated native connection and leaves the original URL unchanged.

For an already-loaded certificate, the caller retains ownership:

```csharp
using DubUrl;
using DubUrl.Authentication;
using System.Security.Cryptography.X509Certificates;

using var certificate = X509Certificate2.CreateFromPemFile("client.crt", "client.key");
using var options = new X509CertificateOptions(certificate);
var target = new ConnectionUrl("postgres://alice@db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.ClientCertificate(options, caPath: "server-ca.crt"));
await using var connection = await target.OpenAsync(cancellationToken);
```

For PEM files, DubUrl loads and owns the certificate:

```csharp
using var options = new X509CertificateOptions("client.crt", "client.key");
var target = new ConnectionUrl("postgres://alice@db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.ClientCertificate(options));
await using var connection = await target.OpenAsync(cancellationToken);
```

For a bundle, obtain its decryption password from a secure application source:

```csharp
using var options = new Pkcs12CertificateOptions("client.p12")
{
    DecryptionPasswordSource = certificatePasswordSource
};
var target = new ConnectionUrl("mysql://alice@db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.ClientCertificate(options, "server-ca.crt"));
await using var connection = await target.OpenAsync(cancellationToken);
```

`certificatePasswordSource` implements `IUsernamePasswordCredentialSource`; only its `Password` is used. A stored username never replaces the database username. Alternatively construct `new Pkcs12CertificateOptions("client.pfx", bundlePassword)` or `new X509CertificateOptions("client.crt", "client.key", keyPassword)`. Do not combine a direct decryption password with a decryption source. Omit the password for unencrypted PEM or passwordless bundles; an explicitly empty string is preserved.

For MySQL, MariaDB and SingleStore accounts requiring both a certificate and a database password:

```csharp
using var options = new X509CertificateOptions("client.crt", "client.key")
{
    DecryptionPasswordSource = certificatePasswordSource, // only for an encrypted PEM key
    SupplementalCredentials = databaseCredentials
};
var target = new ConnectionUrl("mysql://alice@db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.ClientCertificate(options));
await using var connection = await target.OpenAsync(cancellationToken);
```

`databaseCredentials` independently implements `IUsernamePasswordCredentialSource`. PostgreSQL-family certificate authentication rejects this supplement. Both sources must implement `ISynchronousUsernamePasswordCredentialSource` for synchronous creation before their first resolution. Asynchronous operations honor cancellation.

### Server trust, lifetime and migration

`caPath` is an optional PEM CA file that verifies the **server**. Omitting it uses the driver's normal trust configuration, with certificate and hostname verification still required. The Npgsql mapping sets `Root Certificate`; MySqlConnector and SingleStoreConnector use `SSL CA`. A CA file does not establish client identity.

Construction, dialect inspection and CLI dry runs do not load files or acquire secrets. First connection creation validates the material and lazily loads it once per options instance. Missing/malformed files, mismatched or absent keys, wrong decryption passwords and invalid CA files produce safe authentication diagnostics.

Keep the options alive until every returned connection has been disposed, including connections that will be closed and reopened. Dispose the options after those connections. DubUrl disposes file-loaded certificates; disposing options around a caller-supplied `X509Certificate2` never disposes that certificate. Keep caller-owned certificates alive for the same interval. The managed mappings append an opaque per-options identity to the native application name, isolating pools across options instances. Disposing options clears their managed pools before releasing owned keys; dispose all returned connections first. The application name prefix is retained. MySql.Data has no application-name pool discriminator, so its file-only mapping disables managed pooling. ODBC driver-manager pooling is native configuration: keep file material unchanged until all sessions are retired and restart/clear native pools when rotating identities. DubUrl does not change machine-wide pooling settings.

File-only drivers reopen certificate/key paths themselves. Keep those files available and unchanged for the entire native connection lifetime. They cannot accept an existing certificate object. DubUrl does not export private keys to temporary files to accommodate such drivers.

The previous `X509Certificate2` overloads remain available with obsolete migration messages. Wrap the certificate in `X509CertificateOptions` and move `ClientCertificateAuthenticationOptions.SupplementalCredentials` to `CertificateOptions.SupplementalCredentials`.

Competing URL passwords, certificate files, CA settings, opaque authentication strings and weaker TLS modes are rejected before decryption. Supply trust with `caPath` instead of a URL option. No fallback authentication or disabled server verification is used.

## CLI

Certificate selection uses the same Library strategy for `connection check`, `schema deploy`, and `schema inspect` wherever the shared authentication bindings are offered. Direct URLs, `--connection` names and `--connection-env` variables retain their existing selection rules.

```shell
duburl connection check 'postgres://alice@db.example.com/app' --client-certificate client.crt --client-key client.key --ca-certificate server-ca.crt --engine
duburl connection check 'postgres://alice@db.example.com/app' --client-certificate client.pfx --certificate-password-prompt --engine
duburl connection check 'postgres://alice@db.example.com/app' --client-certificate client.p12 --certificate-credential database-client-certificate --engine
duburl schema deploy schema.json --connection production --client-certificate client.crt --client-key client.key --ca-certificate server-ca.crt
```

Use `--certificate-password-prompt` for an encrypted PEM key or bundle. The prompt is hidden and requires an interactive terminal; cancellation returns exit code 130. For headless execution, `--certificate-credential` reads a named credential saved with `duburl credential add` through CredOS. Its stored username is ignored. See [OS credential-store setup and access requirements]({{ '/cli-authentication/' | relative_url }}) for creating named entries and unlocking the appropriate user session/keychain. Denied access, missing entries and invalid material fail safely; authentication failures from connection checks use exit code 5. Never pass a plaintext decryption password on the command line.

The two certificate password sources are mutually exclusive. `--client-key` is required for PEM and forbidden for `.pfx`/`.p12`. Certificate-specific options require `--client-certificate`. OS identity and OAuth selections conflict with certificate selection. These conflicts and unsupported routes are validated before prompting or reading stores. Schema deployment `--dry-run` loads no certificate and reads no secrets.

`--password-prompt` and `--credential` remain separate database-password sources. For example, a MySQL account can use `--certificate-password-prompt --credential database-account` together. The certificate decryption password unlocks local material; it is never used as the database-account password. CLI-owned options are disposed after command execution on success, failure and cancellation.

## Database, driver and client-OS support

The following are implemented configurations, **not claims of verified live login on every OS**. All require the corresponding provider package, compatible native driver where applicable, TLS-enabled server, and an account authorized for the presented certificate. Windows/Linux/macOS refer to the client. Mapping tests do not configure the server or prove certificate-to-account authorization.

| Database | Driver route | Windows | Linux | macOS | Requirements and limits |
|---|---|---|---|---|---|
| PostgreSQL | Npgsql 10.0.3 | Yes | Yes | Yes | All three options forms; `cert`/`hostssl` rule and certificate-to-role mapping. |
| CockroachDB | Npgsql 10.0.3 | Yes | Yes | Yes | All forms; secure cluster and certificate for the database user. |
| TimescaleDB (self-hosted PostgreSQL) | Npgsql 10.0.3 | Yes | Yes | Yes | All forms; configure PostgreSQL certificate authentication. Managed services may not expose the necessary server rules. |
| MySQL, MariaDB | MySqlConnector 2.6.2 | Yes | Yes | Yes | All forms; account `REQUIRE X509`, `SUBJECT` or `ISSUER`, plus database password if required. |
| SingleStore | SingleStoreConnector 1.2.0 | Yes | Yes | Yes | All forms; deployment/account must require client certificates. |
| MySQL, MariaDB | MySql.Data 26.7.0 | Yes | Yes | Yes | Explicitly register/select the alternative mapper. File-only: unencrypted PEM, or PFX/P12. Existing objects and encrypted PEM are unsupported. A bundle uses `CertificateFile`/`CertificatePassword` and normal trust; custom CA plus bundle is rejected because this driver aliases the CA setting to `CertificateFile`. PEM supports `SslCert`/`SslKey`/`SslCa`. |
| PostgreSQL, CockroachDB, self-hosted TimescaleDB | psqlODBC | Yes | Yes | Yes | File-only unencrypted PEM; `Pqopt` forwards `sslcert`, `sslkey`, `sslrootcert` to libpq, with `SSLmode=verify-full`. Requires libpq 16+ for `require_auth=none` and `sslcertmode=require`; GSS encryption is disabled. Without a CA path, `sslrootcert=system` uses system trust. Install matching psqlODBC/native libpq; older libraries fail rather than weaken verification. |
| MySQL, MariaDB | MySQL Connector/ODBC 8+ | Yes | Yes | Yes | File-only unencrypted PEM; `SSLCERT`, `SSLKEY`, optional `SSLCA`, `SSLMODE=VERIFY_IDENTITY`. Install vendor ODBC driver and driver manager. |
| MariaDB | MariaDB Connector/ODBC 3.1.1+ | Yes | Yes | Yes | File-only unencrypted PEM; `SSLVERIFY=1`, `FORCETLS=1`. TLS backend must support the selected PEM files and trust configuration. |
| Apache Drill, CrateDB, DuckDB, Firebird SQL, IBM Db2, Microsoft Access, Microsoft Excel, Microsoft SQL Server, Oracle Database, QuestDB, Snowflake, Trino | Catalogued ADO.NET/ODBC/OLE DB routes | No | No | No | Shared TLS-client-certificate strategy is not implemented for these routes. Server TLS, wallets, token acquisition or wire compatibility alone do not establish support. |
| MySQL and other catalogued databases | OLE DB | No | No | No | No implemented certificate-to-account mapping in the catalogued OLE DB providers; OLE DB itself is Windows-only. |

ODBC accepts only explicit supported vendor drivers, not DSNs or arbitrary paths. It rejects bundles, existing certificate objects and encrypted keys before password acquisition. Provider packages and native drivers must match the client architecture. Existing isolated live fixtures cover PostgreSQL, CockroachDB, MySQL, MariaDB and SingleStore when their environment prerequisites are supplied; unavailable fixtures are reported as skips.

Driver references: [Npgsql security](https://www.npgsql.org/doc/security.html), [MySqlConnector options](https://mysqlconnector.net/connection-options/), [Connector/NET options](https://dev.mysql.com/doc/connectors/en/connector-net-8-0-connection-options.html), [psqlODBC configuration](https://odbc.postgresql.org/docs/config.html), [libpq SSL](https://www.postgresql.org/docs/current/libpq-ssl.html), [MySQL ODBC options](https://dev.mysql.com/doc/connector-odbc/en/connector-odbc-configuration-connection-parameters.html), [MariaDB ODBC TLS](https://mariadb.com/docs/connectors/mariadb-connector-odbc/mariadb-connector-odbc-guide).

[CLI authentication]({{ '/cli-authentication/' | relative_url }}) · [Library authentication]({{ '/authentication/' | relative_url }})
