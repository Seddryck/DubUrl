---
title: Connection encryption and defaults
parent: Connecting to databases
nav_order: 5
permalink: /connection-configuration/
---

Configure transport security, opening, commands, native pools and session behavior independently:

```csharp
using DubUrl;
using DubUrl.Authentication;

var target = new ConnectionUrl("postgres://db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.UsernamePassword(username, password))
    .WithEncryption(ConnectionEncryption.Tls(caPath: "server-ca.crt"))
    .WithConnectionTimeout(TimeSpan.FromSeconds(15))
    .WithCommandTimeout(TimeSpan.FromSeconds(30))
    .WithPooling(new ConnectionPoolingOptions
    {
        MaxSize = 50,
        IdleTimeout = TimeSpan.FromMinutes(5),
        MaxLifetime = TimeSpan.FromHours(1)
    })
    .WithApplicationName("Reporting")
    .WithTimeZone("Europe/Brussels");

await using var connection = await target.OpenAsync(cancellationToken);
using var command = connection.CreateCommand();
command.CommandTimeout = 60; // Per-command values override the native default.
```

Every fluent call returns a new target and preserves `Url`. Settings compose regardless of call order.
Repeated calls for one setting use the last required value. Pooling is one complete setting, so a
later `WithPooling` replaces the earlier pool options. Except for TLS conflicts, fluent values override
equivalent URL options; unspecified values retain native defaults. Unsupported required options fail
before opening. [Optional configuration]({{ '/docs/optional-configuration/' | relative_url }}) remains
available for application name, pooling and timeouts. Encryption and time zone are always required.

## CLI

`connection check`, `schema deploy` and `schema inspect` accept the same options through the Library:

```sh
duburl connection check 'mssql://sql.example.com/app' --os-identity --tls --engine
duburl connection check 'postgres://alice@db.example.com/app' --password-prompt --ca-certificate server-ca.crt --engine
duburl connection check 'postgres://db.example.com/app' --connection-timeout 15s --command-timeout 30s --application-name Reporting --time-zone Europe/Brussels --pooling enabled --pool-max-size 50 --pool-idle-timeout 5m --pool-max-lifetime 1h
duburl connection check --connection production --tls --engine
duburl connection check --connection-env DATABASE_URL --tls --engine
duburl schema deploy schema.yaml --connection production --tls --application-name SchemaDeploy
```

Use `--pooling disabled` to disable pooling. `--pool-min-size` sets its minimum size. Durations require
`ms`, `s`, `m` or `h` units and may contain a decimal point, e.g. `500ms` or `1.5s`. Positive durations
round **up** to seconds. For connection/command timeouts, `infinite` and `0s` select native zero.
Negative values and values above 2,147,483,647 seconds fail; native providers may impose smaller limits.
Zero pool lifetime means no maximum age; zero idle timeout is accepted only for the MySQL-family
drivers where it disables idle expiration. Npgsql and Teradata require positive idle durations.

`--ca-certificate` implies `--tls` and is shared with client-certificate authentication. The selected
URL may be direct, named or environment-backed, following each command's existing selector rules.
Help and schema dry runs perform no database connection, credential acquisition or certificate-file
loading. Configuration validation does not prove that a server accepts a setting.

For token/OAuth login, add `--tls` to the authentication options documented in
[CLI authentication]({{ '/cli-authentication/' | relative_url }}), on a TLS-supported route.
For example, SQL Server Entra device code can combine `--tls --oauth device-code` with its required
client, tenant and scope options. Unsupported combinations fail explicitly.

## Server trust and authentication

Authentication chooses the database identity. `ConnectionEncryption.Tls()` requires encryption,
certificate-chain validation and verification of the actual server hostname. There is no plaintext
fallback. With no CA argument, the driver uses its normal system/driver trust. With a CA file, use a
PEM trust bundle supported by the selected driver. DubUrl configures the client; administrators must
enable TLS on the server, install a certificate matching its DNS name and arrange appropriate trust.
DubUrl never installs CA certificates into machine-wide stores.

Conflicting native URL TLS modes, trust bypasses, hostname overrides and incompatible CA selections
are rejected. SQL Server `Encrypt=Strict` is preserved. A SQL Server `ServerCertificate` pin is not a
CA bundle and is not used to implement custom CA trust. SQL Server and native Trino require external
system trust if their drivers cannot consume a CA path. A Unix socket or named pipe is not TCP TLS.

Client certificates authenticate the **client**, and require a matching private key. Server CA trust
is independent:

```csharp
using var certificate = new X509CertificateOptions("client.crt", "client.key");
var target = new ConnectionUrl("postgres://alice@db.example.com/app")
    .WithEncryption(ConnectionEncryption.Tls("server-ca.crt"))
    .WithAuthentication(ConnectionAuthentication.ClientCertificate(certificate));
```

The legacy `ClientCertificate(options, caPath)` entry point remains supported. Identical CA paths
compose; conflicting custom paths fail before loading certificate material. A legacy CA remains in
effect when `Tls()` supplies no replacement CA. Client callbacks, supplemental credentials and
certificate ownership remain with the authentication strategy. Connector/NET bundle authentication
cannot combine its certificate-file mode with a separate CA, and its certificate route requires
disabled pooling. Native certificate strategies may add a pool discriminator to application metadata.
Keep certificate options alive for their documented lifetime. Rotation does not revoke existing sessions.

## Native mapping and platform support

These are exact route declarations, checked against the dependency versions in the project. Native
managed routes below are available on Windows, Linux and macOS subject to the driver's own runtime,
native-library and server prerequisites. This change's executable validation runs on Windows only.
ODBC requires the named installed vendor driver; OLE DB is Windows-only. A dash means the shared API
rejects that option, even when a vendor offers another configuration mechanism.

| Database / driver | TLS | Open timeout | Command default | Application | Session time zone |
|---|---|---|---|---|---|
| SQL Server / SqlClient 7.1.1 | `Encrypt=Mandatory;Trust Server Certificate=false`; system CA only | `Connect Timeout` | `Command Timeout` | `Application Name`, maximum 128 UTF-8 bytes | — |
| PostgreSQL, Timescale, CockroachDB / Npgsql 10.0.3 | `SSL Mode=VerifyFull`, optional `Root Certificate`, GSS encryption disabled | `Timeout` | `Command Timeout` | `Application Name`, maximum 63 UTF-8 bytes | `Timezone`; named zones |
| CrateDB, QuestDB / Npgsql | Same verified TLS; CrateDB needs PostgreSQL TLS enabled; QuestDB requires Enterprise PGWire TLS | `Timeout` | `Command Timeout` | — | — |
| MySQL, MariaDB / MySqlConnector 2.6.2 | `SSL Mode=VerifyFull`, optional `SSL CA` | `Connection Timeout` | `Default Command Timeout` | `Application Name` → `program_name` | `SET SESSION time_zone`; native connection reset required when pooled |
| MySQL, MariaDB / Connector/NET 26.7.0, explicitly selected alternative mapper | `SSL Mode=VerifyFull`, optional `SslCa` | `Connection Timeout` | `Default Command Timeout` | — | Same SQL; use `Connection Reset=true` or disable pooling |
| SingleStore / SingleStoreConnector 1.2.0 | `SSL Mode=VerifyFull`, optional `SSL CA` | `Connection Timeout` | `Default Command Timeout` | `Application Name` → `program_name` | — |
| Oracle / ODP.NET 23.26.301 | — (wallet/TCPS configuration remains external) | `Connection Timeout` | — | Native `ModuleName` property, maximum 48 UTF-8 bytes; reapplied on every open, reset by managed ODP.NET on close | `ALTER SESSION SET TIME_ZONE`; requires disabled pooling |
| Db2 / IBM provider 8.0.0.300 on net8, 9.0.0.200 otherwise | — | `Connect Timeout` | — | `ClientApplicationName`, maximum 255 UTF-8 bytes; servers can impose smaller limits | — (server-family-specific semantics) |
| Firebird / FirebirdClient 10.3.3 | — (wire encryption is not TLS) | `Connection Timeout` | `Command Timeout` | `Application Name` | `SET TIME ZONE`; Firebird 4+ and disabled pooling required |
| Teradata / provider 20.0.6 | `SslMode=Verify-Full`, optional `SslCA`; engine 16.20.53.30+ with Gateway TLS | `Connection Timeout` | — (native connection default is not a writable instance default) | Native `TdQueryBand.ApplicationName`; preserves other bands, native value restrictions apply | `SET TIME ZONE INTERVAL`; signed offsets only and disabled pooling |
| Trino / NReco.PrestoAdo 1.1.0 | `UseSsl=true`, system trust only | — (HTTP request timeout is not opening) | — | — | — (request/session-state propagation is not established) |
| SQLite / Microsoft.Data.Sqlite 10.0.12 | — (local file) | — | — (busy/lock timeout is not a general command deadline) | — | — |
| DuckDB native and ADBC | — (local file) | — | — | — | — (no mapped, isolated connection-scoped contract) |
| Snowflake / Snowflake.Data 6.3.0 | — (native mandatory HTTPS remains unchanged) | — | — | — (`APPLICATION` has partner-application restrictions) | — (`HonorSessionTimezone` concerns materialization, not setting the session zone) |
| Access, Excel, Text, ADOMD and remaining routes | — | — | — | — | — |

| Native pool route | Enable / size keywords | Idle expiration | Maximum physical age |
|---|---|---|---|
| SqlClient | `Pooling`, `Min Pool Size`, `Max Pool Size` | — | `Load Balance Timeout` |
| All declared Npgsql routes | `Pooling`, `Minimum Pool Size`, `Maximum Pool Size` | `Connection Idle Lifetime` | `Connection Lifetime` |
| MySqlConnector, SingleStoreConnector | `Pooling`, `Minimum Pool Size`, `Maximum Pool Size` | `Connection Idle Timeout` | `Connection Lifetime` |
| Connector/NET | Same size keywords | — | `Connection Lifetime` |
| ODP.NET | `Pooling`, `Min Pool Size`, `Max Pool Size` | — | `Connection Lifetime` |
| Firebird | `Pooling`, `MinPoolSize`, `MaxPoolSize` | — | `Connection Lifetime` |
| Teradata | `Connection Pooling`, `Min Pool Size`, `Max Pool Size` | `Connection Pooling Timeout` | — |
| SQLite | `Pooling` only | — | — |
| Other routes, ODBC, OLE DB | — | — | — |

Driver-manager ODBC pooling and OLE DB resource pooling are not per-target portable pools. DubUrl does
not change their process/machine settings or disable unrelated OLE DB services. All shared opening,
command, application, time-zone and pool options are rejected for ODBC/OLE DB. Explicit TLS supports
SQL Server ODBC Driver 18+ and MSOLEDBSQL/MSOLEDBSQL19, psqlODBC Unicode/ANSI (libpq 16+), MySQL ODBC
8+ and MariaDB ODBC 3.1+. SQL Server uses native encryption plus verification with external CA trust;
psqlODBC uses `SSLmode=verify-full` and `Pqopt` for GSS-disable/CA trust; MySQL uses
`SSLMODE=VERIFY_IDENTITY`/`SSLCA`; MariaDB uses `FORCETLS=1;SSLVERIFY=1`/`SSLCA`. Unknown drivers and DSNs
are rejected. Drill, Simba Trino, Oracle OLE DB and other transport drivers have no declared TLS mapping.
These rejections describe this API's coverage, not whether a database supports TLS or mutual TLS.

## Timing, pooling and session semantics

Opening timeout configures the native driver's establishment budget, not an OAuth/password prompt,
credential store, query, server statement timeout or hard end-to-end deadline. SqlClient, Npgsql and
MySQL-family drivers also use opening budgets for waiting on a pool lease; DNS, multiple endpoints,
retries, authentication and network reads remain driver-specific. Oracle's timeout can primarily bound
pool-lease waiting rather than every physical network stage. Teradata has separate retry controls.
Native command defaults govern the provider's execution/network-read behavior and do not guarantee
server-side cancellation. Caller cancellation remains separate; recognized native timeout exceptions
remain native even with authentication. The CLI check's `--timeout` is still an overall database-check
budget and bounds a larger/infinite command default without replacing a smaller explicit default.

Pools belong to the provider/application process, normally keyed by native connection configuration.
Separate one-shot CLI invocations do not share a persistent pool. Size, idle expiration above the
minimum and maximum physical connection age are distinct; expiration generally happens during native
pruning/return to the pool rather than aborting an active query. Unspecified fields retain defaults.
Sizes must be valid and ordered, and no limits or lifetimes can accompany disabled pooling.

Time zones affect database session SQL behavior, not the machine clock or arbitrary .NET values.
Named zones preserve daylight-saving/history; DubUrl never substitutes today's offset. Input is a
named identifier or a signed `HH:mm` offset between -12:00 and +14:00, subject to the table's restrictions.
Npgsql numeric offset strings use PostgreSQL POSIX semantics, so this API rejects them; use a named
zone, e.g. `Etc/GMT-2` for fixed UTC+02. Npgsql application/time-zone defaults reject `No Reset On Close=true`.
MySQL/MariaDB named zones require loaded server time-zone tables.
Oracle needs its server time-zone data. Firebird needs server 4+. SQL-based initialization runs on every
logical open, including reopen; it uses a strict identifier allowlist, honors Library opening
cancellation via native command cancellation and disposes the connection on failure. Oracle, Firebird
and Teradata require disabled pools to avoid leaking SQL session state into unrelated callers.

## Verification and vendor references

Mapping tests exercise actual installed native builders and command defaults without opening remote
databases. Lifecycle tests cover repeated initialization, failure disposal and cancellation. This is
not evidence of a successful TLS handshake, effective server session metadata, pool session reuse or
real slow-query timeout behavior. Those need appropriately configured external fixtures, including
untrusted-CA, wrong-hostname, no-TLS, lease-wait and controlled slow-operation cases.

Reviewed contracts: [SqlClient](https://learn.microsoft.com/sql/connect/ado-net/connection-string-syntax),
[Npgsql](https://www.npgsql.org/doc/connection-string-parameters.html),
[MySqlConnector](https://mysqlconnector.net/connection-options/),
[SingleStoreConnector](https://github.com/memsql/SingleStoreNETConnector/blob/master/docs/content/connection-options.md),
[ODP.NET ModuleName](https://docs.oracle.com/en/database/oracle/oracle-database/19/odpnt/ConnectionModuleName.html),
[Db2 client application name](https://www.ibm.com/docs/en/db2/11.5.x?topic=keywords-clientapplicationname),
[Firebird driver source](https://github.com/FirebirdSQL/NETProvider/blob/master/src/FirebirdSql.Data.FirebirdClient/FirebirdClient/FbConnectionStringBuilder.cs),
[Teradata TLS](https://teradata-docs.s3.amazonaws.com/doc/connectivity/tdnetdp/17.20/help/Teradata.Client.Provider~Teradata.Client.Provider.TdConnectionStringBuilder~SslMode.html),
[CrateDB TLS](https://cratedb.com/docs/crate/reference/en/latest/admin/ssl.html),
[QuestDB TLS](https://questdb.com/docs/security/tls/), and
[CockroachDB session variables](https://docs.cockroachlabs.com/docs/stable/show-vars).
