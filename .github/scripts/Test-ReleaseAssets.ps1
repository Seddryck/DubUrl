$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$selector = Join-Path $PSScriptRoot 'Get-ReleaseAssets.ps1'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$fixtureRoot = Join-Path $temporaryRoot "duburl-release-assets-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
$version = '0.46.0'

function Assert-Rejected([string] $Message) {
    try { & $selector -Version $version -ArtifactDirectory $fixtureRoot | Out-Null }
    catch {
        if (-not $_.Exception.Message.Contains($Message)) { throw }
        return
    }
    throw "Expected release validation to reject: $Message"
}

try {
    New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'cli'), (Join-Path $fixtureRoot 'worker') | Out-Null
    foreach ($index in 1..18) {
        New-Item -ItemType File -Path (Join-Path $fixtureRoot "cli/duburl-$index.zip") | Out-Null
    }
    foreach ($index in 1..2) {
        New-Item -ItemType File -Path (Join-Path $fixtureRoot "cli/duburl-$index-setup.exe") | Out-Null
    }
    foreach ($index in 1..6) {
        New-Item -ItemType File -Path (Join-Path $fixtureRoot "worker/duburl-worker-$index.tar.gz") | Out-Null
    }
    $connectorName = "duburl-connector-example-$version.zip"
    New-Item -ItemType File -Path (Join-Path $fixtureRoot $connectorName) | Out-Null
    @{
        schemaVersion = 2
        entries = @(@{
            provider = 'example'; version = $version
            downloadUri = "https://github.com/Seddryck/DubUrl/releases/download/v$version/$connectorName"
        })
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fixtureRoot 'connector-catalog.json')

    $assets = @(& $selector -Version $version -ArtifactDirectory $fixtureRoot)
    if ($assets.Count -ne 28) { throw "Expected 28 public fixture files, found $($assets.Count)." }

    # Internal inventories must never become public release attachments, even in a subdirectory.
    foreach ($relativePath in @('CoreDependencyPaths.txt', 'cli/CoreDependencyPaths.txt', 'worker/validation-inputs.txt')) {
        $path = Join-Path $fixtureRoot $relativePath
        Set-Content -LiteralPath $path 'internal dependency inventory'
        Assert-Rejected 'Unexpected public release files'
        Remove-Item -LiteralPath $path
    }

    $archive = Join-Path $fixtureRoot 'cli/duburl-18.zip'
    Remove-Item -LiteralPath $archive
    Assert-Rejected 'Expected 18 CLI archives'
    New-Item -ItemType File -Path (Join-Path $fixtureRoot 'cli/DubUrl-18.zip') | Out-Null
    Assert-Rejected 'Release assets must be lower-case'
    Write-Host 'Passed release asset checks: complete set, internal metadata exclusion, missing archives, and filename casing.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolved.StartsWith($temporaryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Fixture path escapes temporary root: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
