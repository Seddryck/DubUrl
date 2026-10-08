---
name: dotnet-ci-release
description: Design or revise .NET CI, packaging, and release workflows from repository evidence, including TFM, runner, RID, architecture, libc, generated-asset, NuGet, and publishing decisions. Use when creating or reviewing a .NET delivery workflow; do not use for unrelated application implementation.
---

# .NET CI and release workflow

Design the workflow from the repository's declared support, implementation, dependencies, packaging model, and release policy. Preserve valid existing contracts unless the task explicitly changes them.

Do not begin by asking the user to enumerate platforms, frameworks, or architectures. Inspect the repository, infer the justified matrices, and present evidence-backed decisions. Ask only when a remaining choice is a product-policy decision that repository evidence cannot resolve.

## Expected pipeline

Organize the workflow into these phases when they apply:

1. calculate and validate versions;
2. prepare shared inputs and validate source metadata;
3. restore, build, and test;
4. package and validate immutable artifacts;
5. reserve or create release identities;
6. publish the already-tested artifacts;
7. create or update releases.

Keep build/test concerns distinct from distribution concerns. They commonly require different matrices.

## Inspect before deciding

Collect evidence from:

- workflows and reusable workflow components;
- solutions, projects, central build properties, package manifests, and lock files;
- `TargetFramework`, `TargetFrameworks`, `RuntimeIdentifier`, and `RuntimeIdentifiers` declarations;
- direct and transitive dependency graphs, including `runtimes/<rid>/native` and runtime-specific managed assets;
- conditional compilation, P/Invoke, native loading, OS checks, filesystem and path behavior, process execution, registry access, shell invocation, and environment-sensitive code;
- packaging, installer, container, signing, and generated-asset scripts;
- documentation listing supported systems, installation methods, and artifact names;
- current release assets and package identities;
- test projects, categories, framework coverage, and runner operating systems;
- version configuration, tags, release scripts, and publishing destinations.

Search both first-party source and dependencies. Absence of an obvious platform API in first-party source supports portability but does not prove dependencies are portable.

Report a decision ledger before or alongside the proposed workflow:

| Dimension | Repository evidence | Decision | Confidence or unresolved policy |
|---|---|---|---|
| Target frameworks | ... | ... | ... |
| Test operating systems | ... | ... | ... |
| Distribution runtimes | ... | ... | ... |
| libc variants | ... | ... | ... |
| Architectures | ... | ... | ... |
| Package identities | ... | ... | ... |
| Generated prerequisites | ... | ... | ... |
| Release identities | ... | ... | ... |
| Publishing destinations | ... | ... | ... |

State useful negative evidence, such as no native interop, no runtime-specific dependency assets, or no documented macOS support. Be precise: `System.IO` is cross-platform, while specific filesystem, permission, casing, symlink, and path semantics may still justify multi-OS tests.

## Classify deliverables

Classify each published output separately:

- portable managed library;
- runtime-specific managed library;
- framework-dependent executable or .NET tool;
- self-contained executable;
- Native AOT executable;
- installer;
- container image;
- extension or ecosystem package;
- documentation or generated metadata.

Do not apply an executable distribution matrix to a portable library merely because both are built in the same repository.

## Decide the target-framework matrix

Compile every publicly supported TFM. Run tests on every TFM when compilation, reference assemblies, conditional code, dependency resolution, API availability, or observable behavior can differ.

A managed multi-targeted library normally produces one NuGet package containing its TFM-specific assemblies. Do not create a package or semantic version per framework unless an established ecosystem or public contract requires it.

For applications, include the TFM in the distribution matrix only when users receive distinct framework-dependent artifacts and the product promises those choices. For self-contained or AOT applications, prefer one deliberately selected release TFM unless multiple TFM builds offer a documented benefit.

Run repository-wide checks such as metadata, documentation, and formatting once on an appropriate TFM unless their behavior is framework-dependent.

## Minimize SDK and runtime installation

Install only the tooling required by each isolated job. Do not mirror the complete TFM matrix in every SDK setup step.

- A build or test job scoped to one TFM should map that framework to its matching SDK in the matrix, install only that SDK and runtime, and scope restore to the selected TFM. Passing `--framework` to build or test does not scope an earlier unqualified restore.
- When packaging combines immutable binaries produced by framework-scoped build jobs, restore the complete package project once in the package job with one compatible SDK, then run `dotnet pack --no-build --no-restore`. Reuse the compiled binaries; do not treat framework-scoped `obj` state as a substitute for the package's full multi-target restore.
- A package or publish job does not need one SDK per packaged TFM merely because the package contains several TFM-specific assemblies.
- A job that performs a full multi-target restore or build may need all corresponding targeting packs or SDKs. Keep multiple SDKs there only when that work genuinely depends on them.
- Installing several SDKs does not make matrix entries use each SDK. Without `global.json` or another explicit selector, `dotnet` normally chooses the newest compatible installed SDK. When SDK-version compatibility itself must be validated, select the intended SDK explicitly in each matrix entry.

## Decide the runner operating-system matrix

Add native OS runners when tests must exercise differences that cross-publishing cannot validate, including:

- filesystem casing, separators, permissions, or symlinks;
- process or shell invocation;
- registry or platform services;
- native library loading;
- OS-specific cryptography, globalization, networking, or certificates;
- installers;
- platform-guarded code or platform-specific dependencies.

Do not add Windows, Linux, and macOS runners to a portable library solely for symmetry. Portable filesystem or path APIs alone do not mandate a complete OS matrix. When risk is plausible but limited, recommend a focused native smoke job instead of multiplying the whole suite.

A runner matrix and a distribution RID matrix are different. Cross-publishing can create multiple RIDs on one runner; it does not prove those artifacts execute correctly on their target systems.

## Decide RIDs, operating systems, and architectures

Add a RID only for a supported deliverable justified by a native executable or app host, self-contained or AOT publishing, runtime-specific package layout, native dependency, installer, documented portable distribution, or existing support contract.

Keep ecosystem vocabularies distinct:

| Concept | Examples |
|---|---|
| GitHub runner | `windows-latest`, `macos-latest`, `ubuntu-latest` |
| .NET RID | `win-x64`, `win-x86`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-musl-x64` |
| Node platform token | `win32`, `darwin`, `linux` |
| Architecture | `x64`, `x86`, `arm64` |

Normalize names to the target ecosystem. Do not create both `win-32` and `win-x86` entries for the same .NET target. Use explicit matrix `include` entries when not every combination is valid or useful.

### Windows

Use `win-x64` as the default Windows distribution RID when the product ships a Windows executable. Add `win-arm64` when documentation, runtime declarations, native assets, release history, or target users justify it and the installer and dependency chain support it.

Include `win-x86` only for an established 32-bit compatibility promise, x86 dependency, installer requirement, or evidenced user base. Do not include it merely because the SDK can emit it.

### macOS

Use `osx-x64` and `osx-arm64` for .NET artifacts; use `darwin` only in ecosystems where it is the canonical platform token.

Add macOS artifacts when direct macOS installation is promised or the product requires a macOS executable, native asset, signing flow, or archive. If support is absent from metadata, documentation, dependencies, and release history, report it as unsupported instead of asking an ungrounded yes/no question.

When cross-platform CLI intent is clear but policy is undocumented, recommend `osx-arm64` and add `osx-x64` only if Intel Macs remain supported. Identify that support commitment as the policy decision requiring confirmation.

### glibc and musl

Treat glibc and musl as distinct targets for native, self-contained, app-host, or runtime-specific Linux artifacts. Add `linux-musl-*` when Alpine or another musl environment is documented or otherwise supported. Do not add a musl dimension to a portable managed library without runtime-specific assets.

Do not assume a `linux-x64` executable works on Alpine.

## Decide package identities and versions

Use one release version for all artifacts built from the same source revision. Do not assign different semantic versions to framework or runtime variants.

Use distinct RID package IDs only when the ecosystem requires them, a pointer package selects them, payloads are incompatible across runtimes, or the public packaging contract already exposes them. A portable managed library should normally remain one multi-target package.

For a runtime-specific .NET tool, a valid design is:

- one pointer package;
- one package per supported RID;
- the same version across the complete set;
- validation that the pointer references exactly those RID packages;
- publication of the complete set as one release operation.

Fail before publishing if an expected package is missing, duplicated, misnamed, or contains the wrong TFM/RID layout.

Independently versioned deliverables may expose separate calculated versions when their lifecycle genuinely differs. Keep that exception explicit.

## Handle generated build prerequisites

Identify generated files required by compilation or packaging, such as icons, embedded resources, generated source, manifests, schemas, and version files.

Determine whether generation:

- creates an input required by another build or package;
- creates a published artifact of its own;
- embeds the release version in content;
- uses the version only for filenames or archive identity;
- depends on tools outside the primary .NET SDK.

Generate prerequisites before their earliest consumer. Do not treat them as release artifacts unless they are part of the published product.

When isolated jobs need identical generated files, choose deliberately between:

1. generate and validate once, then distribute an internal artifact; or
2. regenerate independently in each consuming job.

Prefer a shared artifact when generation is expensive, nondeterministic, security-sensitive, or depends on tightly pinned tooling. Independent regeneration is valid when it is deterministic, inexpensive, and preserves useful job independence.

If a generator also creates a published artifact, invoke it without a release version for ordinary build prerequisites when supported, and with the calculated version for the publishable artifact. Establish whether the version changes content or only artifact identity.

Set up or verify external tools explicitly. Do not rely silently on hosted-runner images containing image processors, archive utilities, package managers, installer compilers, or signing tools. Pin versions when their output affects reproducibility.

Validate generated outputs according to their consumers: required files, names, formats, tracked-asset drift, archive contents, and reproducibility where promised.

## Preserve valid existing workflow structure

Treat current TFMs, RIDs, package identities, release cadence, artifact formats, documentation, and release history as evidence of the product contract. Do not remove or expand them solely to match a generic template.

When a root `appveyor.yml` exists, allow the new workflow to build, test, and package, but block it before release identity reservation or publication. Put this explicit failing guard at the same release boundary as the main-branch or release-event check. The guard remains in place until `appveyor.yml` is removed so two delivery systems cannot publish the same release.

Compare repeated support declarations across project properties, script defaults, workflow matrices, package validators, installers, and documentation. Report disagreement as drift and choose one authoritative source before modifying the matrix.

A validation may run in the packaging job when it must inspect the generated artifact. It does not require a separate test job, but publication must depend on that validation succeeding.

Cross-publishing does not prove executability. Recommend native smoke tests when runtime behavior, native dependencies, archive permissions, or platform-sensitive APIs justify them; do not automatically require a full OS test matrix.

## Construct the workflow

### 1. Calculate versions

Fetch full history when versions depend on commits or tags. Calculate each version once and expose it as a job output. Validate destination syntax, tag availability, and queued-release collisions. All downstream jobs consume these outputs.

### 2. Prepare and validate

Set up SDKs and caches using project or lock files as cache keys. Run cheap source-level validation early. Generate shared inputs once when sharing is the deliberate strategy; otherwise make deterministic regeneration explicit.

### 3. Build and test

Restore deterministically, preferring locked mode for release inputs. Build before testing, then use `--no-build` and `--no-restore` or their equivalents. Matrix dimensions must correspond to compilation, dependency, or behavior differences. Give every entry a descriptive name and unique coverage identity.

Prefer a workflow-native matrix with direct build and test commands when the project, target-framework, runner, or architecture dimensions are stable and enumerable. Do not hide those dimensions in a wrapper script whose main purpose is nested iteration. A script remains appropriate for substantial reusable logic, but the workflow should still expose the dimensions that determine job isolation, status, retry scope, and artifact identity.

Select one coverage driver that matches the active test platform and do not mix drivers in the same test project:

- use `coverlet.collector` with `dotnet test --collect:"XPlat Code Coverage"` in VSTest mode;
- use `coverlet.MTP` for Microsoft Testing Platform and pass its extension arguments after the `--` separator, for example `dotnet test --project <project> -- --coverlet`;
- avoid `coverlet.msbuild` when the test host can be terminated before its process-exit hit-file flush, and never use it with MTP v2.

Inspect the test SDK and effective `dotnet test` mode rather than assuming compatibility from package presence. Make runner selection and framework-adapter activation explicit in repository configuration. Exercise the exact CI coverage command locally and fail when the expected report is absent, duplicated, or below its required threshold. When the selected driver does not enforce thresholds itself, validate the generated report explicitly.

When users need test failures and insufficient coverage to appear as different checks, do not enforce the coverage threshold inside the test command or matrix job. Let test jobs generate and retain reports, then evaluate those reports in a clearly named downstream threshold job. Keep coverage-service upload separate as well so a low-coverage result, a test failure, and an upload outage have distinct check conclusions.

### 4. Package

Package only after relevant validation succeeds. Create each distributable once and upload it under a stable, collision-free artifact name. Validate contents, executable names, manifests, archive formats, permissions, installers, and expected counts.

Remember that `dotnet pack` builds by default. Do not add an immediately preceding `dotnet build` followed by `dotnet pack --no-build` when the build exists only to feed that pack invocation. Prefer an explicit package matrix that restores each project, runs `dotnet pack --no-restore`, uploads one uniquely named artifact per entry, and validates the combined package set in a downstream job. Reuse a prior build only when its outputs are deliberately transferred as immutable artifacts and their version and configuration exactly match the package inputs.

### 5. Reserve release identities

Only on the release event, refresh remote tags and re-evaluate versions against the tested commit. Confirm the checkout identifies the commit whose artifacts were built and that the recalculated versions match the immutable packaged artifacts. Create tags only after every required package succeeds. Reuse a tag idempotently only when it identifies the expected commit, and handle a concurrent creator only when the remote tag resolves to that same commit.

Decide from repository evidence whether reservation belongs in its own job. A distinct version or tag job is justified when publication has multiple destinations, multiple independently versioned deliverables or tag namespaces, long or parallel packaging stages, queued releases, another tag-producing workflow, or a requirement for a visible version-integrity check. Keeping reservation inside one publish job is reasonable for a single version and destination when it intentionally provides sequential atomicity. Do not copy multi-version or concurrency logic into a simpler repository without corresponding evidence.

### 6. Publish

Download package-job artifacts; do not rebuild. Revalidate the complete artifact set before the first external publication. Use least-privilege permissions and trusted publishing where available. Pull requests and ordinary branch builds must not publish.

Treat each external destination—such as NuGet, a container registry, or a GitHub release—as a potential job boundary. Separate destinations when evidence shows different triggers, artifacts, credentials, permissions, retry behavior, release cadence, or a need for distinct check conclusions. Combining destinations is valid when the repository deliberately requires one sequential operation and accepts the combined permissions and retry scope.

When destinations are separated, choose their dependency relationship explicitly. Parallel jobs improve isolation and allow independent retries but can leave a partial release. Make an announcement or GitHub release depend on registry publication when users must not see a release before its installable package is available. Grant each job only its destination-specific permissions; for example, NuGet trusted publishing needs an identity token, while GitHub release or discussion creation needs repository-content or discussion permissions rather than that token.

### 7. Release

Create or update the release only after its tag exists. Upload validated artifacts with deterministic names. Encode any rule limiting release pages to selected semantic versions separately from package publication. Preserve the repository's evidenced relationship between registry publication and release-page creation; do not assume that every published package requires a release page or that both operations share the same cadence.

## Avoid matrix explosions

Do not multiply every dimension together automatically. Model compilation TFM, test runner OS, distribution TFM, distribution RID, installer availability, package format, and publishing destination independently.

Use explicit `include` records when installers exist only on Windows or the newest TFM, musl exists only on Linux, macOS has archives but no installer, libraries are tested per TFM but packed once, or invariant checks run once.

Every row must represent a meaningful supported contract, not merely a technically possible build.

## Present decisions without unguided questions

Lead with the inferred design and evidence. For example:

> The project produces a Windows executable and documents 64-bit Windows, so the distribution includes `win-x64`. No 32-bit support evidence was found, so `win-x86` is excluded.

For a genuine policy gap, give a bounded recommendation:

> The CLI is described as cross-platform, but metadata and release assets do not define macOS support. No Windows-only dependency was identified. Add `osx-arm64`; add `osx-x64` only if Intel Macs remain in the support policy. Confirm that policy before publishing new artifacts.

## Validate and report

Validate workflow syntax and repository-provided workflow checks. Exercise scripts or commands whose behavior changed when practical. Run focused validation before expensive suites.

Report:

- deliverable classifications;
- build/test and distribution matrices;
- excluded dimensions and supporting evidence;
- package identity and version strategy;
- generated prerequisites and external tools;
- release triggers and permission boundaries;
- artifact validation;
- unresolved product-policy decisions;
- validation run and intentionally not run.
