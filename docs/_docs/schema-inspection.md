---
title: Inspect and export database schemas
---

Schema inspection reads catalogs into the existing schema domain model. The same application service backs the CLI and JSON-RPC worker. Discovery is independent of SQL rendering: a dialect can render schemas without a registered discovery reader.

## CLI workflow

Add `--read-only` to require the shared native read-only access mode on supported file routes.
Unsupported routes fail before inspection; `--allow-partial` cannot relax this requirement. See
[read-only connections]({{ '/docs/read-only-connections/' | relative_url }}) for the provider matrix
and the difference from transaction defaults or read-replica routing.

Use a named connection from the existing ConnectionStrings configuration, or an environment variable containing the URL. Specify exactly one selector; URLs are not command-line arguments.

```powershell
duburl schema inspect --connection-env DATABASE_URL --table public.customer --output schema.yaml
duburl schema inspect --connection source --table public.customer --table public.orders --include-references --output schema.json
duburl schema inspect --connection source --basic --output-format json
duburl schema validate schema.yaml
duburl schema render schema.yaml --dialect pgsql --output schema.sql
# Optional: deploy only after reviewing the generated SQL.
duburl schema deploy schema.yaml --connection destination
```

Omitting --table selects all visible user tables. Repeat --table or --schema for multiple literal identifiers. Table identifiers support up to four components (catalog.database.schema.name); quote components containing dots or spaces with SQL double quotes, square brackets, or backticks. Comparisons are ordinal and case sensitive. No user identifier is inserted into a catalog SQL query. Readers collect visible metadata and the inspector applies filters to structured identities.

The default inspection requests every feature. --basic requests tables, columns, primary keys and foreign keys only. --include-references includes the transitive closure of visible foreign-key target tables, even outside the filters. Without it, keys targeting excluded tables are omitted with SDI005 and the result is incomplete. Unavailable targets also produce incomplete results. Missing selected tables produce SDI004; absence cannot distinguish a nonexistent object from one hidden by catalog visibility.

--output writes only the document to a file; otherwise only document bytes go to stdout. --output-format yaml|json takes precedence over the filename. Without it, .yaml/.yml/.json determine the file format; unknown extensions are rejected. Stdout defaults to YAML. Diagnostics always go to stderr.

Strict export is the default: incomplete inspection does not write a document. --allow-partial deliberately permits an incomplete export and still prints diagnostics and returns exit code 7. Existing output files are untouched when discovery or conversion fails. Exit codes are 0 for complete export, 2 for invalid input, 7 for incomplete inspection, 8 for unsupported discovery, 9 for inspection/export failure, and 130 for cancellation. A failed strict request containing an unsupported requested feature returns 8; other incomplete strict requests return 7.

A document has no discovery-completeness assertion. Retain application/worker results and diagnostics alongside exported documents. A partial document must not be treated as a complete database specification.

## Public extension contracts

Core contains DubUrl.Discovery metadata records and ISchemaMetadataReader. It references neither DubUrl.Schema nor concrete drivers. A custom provider can implement the reader and attach new SchemaDiscoveryCapability(reader) through ProviderRegistration.WithCapabilities, or annotate its mapper with SchemaDiscovery(typeof(MyReader)). Attributes apply to the explicit mapper only; sharing a driver/protocol does not opt other databases into catalog compatibility. Native connector packs load these same provider-owned readers.

The reader receives an open, borrowed DbConnection, options, and cancellation token. It must issue only metadata reads and must not close/dispose the connection. Capabilities describes support before inspection; SchemaMetadata.Coverage and Diagnostics describe the actual result. ConfigureOwnedConnection can restrict a closed, owned connection before opening it. SQLite uses this to force read-only mode, avoiding accidental creation of missing database files.

SchemaInspector owns public inspection and mapping. Its ConnectionUrl overload creates/opens/disposes the connection on success, failure and cancellation. Its DbConnection overload requires an already-open connection and leaves it owned by the caller. ISchemaInspectionService/SchemaInspectionService adds domain-to-v1 document conversion. Both overloads propagate cancellation. Provider exception text is replaced with stable, credential-free diagnostics; provider extension authors must keep their own returned metadata diagnostics free of secrets.

MetadataCoverage distinguishes Available, Unsupported, Inaccessible, Absent and Unrepresentable. Only available/absent feature coverage with no loss diagnostics is complete. Column and key order is preserved; tables, indexes, constraints, and views are ordered deterministically using ordinal identities/names. Unknown logical types are rejected instead of guessed.

Stable inspection codes:

| Code | Meaning |
|---|---|
| SDI001 | Provider has no discovery capability |
| SDI002 | Metadata or connection is inaccessible |
| SDI003 | Requested feature has incomplete coverage |
| SDI004 | Selected table is missing or invisible |
| SDI005 | Foreign-key target lies outside the selected/visible model |
| SDI006 | Named table unique constraint is not representable |
| SDI007 | Metadata cannot form a valid domain schema |
| SDI008 | View definition is not a portable projection |
| SDI009 | Catalog construct is not representable |
| SDI010 | Domain schema cannot be exported losslessly to v1 |

Inspection runs several catalog queries and does not establish a transaction or a database-wide snapshot. Concurrent DDL may cause inconsistent results and validation failure. For consistent exports, inspect a stable database or supply a borrowed connection with an appropriate caller-managed snapshot/transaction environment. Catalog permission filtering can conceal objects entirely; completeness is relative to visible objects and requested features, not proof of unrestricted database access.

## Provider coverage and limits

| Provider | Initial coverage | Additional metadata and limits |
|---|---|---|
| SQL Server | User tables, ordered columns/facets/nullability, primary/foreign keys | Literal defaults, identity seed/increment, simple generated expressions, ordered B-tree indexes, simple comparison checks, MS_Description comments, view inventory/simple projections. Filtered/included/specialized indexes are diagnosed. |
| PostgreSQL | Ordinary/partitioned user tables, ordered columns/facets/nullability, primary/foreign keys | Literal defaults, identity seed/increment, simple generated expressions, B-tree indexes, simple comparison checks, comments, view inventory/simple projections. Expression/partial/included/non-B-tree indexes are diagnosed. Other databases using Npgsql are not automatically qualified. |
| DuckDB | Current database user tables, ordered columns/facets/nullability, primary/foreign keys | Literal defaults, dedicated sequence-backed identities, representable virtual generated expressions, ordinary/unique secondary indexes in column order, simple checks, comments, and view inventory/simple projections. DuckDB does not retain index ASC/DESC directions; discovery leaves direction unspecified. Foreign keys render inline with dependency-ordered tables; cycles are rejected. |
| SQLite | main database user tables, ordered columns/declared facets/nullability, primary/foreign keys | Literal defaults, ordinary indexes/directions, view inventory/simple projections. PRAGMA metadata cannot recover generated expressions, check definitions, declared constraint names, comments or all rowid/autoincrement semantics. Foreign-key names are deterministic synthesized names. Attached databases are outside this reader's scope. |

Named table unique constraints are retained in neutral metadata, diagnosed during domain mapping, and never duplicated as backing indexes. The domain has no corresponding named table constraint. Expression/partial indexes, unsupported types, arbitrary SQL expressions and arbitrary views are diagnosed, never inserted as trusted portable DDL. Literal expressions and a narrow column/function expression subset are recognized; this is not a general SQL parser. Portable views are limited to simple single-source projections with optional AS aliases. Unsupported views retain identity/column inventory in SchemaInspectionResult.ViewInventory.

Native types are validated through NativeDatabaseType and remain dialect scoped when discovered as native overrides (for example PostgreSQL JSONB). v1 cannot encode explicit native dialect scopes: export rejects such domain schemas with SDI010 instead of widening their scope or changing the document contract. Logical mappings of ordinary scalar types are portable approximations; source vendor storage details beyond the model are not a promise of identical physical DDL. SQL Server unbounded column facets are diagnosed.

Use least-privilege metadata visibility for inspection: SQL Server catalog visibility/VIEW DEFINITION as appropriate, PostgreSQL catalog and information_schema visibility for the selected objects, DuckDB local catalog access, or SQLite read permission. Fixture creation/deployment requires extra DDL permissions only in disposable QA databases.

## Offline export

```csharp
var document = new SchemaDocumentExporter().Map(schema);
using var stream = File.Create("schema.json");
new SchemaDocumentWriter().Write(stream, document, SchemaDocumentFormat.Json);
```

The writer leaves the stream open, emits deterministic UTF-8 without timestamps or connection state, validates v1 semantics before writing, and supports JSON/YAML. Export rejects unsupported custom constraints, expressions, column subclasses, literal types and native scopes instead of discarding them. Schema-only consumers do not acquire a serializer dependency; writing lives in DubUrl.Schema.Documents.

## Worker example

Configure ConnectionStrings:source on the worker, then send:

```json
{"jsonrpc":"2.0","id":1,"method":"schema.inspect","params":{"connection":"source","tables":[{"schema":"public","name":"customer"}],"includeReferences":true,"basic":true}}
```

The result includes success, document, isComplete, coverage and diagnostics. allowPartial defaults to false; schemas supplies literal schema-name filters. Only configured connection references are accepted. The worker's existing request cancellation/timeouts apply. worker.capabilities advertises schemaDiscovery separately from rendering/deployment. Serialize the document with SchemaDocumentWriter when saving a standalone file.

## Qualification

Embedded tests exercise DuckDB.NET 1.5.6 and Microsoft.Data.Sqlite 10.0.x. Provider-owned SQL Server/PostgreSQL Discovery QA fixtures require DUBURL_SQLSERVER_DISCOVERY_QA_CONNECTION_STRING or DUBURL_POSTGRESQL_DISCOVERY_QA_CONNECTION_STRING pointing to disposable databases. They create uniquely named schemas, assert independently authored expectations, and inspect/render/deploy/reinspect a supported subset. Rich fixtures exercise defaults, identity/generated values, indexes, constraints and views. Driver versions are Microsoft.Data.SqlClient 7.1.1 and Npgsql 10.0.3. Live server versions must be recorded by the environment executing those fixtures; none are claimed without a live run. Shared release-matrix orchestration remains owned by issue #1579.

## Why literal defaults have limited coverage

The domain's Column.DefaultValue stores a typed constant. Catalog defaults are SQL expressions, so discovery recognizes only a conservative literal subset: strings, numbers, booleans, NULL and selected known catalog casts. CURRENT_TIMESTAMP, nextval(...), arbitrary function calls and arithmetic are not literal values. Discovery never evaluates them to manufacture a constant, because that would change their behavior. Some typed literals and vendor-specific CAST wrappers are also outside the recognizer. They produce incomplete-result diagnostics rather than being silently dropped.

Generated columns have a separate structured expression model. Discovery currently recognizes column references, literals, and nested Length/Upper/Lower calls. Operators such as amount * 2 remain unrepresentable by the current expression contract.

## DuckDB identities, generated columns and indexes

Verified with DuckDB.NET / engine 1.5.6:

- DuckDB rejects native GENERATED ALWAYS AS IDENTITY. DubUrl renders portable identities with a deterministic, dedicated CREATE SEQUENCE and a DEFAULT nextval(...) reference. Seed and positive/negative increment are preserved. DropIfExists recreates the generated sequence after dropping the table. Sequence names are derived from the table/column identity within the table's namespace; the document does not preserve source sequence names or current counter state.
- Discovery recognizes a direct nextval('qualified-sequence') default on an integer column when catalog dependencies establish exclusive column/table use, the sequence does not cycle, and its bounds are representable. Shared sequences, cycling/custom bounds, ambiguous references and expressions around nextval are diagnosed instead of flattened into independent identities. Inspection does not advance sequences.
- Virtual generated expressions are read from canonical table DDL because information_schema's generation fields are empty and column_default also contains generated expressions. Those expressions are not mistaken for ordinary defaults. Stored generation is unsupported by this DuckDB engine.
- Ordinary and unique secondary index names, owning tables, and column declaration order are discovered. Primary/unique-constraint backing indexes remain separate. Expression or specialized index forms are diagnosed.
- DuckDB accepts ASC/DESC in CREATE INDEX but discards directions in its catalog. Discovery preserves column order and sets direction to unspecified; it cannot recover discarded declarations. Explicit model directions can be emitted in SQL, but DuckDB does not retain them.
- CREATE INDEX uses an unqualified index name with a qualified ON table; DuckDB derives the index namespace from its table. Conflicting explicit index/table namespaces are rejected.

Reference: [DuckDB generated columns](https://duckdb.org/docs/current/sql/statements/create_table), [sequences](https://duckdb.org/docs/current/sql/statements/create_sequence), and [catalog functions](https://duckdb.org/docs/current/sql/meta/duckdb_table_functions).
