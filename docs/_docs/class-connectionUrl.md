---
title: ConnectionUrl API
parent: Reference
nav_order: 4

permalink: /docs/class-connectionUrl/
subtitle: Instantiate and manipulate the class ConnectionUrl
---

`ConnectionUrl` resolves a URL using a scheme registry. Install and register the selected provider first; see [provider setup]({{ '/docs/installation-ado-net-provider/' | relative_url }}).

## Parse a URL

`Parse()` returns the provider-specific connection string. Resolution requires the provider factory to be registered even when no connection is opened. This example uses the full `Microsoft.Data.Sqlite` package:

```csharp
using System.Data.Common;
using DubUrl;
using Microsoft.Data.Sqlite;

DbProviderFactories.RegisterFactory(
    "Microsoft.Data.Sqlite", SqliteFactory.Instance);
var connectionUrl = new ConnectionUrl("sqlite:///:memory:");
Console.WriteLine(connectionUrl.Parse());
```

Do not print connection strings containing real credentials in application logs.

## Connect and open

Both methods return `IDbConnection`. `Connect()` creates a closed connection; `Open()` also opens it. The caller owns and must dispose the returned connection.

```csharp
using var closed = connectionUrl.Connect();
using var opened = connectionUrl.Open();
using var command = opened.CreateCommand();
command.CommandText = "SELECT 1";
Console.WriteLine(command.ExecuteScalar());
```

The default registry resolves factories that normally produce `DbConnection` instances, but the public return contract is `IDbConnection`. Use an explicit cast/type check before depending on provider-specific or asynchronous `DbConnection` members.

## Execute a batch

`CreateBatch()` returns the provider's ADO.NET `DbBatch` on a new open connection. It throws
`NotSupportedException` before opening the connection if the provider lacks ADO.NET batching.
Dispose the batch first, then its connection.

```csharp
var batch = connectionUrl.CreateBatch();
await using var batchConnection = batch.Connection!;
await using (batch)
{
    var first = batch.CreateBatchCommand();
    first.CommandText = "SELECT 1";
    batch.BatchCommands.Add(first);

    var second = batch.CreateBatchCommand();
    second.CommandText = "SELECT 2";
    batch.BatchCommands.Add(second);

    await using var reader = await batch.ExecuteReaderAsync();
    do
    {
        while (await reader.ReadAsync())
            Console.WriteLine(reader.GetInt32(0));
    }
    while (await reader.NextResultAsync());
}
```

Add one batch command for each SQL command you want to send. For SQL Server scripts, split at
`GO` separators before adding the commands, or use [ExecuteScriptAsync](#execute-a-sql-server-script) for a script that needs no result sets. Error handling and transaction behavior follow the provider.

## Execute a SQL Server script

Use `CanExecuteScript` to discover support without opening a connection. It reports whether
the registry handles the URL as a SQL Server route. Other routes throw `NotSupportedException`
from `ExecuteScriptAsync`.

```csharp
var sqlServer = new ConnectionUrl("mssql://localhost/demo");
if (sqlServer.CanExecuteScript)
{
    await sqlServer.ExecuteScriptAsync("""
        CREATE TABLE #items (id int);
        GO
        INSERT INTO #items VALUES (1);
        GO 2
        """, cancellationToken);
}
```

Put `GO` on its own line, optionally followed by a positive repeat count and comments.
Batches run in order on one connection, so the temporary table in this example survives
the first separator. Execution stops when a command fails. Transaction control belongs to
the script, and earlier changes may already be committed. The API is for executing scripts
without returning result sets. It supports `GO` as the client directive.

## Required and optional defaults

`WithReadOnly()` requires verified native read-only access to the primary database. It is strict and
is not exposed inside the optional builder. See [read-only connections]({{ '/docs/read-only-connections/' | relative_url }})
for supported routes, write-mode precedence, instance-reuse safeguards, and scope limits.

`WithApplicationName(...)`, `WithPooling(...)`, `WithConnectionTimeout(...)`, and
`WithCommandTimeout(...)` return a new configured URL and reject unsupported settings by default.
Use `WithOptional(options => ...)` to skip unsupported settings from that restricted set. Invalid
configuration and provider failures still throw. See [required and optional configuration]({{ '/docs/optional-configuration/' | relative_url }})
for examples, precedence, diagnostics, and the exact provider support matrix.

## Custom mappings and dialects

For ASP.NET Core applications and other hosts using dependency injection, see
[when to use DI and how to inject a factory]({{ '/docs/dependency-injection/' | relative_url }}).
DI supports application composition and configuration; direct construction already shares Core's
registry cache.

The constructor `ConnectionUrl(string url, ISchemeRegistry registry)` accepts a built registry, not a builder. Create fixed registries with `DubUrl.Mapping.ProviderCatalogBuilder`; see the [OLE DB guide]({{ '/docs/oledb-provider-locators/' | relative_url }}) for extension-assembly registration.

`Dialect`, `Connectivity`, and `Parametrizer` expose the metadata resolved for the URL. For connection leases and reusable sources, see the [connection source API]({{ '/docs/connection-source-reference/' | relative_url }}).
