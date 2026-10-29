# Operating-system identity

The CLI and Library select the same strategy:

```shell
duburl connection check 'mssql://sql.example.com/app' --os-identity --engine
```

```csharp
using DubUrl.Authentication;
var target = new ConnectionUrl("mssql://sql.example.com/app");
var authenticated = target.WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
await using var connection = await authenticated.OpenAsync(cancellationToken);
// Connect()/ConnectAsync() instead return a configured, closed native connection.
```

The original URL is unchanged. No password prompt, credential-store read, alternate
user acquisition, impersonation or machine configuration occurs. OS identity
conflicts with URL passwords (including explicitly empty ones), native password
aliases, tokens, OAuth, client certificates and competing authentication options.
CLI `--password-prompt`, `--credential-store` and `--username` also conflict.
PostgreSQL and Trino may retain a URL database-role hint; it does not select a
different OS identity. SqlClient, Db2, Oracle, Teradata and Firebird reject URL
usernames. A subsequent Library `WithAuthentication` call replaces the strategy.

## Common prerequisites

A working authenticated OS session or existing ticket cache must already exist.
Administrators must configure server authentication, account/role mappings, DNS,
SPNs/principals and the domain/KDC. Install the selected native driver, its platform
libraries and any required plugins/packages independently. Ticket acquisition,
keytabs, renewal, delegation and infrastructure setup are outside this API.
Use provider TLS verification options for network connections; authentication is
not a substitute for transport encryption. Reused physical pooled connections
may retain an earlier identity; use fresh physical connections for negative tests.

SSPI is a Windows API which may negotiate Kerberos or NTLM. Kerberos is a network
authentication protocol. Local PostgreSQL peer authentication checks the identity
of a local Unix-socket process and is separate from both. A successful SSPI login
does not by itself prove Kerberos. Firebird's current SSPI implementation chooses
NTLM and is not advertised here as verified Kerberos.

## Driver and client OS support

"Implemented" below means the shared strategy configures that exact route.
Mapping tests exercise real closed native connections, not successful logins.
The new network routes have no live domain/KDC/server acceptance evidence.
ADOMD expansion is outside this feature.

| Database / selected driver | Client OS and driver capability | Implemented settings / limits | Live verification |
|---|---|---|---|
| SQL Server / Microsoft.Data.SqlClient 7.1.1 | Windows SSPI; Linux/macOS depend on native Kerberos prerequisites | `Integrated Security=true`; no UID/password; LocalDB Windows only | Existing isolated LocalDB fixture; no network domain/Kerberos claim |
| SQL Server / Microsoft ODBC 17 or 18 | Windows integrated security; Linux/macOS Kerberos with vendor prerequisites | `Trusted_Connection=yes`; other ODBC drivers and opaque paths reject | Unverified |
| SQL Server / MSOLEDBSQL or MSOLEDBSQL19 | Windows | `Integrated Security=SSPI`; legacy SQLOLEDB/SQLNCLI reject | Unverified |
| PostgreSQL / Npgsql 10.0.3 | Windows/Linux/macOS network GSS/SSPI; Unix peer on non-Windows | Network `Require Auth=GSS, SSPI`, preserving TLS; local socket `Require Auth=None`, `SSL Mode=Disable` | Existing controlled Linux peer fixture; network unverified |
| Self-hosted TimescaleDB / Npgsql 10.0.3 | Same underlying PostgreSQL capabilities when enabled by deployment | Same native strategy; hosted services are not presumed to support it | Unverified |
| CockroachDB / Npgsql 10.0.3 | Self-hosted GSSAPI requires an Enterprise license and client Kerberos prerequisites | Network GSS/SSPI only; no insecure/password fallback | Unverified |
| PostgreSQL / self-hosted TimescaleDB / psqlODBC | Windows/Linux/macOS; selected PostgreSQL ANSI/Unicode driver must use libpq >=16 | `Pqopt={require_auth=gss,sspi}`; TLS/role retained; older libpq rejects unsupported option | Unverified |
| Db2 / IBM.Data.Db2 8.0.0.300 (net8), 9.0.0.200 (net9/10) | Windows/Linux packages are platform specific; IBM Kerberos prerequisites apply | `Authentication=KERBEROS`; explicit nonempty `TargetPrincipal` required | Unverified |
| Oracle / ODP.NET Managed/Core 23.26.301 | Windows NTS is separate from Kerberos; Core Kerberos requires `Oracle.ManagedDataAccess.Kerberos` and Oracle Net configuration | `User Id=/`; external authentication only; package/Oracle Net errors fail safely on opening | Unverified |
| Oracle / OraOLEDB.Oracle | Windows client/Oracle Net prerequisites | `OSAuthent=1`, `User ID=/`, password-change dialog disabled | Unverified |
| Teradata / Teradata.Client.Provider 20.0.6 | Vendor platform support and server SPNEGO/TeraGSS configuration required | `Integrated Security=true`, `Authentication Mechanism=SPNEGO` | Unverified |
| Firebird / FirebirdSql.Data.FirebirdClient 10.3.3 | Windows trusted/Win_Sspi; current client chooses NTLM | `Integrated Security=true`; non-Windows rejects | Mapping only; no Kerberos claim |
| Drill / MapR Drill ODBC Driver or HPE Drill ODBC Driver | Windows SSPI/MIT; non-Windows MIT where vendor supports installed driver | `AuthenticationType=Kerberos`; explicit `KrbServiceHost`, default `KrbServiceName=drill`; optional Windows `UseOnlySSPI` | Unverified |
| Trino / Simba Trino ODBC Driver | Windows native SSPI or MIT (`UseGSSAPI`); Linux/macOS MIT | `AuthenticationType=Kerberos Authentication`, `UseExistingKrbCreds=1`, `DelegateKrbCreds=0`, `SSL=1`; optional `KrbServiceName` | Unverified |
| Trino / NReco.PrestoAdo 1.1.0 | No implemented Kerberos route | Explicitly unsupported; never substitutes ODBC | None |
| MariaDB / MySqlConnector 2.6.2 or MariaDB ODBC | Vendor GSSAPI plugin capability exists; MySqlConnector accepts `ServerSPN` | Unverified OS-only enforcement: strategy rejects rather than treating an empty password as proof of GSSAPI | None |
| MySQL / MySql.Data 26.7.0 or MySQL ODBC | Enterprise/server-plugin/version/OS dependent; Connector.NET Kerberos excludes macOS; ODBC mode option requires >=8.0.32 | Unverified OS-only enforcement: strategy rejects; no implicit substitution of MySqlConnector | None |
| SingleStore / SingleStoreConnector 1.2.0 | Client GSSAPI/SPNEGO code is not server-plugin acceptance evidence | Explicitly unverified and unsupported by this strategy | None |

ODBC/OLE DB mappings validate the actual mapped driver/provider, not just the
scheme. DSN/FileDSN and opaque nested authentication strings reject because they
can hide competing identities/secrets. Caller `Pqopt`/`D5`, provider strings,
keytabs, kinit options and delegation overrides also reject. URL options such as
Db2 `TargetPrincipal`, Drill `KrbServiceHost` and Trino `KrbServiceName` are copied
through the normal mapper; the strategy invents no environment-specific principal.
Unknown providers/drivers fail with `UnsupportedAuthentication`; missing Db2/Drill
configuration fails with `MissingCredentials`. Opening failures use the safe
`AuthenticationFailed` diagnostic. Cancellation and disposal use the shared
Library lifecycle. Neither CLI nor Library catches an OS failure and retries with
password authentication.

For PostgreSQL peer use:
`postgres://alice@localhost:5432/app?Host=%2Fvar%2Frun%2Fpostgresql`.
The absolute Host directory and Port select the Unix socket. Mixed/multiple socket
hosts reject. The server's pg_hba/pg_ident must enforce peer; `Require Auth=None`
rejects password/GSS but cannot distinguish peer from a server configured for trust.
Network connections never receive peer-only settings or have TLS weakened.

## Vendor evidence and verification boundaries

Checked against repository dependency declarations and these vendor sources:

- [Npgsql security](https://www.npgsql.org/doc/security.html) and [connection parameters](https://www.npgsql.org/doc/connection-string-parameters.html).
- [Microsoft ODBC integrated authentication](https://learn.microsoft.com/en-us/sql/connect/odbc/linux-mac/using-integrated-authentication) and [OLE DB authentication](https://learn.microsoft.com/en-us/sql/connect/oledb/quickstart-cpp).
- [psqlODBC parameters](https://odbc.postgresql.org/docs/config.html) and [libpq require_auth](https://www.postgresql.org/docs/current/libpq-connect.html).
- [CockroachDB GSSAPI](https://www.cockroachlabs.com/docs/stable/gssapi_authentication).
- [IBM authentication](https://www.ibm.com/docs/en/db2/11.5.x?topic=keywords-authentication) and [target principal](https://www.ibm.com/docs/en/db2/11.1?topic=keywords-targetprincipal).
- [Oracle ODP.NET security](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/featConnecting.html) and [OraOLEDB OS authentication](https://docs.oracle.com/en/database/oracle/oracle-database/26/oledb/features-of-oledb.html).
- [Teradata mechanism selection](https://teradata-docs.s3.amazonaws.com/doc/connectivity/tdnetdp/16.20/help/AuthenticationMechanismSelection.html).
- [Firebird authentication plugins](https://www.firebirdsql.org/docs/html/en/refdocs/fbconf/firebird-configuration-reference.html).
- [Drill ODBC connection strings](https://drill.apache.org/docs/testing-the-odbc-connection/) and [ODBC settings](https://drill.apache.org/docs/odbc-configuration-reference/).
- [Simba Trino Kerberos settings](https://documentation.insightsoftware.com/trino-online-documentation-windows/content/odbc/pr/configuring/authenticating/kerberos.htm).
- [MariaDB GSSAPI plugin](https://mariadb.com/docs/server/reference/plugins/authentication-plugins/authentication-plugin-gssapi), [MySqlConnector options](https://mysqlconnector.net/connection-options/) and [2.6.2 handshake source](https://github.com/mysql-net/MySqlConnector/blob/2.6.2/src/MySqlConnector/Core/ServerSession.cs). The client starts with password mechanisms and accepts server-selected plugin switches; no per-connection OS-only allowlist was established.
- [MySQL Connector.NET authentication](https://dev.mysql.com/doc/connector-net/en/connector-net-authentication.html) documents `DefaultAuthenticationPlugin=authentication_kerberos_client`, KerberosAuthMode and platform/plugin prerequisites. A default plugin is not an enforced allowlist. MariaDB GSSAPI does not establish MySQL authentication_kerberos_client support.

Existing [OS-identity live QA](operating-system-identity-qa.md) remains available.
Mapping passes must not be described as successful Kerberos logins. Windows/Linux/
macOS network domains, KDCs and the corresponding protected servers are not
provisioned by this feature. See the PR for the exact commands, TFMs and live
fixtures actually run for this change.
