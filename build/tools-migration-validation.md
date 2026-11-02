# Tools extraction validation

Validation ran on Windows on 2 November 2026 in dedicated `feat/extract-tools` worktrees based on the fetched main branches. Candidate library packages were built into an ignored local feed; Tools restored those packages without library project references.

## Library repository

| Command/check | Result |
| --- | --- |
| `dotnet build DubUrl.sln -c Release -f net10.0 --no-restore` | Passed |
| `dotnet test DubUrl.Core.Testing -c Release -f net10.0 --no-build --no-restore --filter "FullyQualifiedName~Mapping\|FullyQualifiedName~Discovery\|FullyQualifiedName~Authentication"` | 511 passed, 12 prerequisite-gated skips; isolated default provider store |
| `dotnet test QA/DubUrl.Provider.Testing -c Release -f net10.0 --no-build --no-restore` | 20 passed |
| `dotnet test Providers/DuckDb/DubUrl.Provider.DuckDb.QA -c Release -f net10.0 --no-build --no-restore --filter "FullyQualifiedName~EncryptedConnectionTests"` | 33 passed |
| Pack retained production libraries, authentication projects and reusable QA project at `0.56.0`; `Validate-ReleaseArtifacts.ps1` | 27 NuGet packages and 27 symbol packages validated across net8.0/net9.0/net10.0 |
| `python build/test_tools_extraction.py` | 4 passed |
| `python build/test_schema_docs.py` | 9 passed |
| `python build/import_schema_docs.py`; `./build/verify-doc-examples.ps1` | 29 snippet groups compiled, 25 executed |
| `node --test .github/scripts/Test-DownloadPage.cjs` | 10 passed, including legacy download fallback and independent library versions |
| `Test-ReleaseAssets.ps1`; `Test-CliReleaseGuards.ps1` | Passed |
| actionlint for both changed workflows; PowerShell AST parsing of their embedded commands | Passed |

## Extracted Tools

The companion repository's `docs/validation.md` records exact commands. Application, CLI and worker suites passed on net8.0, net9.0 and net10.0, as did the shared-QA/SQLite consumer suite. Both seven-package tool families, the Application package, installed commands, Windows x64 archives and persistent worker JSON-RPC were checked.

## Intentionally not run

No full Core test suite, remote database/container QA, interactive OAuth/certificate acceptance, native credential-store/terminal opt-ins, Linux/macOS native execution, Windows Arm64 execution, Windows installer compilation, live NuGet publication or GitHub Release publication was run locally. Their applicable CI matrices and opt-in prerequisites remain explicit. The new Tools GitHub release environment is restricted to main; NuGet trusted-publisher policies must be configured before release.

Merge/publish the Core prerequisite first so the pinned `0.56.0` packages exist on NuGet.org before the Tools pull request is merged. The local candidate feed is validation input only and is not committed.

CI follow-up: the first package-validation run found 26 packages because DubUrl.BulkCopy was missing from the packaging matrix. Restored its matrix entry and added a package-matrix/validator inventory regression check, demonstrated failing before the fix and passing afterward. The 27-package/27-symbol validation and actionlint pass.
