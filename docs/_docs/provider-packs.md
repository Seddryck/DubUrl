---
title: Installable provider packs
subtitle: Store, compatibility, integrity, and trust model
tags: [installation, providers, security]
---

A provider pack is a ZIP archive with `provider-pack.json` at its root. The versioned manifest identifies
the provider and pack version, the compatible DubUrl API range, its `IDubUrlProviderModule` assembly and
type, supported target frameworks and runtime identifiers, and every managed or native asset with a SHA-256
digest. Native assets declare the runtime identifier to which they belong.

DubUrl installs packs only from a curated catalog supplied by the application distributor. The catalog
binds a provider name and version to a download URI and SHA-256 archive digest. Arbitrary NuGet feeds,
package IDs, and unlisted URLs are intentionally not accepted. A digest proves catalog integrity, not
publisher identity: distributors are responsible for authenticating and signing their catalog delivery.

The CLI presents a provider pack as a **connector**. A connector combines a DubUrl database mapping with
one connection technology and its dependencies. Its canonical ID names both sides explicitly, such as
`postgresql-npgsql`, `postgresql-odbc`, or `access-oledb`. Catalog aliases provide convenient defaults:
for example, `postgresql`, `postgres`, `pg`, and `npgsql` can all resolve to `postgresql-npgsql`. Catalog
construction rejects an alias assigned to more than one connector.

The default user store is `%LOCALAPPDATA%\DubUrl\providers` on Windows and
`$XDG_DATA_HOME/duburl/providers` on Linux, falling back to `~/.local/share/duburl/providers`. Installation
copies into a same-volume staging directory, validates the archive and every declared asset, and then uses
an atomic directory move. Versions may exist side by side, while an atomic activation record selects at
most one version per provider. Reinstalling an already valid version is idempotent. Removal first moves the
provider directory out of the active store and then deletes it.

At startup, `ProviderCatalogBuilder.AddInstalledPacks` inspects active packs and loads valid module entry
points through `IDubUrlProviderModule`. Incompatible, incomplete, corrupt, or unloadable packs produce
diagnostics without preventing other valid packs from loading. Pack changes affect new processes and new
catalogs; an application should not assume that assemblies can be replaced after they have been loaded.

## CLI management

The provider-neutral CLI manages only the user-scoped connector store:

```text
duburl connector list
duburl connector install postgresql
duburl connector uninstall postgresql
```

`list --json` emits stable canonical ID, version, source, state, aliases, schemes, diagnostics, and four
independent state facts: `listed`, `installed`, `active`, and `unhealthy`. A listed connector is present in
the curated catalog and available for download. An installed connector exists in the local store regardless
of compatibility. An active connector is installed, compatible, and selected for loading. An unhealthy
connector is installed but incompatible, incomplete, or corrupt. The summary `state` uses the most specific
applicable value (`unhealthy`, `active`, `installed`, then `listed`). Install and uninstall operations affect new
processes and newly created provider catalogs; already loaded assemblies keep running. The CLI does not
bundle selected database drivers: installing a connector installs its DubUrl module, ADO.NET driver,
managed dependencies, and required native assets.

Distributors configure the allow-list under `ProviderPacks:Catalog`; each entry has `Provider` (the
canonical connector ID), `Version`, `DownloadUri`, `Sha256`, `Aliases`, and `Schemes`.
`ProviderPacks:StorePath` can override the user-store location for managed deployments and tests. The CLI
accepts canonical IDs and aliases from this catalog only and never accepts an arbitrary URL, feed, or
package ID.
