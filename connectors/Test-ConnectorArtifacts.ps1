[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $ArtifactDirectory,

    [string] $SharedDependencyInventoryPath = (Join-Path $PSScriptRoot '../DubUrl.Core/obj/Release/net8.0/CoreDependencyPaths.txt')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Test-ConnectorDependencies.ps1')
& (Join-Path $PSScriptRoot 'Test-ConnectorNativeAdapters.ps1')

$root = [IO.Path]::GetFullPath($ArtifactDirectory)
$catalogPath = Join-Path $root 'connector-catalog.json'
if (-not (Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    throw "Official connector catalog is missing: $catalogPath"
}

$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
if ($catalog.schemaVersion -ne 2) { throw "Expected connector catalog schema version 2." }
$entries = @($catalog.entries)

$matrix = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'connector-matrix.json') -Raw | ConvertFrom-Json
if ($matrix.schemaVersion -ne 2) { throw "Expected connector matrix schema version 2." }
$matrixIds = @($matrix.connectors | ForEach-Object id)
if (@($matrixIds | Select-Object -Unique).Count -ne $matrixIds.Count) { throw "Connector matrix IDs must be unique." }
$readyIds = @($matrix.connectors | Where-Object status -eq 'ready' | ForEach-Object id)
$catalogIds = @($entries | ForEach-Object provider)
foreach ($id in $readyIds) {
    $owners = @($entries | Where-Object { $_.provider -eq $id -or $id -in $_.aliases })
    if ($owners.Count -ne 1) { throw "Installation name '$id' must resolve to exactly one archive." }
}
$payloads = @($entries | ForEach-Object { (@($_.implementationAssemblies | Sort-Object) -join '|') + '|' + $_.driver + '|' + ($_.runtimeIdentifiers -join '|') })
if (@($payloads | Sort-Object -Unique).Count -ne $entries.Count) { throw 'Duplicate implementation/driver archives remain.' }
& (Join-Path $PSScriptRoot 'Test-ConnectorArchiveSet.ps1') -Version $Version -ArtifactDirectory $root

function Get-DefinitionSchemes($definition) { @($definition.databases | ForEach-Object { $_.schemes }) }

$names = @{}
if (-not (Test-Path -LiteralPath $SharedDependencyInventoryPath -PathType Leaf)) {
    throw "Core's shared dependency inventory is missing: $SharedDependencyInventoryPath"
}
$sharedAssets = @('DubUrl.Core.dll') + @(Get-Content -LiteralPath $SharedDependencyInventoryPath | ForEach-Object { $_.Replace('\', '/') })
foreach ($entry in $entries) {
    foreach ($name in @($entry.provider) + @($entry.aliases)) {
        if ([string]::IsNullOrWhiteSpace($name) -or $names.ContainsKey($name)) {
            throw "Connector ID or alias '$name' is missing or duplicated."
        }
        $names[$name] = $entry.provider
    }
    if (@(Get-DefinitionSchemes $entry).Count -eq 0 -or @($entry.targetFrameworks).Count -eq 0 -or $null -eq $entry.runtimeIdentifiers) {
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
        if ($manifest.PSObject.Properties['assets'] -or $entry.PSObject.Properties['assets']) {
            throw "Connector definitions must not contain an assets inventory."
        }
        $archivePaths = @($archive.Entries | ForEach-Object FullName)
        if (@($archivePaths | Where-Object { $_ -in $sharedAssets }).Count -ne 0) {
            throw "Connector '$($entry.provider)' duplicates a host Core dependency."
        }
        $catalogDatabases = $entry.databases | ConvertTo-Json -Depth 10 -Compress
        if (($manifest.databases | ConvertTo-Json -Depth 10 -Compress) -cne $catalogDatabases) {
            throw "Connector '$($entry.provider)' database mappings do not match its catalog."
        }
        if ($manifest.implementationAssemblies -cnotcontains $manifest.assembly) {
            throw "Connector '$($entry.provider)' must use one of its provider implementations as the dependency anchor."
        }
        if (@($archivePaths | Where-Object { $_ -like 'ConnectorDependencies*' }).Count) {
            throw "Connector '$($entry.provider)' contains temporary dependency-collector output."
        }
        $driverAssembly = if ($manifest.PSObject.Properties['driverAssembly']) { $manifest.driverAssembly } else { $manifest.driver + '.dll' }
        & (Join-Path $PSScriptRoot 'Assert-ConnectorNativeAdapters.ps1') `
            -Provider $entry.provider -DriverAssembly $driverAssembly -ArchivePaths $archivePaths
        if ($manifest.PSObject.Properties['module'] -or $manifest.PSObject.Properties['type']) {
            throw "Connector definitions must not contain an entry-point type."
        }
        foreach ($implementation in $manifest.implementationAssemblies) {
            if ($archivePaths -cnotcontains $implementation) { throw "Missing implementation '$implementation'." }
        }
        if (@($archivePaths | Where-Object { $_ -like 'DubUrl.Connector.*' }).Count -ne 0) {
            throw "Connector '$($entry.provider)' still carries a legacy connector assembly."
        }
        $bulkAdapters = @($archivePaths | Where-Object { $_ -like 'DubUrl.Provider.*.BulkCopy.dll' })
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
        if ($null -eq $archive.GetEntry($manifest.assembly)) {
            throw "Connector module '$($manifest.assembly)' is missing."
        }
        $dependencyFile = [IO.Path]::ChangeExtension($manifest.assembly, '.deps.json')
        if ($null -eq $archive.GetEntry($dependencyFile)) {
            throw "Connector dependency metadata '$dependencyFile' is missing."
        }
        $dependencyReader = [IO.StreamReader]::new($archive.GetEntry($dependencyFile).Open())
        try { $dependencyDocument = $dependencyReader.ReadToEnd() | ConvertFrom-Json -AsHashtable }
        finally { $dependencyReader.Dispose() }
        $anchorName = [IO.Path]::GetFileNameWithoutExtension($manifest.assembly)
        $anchorKey = @($dependencyDocument.libraries.Keys | Where-Object { $_.StartsWith("$anchorName/") })
        if ($anchorKey.Count -ne 1 -or @($dependencyDocument.libraries.Keys | Where-Object { $_ -like 'ConnectorDependencies/*' }).Count) {
            throw "Connector '$($entry.provider)' has an invalid dependency root."
        }
        foreach ($target in $dependencyDocument.targets.Values) {
            if (-not $target.Contains($anchorKey[0])) { throw 'Dependency target is missing the implementation root.' }
            foreach ($runtime in $target.Values) {
                if ($runtime.Contains('runtime') -and $runtime.runtime.Contains('ConnectorDependencies.dll')) {
                    throw 'Dependency metadata still references the temporary collector DLL.'
                }
            }
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
        foreach ($alias in @($entry.aliases | Where-Object { $_ -in $readyIds })) {
            & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
                install $catalogPath $archivePath $storePath $alias
            if ($LASTEXITCODE -ne 0) { throw "Installation alias '$alias' did not reuse its canonical archive." }
        }
        $verification = if ($entry.provider -eq 'duckdb-native') { 'verify-bulk' } else { 'verify' }
        & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
            $verification $catalogPath $archivePath $storePath $entry.provider
        if ($LASTEXITCODE -ne 0) { throw "Connector '$($entry.provider)' activation lifecycle validation failed." }
        $removeName = @($entry.aliases | Where-Object { $_ -in $readyIds } | Select-Object -First 1)
        if ($removeName.Count -eq 0) { $removeName = @($entry.provider) }
        & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
            uninstall $catalogPath $archivePath $storePath $removeName[0]
        if ($LASTEXITCODE -ne 0) { throw "Connector '$($entry.provider)' uninstall lifecycle validation failed." }
        if ($entry.provider -eq 'postgresql-npgsql') {
            & dotnet run --project $verifier --configuration Release --framework net8.0 --no-build --no-restore -- `
                verify-cache $catalogPath $archivePath $storePath $entry.provider
            if ($LASTEXITCODE -ne 0) { throw 'Cross-process registry cache invalidation failed.' }
        }
    }
    finally {
        if (Test-Path -LiteralPath $storePath) { Remove-Item -LiteralPath $storePath -Recurse -Force }
    }
}

Write-Host "Validated $($entries.Count) connector archives, lifecycles, and the schema-v2 official catalog."
