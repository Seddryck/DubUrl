# Schema extraction validation (2026-11-01)

## Passed locally

- `DubUrlCi.slnf` Release build for net8.0/net9.0/net10.0; locked restore against published Schema 0.55.1 packages. The package pin matches extraction source commit 7606da8d030c869dab0dd9d9946dcdd962dfe000.
- `DubUrl.Application.Testing` net10.0: 163 passed.
- `DubUrl.Cli.Testing` net10.0: 312 passed, 18 existing platform/environment-dependent skips, using an isolated process-local provider store.
- `DubUrl.Worker.Testing` net10.0: 27 passed.
- `DubUrl.Core.Testing` net10.0 filtered to `FullyQualifiedName~DuckDbDiscoveryTests`: 13 passed.
- Six documentation-import regression tests and eight existing download-page JavaScript tests.
- Assembled single Jekyll site built in an isolated Ruby 3.3 container. All 10 imported Schema page URLs and local links plus the stable specification URL validated.
- Existing documentation example verifier: 31 snippet groups compiled, 26 executed.
- Workflow syntax checked with `actionlint` and changed files checked with `git diff --check`.
- Extracted repository: both unit suites on all three TFMs, all seven real database jobs on net10.0, NuGet/package-consumer validation and documentation producer checks. See its `build/VALIDATION.md`.

## Not run

- The complete repository test suite: core outside the focused discovery filter, OleDb, Adomd, Adbc, extensions, bulk-copy, authentication fixtures and complete provider suites.
- Main application/CLI/worker tests on net8.0/net9.0; their assemblies were built.
- Hosted CI, site deployment and publication of the extracted release. The consuming PR must remain a draft until the release bundle exists and its actual source/checksum is pinned.

The first CLI run used installed user provider packs and failed due to duplicate registrations. Rerunning with `DUBURL_PROVIDER_STORE` set to an empty task-local directory passed; no user store or machine configuration was changed.
