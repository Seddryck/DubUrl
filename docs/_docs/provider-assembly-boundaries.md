---
title: Provider assembly boundaries
subtitle: Compatibility and package migration plan
tags: [installation, providers, architecture]
---

DubUrl publishes deliberately acyclic package layers:

1. `DubUrl.Core` contains provider-neutral value types and immutable catalog logic. It does not reference
   provider implementations.
2. `DubUrl.Provider.*` packages own database-family and connection-technology implementations under
   `providers/`. They depend on Core, without concrete ADO.NET driver dependencies.
3. `DubUrl.Connector.*` packages select provider implementations, concrete drivers, schemes, and explicit
   modules under `connectors/`.
4. `DubUrl` remains the compatibility aggregate, referencing the provider packages and
   `DubUrl.Providers` built-in catalog. Explicit consumers can omit both aggregate and built-in catalog.

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
| `DubUrl.ProviderCore/Schema` (assembly `DubUrl.Core`) | `ISchemaSqlRenderer` contract and reusable `StandardSchemaSqlRenderer` implementation |
| `providers/DubUrl.Provider.*/Schema/Generation` | DDL capabilities, native-type sets, and any schema SQL renderer overrides |
| `providers/DubUrl.Provider.*/Schema/Discovery` | Reserved for future catalog queries and schema discovery implementations |
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
