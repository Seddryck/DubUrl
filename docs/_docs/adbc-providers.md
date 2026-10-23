---
title: ADBC providers
subtitle: Use Arrow Database Connectivity through DubUrl's ADO.NET APIs
tags: [connection, adbc, duckdb]
---

Install the optional integration package:

```bash
dotnet add package DubUrl.Adbc
dotnet add package DubUrl.Provider.DuckDb
```

Provide a native DuckDB library for your operating system and process architecture
that exports `duckdb_adbc_init`, then register its factory before creating a
`ConnectionUrl`, `DatabaseUrl`, or scheme registry. If a default registry was already
created, call `DubUrl.Mapping.DefaultRegistry.Refresh()` after replacing a factory:

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

This integration exposes ADO.NET rows through Apache's ADBC client. Direct Arrow
streams are outside this API. Other provider capabilities, including schema
metadata and transaction support, depend on the Apache client and selected driver.
Existing `duckdb://` and `odbc+duckdb://` mappings continue to use their own providers.
