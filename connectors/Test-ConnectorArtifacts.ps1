[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $ArtifactDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath($ArtifactDirectory)
$catalogPath = Join-Path $root 'connector-catalog.json'
if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "Official connector catalog is missing: $catalogPath"
}

$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
if ($catalog.schemaVersion -ne 1) { throw "Expected connector catalog schema version 1." }
$entries = @($catalog.entries)

$matrix = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'connector-matrix.json') -Raw | ConvertFrom-Json
if ($matrix.schemaVersion -ne 1) { throw "Expected connector matrix schema version 1." }
$matrixIds = @($matrix.connectors | ForEach-Object id)
if (@($matrixIds | Select-Object -Unique).Count -ne $matrixIds.Count) { throw "Connector matrix IDs must be unique." }
$readyIds = @($matrix.connectors | Where-Object status -eq 'ready' | ForEach-Object id)
$catalogIds = @($entries | ForEach-Object provider)
if (Compare-Object $readyIds $catalogIds) { throw "The catalog must contain exactly the ready connector matrix rows." }
if ($entries.Count -ne $readyIds.Count) { throw "Expected $($readyIds.Count) ready connectors, found $($entries.Count)." }

$names = @{}
foreach ($entry in $entries) {
    foreach ($name in @($entry.provider) + @($entry.aliases)) {
        if ([string]::IsNullOrWhiteSpace($name) -or $names.ContainsKey($name)) {
            throw "Connector ID or alias '$name' is missing or duplicated."
        }
        $names[$name] = $entry.provider
    }
    if (@($entry.schemes).Count -eq 0 -or @($entry.targetFrameworks).Count -eq 0 -or $null -eq $entry.runtimeIdentifiers) {
        throw "Connector '$($entry.provider)' has incomplete scheme or compatibility metadata."
    }

    $archiveName = [IO.Path]::GetFileName($entry.downloadUri)
    $archivePath = Join-Path $root $archiveName
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "Connector archive '$archiveName' is missing."
    }
    $archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    if ($archiveHash -cne $entry.sha256) { throw "Connector archive '$archiveName' has the wrong SHA-256 digest." }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $manifestEntry = $archive.GetEntry('provider-pack.json')
        if ($null -eq $manifestEntry) { throw "Connector archive '$archiveName' has no provider-pack.json." }
        $reader = [IO.StreamReader]::new($manifestEntry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json }
        finally { $reader.Dispose() }
        if ($manifest.provider -cne $entry.provider -or $manifest.version -cne $Version) {
            throw "Connector archive identity does not match its catalog entry."
        }
        if ($null -eq $archive.GetEntry($manifest.module.assembly)) {
            throw "Connector module '$($manifest.module.assembly)' is missing."
        }
        foreach ($asset in $manifest.assets) {
            $assetEntry = $archive.GetEntry($asset.path)
            if ($null -eq $assetEntry) { throw "Connector asset '$($asset.path)' is missing." }
            $stream = $assetEntry.Open()
            try { $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
            finally { $stream.Dispose() }
            if ($actual -cne $asset.sha256) { throw "Connector asset '$($asset.path)' has the wrong SHA-256 digest." }
        }
    }
    finally {
        $archive.Dispose()
    }
}

$verifier = Join-Path $PSScriptRoot 'ConnectorArtifactVerifier/ConnectorArtifactVerifier.csproj'
& dotnet restore $verifier -p:TargetFramework=net8.0 --disable-parallel --nologo
if ($LASTEXITCODE -ne 0) { throw "Connector lifecycle verifier restore failed." }
& dotnet build $verifier --configuration Release --framework net8.0 --no-restore --nologo --disable-build-servers -m:1 `
    -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Connector lifecycle verifier build failed." }
foreach ($entry in $entries) {
    $archivePath = Join-Path $root ([IO.Path]::GetFileName($entry.downloadUri))
    $storePath = Join-Path ([IO.Path]::GetTempPath()) "duburl-connector-verifier-$([Guid]::NewGuid().ToString('N'))"
    try {
        & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
            install $catalogPath $archivePath $storePath $entry.provider
        if ($LASTEXITCODE -ne 0) { throw "Connector '$($entry.provider)' installation lifecycle validation failed." }
        & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
            verify $catalogPath $archivePath $storePath $entry.provider
        if ($LASTEXITCODE -ne 0) { throw "Connector '$($entry.provider)' activation lifecycle validation failed." }
        & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
            uninstall $catalogPath $archivePath $storePath $entry.provider
        if ($LASTEXITCODE -ne 0) { throw "Connector '$($entry.provider)' uninstall lifecycle validation failed." }
    }
    finally {
        if (Test-Path -LiteralPath $storePath) { Remove-Item -LiteralPath $storePath -Recurse -Force }
    }
}

Write-Host "Validated $($entries.Count) connector archives, lifecycles, and the schema-v1 official catalog."
