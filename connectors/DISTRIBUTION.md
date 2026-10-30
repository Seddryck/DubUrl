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

## Create a catalog from a folder with the CLI

Use `catalog create` to turn approved provider-pack ZIP archives into a new publication
snapshot. It reads only top-level files with a `.zip` extension, case-insensitively. It ignores subfolders
and unrelated files, derives identities from manifests rather than filenames, and never loads provider
assemblies. Include exactly one archive per canonical connector: duplicate IDs, versions of the same
connector, conflicting aliases, and filenames differing only by case are rejected instead of choosing a
winner. Invalid ZIPs, unsafe paths/links, incomplete manifests, and missing required assemblies also fail.
Host API/TFM/RID compatibility is still checked at installation, not inferred from the publisher's runtime.

The default trust policy is `sha256`:

```powershell
duburl catalog create C:\approved\packs --output C:\staging\snapshot-42 --archive-base-uri https://connectors.example.org/packs/ --catalog-uri https://connectors.example.org/catalog.json --source-id enterprise --json
```

The output directory must not already exist, even if empty. Archives are copied unchanged into its
`archives` subdirectory and referenced by their URL-encoded original filenames beneath
`--archive-base-uri`. The command hashes the copied bytes, validates metadata and contents, and stages
all artifacts before renaming the complete snapshot into place. It leaves input archives unchanged and
never uploads, starts a server, installs connectors, or modifies client configuration.

Choose `unsigned` explicitly to trust the configured HTTPS endpoint or protected local deployment
without a catalog-document pin or signature. Individual archive SHA-256 checks remain mandatory:

```powershell
duburl catalog create C:\approved\packs --output C:\staging\snapshot-43 --archive-base-uri https://connectors.example.org/packs/ --trust-mode unsigned
```

For signed publication, provision an encrypted PKCS#8 private PEM on the publisher and inject its
password into a process environment variable from your secret store. For example, with
`CORPORATE_CATALOG_SIGNING_PASSWORD` already populated securely:

```powershell
try {
    duburl catalog create C:\approved\packs --output C:\staging\snapshot-44 --archive-base-uri https://connectors.example.org/packs/ --catalog-uri https://connectors.example.org/catalog.json --trust-mode signed --source-id enterprise --signing-key C:\secure\catalog-signing-private.pem --signing-password-env CORPORATE_CATALOG_SIGNING_PASSWORD --json
} finally {
    Remove-Item Env:CORPORATE_CATALOG_SIGNING_PASSWORD -ErrorAction SilentlyContinue
}
```

`--signing-password-env` defaults to `DUBURL_CATALOG_SIGNING_PASSWORD`; there is no password-value
argument. Signed mode requires only the protected key and its password. The key ID is derived from
the public-key fingerprint, revisions are allocated automatically, and expiry defaults to none.
Omit `--expires-at` or use `--expires-at none` for no expiration. Use `--expires-at <ISO-8601-timestamp>`
when an expiration is wanted.
There are no `--key-id` or `--revision` arguments. Signing arguments in another mode, unknown modes,
missing credentials, and signing failures are rejected without an
automatic downgrade. Private keys are never copied or exported into the snapshot. The generated public
PEM uses the existing signature format and can be provisioned independently to clients.

Use `--signing-key -` to read the encrypted PEM from redirected stdin instead of a file. Only the key
goes through stdin; its password still comes from the configured environment variable. For example,
with `DUBURL_CATALOG_SIGNING_PASSWORD` already injected by the publishing secret store:

```powershell
Get-Content C:\secure\catalog-signing-private.pem -Raw | duburl catalog create C:\approved\packs --output C:\staging\snapshot-45 --archive-base-uri https://connectors.example.org/packs/ --trust-mode signed --signing-key - --json
```

A secret-management tool can supply the encrypted PEM through the same pipe. The command requires
redirected input for `-`; empty, malformed, or incorrectly encrypted keys fail without echoing input or
creating a published snapshot.

The generated artifacts are:

| Artifact | When present | Purpose |
|---|---|---|
| `connector-catalog.json` | Every mode | Validated catalog, deterministically ordered by canonical ID. |
| `archives/` | Every mode | Byte-for-byte copies of eligible input ZIPs. |
| `source-settings.json` | Every mode | A `ProviderPacks:Sources` configuration example for this source. |
| `connector-catalog.json.sha256` | `sha256` | Exact document hash also embedded in the configuration example. |
| `connector-catalog.json.sig` | `signed` | Detached signature over catalog bytes and freshness metadata. |
| `catalog-public.pem` | `signed` | Public key for trusted client deployment. |

Identical inputs and options produce identical catalog bytes and document digest; RSA-PSS signatures
are intentionally nondeterministic. `--json` reports `command`, `success`, and a `result` containing
`trustMode`, `connectorCount`, `catalogSha256`, and relative `artifacts`. Signed results also report
`signingKeyId`, `catalogRevision`, and `signatureExpiresAt` (null for no expiration). The digest in unsigned/signed
results is an informational fingerprint, not an extra client pin. Failures return safe diagnostic codes
and messages without paths, credentials, or authenticated URLs. Exit codes are 0 for success, 2 for
argument parsing errors, 5 for preparation/validation/signing failures, and 130 for cancellation.

Publish `archives/*` at the supplied archive base URI, preserving filenames and bytes. Publish the
catalog at `--catalog-uri` and, in signed mode, its signature at the same URI plus `.sig`. Both hosting
URLs must be clean absolute HTTPS locations without user-info, query, or fragment. The catalog URI
may instead be an absolute `file:` URI. When omitted, it defaults to the final local catalog path, so the
generated settings can be consumed locally with explicit `connector install <id> --archive <path>`.

Merge the example source into managed client settings, preserving any other sources and key-rotation
overlap. `--source-id` defaults to `enterprise`; `official` is reserved. The generated public-key path is
the absolute snapshot path, so adjust it when deploying the public key elsewhere. A signed example
sets `MinimumRevision` to the published revision as a fresh-client floor. Distribute the public key and
configuration through a trusted channel, not by trusting keys advertised by the catalog endpoint. The
official catalog stays enabled unless clients explicitly set `ProviderPacks:OfficialEnabled` to false.
See the signing guidance below for key creation, rotation, expiry renewal, and revision continuity.

## Sign and publish an updatable catalog

Named sources support three explicit trust modes: `sha256` (the compatible default), `unsigned`, and
`signed`. See [source configuration and trust policies](../docs/_docs/provider-packs.md#additional-trusted-catalogs).
`unsigned` explicitly trusts the managed HTTPS endpoint or protected local snapshot without a document
pin or signature. `signed` lets clients retain a configured public key while administrators publish
authenticated updates. Archive hashes remain mandatory in every mode; installing by arbitrary URL or
package ID is never enabled by these settings.

Keep the private key on the publishing system, outside this repository and outside client installations.
Use a protected secret store and narrowly scoped publishing access. For a local encrypted PKCS#8 RSA
key, OpenSSL can create the key and export its public SubjectPublicKeyInfo PEM:

```powershell
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -aes-256-cbc -out C:\secure\catalog-signing-private.pem
openssl pkey -in C:\secure\catalog-signing-private.pem -pubout -out C:\deployment\corporate-catalog-public.pem
```

These commands prompt for the private-key passphrase. Do not put it on the command line or commit
keys or passphrases. See the OpenSSL [genpkey](https://docs.openssl.org/3.0/man1/openssl-genpkey/)
and [pkey](https://docs.openssl.org/3.3/man1/openssl-pkey/) documentation. Distribute only the public PEM
through your trusted client configuration channel, with a stable source ID and the derived key ID emitted by the publisher.

The publisher utility reads an encrypted PEM key and the process environment variable
`DUBURL_CATALOG_SIGNING_PASSWORD`. Inject that value from the publishing secret store; do not embed it
in scripts, arguments, source control, or logs. Prepare a new staging directory containing a valid
distribution catalog. For example, from the repository root with the .NET 10 runtime installed:

```powershell
try {
    dotnet run --project connectors/Signing/ConnectorCatalog.Sign.csproj -f net10.0 -- C:\staging\catalog.json C:\staging\catalog.json.sig C:\secure\catalog-signing-private.pem enterprise
} finally {
    Remove-Item Env:DUBURL_CATALOG_SIGNING_PASSWORD -ErrorAction SilentlyContinue
}
```

The utility derives the key ID and allocates a revision automatically using the same publication state
as the CLI. It defaults to no expiration. An optional fifth argument selects an ISO 8601 expiry,
`none`, or `default`; an optional sixth argument selects the persistent publisher state directory.
The key argument also accepts `-` for redirected encrypted PEM on stdin. The utility validates the
catalog metadata, signs its exact bytes and freshness metadata using RSA-PSS/SHA-256, and creates a new
signature file. It refuses an existing output file, rejects a past explicit expiry, and emits no key material
or underlying exception details. Hosts can instead use `ConnectorCatalogPublication.SignAsync` with a
host-owned RSA key for the same defaults and automatic revisions. `ConnectorCatalogSignature.Sign`
remains the low-level API for hosts that already manage explicit signed metadata.

Automatic revisions are per source ID, independent of signing key or output folder. Publisher state
defaults to `DubUrl/catalog-publication-revisions` beneath local application data, separate from client
provider stores. Override it with `--publisher-state <directory>`. Keep and back up this directory across
builds, renewals, machine migrations, and key rotation. Concurrent publishers must use the same state
directory on a filesystem with working file locks, or transfer its latest state when moving publishers.
The initial floor is current UTC Unix milliseconds; the saved high-water mark guarantees increasing
numbers even when the publisher clock moves backwards. Revisions are reserved before signing and
are never reused, so failed publication can leave harmless gaps. Corrupt, inaccessible, exhausted, or
unwritable state fails signing rather than silently resetting the counter. Deleting state loses this
guarantee; existing clients continue rejecting any revision below their saved high-water mark.

Verify the staged pair using the intended source/key configuration and a separate test provider store.
Publish immutable archives first, then switch both stable catalog and signature routes to the prepared
snapshot. A client fetching mismatched files during an update rejects that source safely and can retry
on its next catalog load; there is no fallback to unsigned loading. Keep the configured URLs stable so
clients need no configuration change for routine updates. Existing installed packs and healthy sources
remain usable if signing, hosting, or verification fails.

No expiration is the default. Choose an explicit timestamp when time-based freshness is needed, and
renew before that timestamp. Without expiration, clients still verify signatures and reject
revision rollback, but fresh clients cannot reject an old signature based on age alone. Persist
client revision state under the provider store's `catalog-revisions` directory. A fresh or reset client
cannot detect an older, still-valid catalog below the newest published revision unless administrators
provision an appropriate `MinimumRevision`. Protect this floor, system time, public keys, and local state.
For an intentional content rollback, republish the earlier content; automatic allocation gives it a new higher revision.

Rotate keys by distributing both public keys, publishing a higher revision using the new key, and then
removing the old public key. Preserve source IDs and revision state throughout rotation. Private keys
never go to clients; catalogs cannot establish or replace their own trust roots.
