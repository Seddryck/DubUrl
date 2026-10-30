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

## Additional trusted catalogs

Administrators can add named catalog sources in `appsettings.json` or through another configuration
provider. The official catalog remains enabled alongside these sources. Set
`ProviderPacks:OfficialEnabled` to `false` to disable it.

The existing `CatalogPath` and inline `Catalog` settings configure the official distributor snapshot.
Protect those files and settings with deployment permissions. Bundled definitions are trusted as part
of the application distribution.

```json
{
  "ProviderPacks": {
    "OfficialEnabled": true,
    "Sources": [
      {
        "Id": "enterprise",
        "Uri": "https://connectors.example.org/catalog.json",
        "Enabled": true,
        "Sha256": "<64 hexadecimal characters supplied by your administrator>",
        "BearerTokenEnvironmentVariable": "CORPORATE_CONNECTOR_CATALOG_TOKEN"
      }
    ]
  }
}
```

Give each source a stable ID that starts with an ASCII letter or number. The remaining characters can
be ASCII letters, numbers, dots, underscores, or hyphens. IDs can contain up to 128 characters and must
be unique without regard to case. The ID `official` is reserved.

Sources are enabled by default. Set `Enabled` to `false` to disable a source. Its `Uri` must be an
absolute HTTPS URL or a `file:` URI pointing to a managed local snapshot. Use `TrustMode` to choose
how the client trusts the catalog.

| Mode | Required configuration | How catalog updates are approved |
|---|---|---|
| `sha256` (default) | Set `Sha256` to the approved document hash. | Deploy a new independently approved hash to clients whenever the catalog changes. Existing pinned configurations keep working. |
| `unsigned` | Set `TrustMode` to `unsigned`. Omit `Sha256` and signing settings. | Trust the configured HTTPS endpoint through normal TLS validation, or trust local deployment permissions. |
| `signed` | Set `SignatureUri` and `TrustedSigningKeys`. Omit `Sha256`. | Publish updates signed by a trusted key. Clients keep their existing configuration while that key remains trusted. |

In `sha256` mode, obtain the catalog digest through a trusted independent channel and update the pin
when approving a new snapshot. In `unsigned` mode, the endpoint or local deployment administrator can
replace catalog contents without distributing a new pin or signature. The following source explicitly
chooses that policy:

```json
{
  "Id": "enterprise",
  "Uri": "https://connectors.example.org/catalog.json",
  "TrustMode": "unsigned",
  "Enabled": true
}
```

Missing or mismatched pins, failed signatures, unknown modes, and conflicting settings cause the
client to reject the source. Verification failures never switch a source to a weaker trust mode.

Catalog updates become available on the next CLI invocation or the host's next explicit `LoadAsync`
call. Existing service instances do not refresh their catalogs in the background. Every mode validates
the catalog schema and distribution metadata. Each archive must have an HTTPS location and its own
SHA-256 digest. Installation verifies that digest before extracting the archive.

HTTPS uses the operating system's certificate validation. For bearer authentication, specify the
process environment variable containing the token. The client reads it when loading the source and
rejects the source if credentials are missing. Store tokens outside source control.

The bearer header applies to the catalog request and, in signed mode, the configured signature request
on the same HTTPS origin. The client rejects signature locations on another origin or transport before
making a request. It does not set default authentication headers on the shared HTTP client or pass the
catalog token to archive downloads. Archives need their own access mechanism, such as approved signed
URLs. Application hosts own their HTTP client and control its redirect and logging policies.

URL user-info authentication is rejected. Query strings may carry authentication, but command output
and persisted provenance exclude catalog and archive locations, queries, credential variable names,
tokens, and underlying transport exceptions.

Sources and entries are merged in stable ordinal order. Canonical IDs and aliases must be unique
across all enabled catalogs that pass validation, without regard to case. A collision stops the merge
with `connector-catalog-collision` and names both source IDs. Changing configuration order cannot
resolve the collision.

An inaccessible, malformed, or unverified source contributes no entries and reports
`connector-source-unavailable`. Healthy sources remain usable. The CLI loads sources when connector
management needs them, so an unavailable catalog does not prevent installed modules from loading.

`duburl connector list` identifies each connector's catalog in the `CATALOG` column. With `--json`, the
output includes `catalogSource`, `catalogProvenance`, and top-level source `diagnostics`.

An installed connector's local manifest retains its source ID, canonical entry ID and version, catalog
digest, and archive digest after its source fails or is disabled. In unsigned mode, the document digest
identifies the catalog contents without acting as a trust pin. Provenance also records the trust mode.
For signed catalogs, it includes the verified key ID, public-key fingerprint, and revision. It contains
no catalog or download URL. Existing installations without provenance remain readable.

`connector install` accepts only a canonical ID or alias resolved from the effective catalog. This also
applies to offline installation with `--archive`. The command does not accept a source selector or an
arbitrary package URL.

Application hosts can call `TrustedConnectorCatalogs.LoadAsync(sources, officialCatalog, token)` to
build the merged `ProviderPackCatalog` and pass it to `ConnectorService`. They can inspect
`IConnectorService.CatalogDiagnostics` for safe diagnostics about individual source failures.

### Signed catalogs

Provision public keys through trusted deployment configuration, independently of the catalog server.
Clients accept PEM SubjectPublicKeyInfo files with the `PUBLIC KEY` label. They do not accept private
keys or certificates as public-key configuration. Relative `PublicKeyPath` values resolve from the
application directory.

Key IDs are case-sensitive names that must be unique within the source. The publisher derives an ID
by prefixing the lowercase SHA-256 fingerprint of the public SubjectPublicKeyInfo bytes with `sha256-`.
Use the ID from the generated settings. Existing explicitly named keys and catalogs remain valid.
A derived ID can also match a public key already trusted under a legacy alias. In every case, trust
comes from the configured public-key file rather than keys advertised by the server.

```json
{
  "Id": "enterprise",
  "Uri": "https://connectors.example.org/catalog.json",
  "TrustMode": "signed",
  "SignatureUri": "https://connectors.example.org/catalog.json.sig",
  "TrustedSigningKeys": [
    {
      "KeyId": "sha256-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "PublicKeyPath": "corporate-catalog-public.pem"
    }
  ],
  "MinimumRevision": 1,
  "Enabled": true
}
```

Version 1 fixes the signing algorithm to RSA-PSS with SHA-256. It accepts RSA keys from 2048 to 8192
bits, with 3072 bits as a practical publishing choice. Downloaded metadata cannot change the algorithm.

The detached JSON document sets `schemaVersion` to 1 and includes the lowercase `sourceId`, `keyId`,
and a positive integer `revision`. Its `expiresAt` value is UTC Unix time in seconds, or 0 for no
expiration. The `signature` field contains the base64-encoded RSA signature.

Publishers sign the UTF-8 header below followed immediately by the exact catalog bytes. Each header
line ends with LF, including the last line. Integers use invariant decimal formatting. Clients verify
those bytes without reserializing the catalog JSON. The source ID binds the signature to the configured
source without regard to case.

```text
DubUrl.ConnectorCatalog/v1
<lowercase-source-id>
<key-id>
<revision>
<expiresAt>
```

The signature authenticates the catalog and its freshness metadata. Clients reject expired signatures
and revisions below `MinimumRevision`, which defaults to 1. They save the highest accepted revision
under `<provider-store>/catalog-revisions`, independently of key IDs. This state prevents another process
from accepting a lower revision or different contents at the same revision.

Clients can reload an identical payload at the accepted revision until it expires. Catalogs without
expiration remain valid while their revision is accepted. Publishing automatically allocates a higher
revision for each new publication, including expiry renewals and key changes. Clients advance their
saved revision only after parsing the catalog and confirming that its entries are unambiguous. Corrupt,
inaccessible, or unwritable state causes that source to fail. Other sources and installed connectors
remain usable.

Protect configuration, public keys, system time, and revision state with normal deployment permissions.
Keep the state directory across upgrades and key rotation. Deleting or relocating the provider store
resets the client's saved revision. A new client can verify a signature but cannot determine the latest
published revision from that signature alone. Provision `MinimumRevision` centrally to establish a
minimum acceptable revision. An expiration can also limit how long an older catalog remains valid.
Without expiration, a new client cannot reject a signature based on age. Revision rollback checks
still apply after it accepts a newer revision. There is no online revocation or transparency service.

To restore earlier catalog contents deliberately, republish them. Automatic revision allocation gives
the restored contents a higher revision.

For rotation, deploy both the current and next public keys first, publish the next revision signed by
the new key, then remove the retired key from clients. Retain the same source ID and revision state.
The server cannot add a new trusted key through a catalog or signature document. Removing a key
blocks future catalog loads signed by that key. Previously installed provider packs remain installed.

See [catalog signing and publication](https://github.com/Seddryck/DubUrl/blob/main/connectors/DISTRIBUTION.md#sign-and-publish-an-updatable-catalog)
for publisher tooling and protected key generation. The implementation uses .NET's built-in
[RSA signing and verification APIs](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsa).

### Create a catalog from a provider-pack folder

The CLI can prepare a catalog directly from approved top-level ZIP archives:

```text
duburl catalog create <folder> --output <new-output-folder> --archive-base-uri https://connectors.example.org/packs/ --trust-mode sha256 --json
```

Choose `--trust-mode unsigned` to trust the managed endpoint or local deployment. For signed catalogs,
use `--trust-mode signed` with `--signing-key <encrypted-PEM-path>`. Use `--signing-key -` when supplying
the encrypted PEM through redirected stdin. Key IDs are derived and revisions are allocated automatically.

Signatures have no expiration by default. Set `--expires-at` to an ISO 8601 timestamp to enable
expiration, or use `--expires-at none` to keep the default. The signing password comes from the
environment variable selected by `--signing-password-env`. That option defaults to
`DUBURL_CATALOG_SIGNING_PASSWORD`. There is no password-value argument.

Set `--catalog-uri` to the published catalog location. When it is omitted, the generated settings point
to the local catalog file. `--source-id` defaults to `enterprise`. Preserve publisher revision state
across output folders and key rotation. Use `--publisher-state` to choose a different state directory.
The default is `DubUrl/catalog-publication-revisions` beneath local application data, separate from
client stores.

The new snapshot contains a validated catalog, unchanged copies in `archives/`, and `source-settings.json`.
Pinned mode adds a document digest. Signed mode adds a detached signature and public PEM. The command
ignores subfolders and unrelated files. It rejects conflicting IDs or aliases, multiple versions of the
same connector, unsafe archives, and existing output directories. Administrators publish the prepared
files and distribute trusted configuration and keys. See the
[complete CLI publication workflow](https://github.com/Seddryck/DubUrl/blob/main/connectors/DISTRIBUTION.md#create-a-catalog-from-a-folder-with-the-cli)
for examples, artifact layout, security policy, exit codes, and deployment guidance.

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
