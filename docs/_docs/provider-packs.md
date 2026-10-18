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
