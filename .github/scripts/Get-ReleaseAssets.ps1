[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $ArtifactDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath $ArtifactDirectory).Path
$cliFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'cli') -Recurse -File)
$workerFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'worker') -Recurse -File)
$archives = @($cliFiles | Where-Object Extension -In @('.zip', '.gz'))
$installers = @($cliFiles | Where-Object Name -Like '*-setup.exe')
$workerArchives = @($workerFiles | Where-Object Extension -In @('.zip', '.gz'))
$connectorArchives = @(Get-ChildItem -LiteralPath $root -File -Filter 'duburl-connector-*.zip')
if ($archives.Count -ne 18) { throw "Expected 18 CLI archives, found $($archives.Count)." }
if ($installers.Count -ne 2) { throw "Expected two CLI installers, found $($installers.Count)." }
if ($workerArchives.Count -ne 6) { throw "Expected six worker archives, found $($workerArchives.Count)." }

& (Join-Path $PSScriptRoot '../../connectors/Test-ConnectorArchiveSet.ps1') -Version $Version -ArtifactDirectory $root
$catalog = Get-Item -LiteralPath (Join-Path $root 'connector-catalog.json')
$assets = @($archives) + @($installers) + @($workerArchives) + @($connectorArchives) + @($catalog)
$mixedCase = @($assets | Where-Object { $_.Name -cne $_.Name.ToLowerInvariant() })
if ($mixedCase.Count -ne 0) { throw "Release assets must be lower-case: $($mixedCase.Name -join ', ')." }

# Fail closed if internal build metadata or another unexpected file reaches the public directory.
$unexpected = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object FullName -NotIn $assets.FullName)
if ($unexpected.Count -ne 0) {
    throw "Unexpected public release files: $($unexpected.Name -join ', ')."
}
$assets
