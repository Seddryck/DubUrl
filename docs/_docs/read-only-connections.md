---
title: Read-only connections
parent: Connecting to databases
nav_order: 5
subtitle: Require native read-only access without silently falling back to a writable database
---

`WithReadOnly()` requests the selected provider's native read-only access mode for the primary
database. The current mappings support existing SQLite and DuckDB files. Unsupported routes throw
`NotSupportedException` during resolution, before a connection is created. There is no writable
fallback and this method is unavailable inside `WithOptional(...)`.

```csharp
var target = new ConnectionUrl("sqlite:///reporting.db")
    .WithReadOnly()
    .WithOptional(options => options.WithApplicationName("Reporting"));

using var connection = target.Open();
using var command = connection.CreateCommand();
command.CommandText = "SELECT count(*) FROM sales";
Console.WriteLine(command.ExecuteScalar());
```

The caller owns the connection. `Connect()` returns a closed native connection and `Open()` returns
an opened one; neither introduces a wrapper or hidden transaction. Fluent calls return a new URL,
preserve the original URL and unrelated settings, and preserve the required read-only request across
subsequent optional calls. Repeated `WithReadOnly()` calls are idempotent. There is deliberately no
boolean overload to disable the requirement accidentally.

## Scope and precedence

The fluent requirement overrides equivalent native write-mode parameters in the URL. For example,
`sqlite:///reporting.db?Mode=ReadWriteCreate` followed by `WithReadOnly()` opens the primary file
read-only and cannot create a missing database. Invalid native configuration still fails validation.
Empty/in-memory databases and MotherDuck URLs are outside these file mappings and are rejected.

Read-only database access is **not a sandbox for arbitrary SQL**. Temporary tables remain writable
where the engine permits them. Attached databases, extensions, external files, and explicit later
connection reconfiguration follow native rules. Use filesystem/database permissions and appropriately
restricted identities when callers must be unable to bypass restrictions. `WithReadOnly()` does not
grant permissions, change server configuration, or make a read-only transaction permanent.

SQLite's ordinary file attachments inherit the connection's native read-only open flags. DuckDB
attachments have their own access modes: explicitly use `(READ_ONLY)` when attaching another file
that must be protected. Do not assume the primary database's mode protects every external resource.

## Supported provider routes

| Database | Driver route | Mapping and actual guarantee |
|---|---|---|
| SQLite | ADO.NET / Microsoft.Data.Sqlite | `Mode=ReadOnly`; persistent DML and DDL against the primary database are rejected, including explicit transactions. Temporary objects follow SQLite rules. |
| DuckDB | ADO.NET / DuckDB.NET.Data | `access_mode=READ_ONLY`; rejects persistent writes to the primary database. Effective mode is checked after each native open, including direct `Connect().Open()` and reopen. |
| DuckDB | ODBC / DuckDB ODBC driver | `access_mode=READ_ONLY`, with the same effective-mode check. Requires the actual DuckDB ODBC driver and a version exposing `duckdb_databases().readonly`. |

SQLite is tested with Microsoft.Data.Sqlite 10.0.12; DuckDB.NET is tested with 1.5.6. Native package
and engine prerequisites still apply. DuckDB ODBC mapping is covered by driver-builder tests; no
DuckDB ODBC driver was installed on the Windows development host, so actual ODBC integration was
not run. OLE DB is Windows-only; no OLE DB route is currently advertised by this shared API.

The [SQLite provider documentation](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings),
[DuckDB.NET connection options](https://duckdb.net/docs/connection-string.html), and
[DuckDB ODBC configuration](https://duckdb.org/docs/stable/clients/odbc/configuration) describe these
native settings.

### Opening, pooling, and driver instance reuse

SQLite read-only and writable configurations use their own native pool keys. The mode is preserved
across pool checkout and reopening; unrelated writable connections retain their normal access.

DuckDB.NET can reuse an existing database instance even when a later connection asks for different
access options. Setting the keyword alone is therefore insufficient. DubUrl reads the primary
database's `readonly` value from `duckdb_databases()` on the native connection's open event. If the
mode is writable, cannot be determined, or verification fails, the connection is disposed and opening
fails. It is never handed back as successfully read-only. This guard also runs on direct native open
and reopen; it executes only a metadata query, never a write probe.

Close existing writable DuckDB instances for that file before opening it read-only. A verification
failure does not alter or dispose the separate writable connection. Command failures, cancellation,
and missing/unreadable files remain failures; `WithOptional(...)` cannot suppress them.

## Other databases and drivers reviewed

The following routes have **no shared mapping in this implementation**. This does not mean the
database cannot restrict a user to reading data. It means no equivalent native connection access
mode has been verified for the selected route under this method's contract.

| Database | Catalogued routes | Reason / alternative |
|---|---|---|
| SQL Server | SqlClient, ODBC, OLE DB | `Application Intent=ReadOnly` selects read-oriented routing; it does not prevent writes. Use database permissions for enforcement. |
| PostgreSQL, Timescale | Npgsql, psqlODBC | `default_transaction_read_only` is a changeable transaction default, not native database access mode. |
| CockroachDB | Npgsql | Read-only transaction defaults are not mapped as immutable database access mode. |
| MySQL, MariaDB | Native ADO.NET, ODBC; legacy MySQL OLE DB | Session/transaction read-only settings have different scope and can be overridden; do not start a hidden long-lived transaction. |
| Oracle | ODP.NET, OraOLEDB | Read-only transactions end at commit/rollback; no verified native file/connection access-mode mapping here. |
| Db2 | IBM .NET | IBM documents `ReadOnlyConnection` for CLI/ODBC/configuration files but explicitly lists its equivalent .NET connection-string keyword as unavailable. No machine-wide configuration is changed. |
| Firebird | Native ADO.NET | Read-only transaction options are distinct from a connection access mode. |
| CrateDB, QuestDB | Npgsql; QuestDB also psqlODBC | PostgreSQL protocol compatibility is not proof of an equivalent effective setting. No verified mapping. |
| SingleStore | SingleStoreConnector | MySQL protocol compatibility is not proof of an equivalent effective setting. No verified mapping. |
| Snowflake | Snowflake.Data | Roles/permissions are distinct from an established connection-mode mapping. |
| Teradata | Teradata.Client | No verified equivalent connection access-mode mapping. |
| Trino | NReco.PrestoAdo, Simba ODBC | Server access-control policies and transaction characteristics are not a shared client connection mode. |
| Apache Drill | ODBC | No verified equivalent connection access-mode mapping. |
| Access, Excel | ODBC, ACE OLE DB | Driver-specific `ReadOnly`/`Mode=Read` possibilities require verification of SQL-write enforcement and effective scope for the actual driver/version. They are not assumed from generic ODBC/OLE DB hints. |
| Text files | ODBC | No verified shared mapping. |

See [SQL Server application intent](https://learn.microsoft.com/en-us/sql/connect/ado-net/connection-options),
[PostgreSQL transaction defaults](https://www.postgresql.org/docs/current/sql-set-transaction.html),
[MySQL transaction characteristics](https://dev.mysql.com/doc/refman/8.4/en/set-transaction.html),
and [IBM's driver/.NET keyword distinction](https://www.ibm.com/docs/en/db2/11.5.x?topic=keywords-readonlyconnection).
New mappings must be verified for the actual driver and must not silently substitute a weaker hint.

## CLI

Require native read-only mode while checking a connection:

```sh
duburl connection check 'sqlite:///reporting.db' --read-only
duburl connection check --connection reporting --read-only
duburl connection check --connection-env REPORTING_DATABASE_URL --read-only
```

Specify exactly one URL, named connection, or environment selector. This check opens and disposes the
connection without testing writes. Its success message identifies the requested read-only mode;
driver errors do not print credentials or raw connection strings. Without `--read-only`, provider
defaults still apply, including creation of missing SQLite files when native defaults permit it.

Schema inspection also supports the same strict requirement:

```sh
duburl schema inspect --connection reporting --read-only --basic --output-format json
```

`--allow-partial` concerns incomplete metadata only; it cannot relax read-only access. Unsupported
read-only routes return invalid-input exit code 2. A failure to open or verify mode during
`connection check` returns provider-operation exit code 5; cancellation returns 130. Schema inspection
retains its existing inaccessible-metadata diagnostics for failures after opening has begun.

`--read-only` is not offered on schema deployment, which intentionally writes database objects, or
offline commands that do not open a database connection. There is no optional read-only CLI switch.
