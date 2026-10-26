# Offline installation and organization mirrors

Connector catalogs are executable-code allow-lists. A SHA-256 digest proves that an archive matches
the catalog; it does not prove who approved that catalog. Obtain the versioned distribution catalog
through an authenticated, administrator-approved channel and review its provenance before using it.
The bundled metadata snapshot does not include archive hashes and is insufficient for installation.
These workflows do not add automatic catalog discovery, catalog signatures, or multiple-source resolution.

## Prepare an approved snapshot

On a connected preparation machine, obtain the published distribution catalog for the desired release.
Select exact connector IDs and versions; aliases resolve to their canonical archive. For example:

```powershell
duburl connector mirror C:\approved\upstream-catalog.json C:\staging\connectors-1.2.3 https://packages.example.org/duburl/1.2.3/ postgresql-npgsql@1.2.3 sqlite-microsoft@1.2.3
```

Use IDs actually listed in your catalog. Each selection must match that catalog's exact version.
Use separate source catalogs/snapshots for different release versions. The output directory must not
already exist. The command downloads complete archives, checks every upstream SHA-256, and preserves
their bytes. It writes `<sha256>.zip` files and a validated schema-2 `connector-catalog.json`, preserving
all compatibility, database, alias and implementation metadata and hashes while changing only download URLs.
It stages the snapshot before exposing the output directory. Failed preparation leaves no published snapshot.
Preparation verifies distribution metadata and archive integrity; installation still validates archive structure,
manifest agreement, required assemblies and host compatibility before activation.

In process, use `ConnectorMirror.PrepareAsync(approvedCatalog, selections, archiveBaseUri, outputDirectory, token)`.
Pass `ConnectorMirrorSelection` values with explicit versions. The caller owns the supplied `HttpClient`.

## Install without a network connection

Transfer the approved catalog and its complete ZIP files to the isolated machine, along with any required
DubUrl runtime/application packages. Verify the catalog's provenance independently of the removable media;
protect it with your organization's signed transfer manifest or equivalent approval process. A local ZIP or
its embedded `provider-pack.json` is never an approval source. Do not create a new catalog/hash from an
untrusted archive merely to make installation succeed.

Configure the approved local catalog using an absolute path and install a selected archive explicitly:

```powershell
$env:DUBURL_ProviderPacks__CatalogPath = 'C:\approved\connector-catalog.json'
duburl connector list --json
duburl connector install postgresql-npgsql --archive C:\approved\<sha256>.zip --json
```

Replace `<sha256>` with the archive filename from the prepared snapshot. `--archive` never accesses HTTP
or falls back to downloading, including when the file is missing or corrupt. The catalog may retain its
HTTPS download URLs; they are unused for this operation. A missing explicitly configured catalog fails
closed. A replacement catalog excludes bundled/public entries rather than extending them.

For embedding applications:

```csharp
using var metadata = File.OpenRead(approvedCatalogPath);
var catalog = ProviderPackCatalog.Read(metadata);
using var http = new HttpClient(); // Not used by InstallOfflineAsync.
IConnectorService connectors = new ConnectorService(new ProviderPackStore(), catalog, http);
await connectors.InstallOfflineAsync("postgresql-npgsql", archivePath, cancellationToken);
```

The same store installer checks the approved archive hash, manifest metadata, API range, TFM, RID,
safe extraction and required files. It stages before activation and supports idempotent reinstallation.
Missing files, invalid ZIP/metadata, hash mismatches and incompatible packs return stable diagnostics;
validation failures preserve the existing active installation. New processes/catalogs observe activation.
Current packs have an archive hash, not a separate per-asset hash inventory; these workflows retain the
existing archive and installation checks without inventing asset digests.

ODBC and OLE DB ZIPs contain only the managed bridge. Install the matching vendor ODBC driver or OLE DB
provider separately, using approved offline vendor installers and matching OS/architecture. Installing a
connector does not install these machine prerequisites. Honor the bundled license and prerequisite notices.

## Publish and operate an internal HTTPS mirror

Upload the immutable digest-named ZIPs to the exact internal HTTPS base URL used during preparation.
Check the hosted bytes against the catalog digests, then publish the catalog through your approved
configuration channel. Publish archives first and the catalog last, preferably by an atomic pointer switch.
Never replace an archive at an existing digest URL. Retain every archive referenced by any deployed
catalog until those clients have migrated; retain prior catalog snapshots for rollback and audit.

Distribute the approved catalog file to clients and set `DUBURL_ProviderPacks__CatalogPath` as above.
`duburl connector list` reads local approved metadata; ordinary `duburl connector install <id>` uses only
the internal URL in that replacement catalog. An unavailable internal archive fails the installation without
public fallback. Enforce public-host egress restrictions at your network boundary, and configure the internal
server without redirects to public release hosts. Do not leave additional public entries in configured
`ProviderPacks:Catalog` settings. Additional trusted source resolution remains the separate #1637 workflow.

For private hosts, prefer integrated access at a managed gateway or authenticated internal proxy, so the
CLI can use clean HTTPS URLs. For bearer tokens, client certificates, or custom identity flows, embed
`ConnectorService`/`ConnectorMirror` with a host-owned `HttpClient` and authentication handler reading
credentials from the organization's secret store. The CLI has no token flag or custom authentication
handler configuration. Keep credentials out of URLs, catalogs, source control, command arguments and logs;
do not use signed query strings as mirror base URLs. Mirror preparation rejects credentials, queries and
fragments in that base URL. Configure TLS trust through your normal administrative process.

Before rollout, block public endpoints and verify listing and installation using only the replacement
catalog and internal archive host. For fully isolated clients, use `--archive` instead. Catalog updates and
internal hosting do not bypass compatibility, integrity or activation checks.
