---
title: OS identity driver mappings and verification
permalink: /operating-system-identity-mappings/
nav_exclude: true
---

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
CLI password prompts, named credentials, OAuth and --username also conflict.
PostgreSQL, MariaDB, MySQL, SingleStore and Trino may retain a URL database-role hint; it does not select a
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
MariaDB and SingleStore native routes have real Linux Kerberos evidence from the
[isolated KDC fixtures](../QA/Authentication/Kerberos/README.md). Other new network
routes remain unverified unless explicitly stated below.
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
| MariaDB / MySqlConnector 2.6.2 | Windows SSPI; Linux/macOS GSSAPI with the required native libraries | Explicit URL database role required; no password supplied; server account must use gssapi exclusively; optional ServerSPN retained | Real Linux/.NET 8/9/10 login/TLS and missing/wrong-ticket rejection verified against MariaDB 11.4.13 |
| MariaDB / MariaDB ODBC 3.1 or 3.2 | Vendor GSSAPI client plugin for the installed OS/architecture required | Preserve UID, TLS and PLUGIN_DIR; server-account plugin negotiation; no password or invented DEFAULT_AUTH/ServerSPN setting | Closed mappings tested; installed-driver login unverified |
| MySQL / MySql.Data 26.7.0 | Windows/Linux; Enterprise Kerberos server plugin; macOS rejected | Explicitly select MySql.Data; DefaultAuthenticationPlugin=authentication_kerberos_client; Windows KerberosAuthMode defaults to SSPI, accepts explicit SSPI/GSSAPI/AUTO | Mapping tested; live run blocked by unavailable Enterprise server/plugin |
| MySQL / MySQL ODBC >=8.0.32 | Documented Windows/Linux clients with Enterprise server plugin and matching client plugin installed | DEFAULT_AUTH=authentication_kerberos_client; authentication-kerberos-mode=SSPI on Windows, GSSAPI on Linux; preserve explicit compatible mode/TLS/PLUGIN_DIR | Closed mappings tested; displayed 8.0 names do not establish installed patch version |
| MySQL / default MySqlConnector 2.6.2 | MariaDB GSSAPI support does not implement the MySQL authentication_kerberos_client plugin | Explicitly unsupported for OS identity; never substitutes MySql.Data or another driver | None |
| SingleStore / SingleStoreConnector 1.2.0 | Source-compatible auth_gssapi_client using default process credentials and platform NegotiateStream; server authentication_gss account/KDC prerequisites apply | Explicit URL database role required; no password; optional ServerSPN retained | Real Linux/.NET 8/9/10 login/TLS and missing/wrong-ticket rejection verified against SingleStore 9.1.1 |

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

### MariaDB and MySQL selection

```shell
duburl connection check 'maria://app_role@db.example.com/app?SSL%20Mode=VerifyFull&ServerSPN=mariadb%2Fdb.example.com' --os-identity --engine
duburl connection check 'odbc+mysql://app_role@db.example.com/app?Driver=MySQL%20ODBC%209.4%20Unicode%20Driver&SSLMODE=VERIFY_IDENTITY' --os-identity --engine
duburl connection check 'odbc+maria://app_role@db.example.com/app?Driver=MariaDB%20ODBC%203.2%20Driver' --os-identity --engine
```

MariaDB accounts must be configured for `gssapi` exclusively. A database-role
hint is required for the native MySqlConnector and SingleStore mappers; omitting
it fails with `MissingCredentials`, before native creation. Neither mapper
guesses an account from the client's environment. `ServerSPN` is optional on those
native routes; no value is invented. MariaDB ODBC has no corresponding SPN
keyword, so that combination rejects; provision its client plugin where needed
and use the vendor-supported `PLUGIN_DIR` option. Plugin architecture must match
the driver. Modern Windows MariaDB ODBC bundles plugins; other installations may
need the Connector/C plugin files. These are deployment prerequisites, not setup
actions performed by the flag.

For MySQL, use an Enterprise server account configured exclusively for Kerberos.
`DEFAULT_AUTH`/`DefaultAuthenticationPlugin` select the MySQL Kerberos client
plugin. Windows defaults to SSPI; explicit GSSAPI uses an existing MIT cache.
Connector.NET also accepts explicit AUTO (SSPI then GSSAPI). Connector.NET's
`KerberosAuthMode` is Windows-only and rejects on Linux; MySQL ODBC uses
`authentication-kerberos-mode=GSSAPI` on Linux. The latter option requires ODBC
8.0.32 or later. A driver display name containing only `8.0` cannot identify its
patch version; verify that prerequisite independently. Explicit older versions,
unknown drivers, cross-vendor driver selections and macOS MySQL Kerberos reject.

`mysql://` normally selects MySqlConnector in the bundled native catalog, which
does not implement MySQL's Kerberos plugin. The strategy never silently swaps
that driver. CLI users can explicitly select the MySQL ODBC route above. Library
applications can register `MySql.Data` as their chosen `mysql` provider using the
existing `MySqlDataMapper` and `ProviderRegistration`; that explicit catalog
selection is covered by tests. No new bundled MySql.Data connector pack is added.
Userless Connector.NET calls derive their username from the native credential
cache/current session. Its SSPI driver requires any supplied MySQL username to
match the current Windows username; the strategy does not acquire another user's
credentials to satisfy a mismatched role.

These MySQL-family routes rely on the configured server account's plugin policy
and the selected driver's negotiation. DubUrl supplies no password, invokes no
credential source and adds no retry with another authentication strategy. This
is not a client protocol allowlist: it cannot certify that an arbitrary endpoint
or a misconfigured passwordless account actually requires Kerberos. Provision
plugin-only accounts and verify server policy independently. Mapping tests are
not proof of that policy or of a completed GSSAPI exchange.

### SingleStore verification

SingleStore's server account plugin is `authentication_gss`. Its vendor ODBC
source registers the wire plugin as `auth_gssapi_client`, reads the NUL-terminated
server SPN and exchanges GSSAPI/SSPI tokens using existing credentials. The
installed SingleStoreConnector 1.2.0 source dispatches that same wire plugin,
parses the same SPN field and calls `NegotiateStream` with
`CredentialCache.DefaultNetworkCredentials`. A supplied `ServerSPN` additionally
requires mutual authentication. Those checks establish source compatibility
with the vendor client protocol rather than assuming that MariaDB support alone
establishes SingleStore support.

Evidence: [SingleStore server account prerequisites](https://docs.singlestore.com/db/v8.7/security/authentication/kerberos-authentication/configuring-singlestore-for-kerberos-authentication/),
[vendor ODBC v1.2.2 wire plugin](https://github.com/memsql/singlestore-odbc-connector/blob/v1.2.2/libmariadb/plugins/auth/auth_gssapi_client.c),
[its GSSAPI exchange](https://github.com/memsql/singlestore-odbc-connector/blob/v1.2.2/libmariadb/plugins/auth/gssapi_client.c),
[SingleStoreConnector 1.2.0 dispatch](https://github.com/memsql/SingleStoreNETConnector/blob/b684828029856c5d74d5b9ed09b5f76c9daa71e6/src/SingleStoreConnector/Core/ServerSession.cs)
and [credential/SPN handling](https://github.com/memsql/SingleStoreNETConnector/blob/b684828029856c5d74d5b9ed09b5f76c9daa71e6/src/SingleStoreConnector/Protocol/Serialization/AuthGSSAPI.cs).
The [isolated fixture](../QA/Authentication/Kerberos/README.md) now also verifies
real SingleStore 9.1.1 login using SingleStoreConnector 1.2.0 against an MIT KDC
on Linux/.NET 8/9/10. Sync/async queries use certificate-verified TLS; missing and
different-principal caches reject. The server account is exclusively
`authentication_gss` and requires TLS. This adds live evidence to the source
checks. Windows SSPI/AD, macOS, other server versions, ticket renewal/expiry and
pooling remain unverified by this fixture.

### Opt-in real authentication tests

`MySqlOperatingSystemIdentityLiveTests` provides synchronous/asynchronous query
cases for each native provider. Provision a server account exclusively using
MariaDB `gssapi`, MySQL `authentication_kerberos`, or SingleStore
`authentication_gss`, and an existing OS ticket cache independently. Set
both `URL` and `ACCOUNT` variables using the appropriate prefix:
`DUBURL_MARIADB_OS_QA_`, `DUBURL_MYSQL_OS_QA_`, or `DUBURL_SINGLESTORE_OS_QA_`.
For example, MariaDB uses `DUBURL_MARIADB_OS_QA_URL` and
`DUBURL_MARIADB_OS_QA_ACCOUNT`. `ACCOUNT` is the exact expected
`CURRENT_USER()` result; do not place a password in the URL. Select a finite
native connection timeout in fixture configuration. Tests perform no account,
KDC, ticket or machine configuration and print neither URL nor identity values.

Run `dotnet test DubUrl.Core.Testing -f net10.0 --filter FullyQualifiedName~MySqlOperatingSystemIdentityLiveTests`.
Missing fixtures explicitly skip. A successful account query is acceptance
evidence only when the prerequisite plugin-only server policy is established;
the query itself does not identify the negotiated authentication protocol.

Alternatively, [run the isolated fixtures](../QA/Authentication/Kerberos/README.md)
to provision the test-only KDC, TLS and MariaDB/SingleStore plugin-only accounts.
That harness also runs `PluginOnlyAccountRejectsOtherOrMissingTicket` in separate
processes with missing/wrong caches. MySQL remains blocked because its Enterprise
server Kerberos plugin/image is unavailable; no Community/MariaDB substitute is used.

### References

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
- [MariaDB GSSAPI plugin](https://mariadb.com/docs/server/reference/plugins/authentication-plugins/authentication-plugin-gssapi), [ODBC client-plugin deployment](https://mariadb.com/docs/connectors/mariadb-connector-odbc/mariadb-connector-odbc-guide) and [MySqlConnector 2.6.2 GSSAPI source](https://github.com/mysql-net/MySqlConnector/blob/2.6.2/src/MySqlConnector/Protocol/Serialization/AuthGSSAPI.cs).
- [MySQL Connector.NET authentication](https://dev.mysql.com/doc/connector-net/en/connector-net-authentication.html) documents `DefaultAuthenticationPlugin=authentication_kerberos_client`, KerberosAuthMode and platform/plugin prerequisites. The implementation selects that plugin explicitly and never obtains a password. MariaDB GSSAPI does not establish MySQL authentication_kerberos_client support.
- [Connector.NET 26.7.0 credential handling](https://github.com/mysql/mysql-connector-net/blob/26.7.0/MySQL.Data/src/Authentication/KerberosAuthenticationPlugin.cs) and [MySQL ODBC options](https://dev.mysql.com/doc/connector-odbc/en/connector-odbc-configuration-connection-parameters.html) define the selected native modes.

Existing [OS-identity live QA](operating-system-identity-qa.md) remains available.
Mapping passes must not be described as successful Kerberos logins. The strategy
does not provision production domains, KDCs or servers. The separate opt-in fixture
provisions only disposable test containers. See its README and the PR for exact
commands, TFMs and verified live scope.
