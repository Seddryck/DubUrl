[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $ArtifactDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$catalogPath = Join-Path $ArtifactDirectory 'connector-catalog.json'
$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
if ($catalog.schemaVersion -ne 2) { throw 'Expected connector catalog schema version 2.' }
$entries = @($catalog.entries)
if ($entries.Count -eq 0) { throw 'Connector catalog must contain at least one archive.' }

# Catalog entries represent canonical payloads; installation aliases share those archives.
$expected = @($entries | ForEach-Object {
    $name = "duburl-connector-$($_.provider)-$Version.zip".ToLowerInvariant()
    if ($_.version -cne $Version -or [IO.Path]::GetFileName($_.downloadUri) -cne $name) {
        throw "Connector '$($_.provider)' must reference release archive '$name'."
    }
    $name
})
if (@($expected | Sort-Object -Unique).Count -ne $expected.Count) {
    throw 'Connector catalog archive names must be unique.'
}

# Only the root contains connector packs. CLI and worker archives live in subdirectories.
$actual = @(Get-ChildItem -LiteralPath $ArtifactDirectory -File -Filter '*.zip' | ForEach-Object Name)
$missing = @($expected | Where-Object { $_ -cnotin $actual })
$unexpected = @($actual | Where-Object { $_ -cnotin $expected })
if ($missing.Count -or $unexpected.Count) {
    throw "Connector archive set differs from catalog. Missing: [$($missing -join ', ')]. Unexpected: [$($unexpected -join ', ')]."
}

Write-Host "Validated $($expected.Count) connector archive names against the official catalog."
