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
