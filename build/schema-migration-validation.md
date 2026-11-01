# Schema extraction validation (2026-11-01)

## Passed locally

- `DubUrlCi.slnf` Release build for net8.0/net9.0/net10.0; locked restore against published Schema 0.55.1 packages. The package pin matches extraction source commit 7606da8d030c869dab0dd9d9946dcdd962dfe000.
- `DubUrl.Application.Testing` net10.0: 163 passed.
- `DubUrl.Cli.Testing` net10.0: 312 passed, 18 existing platform/environment-dependent skips, using an isolated process-local provider store.
- `DubUrl.Worker.Testing` net10.0: 27 passed.
- `DubUrl.Core.Testing` net10.0 filtered to `FullyQualifiedName~DuckDbDiscoveryTests`: 13 passed.
- Complete SingleStore provider QA on net8.0 passed after adding explicit Schema runtime dependencies to the shared provider QA project (the private CLI project reference does not propagate them).
- Six documentation-import regression tests and eight existing download-page JavaScript tests.
- Assembled single Jekyll site built in an isolated Ruby 3.3 container. All 10 imported Schema page URLs and local links plus the stable specification URL validated.
- Existing documentation example verifier: 31 snippet groups compiled, 26 executed.
- Workflow syntax checked with `actionlint` and changed files checked with `git diff --check`.
- Extracted repository: both unit suites on all three TFMs, all seven real database jobs on net10.0, NuGet/package-consumer validation and documentation producer checks. See its `build/VALIDATION.md`.

## Not run

- The complete repository test suite: core outside the focused discovery filter, OleDb, Adomd, Adbc, extensions, bulk-copy, authentication fixtures and complete provider suites except SingleStore net8.0.
- Main application/CLI/worker tests on net8.0/net9.0; their assemblies were built.
- Hosted CI, site deployment and publication of the extracted release. The consuming PR must remain a draft until the release bundle exists and its actual source/checksum is pinned.

The first CLI run used installed user provider packs and failed due to duplicate registrations. Rerunning with `DUBURL_PROVIDER_STORE` set to an empty task-local directory passed; no user store or machine configuration was changed.

## GitVersion follow-up

Schema now calculates releases from its inherited Conventional Commit/GitVersion configuration, starting at 0.100.0. Main pushes publish automatically; every package version is tagged and only zero-patch versions create a documentation release. Six real-history GitVersion tests, release guards, three-TFM versioned builds/packages/consumer smoke checks and documentation bundle checks passed in the Schema worktree. This metadata-only consumer update reran the six importer tests; unrelated application/database suites were not rerun.

## CI scope and package reuse follow-up

Schema's database jobs and embedded DuckDB cases now run only on .NET 10. Unit coverage remains on .NET 8/9/10 (Schema 176/176/181 cases; documents 127 each). The package job restores metadata and packs the tested matrix DLLs/PDBs with no build; provenance manifests and package payload hashes enforce reuse. Five artifact contract tests and a fresh-checkout packaging/consumer simulation passed, including deliberately invalid fixture source to prove no compilation occurs. External database and unrelated main-repository suites were not rerun for this CI-only change.


## Installed SDK/runtime reuse follow-up

Schema CI now reuses stable .NET 10 SDK/runtime installations, excludes previews and pins the detected SDK per job. It downloads only missing components; packaging requests only the extra .NET 8/9 consumer runtimes, preserving no-build package reuse. Eleven setup scenarios, script/workflow syntax checks and a full three-TFM Release build passed. Actual SDK downloads and domain/database suites were not repeated for this setup-only change.

## Codecov follow-up

Schema uploads the six canonical multi-target unit coverage reports in a separate Codecov job, using the same pinned action and GitHub OIDC authentication as DubUrl. The threshold check remains separate. Workflow lint, six-report selection and the existing coverage threshold passed against hosted artifacts. The consumer source/checksum pin now follows this Schema commit; importer tests were rerun. Application builds and database suites were not repeated for this workflow-only change.

## Shared QA helper rename

Schema's shared fixtures now live in the internal, non-packable DubUrl.Schema.QA project. Packaging transfers only the two published libraries. Five artifact tests, workflow lint, the three-TFM solution build, three live SQLite QA tests on .NET 10 and two-package validation passed. Other database/full unit suites were not rerun. The consumer pin follows the renamed project commit; six importer tests passed.

## Mandatory shared database baseline

All seven Schema QA providers now execute the shared baseline on .NET 10, with required per-provider TRX validation and retained feature tests. All seven live runs passed (27 tests, zero skips), including Oracle and MySQL baseline additions; containers/volumes were cleaned up. Five result guard tests, framework rejection, workflow/script syntax and the three-TFM solution build passed. The consumer pin follows this Schema commit; six importer tests passed. Full unit/document/package smoke suites were not repeated for this QA-only change.
