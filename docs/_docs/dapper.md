---
title: Use Dapper
parent: Querying data
nav_order: 5

subtitle: Integration of DubUrl with the micro-orm Dapper
---

Use DubUrl to open an `IDbConnection`, then call Dapper's mapping extensions on that connection. The example uses SQLite and an existing Customer table with CustomerId and FullName columns. Set `DUBURL_DATABASE_URL` to a file URL such as `sqlite:///C:/data/customers.db` on Windows or `sqlite:////tmp/customers.db` on Linux/macOS.

## Install

```shell
dotnet add package DubUrl
dotnet add package Microsoft.Data.Sqlite
dotnet add package Dapper
```

## Read customers

```csharp
using System.Data.Common;
using DubUrl;
using Dapper;
using Microsoft.Data.Sqlite;

DbProviderFactories.RegisterFactory(
    "Microsoft.Data.Sqlite", SqliteFactory.Instance);
var url = Environment.GetEnvironmentVariable("DUBURL_DATABASE_URL")
    ?? throw new InvalidOperationException("Set DUBURL_DATABASE_URL first.");
using var connection = new ConnectionUrl(url).Open();
var customers = connection.Query<Customer>(
    "SELECT CustomerId, FullName FROM Customer WHERE CustomerId = @id",
    new { id = 1 }).ToList();
foreach (var customer in customers)
    Console.WriteLine(customer.FullName);

public sealed class Customer
{
    public long CustomerId { get; set; }
    public string FullName { get; set; } = "";
}
```

The `using` declaration disposes the connection after the results have been materialized. Choose property types matching the provider's returned values.

## Connections and repositories

Use this same sequence inside a repository method: open a connection, run and materialize the query, then dispose the connection. Register the provider once during application startup. Keep dialect-specific SQL in your repository layer, or use DubUrl's command providers and template APIs when appropriate.

For explicit connection reuse or a shared transaction, [acquire a connection lease]({{ '/docs/connection-source/' | relative_url }}) and use its connection inside the lease scope. Do not dispose the borrowed connection itself.
