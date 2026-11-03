# SingleStore schema provider contracts

The MySQL provider package owns SingleStore SQL and catalog queries. Core contains
only provider-neutral hook and metadata types; it has no dependency on Schema models.

## Schema consumer follow-up

This DubUrl change does not update the separate DubUrl.Schema repository. Its
consumer must implement the following before the new physical choices and inline
table descriptions are available through Schema builders/documents:

- Detect `ITableDescriptionSqlRenderer`. Append `RenderTableDescription` after
  the closing CREATE TABLE parenthesis and before the terminator. Do not also
  call `RenderComment` for that table. The latter rejects the non-persisting
  SingleStore ALTER TABLE path. Keep inline column comments.
- Add structured storage, shard-key, sort-key, and index-structure model/builder
  options, with an explicit document-contract compatibility decision.
- Resolve all inline/table primary keys, unique keys, and unique indexes into
  `TableDefinitionContext`. Call `ITableDefinitionSqlRenderer` before rendering
  the table. Use its CREATE prefix and append its definitions inside parentheses.
  Omit the consumed portable key/index definitions, including inline column keys.
- Unique indexes belong in `TableDefinitionContext.Indexes` at table creation;
  SingleStore cannot add them online. Ordinary indexes may use the complete
  `RenderCreateIndex` statement. Never silently discard direction or fall back
  to portable templates when physical options were supplied.
- Replace the affected SingleStore expected-limit assertions with positive cases,
  retaining native restrictions. Cover shared inspect/filter/strict/partial and
  render/deploy/reinspect behavior. Coordinate UNIQUE mapping with Schema #2 and
  database identity/filter semantics with Schema #3.

Native CHAR/VARCHAR aliases require a bounded length; CHAR can be stored as
VARCHAR by SingleStore. They do not enable foreign-key enforcement or bypass
shard-key rules. Logical type inference stays separate from explicit aliases.
Stored generation uses `AS (...) PERSISTED <type>`; virtual generation is rejected.

## Physical choices and restrictions

All option inputs are enums and literal identifier lists, not raw SQL. Rowstore
uses skip-list indexes (`USING BTREE`) for ordering. Columnstore uses hash indexes
and an explicit SORT KEY (empty for unordered storage). Default storage is rendered
as columnstore with an explicit sort key, independent of the session default.

Every enforced primary/unique key must contain the nonempty shard key. An omitted
shard key uses the primary key, when present; keyless tables cannot enforce unique
keys. The supported columnstore subset permits one primary/unique key. Choose
rowstore for multiple unique keys. Rowstore rejects SORT KEY and columnstore
rejects ordered secondary indexes. Hash indexes reject any requested ASC/DESC.

The reader registers only for SingleStore, queries visible user catalogs without
DDL/DML, and borrows the open connection. Catalog database identities remain
`MetadataName.Database`. Filters, dependency closure and strict/partial diagnostics
remain the shared inspector's responsibility. Foreign keys/checks are unsupported.
Unsigned types, nonportable expressions and specialized indexes are not fabricated
as portable equivalents. Unknown index direction remains unsupported; collation
names are never interpreted as ASC. Physical shard/storage/sort metadata is not yet
round-tripped by the shared metadata model and requires the Schema #5 follow-up.

## Validation decisions

| Dimension | Evidence and decision |
| --- | --- |
| Build targets | Existing net8.0/net9.0/net10.0 provider targets retained. |
| Real database tests | Issues require .NET 10 only; SingleStore QA workflow narrowed accordingly. |
| Runner and engine | Existing Ubuntu 24.04 x64 runner and pinned SingleStore 9.1.1 Docker image retained. |
| Distribution/package | Existing portable managed provider and QA bundle; no new RID, libc, architecture, package identity or version policy. |
| Prerequisites | Existing Compose readiness, fixture initialization and bundle validation retained. |
| Publishing | No release triggers, destinations or permission changes. |

Provider QA verifies duplicate rejection, native catalog facets/defaults/generation,
connection ownership, table/column comment persistence including Unicode/apostrophe/
CRLF/backslash, computed insert/update/null behavior, and native SHOW CREATE TABLE
index/sort direction. Shared Schema QA is deferred to its repository. Run database
QA only on .NET 10, for example:

```powershell
./QA/DubUrl.QA/SingleStore/deploy-singlestore-test-env.ps1 -frameworks @('net10.0')
```

References: [CREATE TABLE](https://docs.singlestore.com/db/v9.0/reference/sql-reference/data-definition-language-ddl/create-table/)
and [persisted computed columns](https://docs.singlestore.com/db/v7.6/create-a-database/using-persistent-computed-columns/).
