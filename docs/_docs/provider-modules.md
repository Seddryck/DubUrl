---
title: Register provider modules
subtitle: Build a deterministic provider catalog without assembly scanning
tags: [installation, providers, trimming, aot]
---

Provider modules are the preferred extension point for adding connection URL schemes. A module registers
the provider factory, aliases, dialect, parameter convention, connection-string compiler, and optional
capability objects together. Registration order is deterministic and a duplicate alias throws a
`ProviderRegistrationException` naming both conflicting registrations.

## Discover installed connectors automatically

With `DubUrl.Core` and an installed PostgreSQL connector,
`new ConnectionUrl("pg://localhost/demo")` discovers its module automatically.

Discovery reads active, compatible, integrity-verified packs from the per-user connector store:
`%LOCALAPPDATA%/DubUrl/providers` on Windows, `$XDG_DATA_HOME/duburl/providers` on Unix when set,
otherwise `~/.local/share/duburl/providers`. Set `DUBURL_PROVIDER_STORE` to use a different store.
Discovery does not install or download connectors and does not create a missing store.

Installed connectors take precedence over the `DubUrl` aggregate's bundled registrations for matching
aliases. Remaining bundled aliases remain available. Conflicting installed aliases throw
`ProviderRegistrationException`; an active module that cannot load reports `ProviderPackException`
with code `module-load-failed`. Inactive, incompatible, incomplete, and corrupt packs are not loaded.
Default dialect discovery uses the same provider selection.

Each default construction reads the current installed state and retains its registry snapshot, so
installation or removal affects subsequent defaults without changing existing URLs. For repeated
construction with a fixed selection, create a `ConnectionUrlFactory` from one registry. Passing an
explicit registry bypasses discovery entirely and remains the deterministic option for applications
that manage their own dependencies, tests, or trimmed/AOT deployment.

## Register providers explicitly

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
Reuse the family's rewriter unless the driver needs different keywords or URL rules. Standard bindings
are declared in `connectors/connector-matrix.json` and built by
`Providers/Build/ProviderBinding.Build.csproj`. The build script passes each row's settings to that
same project, including PostgreSQL/Npgsql. Its default settings build PostgreSQL/Npgsql for local
solution builds and focused consumer tests; no separate Npgsql project is required. Driver bindings
are distributed as connector packs, and this build project is not published as a NuGet package.
An alternative connector can supply its own factory and aliases while reusing `PostgresqlMapper`.
Registering two connectors for the same alias produces a `ProviderRegistrationException`; choose
the connector explicitly rather than relying on registration order.

For a new database, create a provider implementation project referencing `DubUrl.Core`. Own its
database identity, mapper, rewriter, dialect, and any specialized SQL or driver discovery behavior
there. Reuse the public `BaseMapper`, `ConnectionStringRewriter`, token mappers, and ANSI SQL
components where appropriate. Keep shared family behavior in provider projects and concrete driver
dependencies in driver binding projects named `DubUrl.Provider.<Database>.<Driver>`.
Core must never reference either implementation project.

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
compatibility aggregate, which reads an embedded connector catalog. Adding an independently registered
connector does not require editing a built-in provider list. Including it in the official distribution
requires updating `connectors/connector-matrix.json`, the applicable package/dependencies, and documentation.
The shared generator produces its catalog and local definition; the aggregate automatically includes its
database name and schemes in its metadata view. Adding executable registrations to the compatibility aggregate
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
