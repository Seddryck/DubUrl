---
title: Register provider modules
subtitle: Build a deterministic provider catalog without assembly scanning
tags: [installation, providers, trimming, aot]
---

Provider modules are the preferred extension point for adding connection URL schemes. A module registers
the provider factory, aliases, dialect, parameter convention, connection-string compiler, and optional
capability objects together. Registration order is deterministic and a duplicate alias throws a
`ProviderRegistrationException` naming both conflicting registrations.

```csharp
public sealed class AcmeProviderModule : IDubUrlProviderModule
{
    public void Register(IProviderCatalogBuilder catalog)
    {
        catalog.AddProvider(new ProviderRegistration(
            name: "Acme Database",
            aliases: ["acme", "acmedb"],
            providerInvariantName: "Acme.Data",
            providerFactory: () => AcmeProviderFactory.Instance,
            dialect: AcmeDialect.Instance,
            parametrizerFactory: () => new NamedParametrizer(),
            compile: context => new AcmeMapper(
                context.ConnectionStringBuilder,
                context.Dialect,
                context.Parametrizer)));
    }
}

var registry = new ProviderCatalogBuilder()
    .WithRootPath(AppContext.BaseDirectory)
    .AddModule(new AcmeProviderModule())
    .Build();
```

The `compile` delegate is an ordinary factory. DubUrl does not infer constructor shapes or activate the
mapper through reflection. Add optional capability objects with
`registration.WithCapabilities(...)` and retrieve them with `TryGetCapability<TCapability>`.

## Author a driver binding without changing Core

For a new driver binding to an existing database, reference `DubUrl.Core`, the relevant
`DubUrl.Provider.*` family package, and the concrete driver. Declare the factory, family mapper
and dialect, aliases, invariant name, and parametrizer in the project and import
`Providers/Build/ProviderBinding.props`. It generates the binding's `ProviderModule` and metadata;
Core's `AssemblyMetadataProviderModule` performs registration for every standard binding.
Reuse the family's rewriter unless the driver needs different keywords or URL rules. The project
in `Providers/PostgreSql/DubUrl.Provider.PostgreSql.Npgsql` is an example of this declarative configuration.
An alternative connector can supply its own factory and aliases while reusing `PostgresqlMapper`.
Registering two connectors for the same alias produces a `ProviderRegistrationException`; choose
the connector explicitly rather than relying on registration order.

For a new database, create a provider implementation project referencing `DubUrl.Core`. Own its
database identity, mapper, rewriter, dialect, and any specialized SQL or driver discovery behavior
there. Reuse the public `BaseMapper`, `ConnectionStringRewriter`, token mappers, and ANSI SQL
components where appropriate. Keep shared family behavior in provider projects and concrete driver
dependencies in driver binding projects named `DubUrl.Provider.<Database>.<Driver>`.
Core must never reference either implementation project.

The buildable `samples/DubUrl.Provider.Sample` project demonstrates a new database using only
public Core APIs. Its `SampleProviderModule` receives a factory delegate, constructs a `SampleDialect`
directly, and registers `SampleMapper` with `NamedParametrizer`. Its rewriter translates the host,
port, database path, and query options. The sample uses `StraightSpecificator` for an unrestricted
`DbConnectionStringBuilder`; production drivers with a fixed keyword set can use `Specificator`
to validate keywords through their own builder. Add provider-specific validation for authentication,
default values, path depth, and accepted options as the real driver requires.

Compose the sample with an application's concrete driver factory:

```csharp
using DubUrl;
using DubUrl.Provider.Sample;
using DubUrl.Mapping;
using System.Data.Common;

static ConnectionUrl CreateSampleUrl(DbProviderFactory driverFactory)
{
    var registry = new ProviderCatalogBuilder()
        .AddModule(new SampleProviderModule(() => driverFactory))
        .Build();
    return new ConnectionUrl("sample://localhost/demo", registry);
}
```

The factory delegate supplies the driver directly, so this path does not require global
`DbProviderFactories.RegisterFactory` calls. If the module intentionally looks up a globally registered
factory, register it under the module's invariant name before building the catalog. A factory delegate
returning null skips that provider; resolving its scheme then reports `SchemeNotFound`. Deployment
checks for native libraries or installed ODBC/OLE DB drivers remain connector responsibilities.

Select `NamedParametrizer`, `PositionalParametrizer`, or a specialized implementation according to the
driver's parameter binding rules. These affect parameters added to executed commands independently
of SQL identifier and literal rendering. `DubUrl.Provider.Testing` executes a parameterized query
through a deterministic fake driver and tests an alternative PostgreSQL connector in a consumer
that does not reference the compatibility aggregate or unrelated providers.

Applications using explicit modules install only the selected packages and pass their built registry
to `ConnectionUrl` or `ConnectionUrlFactory`. Existing applications can continue using the `DubUrl`
compatibility aggregate, which supplies the built-in catalog. Adding an independently registered
connector does not require editing that built-in catalog. Including it in the official distribution
does require the applicable `connectors/connector-matrix.json`, generated composition, catalog,
package/dependency, and documentation inventory updates. Adding it to the compatibility aggregate
is a separate composition decision. None of these inventories belong in the provider-neutral Core.

## ODBC locator options and factories

Register every enum that can appear as an ODBC locator option:

```csharp
catalog.AddOdbcLocatorOption<AcmeEncoding>();
```

This list is passed to the provider compiler as `ProviderCompilationContext.OdbcLocatorOptions`; DubUrl
does not scan `AppDomain` assemblies for option enums. ODBC locator factories can also be registered
without constructor reflection:

```csharp
driverLocators.AddDriver("acme", options =>
    new AcmeDriverLocator((AcmeEncoding)options[typeof(AcmeEncoding)]));
```

The older `AddDriver(string, Type)` overload remains available as a reflection-based compatibility path.

## Compatibility discovery and trimming

`AttributeProviderModule` preserves the former attribute and exported-type discovery model for migrations:

```csharp
var registry = new ProviderCatalogBuilder()
    .AddModule(new AttributeProviderModule(typeof(LegacyMapper).Assembly))
    .Build();
```

That adapter scans assemblies and reflects over mapper constructors. It is annotated with
`RequiresUnreferencedCode` and is not supported for trimmed or Native AOT applications.

`BuiltInProviderModule` uses explicit provider, alias, mapper, ODBC driver, and ODBC option registrations.
Its current dialect composition still reads dialect component attributes, so it is also annotated with
`RequiresUnreferencedCode`. A fully trim-safe application should use its own explicit module with
preconstructed `IDialect` instances and direct mapper/driver factories. This limitation is intentional and
tested so trimming warnings are surfaced rather than silently producing an incomplete catalog.
