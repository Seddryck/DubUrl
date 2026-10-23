---
title: ADBC providers
subtitle: Use Arrow-native batches or ADO.NET rows through Arrow Database Connectivity
tags: [connection, adbc, duckdb]
---

Install the optional integration package:

```bash
dotnet add package DubUrl.Provider.DuckDb.Adbc
```

Install a native DuckDB library for your operating system and process architecture
and make it discoverable through an ADBC driver manifest, the ADBC registry on
Windows, or `ADBC_DRIVER_PATH`. The package includes the DuckDB family mapping and
reusable `DubUrl.Adbc` adapter. The default catalog discovers its singleton factory
automatically from application dependencies; native loading waits until Open.
Explicit registration is also supported, before creating a URL or registry:

```csharp
using System.Data.Common;
using DubUrl;
using DubUrl.Adbc;
using DubUrl.Provider.DuckDb.Adbc;

DbProviderFactories.RegisterFactory(
    "DubUrl.Provider.DuckDb.Adbc",
    DuckdbAdbcFactory.Instance);

using var connection = new ConnectionUrl("adbc+duckdb:///customer.db").Open();
using var command = connection.CreateCommand();
command.CommandText = "SELECT * FROM Customer";
using var reader = command.ExecuteReader();
while (reader.Read())
{
    // Consume ordinary ADO.NET rows.
}
```

Existing query methods work with the same URL:

```csharp
var database = new DatabaseUrl("adbc+duckdb:///customer.db");
var count = database.ReadScalar<long>("SELECT COUNT(*) FROM Customer");
var countAsync = await database.ReadScalarAsync<long>("SELECT COUNT(*) FROM Customer");
```

DuckDB is the first supported ADBC URL mapping. `adbc+duck` is also accepted,
as is the reversed order `duckdb+adbc`; schemes are case insensitive.
`adbc+duckdb://` and `adbc+duckdb://memory` open an in-memory database.
File paths follow the existing DuckDB URL and configured root-path rules.
The resolved connection string uses ADBC's `path` option; query options such as
`?access_mode=read_only` are passed as database options. Driver paths and entry
points come from discovery or explicit registration, not from URL options.

### Automatic driver discovery

`DuckdbAdbcFactory.Instance` performs discovery when a connection or Arrow-native
session opens. Registering the singleton, constructing URLs, and calling
`ConnectionUrl.Connect()` do not load a driver. Each open receives its own driver
with the same ownership and cleanup behavior as an explicitly configured factory.

Discovery uses the standard ADBC locations in this order:

1. Directories in `ADBC_DRIVER_PATH`, in list order (`;` on Windows, `:` on Unix).
   Use fully qualified directories; empty and relative entries are ignored.
2. User configuration. On Windows, the read-only lookup checks
   `HKEY_CURRENT_USER\SOFTWARE\ADBC\Drivers\duckdb`, then
   `%LOCALAPPDATA%\ADBC\Drivers`. On Linux it checks
   `$XDG_CONFIG_HOME/adbc/drivers`, falling back to `~/.config/adbc/drivers` when
   `XDG_CONFIG_HOME` is unset or not absolute. On macOS it checks
   `~/Library/Application Support/ADBC/Drivers`.
3. System configuration. On Windows it checks
   `HKEY_LOCAL_MACHINE\SOFTWARE\ADBC\Drivers\duckdb` in the current process's
   registry view. On Linux it checks `/etc/adbc/drivers`; on macOS,
   `/Library/Application Support/ADBC/Drivers`.
4. Native-library directories in `PATH` on Windows, `LD_LIBRARY_PATH` on Linux,
   or `DYLD_LIBRARY_PATH` on macOS, followed by the .NET native loader's lookup
   for application native assets and operating-system library locations. Empty
   and relative path entries are ignored.

Within a directory, `duckdb.toml` takes precedence over a bare DuckDB library
(`duckdb.dll` on Windows, `libduckdb.so` on Linux, `libduckdb.dylib` on macOS).
Manifest loading uses Apache's C# ADBC driver manager. DubUrl supplies the standard
lookup steps that its current C# implementation does not cover, including Windows
registry entries and Linux's `XDG_CONFIG_HOME`.

An installer can provide `duckdb.toml` in one of these directories. For example:

```toml
manifest_version = 1

[Driver]
shared = 'C:\drivers\duckdb.dll'
entrypoint = 'duckdb_adbc_init'
```

Use your platform's library path in `shared`. Apache's manifest loader also
supports platform-specific `[Driver.shared]` entries and manifest-relative paths.
The manifest's `entrypoint`, or the Windows registry's `entrypoint` value, takes
precedence over DuckDB's known default `duckdb_adbc_init`. For a manifest without
an entry point or a bare library, DubUrl supplies that default. Registry entries
use the `driver` value for the fully qualified native library path.

A selected manifest or library that fails to load causes an error; discovery does
not silently choose a lower-priority installation. Errors name the mechanisms
searched, describe configuration steps, and omit raw manifest and loader messages
that might contain sensitive values. ODBC registration and unixODBC are separate
from ADBC discovery. See the [ADBC discovery specification](https://arrow.apache.org/adbc/24/format/driver_manifests.html).

### Explicit driver override

To select a specific library or initialization symbol, register an explicit
factory instead. This bypasses discovery and preserves the existing API:

```csharp
DbProviderFactories.RegisterFactory(
    "DubUrl.Provider.DuckDb.Adbc",
    AdbcProviderFactory.FromNative(@"C:\drivers\duckdb.dll", "duckdb_adbc_init"));
```

The factory loads a driver when a connection opens. Closing or disposing the
connection releases its native connection, database, and driver. Dispose readers
and commands before their connection. `CommandBehavior.CloseConnection` also
closes the owning connection. `DatabaseUrl` manages these resources for each query.

For a managed driver, construct `AdbcProviderFactory` with a delegate that creates
a fresh `Apache.Arrow.Adbc.AdbcDriver` for each opened connection. The connection
owns and disposes that driver; do not return a shared driver instance.

### Arrow-native batch access

Choose `AdbcConnectionUrl` when downstream code processes Arrow columns, buffers,
or batches. It uses the same driver registration, URL mappings, database options,
and configured root paths as the ADO.NET integration. It returns an owning
`AdbcSession` whose `Connection` is an `Apache.Arrow.Adbc.AdbcConnection`.

```csharp
using Apache.Arrow;
using DubUrl.Adbc;
using DubUrl.Provider.DuckDb.Adbc;

using var session = new AdbcConnectionUrl("adbc+duckdb:///customer.db").Open();
using var statement = session.Connection.CreateStatement();
statement.SqlQuery = "SELECT id, name FROM Customer";

var result = statement.ExecuteQuery();
using var stream = result.Stream!;
while (await stream.ReadNextRecordBatchAsync(cancellationToken) is { } batch)
{
    using (batch)
    {
        var ids = (Int64Array)batch.Column(0); // Assumes id is BIGINT.
        var names = (StringArray)batch.Column(1);
        // Process the typed columns or pass the batch to an Arrow consumer.
    }
}
```

`cancellationToken` is a caller-supplied `CancellationToken`. Reads are incremental:
DubUrl does not collect the result or convert it into row objects. The stream's
`Schema` and each `RecordBatch` are Apache Arrow objects returned by the driver.
Empty results still expose their schema; a driver may emit zero-length batches.
Arrow validity bitmaps preserve null values. Zero-copy behavior depends on the
driver and downstream operations; exposing Arrow batches alone does not guarantee it.

The caller owns each statement, result stream, and fetched batch. Dispose each
batch before fetching more when it is no longer needed. Keep the statement and
session alive until consumption finishes. `session.Connection` is borrowed;
do not dispose it separately. Dispose in this order: batches, stream, statement,
then session. Session disposal releases the connection, database, and driver, and
is idempotent. Sessions are not thread safe.

Use `using` scopes as above so query failures, batch-read failures, cancellation,
and an early `break` release all acquired resources. Failed opens clean up the
driver and any database already created. Cancellation is passed directly to the
Apache stream; how promptly it interrupts a read depends on the driver. Opening
a session with `Open()` is synchronous. Disposing a session does not automatically
dispose caller-owned statements, streams, or batches, so do not dispose it early.

For a custom registry, pass it to `new AdbcConnectionUrl(url, registry)`; this
uses the same `ISchemeRegistry` contract as `ConnectionUrl`. Its resolved factory
must be an `AdbcProviderFactory`. Both API paths share the factory's driver
configuration, including `DuckdbAdbcFactory.Instance` discovery or an explicit
`AdbcProviderFactory.FromNative(...)` override.

### Choosing an API

Use `ConnectionUrl` or `DatabaseUrl` for existing ADO.NET consumers, row-oriented
applications, or integrations expecting `IDbConnection`/`IDataReader`. Those
contracts remain unchanged. Use `AdbcConnectionUrl` for direct Arrow batch access;
its statements and streams use Apache's native APIs instead of an ADO.NET adapter.
Provider capabilities, including schema metadata and transactions, depend on the
Apache client and selected driver. Existing `duckdb://` and `odbc+duckdb://`
mappings continue to use their own providers.

The `duckdb-adbc` connector archive exposes the same mapping and singleton factory.
It contains managed dependencies only; native DuckDB installation remains a prerequisite.
The factory invariant and assembly are both `DubUrl.Provider.DuckDb.Adbc`. Explicit
registrations take precedence over automatic discovery. Call
`DubUrl.Mapping.DefaultRegistry.Refresh()` after changing a cached factory registration.

Arrow-native applications must use the same `DubUrl.Adbc` and Apache Arrow API
assembly versions as their installed connector. The adapter declares these public
API dependencies to the connector loader so sessions, connections, streams, and
batches retain their host type identities. Driver implementation dependencies
remain isolated; mismatched API versions are not silently substituted.
