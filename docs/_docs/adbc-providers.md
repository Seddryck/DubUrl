---
title: ADBC providers
subtitle: Use Arrow-native batches or ADO.NET rows through Arrow Database Connectivity
tags: [connection, adbc, duckdb]
---

Install the optional integration package:

```bash
dotnet add package DubUrl.Adbc
dotnet add package DubUrl.Provider.DuckDb
```

Provide a native DuckDB library for your operating system and process architecture
that exports `duckdb_adbc_init`, then register its factory before creating a
`ConnectionUrl`, `DatabaseUrl`, or scheme registry:

```csharp
using System.Data.Common;
using DubUrl;
using DubUrl.Adbc;

DbProviderFactories.RegisterFactory(
    "DubUrl.Provider.DuckDb.Adbc",
    AdbcProviderFactory.FromNative(@"C:\drivers\duckdb.dll", "duckdb_adbc_init"));

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
points are supplied through registration, not through URL options.

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
configuration; automatic driver discovery is tracked separately in issue #1656.

### Choosing an API

Use `ConnectionUrl` or `DatabaseUrl` for existing ADO.NET consumers, row-oriented
applications, or integrations expecting `IDbConnection`/`IDataReader`. Those
contracts remain unchanged. Use `AdbcConnectionUrl` for direct Arrow batch access;
its statements and streams use Apache's native APIs instead of an ADO.NET adapter.
Provider capabilities, including schema metadata and transactions, depend on the
Apache client and selected driver. Existing `duckdb://` and `odbc+duckdb://`
mappings continue to use their own providers.

The default catalog caches its selected factory. Register before constructing URLs;
call `DubUrl.Mapping.DefaultRegistry.Refresh()` after replacing an existing registration.
For an isolated root path or registry, use `ProviderCatalogBuilder.WithRootPath(...)`
and `AddApplicationProviders().Build()` and pass that registry to either URL API.
