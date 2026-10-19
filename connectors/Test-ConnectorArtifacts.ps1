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
$assemblyNames = @($matrix.connectors | Where-Object status -eq 'ready' | ForEach-Object assemblyName)
if (@($assemblyNames | Select-Object -Unique).Count -ne $readyIds.Count) { throw 'Provider binding assembly names must be unique.' }
$catalogIds = @($entries | ForEach-Object provider)
if (Compare-Object $readyIds $catalogIds) { throw "The catalog must contain exactly the ready connector matrix rows." }
if ($entries.Count -ne $readyIds.Count) { throw "Expected $($readyIds.Count) ready connectors, found $($entries.Count)." }

$names = @{}
$sharedInventoryPath = Join-Path $PSScriptRoot '../DubUrl.Core/obj/Release/net8.0/CoreDependencyPaths.txt'
if (-not (Test-Path -LiteralPath $sharedInventoryPath -PathType Leaf)) {
    throw "Core's shared dependency inventory is missing: $sharedInventoryPath"
}
$sharedAssets = @('DubUrl.Core.dll') + @(Get-Content -LiteralPath $sharedInventoryPath | ForEach-Object { $_.Replace('\', '/') })
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
        foreach ($property in $manifest.PSObject.Properties) {
            $catalogValue = $entry.PSObject.Properties[$property.Name]
            if ($null -eq $catalogValue -or
                (ConvertTo-Json -InputObject $property.Value -Depth 20 -Compress) -cne
                (ConvertTo-Json -InputObject $catalogValue.Value -Depth 20 -Compress)) {
                throw "Connector '$($entry.provider)' catalog and local definition differ at '$($property.Name)'."
            }
        }
        if ($manifest.PSObject.Properties['downloadUri'] -or $manifest.PSObject.Properties['sha256']) {
            throw "Local connector definitions must omit archive distribution fields."
        }
        if (@($manifest.assets | Where-Object { $_.path -in $sharedAssets }).Count -ne 0) {
            throw "Connector '$($entry.provider)' duplicates a host Core dependency."
        }
        $definition = $matrix.connectors | Where-Object id -CEQ $entry.provider
        if ($manifest.module.assembly -cne "$($definition.assemblyName).dll") {
            throw "Connector '$($entry.provider)' does not use its declared provider binding assembly name."
        }
        if ($manifest.module.type -cne "$($definition.assemblyName).ProviderModule") {
            throw "Connector '$($entry.provider)' must expose its generated ProviderModule entry point."
        }
        if (@($manifest.assets | Where-Object path -Like 'DubUrl.Connector.*').Count -ne 0) {
            throw "Connector '$($entry.provider)' still carries a legacy connector assembly."
        }
        $bulkAdapters = @($manifest.assets.path | Where-Object { $_ -like 'DubUrl.Provider.*.BulkCopy.dll' })
        $expectedAdapter = switch ($entry.provider) {
            'duckdb-native' { 'DubUrl.Provider.DuckDb.Native.BulkCopy.dll' }
            'sqlserver-sqlclient' { 'DubUrl.Provider.SqlServer.SqlClient.BulkCopy.dll' }
            default { $null }
        }
        if ($expectedAdapter) {
            if ($bulkAdapters.Count -ne 1 -or $bulkAdapters[0] -cne $expectedAdapter) {
                throw "Connector '$($entry.provider)' must carry only its matching optional bulk adapter."
            }
        } elseif ($bulkAdapters.Count -ne 0) {
            throw "Connector '$($entry.provider)' carries an unrelated bulk adapter."
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

$verifier = Join-Path $PSScriptRoot 'ProviderPackVerifier/ProviderPackVerifier.csproj'
& dotnet restore $verifier -p:TargetFramework=net8.0 --disable-parallel --nologo
if ($LASTEXITCODE -ne 0) { throw "Connector lifecycle verifier restore failed." }
& dotnet build $verifier --configuration Release --framework net8.0 --no-restore --nologo --disable-build-servers -m:1 `
    -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Connector lifecycle verifier build failed." }
foreach ($entry in $entries) {
    $archivePath = Join-Path $root ([IO.Path]::GetFileName($entry.downloadUri))
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $storePath = [IO.Path]::GetFullPath((Join-Path $temporaryRoot "duburl-connector-verifier-$([Guid]::NewGuid().ToString('N'))"))
    if (-not $storePath.StartsWith($temporaryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Verifier store escapes its temporary root: $storePath"
    }
    try {
        & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
            install $catalogPath $archivePath $storePath $entry.provider
        if ($LASTEXITCODE -ne 0) { throw "Connector '$($entry.provider)' installation lifecycle validation failed." }
        $verification = if ($entry.provider -eq 'duckdb-native') { 'verify-bulk' } else { 'verify' }
        & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
            $verification $catalogPath $archivePath $storePath $entry.provider
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
