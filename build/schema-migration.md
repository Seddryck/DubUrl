# Schema repository integration

Schema implementation, schema document specifications, guides, and standalone database fixtures are maintained in https://github.com/Seddryck/DubUrl.Schema.

Application services, the CLI, the worker, provider implementations, and their integration tests stay here and consume pinned Schema packages (`SchemaPackageVersion` in `Directory.Build.props`). Provider discovery/renderer implementations stay here; standalone SchemaInspector and DDL fixtures move with Schema. Existing application-level encrypted DuckDB and discovery tests remain integration checks here.

The website stays here. Run `python build/import_schema_docs.py` before Jekyll or documentation example verification. It verifies the release checksum, version, source commit, per-file hashes, paths and collisions, then assembles `artifacts/docs-site`. No imported documentation is committed to this repository. Existing page permalinks and schema URLs are preserved. The front matter in imported pages supplies navigation to Just the Docs.

Update `docs/schema-docs.lock.json` through a reviewed PR after a Schema release. For local verification use `--bundle <schema-docs.zip>`; verification remains enabled. Preview an unreleased bundle using `--pin <temporary lock>` as well. Production never fetches the latest branch.

## First cutover

The initial package pin uses the already-published 0.55.1 baseline, so code builds do not depend on unpublished packages. Merge and release DubUrl.Schema 1.2.0 first for the documentation cutover. Verify its NuGet packages and ZIP exist, update the lock to the actual release manifest commit/checksum (a squash merge changes the source commit), update `SchemaPackageVersion` to 1.2.0 and regenerate CLI/worker lockfiles, then merge this consuming change. Do not merge the consuming PR while the release is unavailable. The previous DubUrl release remains usable throughout. Reverting the cutover commit restores the in-repository implementation if needed.

Schema and DubUrl versions are independent. The extraction baseline is DubUrl 0.55.1. When changing core dialect/metadata APIs, run the Schema compatibility workflow against candidate core/provider packages before merging the consuming change.
