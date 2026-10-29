# Connection checks

`duburl connection check <url>` opens and disposes one connection. Alternatively use
`--connection development` (`ConnectionStrings:development` configuration) or
`--connection-env DATABASE_URL`. Exactly one selector is required.

Add `--os-identity` to use the current process identity or ticket cache through
the [shared Library strategy and supported driver mappings](operating-system-authentication.md).
For example: `duburl connection check 'mssql://sql.example.com/app' --os-identity --engine`.

`--engine` executes `SELECT 1`; Oracle uses `SELECT 1 FROM DUAL`, Db2 uses
`SELECT 1 FROM SYSIBM.SYSDUMMY1`, and Firebird uses `SELECT 1 FROM RDB$DATABASE`.
`--table public.customers` instead executes exactly
`SELECT 1 FROM <quoted-name> WHERE 1=0`. These options are mutually exclusive.
A successful table check establishes only that this statement was accepted. It
does not establish readable rows/columns, a storage scan, or deployment rights.
No catalog, metadata, permission, alternate query, retry or second identity is used.

Identifiers accept dot-separated simple names and native quoted components.
PostgreSQL/Timescale/Cockroach/Crate unquoted names fold to lower case;
Oracle/Snowflake/Db2/Firebird fold to upper case. Quoted names preserve case.
SQL Server accepts up to catalog.schema.table; Snowflake and Trino accept three
components; other families accept two. Unqualified names use the connection's
native default catalog/schema. Expressions and statement separators are rejected.
MySQL/MariaDB/SingleStore use backticks; SQL Server/Access/Excel/text use brackets;
other families use double quotes. Native case sensitivity still depends on server
configuration and filesystem rules. Use explicit quoting for case-sensitive names.

`--timeout 15` sets a positive overall budget for opening and the query. Explicit
prompt/store acquisition prepares a closed connection before this budget starts.
Native token/OAuth acquisition during opening remains inside the opening budget. The
remaining budget becomes the command timeout and a linked cancellation token is
passed to asynchronous provider operations. There are no abandoned background
attempts. Native providers may implement cancellation cooperatively or ignore it;
the command awaits them before disposal and reports budget expiration afterward.
SQLite native busy handling may block until its provider timeout. User cancellation
returns 130; database/timeout failures return 5, input/unsupported mode errors 2,
unexpected internal errors 1. Existing schema codes 3/4 are unchanged.

SQLite file connections use `Mode=ReadOnly`; native DuckDB uses
`Access_Mode=Read_Only`, avoiding creation of missing databases. In-memory databases
remain supported. Access/Excel/text and ODBC/OLE DB file checks are rejected until their drivers supply equivalent
existing-file semantics before being used for connection checking.

`--output-format json` emits one safe stdout envelope with command, success, mode,
provider, elapsedMilliseconds and diagnostics. Human successes use stdout; failures
use stderr. URLs, connection strings and raw provider messages are never reported.

## Provider evidence and limitations

Providers are optional CLI connectors; install the appropriate approved connector
before use. `capabilities --json` reports installed schemes and connection modes.
Nonrelational/ADOMD unknown SQL families reject SQL modes. There is no live provider
verification implied by code mappings or documentation. Unit diagnostics currently
prefer ambiguity for recognized errors until exact query-path evidence exists.

| Family / native provider | Recognized table diagnostics | Classification / evidence |
|---|---|---|
| SQLite / Microsoft.Data.Sqlite 10.0.3 | specific `no such table` under error 1 | absent for local tables; empty/nonempty/read-only file checks tested locally |
| SQL Server / Microsoft.Data.SqlClient | 208, 229 | ambiguous; live permission ordering not verified |
| PostgreSQL, Timescale, Cockroach, QuestDB, Crate / Npgsql | 42P01, 42501, 3F000 | ambiguous separately per family; protocol does not prove identical semantics |
| MySQL, MariaDB / MySqlConnector or MySql.Data | 1146, 1142, 1044 | ambiguous; authorization may precede existence |
| SingleStore / SingleStoreConnector | 1146, 1142 | ambiguous; not inferred from MySQL evidence |
| Oracle / Oracle.ManagedDataAccess | 942, 1031 | ambiguous |
| Snowflake / Snowflake.Data | 2003, 42S02 | ambiguous |
| Db2 / IBM.Data.Db2 | -204, -551, 42704, 42501 | ambiguous |
| Firebird / FirebirdSql.Data.FirebirdClient | full Errors collection, 335544580, 335544352 | ambiguous |
| Teradata / Teradata.Client.Provider | 3807, 3523 | ambiguous |
| Access, Excel, text, Trino, Drill / ODBC or OLE DB | 42S02, 42501, 3078, 3112 | ambiguous, driver-specific |
| DuckDB / native | no stable mapped diagnostic yet | executionFailed; no normal per-table grant model |

Unknown errors remain executionFailed, including dependency/view errors where no
target-object claim is justified. No adapter presently claims definite denied
existence from an unverified permission error. SQLSTATE/native codes are sanitized
and preserved when available. External provider integration cases (restricted
principals, missing targets under denied schemas, hidden objects, views and linked
objects) require provisioned endpoints and were not run locally. They remain
required evidence before refining these conservative mappings.

Focused validation: `dotnet test DubUrl.Cli.Testing -f net10.0 --filter FullyQualifiedName~ConnectionCheck`.
