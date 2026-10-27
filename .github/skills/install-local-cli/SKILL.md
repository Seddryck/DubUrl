---
name: install-local-cli
description: Install the portable DubUrl CLI locally from a GitHub pull request or release into a user-specified directory. Use for testing downloaded CLI distributions, not building from source or running the Windows installer.
---

# Install DubUrl locally

Use `scripts/install-local.ps1` with PowerShell 7 and authenticated GitHub CLI access (`gh auth status -h github.com`). Defaults target `Seddryck/DubUrl` and the .NET 10 Windows x64 ZIP distribution.

## Select the source

Run from this skill directory, or invoke the script by its full path.

- Open PR: `./scripts/install-local.ps1 -InstallDirectory <path> -PullRequest <number>` selects the newest successful `ci-release.yml` pull-request run for the head SHA and downloads `cli-distribution-net10.0-win-x64`.
- Merged PR: the same command resolves a published release whose tag points to the merge commit. Closed, unmerged PRs are not installable.
- Named release: `./scripts/install-local.ps1 -InstallDirectory <path> -Release <tag>`.
- Latest release: `./scripts/install-local.ps1 -InstallDirectory <path>`.

The install directory is mandatory and must be empty or absent unless the user explicitly wants to reuse it with `-Force`. Forced installation may overwrite archive files while retaining unrelated files.

DubUrl loads `appsettings.json` (see `DubUrl.Cli/CliComposition.cs`). Preserve existing `appsettings.json` and `duburl.config.json` byte-for-byte, including with `-Force`. Copy packaged configuration defaults only when the destination file does not exist. Do not delete or reset user configuration.

The script downloads exactly one matching ZIP, stages it in a unique temporary directory, copies its files into the destination, and verifies that exactly one `duburl.exe` exists. It removes the temporary download directory afterward. Report the selected PR run or release tag and installed executable path. If no successful run, unexpired artifact, or commit-matching release exists, explain that condition without silently choosing another source.

## Parameters and casing

Keep this repository's skill invocations about DubUrl. The script supports reuse through independent parameters:

| Parameter | Default / behavior |
|---|---|
| `-ProductName` | `DubUrl`; derives lowercase `duburl.exe`, `duburl.config.json`, and the default archive prefix |
| `-Repository` | `Seddryck/DubUrl`; preserve supplied casing independently of product filenames |
| `-ArchivePattern` | `duburl-*-net10.0-win-x64.zip`; accepts another ZIP filename pattern |
| `-ArtifactName` | `cli-distribution-net10.0-win-x64`; exact PR artifact name |
| `-Workflow` | `ci-release.yml`; PR workflow filename |
| `-ConfigurationFiles` | `appsettings.json`; configuration basenames to preserve in addition to lowercase `<product>.config.json` |

For another DubUrl distribution, set the archive pattern and matching PR artifact together, for example `-ArchivePattern 'duburl-*-net9.0-win-x64.zip' -ArtifactName 'cli-distribution-net9.0-win-x64'`. Only ZIP distributions with a Windows executable are supported. Do not lowercase repository, workflow, or artifact identifiers.
