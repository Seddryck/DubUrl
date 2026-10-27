# Shared client-certificate authentication

```csharp
using DubUrl.Authentication;
var target = new ConnectionUrl("postgres://alice@localhost/app?Root%20Certificate=" + escapedCaPath)
    .WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate));
await using var connection = await target.OpenAsync(cancellationToken);
```

The certificate must have its private key. No key export, temporary key file or
machine-wide trust change is performed. Server verification is separate and always
requires VerifyFull; explicitly weaker TLS settings conflict. Configure fixture-local
root trust through the native Root Certificate/SSL CA setting. Caller owns the
certificate and supplemental source and must keep them usable for future physical
connections. Rotating certificates requires draining/clearing affected native pools;
reused physical sessions do not prove a new identity handshake.

| Database | Tested native hook/version | Identity/password semantics |
|---|---|---|
| PostgreSQL | Npgsql 10.0.3 ProvideClientCertificatesCallback | cert pg_hba rule maps certificate CN to role (or explicit pg_ident mapping); no password fallback (`Require Auth=None`) |
| CockroachDB | Npgsql 10.0.3 same native hook | secure cluster's user/certificate requirements apply independently; use certificate identity matching its account |
| MySQL | MySqlConnector 2.6.2 ProvideClientCertificatesCallback | account REQUIRE X509/SUBJECT/ISSUER; account plugin may also require a password |
| MariaDB | MySqlConnector 2.6.2 same native hook | configure account certificate restrictions and its password plugin independently |
| SingleStore | SingleStoreConnector 1.2.0 ProvideClientCertificatesCallback | deployment must support required client certificates and account restrictions; not inferred from MySQL |

For MySQL/MariaDB/SingleStore only, pass explicit
`new ClientCertificateAuthenticationOptions { SupplementalCredentials = source }`
to the shared factory when the account also requires credentials. The source resolves
once per new logical connection, preserves cancellation and checks identities.
Private-key decryption passwords are unrelated to DB passwords. Other providers
(including SqlClient, SQLite, embedded DuckDB, Snowflake) reject this mechanism.
Snowflake key-pair JWT and certificate-backed Entra tokens are distinct features.

The dependency-light core binds only the known native per-connection hooks by their
version-tested type/property signatures; unsupported shapes fail closed. This path
requires reflection metadata and is not advertised for trimmed/Native AOT clients.
No global certificate cache or per-connection data source is created. Configuration,
metadata and Parse remain lazy and never resolve supplemental credentials. Parse
stays URL-only. Native provider connections can retain credentials in memory.

## Isolated PostgreSQL fixture

Run `QA/Authentication/PostgreSql/create-fixture.sh` on a Linux Docker host with
OpenSSL. Set `DUBURL_AUTH_FIXTURE_DIR` to its printed temporary directory and run `docker compose
-f QA/Authentication/PostgreSql/compose.yaml up -d`. The fixture uses cert-only host
authentication for alice/app; no weaker host fallback is permitted. Generated keys
remain outside the repository. Delete only that generated temporary directory and
the fixture's containers/volumes when done.

Set `DUBURL_POSTGRESQL_CERT_URL` to
`postgres://alice@localhost:55432/app?Pooling=false&Root%20Certificate=<escaped-ca-path>`,
and `DUBURL_POSTGRESQL_CERT_PEM`/`DUBURL_POSTGRESQL_CERT_KEY` to the generated alice
certificate/key files. Execute the explicitly selected `ClientCertificateLiveTests`
case with `dotnet test DubUrl.Core.Testing -f net10.0 --filter FullyQualifiedName~ClientCertificateLiveTests`.
For other databases set analogous COCKROACHDB/MYSQL/MARIADB/SINGLESTORE variables,
using a certificate-restricted test account and a trusted server certificate.
SingleStore requires an appropriately configured certificate-required deployment; the ordinary upstream QA is now license-free. No cloud credentials
are required by deterministic adapter tests.

The [provider certificate QA inventory](client-certificate-qa.md) separates native configuration from real PostgreSQL certificate-only login, negative TLS/identity cases and native pool/rotation behavior, all discovered by the existing QA workflows without CI changes. Other databases' certificate-required live fixtures remain unprovisioned and explicitly skipped. Routine local runs do not start containers. Hook tests do not prove live authentication or supplemental account-plugin semantics. [Npgsql TLS behavior](https://www.npgsql.org/doc/security.html)
and [MySqlConnector certificate options](https://mysqlconnector.net/connection-options/)
describe the native boundaries.
