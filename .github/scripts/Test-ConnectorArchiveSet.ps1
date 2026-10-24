$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$validator = Join-Path $PSScriptRoot '../../connectors/Test-ConnectorArchiveSet.ps1'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$fixtureRoot = Join-Path $temporaryRoot "duburl-archive-set-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
$version = '0.45.0'

function Write-Catalog($Entries, [int] $SchemaVersion = 2) {
    @{ schemaVersion = $SchemaVersion; entries = @($Entries) } |
        ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $fixtureRoot 'connector-catalog.json')
}

function Assert-Rejected([string] $Message) {
    try { & $validator -Version $version -ArtifactDirectory $fixtureRoot }
    catch {
        if (-not $_.Exception.Message.Contains($Message)) { throw }
        return
    }
    throw "Expected archive validation to reject: $Message"
}

try {
    # A variable number of canonical packs must pass, independently of alias counts.
    $entries = @()
    foreach ($index in 1..33) {
        $name = "duburl-connector-provider-$index-$version.zip"
        $entries += [PSCustomObject]@{
            provider = "provider-$index"
            version = $version
            aliases = @("alias-$index", "other-alias-$index")
            downloadUri = "https://github.com/Seddryck/DubUrl/releases/download/v$version/$name"
        }
        New-Item -ItemType File -Path (Join-Path $fixtureRoot $name) | Out-Null
        if ($index -in @(1, 24, 25, 33)) {
            Write-Catalog $entries
            & $validator -Version $version -ArtifactDirectory $fixtureRoot
        }
    }

    New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'cli'), (Join-Path $fixtureRoot 'worker') | Out-Null
    New-Item -ItemType File -Path (Join-Path $fixtureRoot 'cli/cli.zip'), (Join-Path $fixtureRoot 'worker/worker.zip') | Out-Null
    & $validator -Version $version -ArtifactDirectory $fixtureRoot

    $firstArchive = [IO.Path]::GetFileName($entries[0].downloadUri)
    $firstPath = Join-Path $fixtureRoot $firstArchive
    Remove-Item -LiteralPath $firstPath
    Assert-Rejected "Missing: [$firstArchive]"

    # The count still matches, but one expected archive was replaced by an unrelated ZIP.
    $extraPath = Join-Path $fixtureRoot 'unexpected.zip'
    New-Item -ItemType File -Path $extraPath | Out-Null
    Assert-Rejected 'Unexpected: [unexpected.zip]'
    New-Item -ItemType File -Path $firstPath | Out-Null
    Assert-Rejected 'Unexpected: [unexpected.zip]'
    Remove-Item -LiteralPath $extraPath

    Write-Catalog @($entries + $entries[0])
    Assert-Rejected 'archive names must be unique'
    Write-Catalog @()
    Assert-Rejected 'must contain at least one archive'
    Write-Catalog $entries -SchemaVersion 1
    Assert-Rejected 'schema version 2'

    $entries[0].version = '0.44.0'
    Write-Catalog $entries
    Assert-Rejected "must reference release archive '$firstArchive'"
    $entries[0].version = $version
    $entries[0].downloadUri = 'https://example.test/wrong.zip'
    Write-Catalog $entries
    Assert-Rejected "must reference release archive '$firstArchive'"

    Write-Host 'Passed connector archive set regression checks (variable canonical sets, aliases, nested distributions, missing, unexpected, duplicate, empty, schema, version, and filename mismatches).'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolved.StartsWith($temporaryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Fixture path escapes temporary root: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
