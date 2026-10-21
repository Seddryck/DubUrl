---
title: Dependency injection
parent: Querying data
nav_order: 7
permalink: /docs/dependency-injection/
subtitle: Choose between direct construction and application-managed factories
---

## When DI is useful

Use `DubUrl.Extensions` when your application already uses dependency injection, such as an
ASP.NET Core application or a .NET worker service. Register DubUrl once, then inject its factories
into the services that need database access. This keeps factory setup, query logging and MicroOrm
configuration in application startup, and lets tests supply a controlled registry or replace application services.

For a small program or a single call, direct construction is sufficient:

```csharp
using DubUrl;

using var connection = new ConnectionUrl("pg://localhost/demo").Open();
```

**DI does not add a registry caching benefit.** Direct construction and default DI factories use
the same cached registry in Core. Choose DI for application composition and configuration, rather
than to avoid repeated provider discovery. Both approaches require the chosen provider implementation
and driver, supplied through NuGet dependencies or an installed connector.

## Register and inject a factory

For PostgreSQL in an ASP.NET Core application, add:

```shell
dotnet add package DubUrl.Extensions
dotnet add package DubUrl.Provider.PostgreSql
dotnet add package Npgsql
```

`DubUrl.Extensions` brings the base `DubUrl` package transitively. In `Program.cs`:

```csharp
using DubUrl.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDubUrl();
builder.Services.AddTransient<DatabaseHealthCheck>();

var app = builder.Build();
app.Run();
```

Inject `ConnectionUrlFactory` into your service:

```csharp
using DubUrl;

public sealed class DatabaseHealthCheck(ConnectionUrlFactory urls)
{
    public void Check(string databaseUrl)
    {
        using var connection = urls.Instantiate(databaseUrl).Open();
    }
}
```

The caller owns each connection and disposes it. Registering a singleton factory does not register
a shared database connection. Applications using DubUrl's query APIs can inject `IDatabaseUrlFactory`
instead; `WithMicroOrm()` configures that factory for the MicroOrm implementation.

## Registry and URL lifetimes

| Setup | Registry selection |
| --- | --- |
| `new ConnectionUrl(url)` | Captures the current cached registry when the URL is created |
| Default DI `ConnectionUrlFactory` | Singleton factory; captures the current cached registry for each new URL |
| `new ConnectionUrlFactory(registry)` | Keeps the supplied registry for every new URL |
| DI with a custom registry singleton or explicit provider modules | Keeps the explicitly configured registry |

Default discovery reads a small store revision marker on each lookup. It reuses the registry while
the revision is unchanged. Installing, replacing, updating or removing a connector through the store
invalidates the cache, including when another process makes the change. The next URL created through
the default factory uses the updated snapshot. Existing URLs keep their original snapshot, even when
they have not yet been parsed. Each URL resolves its own connection information; `Open()` creates a
new connection and does not refresh the URL's provider selection.

Resolving `ISchemeRegistry` directly from default DI returns the current snapshot. Holding that instance
keeps that snapshot; inject `ConnectionUrlFactory` when new URLs should observe connector changes.
The DI registry registration resolves on demand and does not maintain a second cache.

## Keep provider selection fixed

When your application should keep the same provider selection until restart, capture one registry:

```csharp
using DubUrl.Extensions.DependencyInjection;
using DubUrl.Mapping;

var registry = SchemeRegistryBuilder.GetDefault();
builder.Services.AddSingleton<ISchemeRegistry>(registry);
builder.Services.AddDubUrl();
```

The singleton can also be registered after `AddDubUrl()`. The explicit provider module overload
`AddDubUrl(module, additionalModules)` likewise builds and retains a fixed registry. This is useful
for controlled application configuration and tests. Updating the default cache does not replace
these explicitly supplied registries.

Call `SchemeRegistryBuilder.RefreshDefault()` after dynamically loading application providers,
changing driver factory registrations or editing installed files manually. Default DI factories
observe the refreshed cache for their next URL. See [connector discovery and refresh behavior]({{ '/docs/provider-packs/' | relative_url }})
and [explicit provider registration]({{ '/docs/provider-modules/' | relative_url }}) for details.
