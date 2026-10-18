[CmdletBinding()]
param(
    [string] $Project = '../DubUrl.Worker/DubUrl.Worker.csproj',
    [string] $Version = $(if ($env:GitVersion_SemVer) { $env:GitVersion_SemVer } else { '0.0.0-local' }),
    [string] $Framework = 'net10.0',
    [Parameter(Mandatory)]
    [string] $Runtime,
    [string] $Configuration = 'Release',
    [string] $OutputRoot = './bin/worker'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Project))
$OutputRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputRoot))
$PublishRoot = Join-Path $OutputRoot 'publish'
$ArchiveRoot = Join-Path $OutputRoot 'archives'
$StagingRoot = Join-Path $OutputRoot 'staging'
$BuildIdentity = 'DubUrl.Worker'
$CommandName = 'duburl-worker'

function Get-DeclaredList([string] $Path, [string] $PropertyName) {
    [xml] $document = Get-Content -LiteralPath $Path -Raw
    $nodes = @($document.SelectNodes("/Project/PropertyGroup/$PropertyName"))
    if ($nodes.Count -eq 0) { throw "Could not find $PropertyName in '$Path'." }
    return @($nodes[-1].InnerText.Split(';', [StringSplitOptions]::RemoveEmptyEntries))
}

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    Write-Host "> $Command $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command '$Command' failed with exit code $LASTEXITCODE." }
}

function Assert-File([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Description does not exist: $Path" }
}

function New-CleanDirectory([string] $Path) {
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $Project -PathType Leaf)) { throw "Worker project '$Project' does not exist." }
$frameworks = Get-DeclaredList (Join-Path (Split-Path -Parent $PSScriptRoot) 'Directory.Build.props') 'TargetFrameworks'
$runtimes = Get-DeclaredList $Project 'RuntimeIdentifiers'
if ($Framework -notin $frameworks) { throw "Unsupported target framework '$Framework'." }
if ($Runtime -notin $runtimes) { throw "Unsupported runtime identifier '$Runtime'." }

$publish = Join-Path $PublishRoot "$Framework/$Runtime"
New-CleanDirectory $publish
Invoke-Checked dotnet @('restore', $Project, '--locked-mode')
Invoke-Checked dotnet @(
    'publish', $Project, '--configuration', $Configuration, '--framework', $Framework,
    '--runtime', $Runtime, '--output', $publish, '--self-contained', 'true', '--no-restore',
    '-p:PublishSingleFile=true', '-p:PublishTrimmed=false', "-p:Version=$Version"
)

$sourceName = if ($Runtime.StartsWith('win-')) { "$BuildIdentity.exe" } else { $BuildIdentity }
$commandName = if ($Runtime.StartsWith('win-')) { "$CommandName.exe" } else { $CommandName }
Assert-File (Join-Path $publish $sourceName) 'Published worker executable'

$stage = Join-Path $StagingRoot "$Framework-$Runtime"
New-CleanDirectory $stage
Copy-Item -Path (Join-Path $publish '*') -Destination $stage -Recurse -Force
Move-Item -LiteralPath (Join-Path $stage $sourceName) -Destination (Join-Path $stage $commandName) -Force
if (-not $Runtime.StartsWith('win-') -and -not $IsWindows) {
    Invoke-Checked chmod @('+x', (Join-Path $stage $commandName))
}

New-Item -ItemType Directory -Path $ArchiveRoot -Force | Out-Null
$baseName = "duburl-worker-$Version-$Framework-$Runtime".ToLowerInvariant()
if ($Runtime.StartsWith('win-')) {
    $archive = Join-Path $ArchiveRoot "$baseName.zip"
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -CompressionLevel Optimal
}
else {
    $archive = Join-Path $ArchiveRoot "$baseName.tar.gz"
    Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
    Invoke-Checked tar @('-czf', $archive, '-C', $stage, '.')
}
Assert-File $archive 'Worker distribution archive'

Remove-Item -LiteralPath $StagingRoot -Recurse -Force
Write-Host "Worker distribution created: $archive" -ForegroundColor Green
