#Requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Release')]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $InstallDirectory,

    [Parameter(Mandatory, ParameterSetName = 'PullRequest')]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $PullRequest,

    [Parameter(Position = 0, ParameterSetName = 'Release')]
    [ValidateNotNullOrEmpty()]
    [string] $Release = 'latest',

    [ValidatePattern('^[^/]+/[^/]+$')]
    [string] $Repository = 'Seddryck/DubUrl',

    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]*$')]
    [string] $ProductName = 'DubUrl',

    [ValidateNotNullOrEmpty()]
    [ValidatePattern('(?i)\.zip$')]
    [string] $ArchivePattern,

    [ValidateNotNullOrEmpty()]
    [string] $ArtifactName = 'cli-distribution-net10.0-win-x64',

    [ValidateNotNullOrEmpty()]
    [string] $Workflow = 'ci-release.yml',

    [ValidateNotNullOrEmpty()]
    [string[]] $ConfigurationFiles = @('appsettings.json'),

    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$filePrefix = $ProductName.ToLowerInvariant()
$executableName = "$filePrefix.exe"
$configurationNames = @("$filePrefix.config.json") + $ConfigurationFiles
if (-not $PSBoundParameters.ContainsKey('ArchivePattern')) {
    $ArchivePattern = "$filePrefix-*-net10.0-win-x64.zip"
}

function Assert-Command {
    param([Parameter(Mandatory)][string] $Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH."
    }
}

function Invoke-GhJson {
    param([Parameter(Mandatory)][string[]] $Arguments)

    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI failed: gh $($Arguments -join ' ')"
    }

    return $output | ConvertFrom-Json
}

function Get-ReleaseForCommit {
    param(
        [Parameter(Mandatory)][string] $CommitSha,
        [Parameter(Mandatory)][string] $Repo
    )

    $page = 1
    do {
        $releases = @(Invoke-GhJson @('api', "/repos/$Repo/releases?per_page=100&page=$page"))
        foreach ($candidate in $releases) {
            if ($candidate.draft) {
                continue
            }

            $encodedTag = [uri]::EscapeDataString([string] $candidate.tag_name)
            $tagCommit = Invoke-GhJson @('api', "/repos/$Repo/commits/$encodedTag")
            if ($tagCommit.sha -eq $CommitSha) {
                return [string] $candidate.tag_name
            }
        }

        $page++
    } while ($releases.Count -eq 100)

    throw "No published release tag points to commit $CommitSha."
}

function Save-ReleaseArchive {
    param(
        [Parameter(Mandatory)][string] $Tag,
        [Parameter(Mandatory)][string] $Repo,
        [Parameter(Mandatory)][string] $Destination
    )

    & gh release download $Tag --repo $Repo --pattern $archivePattern --dir $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "Could not download archive matching $ArchivePattern from release '$Tag'."
    }
}

function Save-PullRequestArchive {
    param(
        [Parameter(Mandatory)] $PullRequestData,
        [Parameter(Mandatory)][string] $Repo,
        [Parameter(Mandatory)][string] $Destination
    )

    $headSha = [string] $PullRequestData.head.sha
    $runs = Invoke-GhJson @('api', "/repos/$Repo/actions/workflows/$([uri]::EscapeDataString($Workflow))/runs?event=pull_request&head_sha=$headSha&status=success&per_page=100")
    $run = @($runs.workflow_runs | Where-Object { $_.conclusion -eq 'success' } | Sort-Object created_at -Descending)[0]
    if (-not $run) {
        throw "No successful CI workflow run exists for open PR #$($PullRequestData.number) at $headSha."
    }

    $artifacts = Invoke-GhJson @('api', "/repos/$Repo/actions/runs/$($run.id)/artifacts?per_page=100")
    $artifact = @($artifacts.artifacts | Where-Object {
        -not $_.expired -and $_.name -ceq $ArtifactName
    } | Sort-Object created_at -Descending)[0]
    if (-not $artifact) {
        throw "Workflow run $($run.id) has no unexpired artifact named $ArtifactName."
    }

    Write-Host "Downloading artifact '$($artifact.name)' from workflow run $($run.id)."
    & gh run download $run.id --repo $Repo --name $artifact.name --dir $Destination
    if ($LASTEXITCODE -ne 0) {
        throw "Could not download artifact '$($artifact.name)' from workflow run $($run.id)."
    }
}

Assert-Command 'gh'

$InstallDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($InstallDirectory)
if (Test-Path -LiteralPath $InstallDirectory) {
    $existingItems = @(Get-ChildItem -LiteralPath $InstallDirectory -Force)
    if ($existingItems.Count -ne 0 -and -not $Force) {
        throw "Install directory '$InstallDirectory' must be empty. Pass -Force to allow files from the archive to overwrite same-named files."
    }
}
else {
    New-Item -ItemType Directory -Path $InstallDirectory | Out-Null
}

$downloadDirectory = Join-Path ([IO.Path]::GetTempPath()) "$filePrefix/install-local/$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null

try {
    if ($PSCmdlet.ParameterSetName -eq 'PullRequest') {
        $pr = Invoke-GhJson @('api', "/repos/$Repository/pulls/$PullRequest")
        if ($pr.state -eq 'open') {
            Save-PullRequestArchive -PullRequestData $pr -Repo $Repository -Destination $downloadDirectory
        }
        elseif ($pr.merged_at) {
            $tag = Get-ReleaseForCommit -CommitSha ([string] $pr.merge_commit_sha) -Repo $Repository
            Write-Host "PR #$PullRequest was merged as $($pr.merge_commit_sha); downloading release '$tag'."
            Save-ReleaseArchive -Tag $tag -Repo $Repository -Destination $downloadDirectory
        }
        else {
            throw "PR #$PullRequest is closed without being merged, so it has no corresponding release."
        }
    }
    else {
        if ($Release -eq 'latest') {
            $latest = Invoke-GhJson @('api', "/repos/$Repository/releases/latest")
            $tag = [string] $latest.tag_name
        }
        else {
            $tag = $Release
        }

        Write-Host "Downloading portable CLI from release '$tag'."
        Save-ReleaseArchive -Tag $tag -Repo $Repository -Destination $downloadDirectory
    }

    $archives = @(Get-ChildItem -Path $downloadDirectory -Filter $archivePattern -File -Recurse)
    if ($archives.Count -ne 1) {
        throw "Expected exactly one downloaded archive matching $ArchivePattern, but found $($archives.Count)."
    }

    Write-Host "Extracting '$($archives[0].FullName)' into '$InstallDirectory'."
    $stagingDirectory = Join-Path $downloadDirectory 'staged'
    Expand-Archive -LiteralPath $archives[0].FullName -DestinationPath $stagingDirectory
    $installRoot = [IO.Path]::GetFullPath($InstallDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    foreach ($file in Get-ChildItem -LiteralPath $stagingDirectory -File -Recurse) {
        $relativePath = [IO.Path]::GetRelativePath($stagingDirectory, $file.FullName)
        $destination = [IO.Path]::GetFullPath((Join-Path $InstallDirectory $relativePath))
        if (-not $destination.StartsWith($installRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Archive destination is outside the installation directory: $destination"
        }

        # User preferences must survive every installation, including -Force.
        if ($file.Name -in $configurationNames -and (Test-Path -LiteralPath $destination)) {
            Write-Host "Preserving existing configuration '$destination'."
            continue
        }

        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force:$Force
    }

    $executables = @(Get-ChildItem -LiteralPath $InstallDirectory -Filter $executableName -File -Recurse)
    if ($executables.Count -ne 1) {
        throw "Expected exactly one $executableName in '$InstallDirectory', but found $($executables.Count)."
    }

    Write-Host "Installed the $ProductName CLI at '$($executables[0].FullName)'."
}
finally {
    $resolvedDownload = [IO.Path]::GetFullPath($downloadDirectory)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedDownload.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Download cleanup path is outside temporary directory.'
    }
    Remove-Item -LiteralPath $resolvedDownload -Recurse -Force -ErrorAction SilentlyContinue
}
