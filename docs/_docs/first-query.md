---
title: Execute your first query
parent: Querying data
nav_order: 1

---

DatabaseUrl opens a connection when a query needs one and releases it when that operation finishes. Before using these examples, [install and register your provider]({{ '/docs/installation-ado-net-provider/' | relative_url }}), and set `url` to your connection URL.

## Read a value

```csharp
await using var database = new DatabaseUrl(url);

var count = await database.ReadScalarAsync(
    "SELECT COUNT(*) FROM Customer");

Console.WriteLine(count);
```

Keep the database object for the queries you want to run, and dispose it when finished. `await using` handles disposal at the end of the surrounding scope. For synchronous code:

```csharp
using var database = new DatabaseUrl(url);
var count = database.ReadScalar("SELECT COUNT(*) FROM Customer");
```

You can also use typed overloads, such as `ReadScalar<long>`. Choose a type that matches your provider's SQL result: some providers return `COUNT(*)` as a 32-bit integer and others as a 64-bit integer.

## Choose how connections are reused

The URL-based constructor uses per-operation `ProviderManaged` ownership. To use `Auto` selection or retain a connection between calls, [create and reuse a connection source]({{ '/docs/connection-source/' | relative_url }}). That guide covers lifetime, pooling, and transactions.
## Next steps

- [Read scalar values]({{ '/docs/read-scalars/' | relative_url }}) for NULL handling.
- [Read one or many rows]({{ '/docs/read-rows/' | relative_url }}) for result cardinality and streaming.
- [DatabaseUrl API]({{ '/docs/class-databaseUrl/' | relative_url }}) for the method selection table.
