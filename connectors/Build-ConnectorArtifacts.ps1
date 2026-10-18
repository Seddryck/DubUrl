[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [string] $OutputDirectory = '../.artifacts/connectors'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputDirectory))
$stagingRoot = Join-Path $outputRoot '.staging'
$publishRoot = Join-Path $stagingRoot 'publish'
$contentRoot = Join-Path $stagingRoot 'content'
$project = Join-Path $repositoryRoot 'DubUrl.Connector.PostgreSql.Npgsql/DubUrl.Connector.PostgreSql.Npgsql.csproj'
$moduleName = 'DubUrl.Connector.PostgreSql.Npgsql'
$connectorId = 'postgresql-npgsql'
$archiveName = "duburl-connector-$connectorId-$Version.zip".ToLowerInvariant()
$archivePath = Join-Path $outputRoot $archiveName
$catalogPath = Join-Path $outputRoot 'connector-catalog.json'
$epoch = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)

if ($Version -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:[-+].+)?$') {
    throw "Connector version '$Version' is not valid SemVer."
}
$apiRange = "[$($Matches.major).$($Matches.minor).$($Matches.patch),$([int]$Matches.major + 1).0.0)"

function New-CleanDirectory([string] $Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command '$Command' failed with exit code $LASTEXITCODE."
    }
}

function Get-Sha256([string] $Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Write-Utf8Json([string] $Path, [object] $Value) {
    $json = $Value | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText($Path, "$json`n", [Text.UTF8Encoding]::new($false))
}

New-CleanDirectory $outputRoot
New-Item -ItemType Directory -Path $publishRoot, $contentRoot -Force | Out-Null

Invoke-Checked dotnet @(
    'publish', $project,
    '--configuration', 'Release',
    '--framework', 'net8.0',
    '--no-self-contained',
    '--output', $publishRoot,
    '--nologo',
    '--disable-build-servers',
    '-m:1',
    "-p:Version=$Version",
    '-p:ContinuousIntegrationBuild=true'
)

$moduleFile = "$moduleName.dll"
$dependencyFile = "$moduleName.deps.json"
$requiredFiles = @(
    $moduleFile,
    $dependencyFile,
    'Npgsql.dll',
    'Microsoft.Extensions.Logging.Abstractions.dll',
    'Microsoft.Extensions.DependencyInjection.Abstractions.dll',
    'System.Diagnostics.DiagnosticSource.dll'
)
$packFiles = @(Get-ChildItem -LiteralPath $publishRoot -File | Where-Object { $_.Name -in $requiredFiles })
foreach ($required in $requiredFiles) {
    if ($packFiles.Name -notcontains $required) {
        throw "The PostgreSQL connector publish output is missing '$required'."
    }
}
foreach ($file in $packFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $contentRoot $file.Name)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'postgresql-npgsql/THIRD-PARTY-NOTICES.md') -Destination $contentRoot

$assets = @(
    Get-ChildItem -LiteralPath $contentRoot -File |
        Sort-Object Name |
        ForEach-Object {
            [ordered]@{
                path = $_.Name
                sha256 = Get-Sha256 $_.FullName
                kind = if ($_.Extension -eq '.md') { 'Content' } else { 'Managed' }
            }
        }
)

$manifest = [ordered]@{
    provider = $connectorId
    version = $Version
    dubUrlApiRange = $apiRange
    module = [ordered]@{
        assembly = $moduleFile
        type = 'DubUrl.Connector.PostgreSql.Npgsql.PostgreSqlNpgsqlProviderModule'
    }
    schemes = @('pg', 'pgx', 'pgsql', 'postgres', 'postgresql')
    targetFrameworks = @('net8.0', 'net9.0', 'net10.0')
    runtimeIdentifiers = @()
    assets = $assets
}
$manifestPath = Join-Path $stagingRoot 'provider-pack.json'
Write-Utf8Json $manifestPath $manifest

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
try {
    $entries = @(
        [PSCustomObject]@{ Source = $manifestPath; Destination = 'provider-pack.json' }
        foreach ($file in Get-ChildItem -LiteralPath $contentRoot -File | Sort-Object Name) {
            [PSCustomObject]@{ Source = $file.FullName; Destination = $file.Name }
        }
    )
    foreach ($item in $entries) {
        $entry = $archive.CreateEntry($item.Destination, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $epoch
        $source = [IO.File]::OpenRead($item.Source)
        $destination = $entry.Open()
        try { $source.CopyTo($destination) }
        finally { $destination.Dispose(); $source.Dispose() }
    }
}
finally {
    $archive.Dispose()
}

$catalog = [ordered]@{
    schemaVersion = 1
    entries = @(
        [ordered]@{
            provider = $connectorId
            version = $Version
            downloadUri = "https://github.com/Seddryck/DubUrl/releases/download/v$Version/$archiveName"
            sha256 = Get-Sha256 $archivePath
            aliases = @('postgresql', 'postgres', 'pg', 'npgsql')
            schemes = @('pg', 'pgx', 'pgsql', 'postgres', 'postgresql')
            dubUrlApiRange = $apiRange
            targetFrameworks = @('net8.0', 'net9.0', 'net10.0')
            runtimeIdentifiers = @()
        }
    )
}
Write-Utf8Json $catalogPath $catalog

Remove-Item -LiteralPath $stagingRoot -Recurse -Force
Write-Host "Created connector pack '$archivePath' and catalog '$catalogPath'."
