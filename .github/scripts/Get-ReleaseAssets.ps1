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
$connectorArchives = @(Get-ChildItem -LiteralPath $root -File -Filter 'duburl-connector-*.zip')

& (Join-Path $PSScriptRoot '../../connectors/Test-ConnectorArchiveSet.ps1') -Version $Version -ArtifactDirectory $root
$catalog = Get-Item -LiteralPath (Join-Path $root 'connector-catalog.json')
$assets = @($connectorArchives) + @($catalog)
$mixedCase = @($assets | Where-Object { $_.Name -cne $_.Name.ToLowerInvariant() })
if ($mixedCase.Count -ne 0) { throw "Release assets must be lower-case: $($mixedCase.Name -join ', ')." }

# Fail closed if internal build metadata or another unexpected file reaches the public directory.
$unexpected = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object FullName -NotIn $assets.FullName)
if ($unexpected.Count -ne 0) {
    throw "Unexpected public release files: $($unexpected.Name -join ', ')."
}
$assets
