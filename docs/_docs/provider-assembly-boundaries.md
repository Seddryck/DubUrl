---
title: Provider assembly boundaries
subtitle: Compatibility and package migration plan
tags: [installation, providers, architecture]
---

DubUrl publishes deliberately acyclic package layers:

1. `DubUrl.Core` contains provider-neutral value types and immutable catalog logic. It does not reference
   provider implementations.
2. `DubUrl.Provider.*` packages own database-family and connection-technology implementations under
   `Providers/`. They depend on Core, without concrete ADO.NET driver dependencies.
3. `DubUrl.Provider.<Family>.<Driver>` bindings select shared family implementations, concrete drivers,
   schemes, and explicit modules alongside the family projects under `Providers/`.
4. `DubUrl` remains the compatibility aggregate, referencing the provider packages and
   `DubUrl.Providers` built-in catalog. Explicit consumers can omit both aggregate and built-in catalog.

Project folders match the assemblies and packages they produce:

| Project | Responsibility |
| --- | --- |
| `DubUrl.Core/DubUrl.Core.csproj` | Provider-neutral contracts and infrastructure; owns its source files directly |
| `DubUrl/DubUrl.csproj` | Compatibility aggregate and built-in provider registration |

Core uses normal SDK source inclusion. It does not link source files from the compatibility project.

Provider-neutral contracts belong in `DubUrl.Core`; database-specific declarations, mappers, rewriters,
dialects, SQL mappings, and driver discovery sources belong in their owning provider projects. Their
sources and resources are physically colocated there, rather than linked from the Core source tree.
Adding a database must not require a change to `DubUrl.Core`. See [provider modules](../provider-modules/)
for the authoring example and explicit registration path.

## Public-type migration

Provider implementation namespaces now begin with `DubUrl.Provider.<Family>` and follow their folders.
For example, import `DubUrl.Provider.PostgreSql.Sql` for `PgsqlDialect` and
`DubUrl.Provider.PostgreSql.Mapping` for `PostgresqlMapper`. Core contracts retain their existing
namespaces. Package and assembly IDs remain unchanged.

This is a public namespace migration: applications must update imports and recompile, and existing
connector binaries and reflection-based type names must be rebuilt or updated. Assembly forwarding
cannot preserve a renamed namespace. No legacy namespace wrappers are introduced.

An additional `DubUrl.Abstractions` package is intentionally deferred. It becomes worthwhile only when at
least two independently versioned hosts need the same contracts without catalog/parsing implementation, or
when `DubUrl.Core` gains a dependency that provider modules should not inherit. Until either condition is
met, another assembly would add versioning surface without creating an actual dependency boundary.

## Shared schema generation and provider SQL

Schema support uses the same provider packages as querying. Connectors select and distribute a provider;
connectors using the same SQL dialect reuse its schema behavior.

| Location | Responsibility |
| --- | --- |
| `DubUrl.Schema` | Models, builders, constraints, dependency planning, expression traversal, templates, native-type validation and fallback, script assembly |
| `DubUrl.Core/Schema` (assembly `DubUrl.Core`) | `ISchemaSqlRenderer` contract and reusable `StandardSchemaSqlRenderer` implementation |
| `Providers/<Family>/DubUrl.Provider.*/Schema/Generation` | DDL capabilities, native-type sets, and any schema SQL renderer overrides |
| `Providers/<Family>/DubUrl.Provider.*/Schema/Discovery` | Reserved for future catalog queries and schema discovery implementations |
| `connectors/` | Provider/driver composition and distribution; no copied schema templates |

`IDialect.SchemaSqlRenderer` defaults to `StandardSchemaSqlRenderer.Instance`. `BaseDialect` exposes a
virtual property for provider overrides. Existing dialects continue to select standard identity,
generated-column, regex, and object-name syntax through `DialectCapabilities`. Comments, views, native
types, and logical type/function mappings retain their existing provider-owned declarations.

`DubUrl.Schema` renders expressions and literals using the dialect's existing formatters, then delegates
SQL variation points to `ISchemaSqlRenderer`. The contract accepts primitive values and already-rendered
SQL, so neither Core nor provider projects need to reference schema models or templates. It also exposes
foreign-key placement independently of column generation. The standard implementation preserves SQLite's
existing inline placement; another provider can override placement without adopting SQLite identity syntax.

When a provider needs syntax outside the existing capability choices, subclass the standard renderer in
that provider project and override only the required operation:

```csharp
using DubUrl.Querying.Dialects;

namespace DubUrl.Provider.Example.Schema.Generation;

internal sealed class ProviderSchemaSqlRenderer : StandardSchemaSqlRenderer
{
    public static new ProviderSchemaSqlRenderer Instance { get; } = new();

    public override string RenderRegex(IDialect dialect, string expression, string pattern)
        => $"provider_regexp({expression}, {pattern})";
}
```

Expose it from the provider's `BaseDialect` subclass:

```csharp
public override ISchemaSqlRenderer SchemaSqlRenderer
    => ProviderSchemaSqlRenderer.Instance;
```

The example's `provider_regexp` stands for the provider's actual SQL function. `expression` and `pattern`
are already rendered SQL: do not quote them again. Unoverridden operations retain shared formatting,
qualification checks, and unsupported-operation errors. An implementation can also implement
`ISchemaSqlRenderer` directly when none of the standard syntax applies. Instances should be stateless
and safe to share between renders.

Adding a database-specific override requires no shared template changes, new capability enum members,
or additional connector/schema packages. Keep common SQL in the standard implementation and introduce
an override only when the provider needs different behavior.

## Bulk copy registration and optional typed adapters

Bulk copy belongs to a provider/connector registration. Native DuckDB and SqlClient registrations opt
in; ODBC registrations remain unsupported even when they share the exact same SQL dialect.
`SchemaSqlRenderer` stays on `IDialect`; there is no BulkCopy hook on the dialect.

| Location | Responsibility |
| --- | --- |
| `DubUrl.Core/BulkCopy` (assembly `DubUrl.Core`) | Contracts, deferred adapter activation, scoped optional dependency resolution |
| `DubUrl.BulkCopy` | Public extension and factory facade; references Core only |
| `Providers/<Family>/DubUrl.Provider.*` | Driver-independent database behavior and optional adapter declarations |
| `Providers/DuckDb/DubUrl.Provider.DuckDb.Native.BulkCopy` | Typed DuckDB appender engine |
| `Providers/SqlServer/DubUrl.Provider.SqlServer.SqlClient.BulkCopy` | Typed Microsoft.Data.SqlClient bulk engine |
| Native connector modules | Driver deployment, conditional adapter dependency, provider registration |

The typed adapters reference their driver packages at compile time. Core, the facade, and provider
families do not. Native DuckDB and SqlClient connector artifacts include only their corresponding
adapter and driver dependency closure; ODBC artifacts do not acquire these native adapters.

### Register and activate an adapter

`ProviderRegistration.BulkCopyProvider` defaults to null and is copied into `ResolvedConnection`.
`ConnectionUrl.BulkCopyProvider` exposes the selection to the shared factory. Explicit registrations
can opt out by leaving the property null, including when reusing a native mapper.

Provider families can declare optional support without linking an adapter assembly:

```csharp
[BulkCopyProvider("DubUrl.Provider.DuckDb.Native.BulkCopy",
    "DubUrl.Provider.DuckDb.Native.BulkCopy.DuckDbBulkCopyProvider")]
```

The non-inherited mapper attribute creates a `LazyBulkCopyProvider` for attribute-based and generated
connector registration. Built-in registrations use the same deferred provider explicitly. Custom
modules can set `BulkCopyProvider = new LazyBulkCopyProvider(assemblyName, typeName)` when constructing
their registration, or supply a preconstructed `IBulkCopyProvider` when linking an adapter intentionally.

Registration and engine creation do not load the optional adapter. On `Write`, the deferred engine
resolves its configured assembly/type in the actual driver's `AssemblyLoadContext`, activates the
`IBulkCopyProvider`, and delegates the operation. Reflection is confined to this activation boundary.
DuckDB row/value calls, null handling, date/time conversion, and SqlClient bulk operations use typed
APIs without method discovery or `MethodInfo.Invoke` in the data-transfer loop.

Deploy the adapter built against the connector's driver version with that connector. A host using
only Core and the facade can load the optional DLLs at runtime without referencing them at compile time.
Older mapper-factory-only registries do not implicitly acquire bulk support: use explicit registrations.
The public `IBulkCopyEngine` namespace remains `DubUrl.BulkCopy`, with its former facade assembly identity
forwarded to Core. Hosts using the one-argument `ConnectionUrl` constructor still need the `DubUrl`
compatibility package for its default provider set; modular hosts supply an explicit connector registry.

### Optional DLL deployment

Use the normal call when the connector's dependency loader can already find its DLLs:

```csharp
using DubUrl.BulkCopy;
connection.BulkCopy("Customer", reader);
```

For separately deployed adapter/helper assemblies, supply search directories:

```csharp
connection.BulkCopy("Customer", reader, new BulkCopyDependencyResolver(new[]
{
    Path.Combine(AppContext.BaseDirectory, "optional-bulk-dependencies")
}));
```

The resolver uses the driver's normal load context first, then probes the driver directory and supplied
directories. Handlers exist only during the operation and are removed after success or failure.
Overlapping scopes cooperate. Missing assemblies produce a diagnostic naming the DLL and explaining how
to supply it. Native runtime assets retain the connector/driver's existing native load-context handling;
the resolver neither downloads packages nor changes machine-wide configuration.

Custom typed engines should wrap native operations in `dependencies.Execute(driverAssembly, action)`
to retain scoped helper resolution and missing-dependency diagnostics. Use `ResolveType` only for an
optional adapter/helper activation boundary. The shared facade disposes engines and leaves source
readers owned by callers. Typed DuckDB appenders and SqlClient bulk objects are disposed on success
and failure. SqlClient retains batch size 1000, streaming enabled, and timeout 0. This does not add an
ODBC or PostgreSQL bulk engine.
