---
title: Your first database connection
parent: Getting started
nav_order: 4

---

This example opens an existing PostgreSQL database and reads its version. You need a reachable PostgreSQL server and a URL stored in the `DUBURL_DATABASE_URL` environment variable.

## Install the packages

```shell
dotnet add package DubUrl
dotnet add package Npgsql
```

## Register the provider and open a connection

```csharp
using System.Data.Common;
using DubUrl;

DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
var url = Environment.GetEnvironmentVariable("DUBURL_DATABASE_URL")
    ?? throw new InvalidOperationException("Set DUBURL_DATABASE_URL first.");

using var connection = new ConnectionUrl(url).Open();
using var command = connection.CreateCommand();
command.CommandText = "SELECT version()";
Console.WriteLine(command.ExecuteScalar());
```

Use a URL such as `pgsql://{user}:{password}@{host}/{database}`, substituting your database's values. The `using` declarations dispose the command and connection when the scope ends.

## Next steps

- [Read values and rows with DatabaseUrl]({{ '/docs/first-query/' | relative_url }}).
- [Choose another database provider]({{ '/docs/choose-provider/' | relative_url }}).
- [Configure connection reuse]({{ '/docs/connection-source/' | relative_url }}).
