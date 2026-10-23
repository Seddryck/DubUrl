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

## Custom mappings and dialects

For ASP.NET Core applications and other hosts using dependency injection, see
[when to use DI and how to inject a factory]({{ '/docs/dependency-injection/' | relative_url }}).
DI supports application composition and configuration; direct construction already shares Core's
registry cache.

The constructor `ConnectionUrl(string url, ISchemeRegistry registry)` accepts a built registry, not a builder. Create fixed registries with `DubUrl.Mapping.ProviderCatalogBuilder`; see the [OLE DB guide]({{ '/docs/oledb-provider-locators/' | relative_url }}) for extension-assembly registration.

`Dialect`, `Connectivity`, and `Parametrizer` expose the metadata resolved for the URL. For connection leases and reusable sources, see the [connection source API]({{ '/docs/connection-source-reference/' | relative_url }}).
