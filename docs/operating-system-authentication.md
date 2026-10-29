---
title: "Operating-system identity"
permalink: /operating-system-authentication/
nav_exclude: true
---

# Operating-system identity

The database authenticates the current process's OS identity. No database password is supplied. This is SQL Server integrated authentication or PostgreSQL local peer authentication; it is not an Entra token or impersonation.

## Database and operating-system support

{% assign supported = '<span class="auth-supported" role="img" aria-label="Supported" title="Supported">&#10004;</span>' %}
{% assign unsupported = '<span class="auth-unsupported" role="img" aria-label="Not supported" title="Not supported">&#10008;</span>' %}

The operating system is the **client** running DubUrl, not the database server. {{ supported }} means this authentication method is supported through the listed driver route; install the required provider or driver and satisfy the prerequisites in the notes. {{ unsupported }} means this documented method is unavailable for that route. Each database has a separate row for each catalogued native ADO.NET, ODBC or OLE DB route. Driver-specific sign-in options for other methods are separate from the shared APIs shown here.

| Database | Driver route | Windows | Linux | macOS | CLI | Library | Requirements / reason |
|---|---|---|---|---|---|---|---|
| Apache Drill | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| CockroachDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| CrateDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| DuckDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| DuckDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Firebird SQL | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| IBM Db2 | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| MariaDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| MariaDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Access | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Access | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Microsoft Excel | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft Excel | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Microsoft SQL Server | ADO.NET | {{ supported }} | {{ supported }} | {{ supported }} | {{ supported }} | {{ supported }} | Windows identity; Kerberos setup on Linux/macOS. LocalDB: Windows only. CLI: select this method through the connection URL; no dedicated sign-in option. |
| Microsoft SQL Server | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Microsoft SQL Server | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| MySQL | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| MySQL | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| MySQL | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| Oracle Database | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| Oracle Database | OLE DB | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no OLE DB adapter. Driver-specific authentication options are configured separately. |
| PostgreSQL | ADO.NET | {{ unsupported }} | {{ supported }} | {{ supported }} | {{ supported }} | {{ supported }} | Local Unix socket with server-enforced peer authentication; no TCP. CLI: select this method through the connection URL; no dedicated sign-in option. |
| PostgreSQL | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| QuestDB | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| QuestDB | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| SingleStore | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| Snowflake | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| SQLite | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| Teradata | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| Text files | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Timescale | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| Timescale | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |
| Trino | ADO.NET | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | No shared operating-system identity selection for this database. |
| Trino | ODBC | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | {{ unsupported }} | The shared API for this method has no ODBC adapter. Driver-specific authentication options are configured separately. |

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
