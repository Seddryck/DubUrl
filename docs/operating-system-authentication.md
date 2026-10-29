---
title: "Operating-system identity"
permalink: /operating-system-authentication/
nav_exclude: true
---

# Operating-system identity

The database authenticates the current process's OS identity. No database password is supplied. This is SQL Server integrated authentication or PostgreSQL local peer authentication; it is not an Entra token or impersonation.

## Database and operating-system support

The operating system is the **client** running DubUrl, not the database server. **Yes** requires the native provider and the database setup described below. **Conditional** names an extra platform or driver requirement. **No** means this documented DubUrl method is unavailable; it does not claim that the database itself lacks that authentication mechanism. ODBC, OLE DB, ADOMD, and ADBC are outside these shared authentication methods; their driver-specific sign-in settings are separate.

| Database | Windows | Linux | macOS | CLI | Library | Requirements / reason |
|---|---|---|---|---|---|---|
| Apache Drill | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Azure Analysis Services | No | No | No | No | No | No shared operating-system identity selection for this database. |
| CockroachDB | No | No | No | No | No | No shared operating-system identity selection for this database. |
| CrateDB | No | No | No | No | No | No shared operating-system identity selection for this database. |
| DuckDB | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Firebird SQL | No | No | No | No | No | No shared operating-system identity selection for this database. |
| IBM Db2 | No | No | No | No | No | No shared operating-system identity selection for this database. |
| MariaDB | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Microsoft Access | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Microsoft Excel | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Microsoft SQL Server | Yes | Conditional | Conditional | URL | Yes | Windows identity; Kerberos setup on Linux/macOS. LocalDB: Windows only. |
| MySQL | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Oracle Database | No | No | No | No | No | No shared operating-system identity selection for this database. |
| PostgreSQL | No | Yes | Yes | URL | Yes | Local Unix socket with server-enforced peer authentication; no TCP. |
| Power BI Desktop | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Power BI Premium | No | No | No | No | No | No shared operating-system identity selection for this database. |
| QuestDB | No | No | No | No | No | No shared operating-system identity selection for this database. |
| SingleStore | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Snowflake | No | No | No | No | No | No shared operating-system identity selection for this database. |
| SQL Server Analysis Services — Multidimensional | No | No | No | No | No | No shared operating-system identity selection for this database. |
| SQL Server Analysis Services — Tabular | No | No | No | No | No | No shared operating-system identity selection for this database. |
| SQLite | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Teradata | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Text files | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Timescale | No | No | No | No | No | No shared operating-system identity selection for this database. |
| Trino | No | No | No | No | No | No shared operating-system identity selection for this database. |

## SQL Server

On Windows, grant the Windows user or group access to SQL Server and the database. For remote integrated sign-in, the server and client must have the required domain/trust configuration. On Linux and macOS, provision Kerberos, the SQL Server service principal, and a valid ticket for the current identity. See [Microsoft's Kerberos setup guidance](https://github.com/microsoft/vscode-mssql/blob/main/extensions/mssql/KERBEROS_HELP.md).

Use a credential-free SQL Server URL. The native SQL Server mapping selects integrated authentication when no login/password is supplied:

```shell
duburl connection check 'mssql://sql.example.com/app' --engine
```

In the Library, select the method explicitly:

```csharp
var target = new ConnectionUrl("mssql://sql.example.com/app")
    .WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
await using var connection = await target.OpenAsync(cancellationToken);
```

LocalDB is Windows-only. An instance can be selected using an escaped Data Source option, such as `mssql://localhost/master?Data%20Source=%28localdb%29%5CMyInstance`.

## PostgreSQL

The client and PostgreSQL must run on the same Linux or macOS host and connect through a Unix-domain socket. Configure PostgreSQL's `pg_hba.conf` to require `peer` and, if needed, `pg_ident.conf` to map the OS user to the database role. The role needs database access. A passwordless `trust` rule is not peer authentication.

Select the socket directory and role in the connection URL:

```shell
duburl connection check 'postgres://alice@localhost:5432/app?Host=%2Fvar%2Frun%2Fpostgresql&Require%20Auth=None&SSL%20Mode=Disable' --engine
```

Replace the directory with the server's socket directory; on macOS it is often `/tmp`. The port selects the socket suffix. The CLI uses these native connection settings; it has no `--operating-system-identity` flag.

The Library uses the same socket URL with `ConnectionAuthentication.OperatingSystemIdentity()`. A URL username selects the database role, not another OS identity. The server decides whether the current OS user may use that role. The shared method does not support TCP, Windows peer authentication, or peer selection for other Npgsql-based databases.

See [PostgreSQL peer authentication](https://www.postgresql.org/docs/current/auth-peer.html) for server configuration.

[CLI authentication]({{ '/cli-authentication/' | relative_url }}) · [Library authentication]({{ '/authentication/' | relative_url }})
