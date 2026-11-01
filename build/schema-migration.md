# Schema repository integration

Schema implementation, schema document specifications, guides, and standalone database fixtures are maintained in https://github.com/Seddryck/DubUrl.Schema.

Application services, the CLI, the worker, provider implementations, and their integration tests stay here and consume versioned Schema packages (SchemaPackageVersion in Directory.Build.props). Provider discovery/renderer implementations stay here; standalone SchemaInspector and DDL fixtures move with Schema. Existing application-level encrypted DuckDB and discovery tests remain integration checks here.

## Single documentation website

The website stays here. Run `python build/import_schema_docs.py` before Jekyll or documentation example verification. By default it downloads schema-docs.zip from the latest published DubUrl.Schema GitHub release:

https://github.com/Seddryck/DubUrl.Schema/releases/latest/download/schema-docs.zip

Both CI documentation consumers use that default. There is no documentation version, source-commit or ZIP-checksum lock to update after a Schema release. The imported bundle's version and source commit are logged for traceability. NuGet dependency versions remain independent of this documentation policy.

The importer still verifies bundle format and metadata, per-file manifest hashes, archive paths, required specification content and content collisions, then assembles artifacts/docs-site. No imported documentation is committed here. Existing page permalinks, JSON specification URLs and front-matter navigation are preserved.

For local verification, use `--bundle <schema-docs.zip>` to import a local ZIP under the same structural/manifest checks. Choose a new staging directory with `--output <directory>` when an earlier output already exists.

## First cutover

The initial package baseline uses already-published 0.55.1 packages, so code builds do not require unpublished packages. Once DubUrl.Schema 0.100.0 is published, update SchemaPackageVersion and regenerate CLI/worker dependency lockfiles for the package cutover. Documentation downloads the latest published release automatically and does not require a matching package-version pin or a docs lock update.

Schema and DubUrl versions are independent. The extraction baseline is DubUrl 0.55.1. When changing core dialect/metadata APIs, run the Schema compatibility workflow against candidate core/provider packages before merging the consuming change.

Schema versions follow the same Conventional Commit/GitVersion configuration and automatic main-branch publishing as DubUrl. NuGet-only patch releases do not create GitHub documentation releases; the website consumes the latest published release that contains schema-docs.zip.
