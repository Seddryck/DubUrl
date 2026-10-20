---
title: Register provider modules
subtitle: Build a deterministic provider catalog without assembly scanning
tags: [installation, providers, trimming, aot]
---

Provider modules are the preferred extension point for adding connection URL schemes. A module registers
the provider factory, aliases, dialect, parameter convention, connection-string compiler, and optional
capability objects together. Registration order is deterministic and a duplicate alias throws a
`ProviderRegistrationException` naming both conflicting registrations.

## Discover providers automatically

With the NuGet packages `DubUrl`, `DubUrl.Provider.PostgreSql`, and `Npgsql`,
`new ConnectionUrl("pg://localhost/demo")` works without a registry, factory registration, catalog,
or provider archive. Core reads attributes from the application's `DubUrl.Provider.*` runtime assemblies.
The `DubUrl` package supplies Core; add only the provider families needed by the application.
Installing a PostgreSQL archive is another way to supply the implementation and driver.

Discovery reads active, compatible, integrity-verified packs from the per-user connector store:
`%LOCALAPPDATA%/DubUrl/providers` on Windows, `$XDG_DATA_HOME/duburl/providers` on Unix when set,
otherwise `~/.local/share/duburl/providers`. Set `DUBURL_PROVIDER_STORE` to use a different store.
Discovery does not install or download connectors and does not create a missing store.

Installed providers take precedence over application dependencies for matching aliases.
Remaining application aliases remain available. Conflicting installed aliases throw
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

For a new driver binding to an existing database, reference `DubUrl`, the relevant
`DubUrl.Provider.*` family package, and the concrete driver. The packaging collector gathers their dependency
closure. Packaging discards its temporary DLL and writes a connector-specific `.deps.json` beside the
selected provider implementation DLL. The connector definition names that implementation, the other
implementation assemblies, and the driver; it has no entry-point type or empty binding DLL. Core reads the implementation attributes directly
through `AttributeProviderModule`. Mapper, dialect, parametrizer, URL schemes, and optional capabilities
come from the attributes; the driver assembly supplies its `DbProviderFactory`.
Reuse the family's rewriter unless the driver needs different keywords or URL rules. Standard bindings
are declared in `connectors/connector-matrix.json` and built by
`Providers/Build/ProviderBinding.Build.csproj`. The build script passes each row's settings to that
same project, including PostgreSQL/Npgsql. Its default settings build PostgreSQL/Npgsql for local
solution builds and focused consumer tests; no separate Npgsql project is required. Driver bindings
are distributed as connector packs, and this build project is not published as a NuGet package.
An alternative connector can supply its own factory and aliases while reusing `PostgresqlMapper`.
Registering two connectors for the same alias produces a `ProviderRegistrationException`; choose
the connector explicitly rather than relying on registration order.

For a new database, create a provider implementation project referencing the provider-neutral `DubUrl` package. Own its
database identity, mapper, rewriter, dialect, and any specialized SQL or driver discovery behavior
there. Reuse the public `BaseMapper`, `ConnectionStringRewriter`, token mappers, and ANSI SQL
components where appropriate. Keep shared family behavior in provider projects and concrete driver
dependencies in connector packaging, with optional typed BulkCopy adapters in their driver-specific projects.
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
that references only the required providers.

Applications may pass an explicitly built registry to `ConnectionUrl` or `ConnectionUrlFactory`, or
rely on automatic discovery from their NuGet dependencies. Add `DubUrl`, the provider package, and its
driver; no all-provider aggregate or `DubUrl.Core` compatibility package is produced.
Adding an independently registered provider does not require editing a built-in provider list.
Official archive distribution requires updating `connectors/connector-matrix.json`, the applicable
dependencies, and documentation. The shared generator produces catalog and local definitions from the
matrix and provider attributes. `DubUrl.Application` embeds the generated catalog. The matrix's
technology table maps driver invariants to build keys and optional assembly names, without listing factory types.
None of these packaging choices belongs in the provider-neutral Core.

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

## Attribute-based registration

Bundled and installed providers use `AttributeProviderModule`. An application can also use it directly:

```csharp
var registry = new ProviderCatalogBuilder()
    .AddModule(new AttributeProviderModule(typeof(MyMapper).Assembly))
    .Build();
```

The module captures provider attributes once during discovery, combines repeated wrapper locators,
and retains constructors, dialect instances, and optional BulkCopy adapters in its registrations.
Dialect lookup and URL compilation use those registrations without scanning provider attributes again.
ODBC mapper constructors accept configured locator options through the same path.

Explicit `IDubUrlProviderModule` implementations remain available for custom registration behavior.
The attribute adapter and built-in module retain `RequiresUnreferencedCode` annotations because
reflection requires preserved metadata; the default registration path does not generate typed code.
