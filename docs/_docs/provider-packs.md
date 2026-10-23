---
title: Installable provider packs
subtitle: Store, compatibility, integrity, and trust model
tags: [installation, providers, security]
---

A provider pack is a ZIP archive with `provider-pack.json` at its root. The local connector definition identifies
the provider and pack version, compatible DubUrl API range, database/driver selectors, implementation
assemblies, provider implementation used as the dependency anchor, supported target frameworks, and runtime identifiers.
The archive includes a connector-specific `.deps.json` beside that implementation DLL; .NET uses it to resolve
managed and native dependencies. No empty binding DLL or generated entry-point class is packaged. Core reads provider attributes directly. There is no separate asset inventory.

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
`$XDG_DATA_HOME/duburl/providers` on Unix, falling back to `~/.local/share/duburl/providers`.
`DUBURL_PROVIDER_STORE` overrides this location for both installation and discovery. Installation
copies into a same-volume staging directory, verifies the complete archive SHA-256 before extraction, and then uses
an atomic directory move. Versions may exist side by side, while an atomic activation record selects at
most one version per provider. Reinstallation replaces the version's files from the newly verified archive,
restoring changed files and removing extra local files. Removal first moves the
provider directory out of the active store and then deletes it.

Extraction confines every ZIP entry to staging and rejects traversal, duplicate paths, and symbolic links.
Archive integrity is checked at installation. Installed files are not subsequently hashed: inspection checks
the definition, compatibility, and required assembly presence. Missing dependencies and invalid binaries surface
when the .NET loader resolves the connector, rather than through a second dependency inventory.

Core's `ConnectionUrl(string)` constructor shares a cached default registry with dialect discovery.
The first use inspects the store and discovers active, compatible connectors and application providers
from their implementation attributes. Later calls read only the store's revision marker and reuse the
snapshot when it is unchanged. Installed aliases take precedence over application provider registrations.
Conflicting installed aliases fail explicitly;
active modules that cannot load report `ProviderPackException` with code `module-load-failed`.
Incompatible, incomplete, and corrupt packs are excluded and their diagnostics are available from
`ProviderPackStore.Inspect()`.

Applications can instead use `ProviderCatalogBuilder.AddInstalledPacks` with their chosen store and
collect module-loading diagnostics, or pass an entirely explicit registry to bypass discovery.
Pack changes affect newly created defaults and catalogs; existing URLs retain their registry snapshot.
Install, same-version replacement, update and removal through `ProviderPackStore` change an atomic
revision marker. This also invalidates cached defaults in other running processes on their next use.
A cross-process store lock prevents discovery from reading a partially published installation.
Concurrent callers share initialization; failed discovery is retried rather than cached permanently.
Each URL still receives its own mapper and connection state. Changing `DUBURL_PROVIDER_STORE` selects
a new snapshot. Call `DefaultRegistry.Refresh()` after dynamically loading an application
provider or changing driver factory registrations, or after editing installed files manually. Explicitly
supplied registries remain under the application's control.

With `DubUrl.Extensions`, `services.AddDubUrl()` registers singleton URL factories that obtain the
current cached registry for each new URL. Resolving `ISchemeRegistry` through DI returns the current
snapshot; retaining that instance retains that snapshot. The DI registration does not maintain another
registry cache or perform discovery when registering services. `IDatabaseUrlFactory`, including
`WithMicroOrm()`, uses the same URL factory and observes connector changes for newly created objects.
Existing URLs keep their original snapshot, including URLs that have not yet been parsed.
Supplying `ISchemeRegistry` as a singleton before or after `AddDubUrl()`, or using the explicit provider
module overload, keeps the chosen registry fixed. `ConfiguredConnectionUrlFactory` constructors that
take a registry also retain it. `ConnectionUrlFactory(Func<ISchemeRegistry>)` allows other hosts to
select a snapshot at URL creation without changing existing URLs.

An application should not assume that assemblies can be replaced after they have been loaded.

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
bundle selected database drivers: installing a connector installs its implementation assemblies, ADO.NET driver,
managed dependencies, and required native assets.

The official catalog uses schema version 2 in `connector-catalog.json` shipped with each CLI distribution.
Its entries and each archive's local `provider-pack.json` use the same `ConnectorDefinition` model:
identity, installation aliases and a `databases` collection containing each supported database name
and its attribute-derived schemes,
`assembly` (a real provider implementation and dependency anchor), `implementationAssemblies`, `driver` (its invariant), optional
`driverAssembly` when its filename differs, and API/TFM/RID compatibility. There is no module/type field.
The build generates the local definition from `connector-matrix.json`, then adds the download URL and
archive SHA-256 to that same definition for the catalog. These distribution fields are optional in the
model and omitted locally. Core loads installed definitions without needing the catalog or a network.
Schema-v1 catalogs and older single-database definitions must be rebuilt for the databases collection.

Database names in definitions are presentation labels. Core retains the database type referenced by
existing mapper and locator attributes, and validates installed mappings by their schemes and driver.
Changing a label does not change which database is registered; distinct databases may share a label
when their schemes differ. Duplicate or incorrect schemes still fail validation. The source matrix uses
`databaseScheme` to select an existing database attribute alias rather than repeating its display name.
No additional database ID list or serialized implementation type name is required.

`DubUrl.Application` embeds this catalog's definitions during its normal SDK build.
`BundledProviderCatalog.Create()` supplies the default catalog for application services and
`BundledProviderCatalog.Describe()` groups entries by database. Hosts may provide an updated or custom
catalog. Application and archive definitions use the same generator and matrix; download fields are
added only after archives exist. The bundled snapshot supports offline browsing; installation uses a
distribution catalog containing archive URLs and SHA-256 hashes. `DubUrl` supplies provider-neutral Core functionality and embeds no full catalog.

Connector archives exclude Core and its resolved runtime dependencies, including Didot's libraries.
The loader resolves those assemblies from the host; provider and driver dependencies outside Core's
dependency closure remain isolated in the connector. Packaging and loading share Core's generated
dependency inventory rather than maintaining a second library list.
Each entry has `Provider` (the canonical connector ID), `Version`, `DownloadUri`, `Sha256`, `Aliases`,
`Databases` (names and schemes), `DubUrlApiRange`, `TargetFrameworks`, and `RuntimeIdentifiers`. The release workflow generates
this file only after ready connector archives have been built, hashed, and exercised through installation,
new-process activation, and uninstallation. `ProviderPacks:CatalogPath` selects the catalog file;
`ProviderPacks:Catalog` remains available for distributor-supplied inline entries.
`ProviderPacks:StorePath` can override the user-store location for managed deployments and tests. The CLI
accepts canonical IDs and aliases from these curated entries only and never accepts an arbitrary URL, feed,
or package ID.

## In-process host management

`DubUrl.Application` exposes `IConnectorService` and its `ConnectorService` implementation. Hosts supply
an authenticated curated `ProviderPackCatalog`, a configured `ProviderPackStore`, and an `HttpClient`
(which the host owns). No CLI assembly, command-line parser, console, or CLI configuration is needed.
Hosts explicitly choose when to install; listing or constructing the service never downloads a connector.
Catalog authenticity remains the distributor's responsibility. Installation uses the catalog-pinned version
and archive digest, and delegates archive verification, safe extraction, API/TFM/RID compatibility, staging, activation, idempotent
reinstallation, and removal to the existing store.

```csharp
using DubUrl.Application;
using DubUrl.Core.ProviderPacks;

using var catalogFile = File.OpenRead("connector-catalog.json");
var catalog = ProviderPackCatalog.Read(catalogFile);
var store = new ProviderPackStore(Path.Combine(AppContext.BaseDirectory, "connectors"));
using var http = new HttpClient();
IConnectorService connectors = new ConnectorService(store, catalog, http);
var installed = await connectors.InstallAsync("postgresql", CancellationToken.None);
var available = connectors.List();
// Explicit removal, when requested by the host: connectors.Uninstall("postgresql");
```

The [complete minimal host](https://github.com/Seddryck/DubUrl/tree/main/examples/ConnectorHost) accepts
a catalog path, store path, and canonical connector ID or alias. It also shows cancellation and error handling.
Run it with `dotnet run --project examples/ConnectorHost --framework net10.0 -- <catalog.json> <store-path> postgresql`.

Results retain the independent `Listed`, `Installed`, `Active`, and `Unhealthy` facts, including installed
versions absent from the current catalog. Failures throw `ConnectorOperationException` with a stable `Code`:

| Code | Meaning |
| --- | --- |
| `connector-not-curated` | The ID or alias is absent from the supplied catalog. |
| `connector-download-failed` | HTTP download failed or timed out. |
| `connector-archive-invalid` | The ZIP archive or JSON manifest could not be read. |
| `connector-install-failed` | Installation could not access the store. |
| `connector-list-failed` | Listing could not access the store. |
| `connector-uninstall-failed` | Removal could not access the store. |
| `connector-not-installed` | No installed connector matches the ID or alias. |

Store diagnostics include `pack-integrity`, `path-invalid`, `assembly-missing`, `manifest-missing`,
`catalog-mismatch`, `api-incompatible`, `tfm-incompatible`, and `rid-incompatible`. Caller cancellation throws
`ConnectorOperationCanceledException` (an `OperationCanceledException`) with code `connector-cancelled` and
the caller's token. HTTP and filesystem exception details are not retained in operation failures because
they can contain credentials. Hosts decide how to present diagnostics; console/JSON formatting and CLI exit
codes remain in the CLI.

Changes apply to new processes and newly constructed provider catalogs; installing does not load modules
or replace assemblies already loaded by the host. Machine-installed ODBC and OLE DB vendor drivers remain
external prerequisites and are not installed by this service.

## One archive per implementation and driver

Database installation names are aliases when the implementation assemblies, driver and runtime
compatibility match. The 32 existing names resolve to 24 canonical archives. For example:

```sh
duburl connector install cockroachdb-npgsql
```

This installs `postgresql-npgsql`, registering PostgreSQL, CockroachDB, CrateDB, QuestDB and Timescale.
Installing `timescale-npgsql` afterwards reuses that installation. Removing either alias removes the
canonical archive and all its mappings from new registry snapshots. Existing URLs retain their registry.
PostgreSQL/ODBC, MySQL/MySqlConnector and MySQL/ODBC are consolidated the same way. Different drivers
remain separate choices. Each local definition describes the complete supported mapping set, which
Core validates against attributes before activation.
