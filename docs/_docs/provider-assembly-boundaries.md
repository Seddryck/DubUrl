---
title: Provider assembly boundaries
subtitle: Compatibility and package migration plan
tags: [installation, providers, architecture]
---

DubUrl now publishes three deliberately acyclic package layers:

1. `DubUrl.Core` contains provider-neutral value types and immutable catalog logic. It does not reference
   provider implementations.
2. `DubUrl.Providers` references `DubUrl.Core` and supplies shared provider metadata and implementations
   used by the compatibility package and provider packs.
3. `DubUrl` remains the compatibility package. Existing consumers keep their source-compatible public API
   and receive both lower layers transitively.

New provider-pack contracts are added to `DubUrl.Core`; database-specific descriptors, mappers, rewriters,
dialects, and parameter conventions belong in `DubUrl.Providers` or an optional provider pack. Adding a
database must not require a change to `DubUrl.Core`.

## Public-type migration

Existing public types stay in the `DubUrl` assembly for the current major version. Moving them would change
their assembly-qualified names and break binary compatibility even when namespaces remain unchanged. During
the next major-version cycle, provider-neutral types can move to `DubUrl.Core` and built-in implementations
to `DubUrl.Providers`; the `DubUrl` compatibility assembly will retain type forwarders for the previous API.
The packages will be released together with aligned versions before any forwarding begins.

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
| `providers/DubUrl.Provider.*` | Dialect capabilities, native-type sets, and any SQL renderer overrides |
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
