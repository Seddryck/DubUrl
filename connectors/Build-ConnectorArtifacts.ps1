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
$project = Join-Path $PSScriptRoot 'DubUrl.Connector.Generated/DubUrl.Connector.Generated.csproj'
$matrixPath = Join-Path $PSScriptRoot 'connector-matrix.json'
$catalogPath = Join-Path $outputRoot 'connector-catalog.json'
$noticePath = Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md'
$epoch = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if ($Version -notmatch '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:[-+].+)?$') {
    throw "Connector version '$Version' is not valid SemVer."
}
$apiRange = "[$($Matches.major).$($Matches.minor).$($Matches.patch),$([int]$Matches.major + 1).0.0)"

$technologies = @{
    'Npgsql' = @{ Key = 'Npgsql'; Invariant = 'Npgsql'; Factory = 'Npgsql.NpgsqlFactory, Npgsql' }
    'IBM.Data.Db2' = @{ Key = 'Db2'; Invariant = 'IBM.Data.Db2'; Factory = 'IBM.Data.Db2.DB2Factory, IBM.Data.Db2' }
    'DuckDB.NET.Data' = @{ Key = 'DuckDb'; Invariant = 'DuckDB.NET.Data'; Factory = 'DuckDB.NET.Data.DuckDBClientFactory, DuckDB.NET.Data' }
    'FirebirdSql.Data.FirebirdClient' = @{ Key = 'Firebird'; Invariant = 'FirebirdSql.Data.FirebirdClient'; Factory = 'FirebirdSql.Data.FirebirdClient.FirebirdClientFactory, FirebirdSql.Data.FirebirdClient' }
    'MySqlConnector' = @{ Key = 'MySqlConnector'; Invariant = 'MySqlConnector'; Factory = 'MySqlConnector.MySqlConnectorFactory, MySqlConnector' }
    'Microsoft.Data.SqlClient' = @{ Key = 'SqlClient'; Invariant = 'Microsoft.Data.SqlClient'; Factory = 'Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient' }
    'Oracle.ManagedDataAccess' = @{ Key = 'Oracle'; Invariant = 'Oracle.ManagedDataAccess'; Factory = 'Oracle.ManagedDataAccess.Client.OracleClientFactory, Oracle.ManagedDataAccess' }
    'SingleStoreConnector' = @{ Key = 'SingleStore'; Invariant = 'SingleStoreConnector'; Factory = 'SingleStoreConnector.SingleStoreConnectorFactory, SingleStoreConnector' }
    'Snowflake.Data' = @{ Key = 'Snowflake'; Invariant = 'Snowflake.Data'; Factory = 'Snowflake.Data.Client.SnowflakeDbFactory, Snowflake.Data' }
    'Microsoft.Data.Sqlite' = @{ Key = 'Sqlite'; Invariant = 'Microsoft.Data.Sqlite'; Factory = 'Microsoft.Data.Sqlite.SqliteFactory, Microsoft.Data.Sqlite' }
    'Teradata.Client' = @{ Key = 'Teradata'; Invariant = 'Teradata.Client'; Factory = 'Teradata.Client.Provider.TdFactory, Teradata.Client.Provider' }
    'NReco.PrestoAdo' = @{ Key = 'Trino'; Invariant = 'NReco.PrestoAdo'; Factory = 'NReco.PrestoAdo.PrestoDbFactory, NReco.PrestoAdo' }
    'ODBC' = @{ Key = 'Odbc'; Invariant = 'System.Data.Odbc'; Factory = 'System.Data.Odbc.OdbcFactory, System.Data.Odbc' }
    'OLE DB' = @{ Key = 'OleDb'; Invariant = 'System.Data.OleDb'; Factory = 'System.Data.OleDb.OleDbFactory, System.Data.OleDb' }
}

function New-CleanDirectory([string] $Path) {
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command '$Command' failed with exit code $LASTEXITCODE." }
}

function Get-Sha256([string] $Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function ConvertTo-MSBuildValue([string] $Value) {
    $Value.Replace('%', '%25').Replace(',', '%2C').Replace(';', '%3B')
}

function Write-Utf8Json([string] $Path, [object] $Value) {
    $json = $Value | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText($Path, "$json`n", [Text.UTF8Encoding]::new($false))
}

function Get-AssemblyName([string] $ConnectorId) {
    $parts = $ConnectorId -split '-' | ForEach-Object {
        if ($_.Length -eq 0) { return }
        $_.Substring(0, 1).ToUpperInvariant() + $_.Substring(1)
    }
    "DubUrl.Connector.$($parts -join '.')"
}

function Get-AssetKind([string] $RelativePath) {
    if ($RelativePath -match '(^|/)runtimes/[^/]+/native/') { return 'Native' }
    if ([IO.Path]::GetExtension($RelativePath) -eq '.md') { return 'Content' }
    'Managed'
}

function Get-AssetRuntime([string] $RelativePath) {
    if ($RelativePath -match '(^|/)runtimes/(?<rid>[^/]+)/native/') { return $Matches.rid }
    $null
}

New-CleanDirectory $outputRoot
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json
if ($matrix.schemaVersion -ne 1) { throw 'Expected connector matrix schema version 1.' }
$ready = @($matrix.connectors | Where-Object status -eq 'ready' | Sort-Object id)
$catalogEntries = [Collections.Generic.List[object]]::new()

foreach ($connector in $ready) {
    if (-not $technologies.ContainsKey($connector.technology)) {
        throw "Connector '$($connector.id)' uses unknown technology '$($connector.technology)'."
    }
    $technology = $technologies[$connector.technology]
    $assemblyName = Get-AssemblyName $connector.id
    $mapperAssembly = if ($technology.Key -eq 'Odbc') { 'DubUrl.Provider.Odbc' }
        elseif ($technology.Key -eq 'OleDb') { 'DubUrl.OleDb' }
        else { "DubUrl.Provider.$($connector.family)" }
    $mapperType = if ($technology.Key -eq 'OleDb') { "DubUrl.OleDb.Mapping.$($connector.mapper), $mapperAssembly" }
        else { "DubUrl.Mapping.Implementation.$($connector.mapper), $mapperAssembly" }
    $publishRoot = Join-Path $stagingRoot "$($connector.id)/publish"
    $contentRoot = Join-Path $stagingRoot "$($connector.id)/content"
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
        '-p:ContinuousIntegrationBuild=true',
        "-p:ConnectorId=$($connector.id)",
        "-p:ConnectorAssemblyName=$assemblyName",
        "-p:ConnectorFamily=$($connector.family)",
        "-p:ConnectorTechnology=$($technology.Key)",
        "-p:ConnectorDatabaseName=$($connector.database)",
        "-p:ConnectorAliases=$(@($connector.schemes) -join '|')",
        "-p:ConnectorProviderInvariant=$($technology.Invariant)",
        "-p:ConnectorFactoryType=$(ConvertTo-MSBuildValue $technology.Factory)",
        "-p:ConnectorDialectType=$(ConvertTo-MSBuildValue "DubUrl.Querying.Dialects.$($connector.dialect), DubUrl.Provider.$($connector.family)")",
        "-p:ConnectorMapperType=$(ConvertTo-MSBuildValue $mapperType)",
        "-p:ConnectorParametrizerType=$(ConvertTo-MSBuildValue "DubUrl.Querying.Parametrizing.$($connector.parametrizer), DubUrl.Core")"
    )

    $excluded = @('DubUrl.Core.dll', 'DubUrl.Core.pdb', 'DubUrl.Core.xml')
    foreach ($file in Get-ChildItem -LiteralPath $publishRoot -Recurse -File) {
        if ($file.Name -in $excluded -or $file.Extension -in @('.pdb', '.xml')) { continue }
        $relative = [IO.Path]::GetRelativePath($publishRoot, $file.FullName)
        $destination = Join-Path $contentRoot $relative
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination
    }
    Copy-Item -LiteralPath $noticePath -Destination (Join-Path $contentRoot 'THIRD-PARTY-NOTICES.md')
    if ($null -ne $connector.prerequisite) {
        [IO.File]::WriteAllText(
            (Join-Path $contentRoot 'PREREQUISITES.md'),
            "# External prerequisite`n`n$($connector.prerequisite)`n",
            [Text.UTF8Encoding]::new($false))
    }

    $assets = @(
        Get-ChildItem -LiteralPath $contentRoot -Recurse -File |
            ForEach-Object {
                $relative = [IO.Path]::GetRelativePath($contentRoot, $_.FullName).Replace('\', '/')
                $asset = [ordered]@{
                    path = $relative
                    sha256 = Get-Sha256 $_.FullName
                    kind = Get-AssetKind $relative
                }
                $rid = Get-AssetRuntime $relative
                if ($null -ne $rid) { $asset.runtimeIdentifier = $rid }
                $asset
            } |
            Sort-Object path
    )

    $moduleFile = "$assemblyName.dll"
    if (-not ($assets.path -contains $moduleFile)) {
        throw "Connector '$($connector.id)' publish output is missing module '$moduleFile'."
    }
    $manifest = [ordered]@{
        provider = $connector.id
        version = $Version
        dubUrlApiRange = $apiRange
        module = [ordered]@{ assembly = $moduleFile; type = 'DubUrl.Connector.ProviderModule' }
        schemes = @($connector.schemes)
        targetFrameworks = @('net8.0', 'net9.0', 'net10.0')
        runtimeIdentifiers = @($connector.runtimeIdentifiers)
        assets = $assets
    }
    $manifestPath = Join-Path $stagingRoot "$($connector.id)/provider-pack.json"
    Write-Utf8Json $manifestPath $manifest

    $archiveName = "duburl-connector-$($connector.id)-$Version.zip".ToLowerInvariant()
    $archivePath = Join-Path $outputRoot $archiveName
    $archive = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $entries = @([PSCustomObject]@{ Source = $manifestPath; Destination = 'provider-pack.json' })
        $entries += Get-ChildItem -LiteralPath $contentRoot -Recurse -File | ForEach-Object {
            [PSCustomObject]@{
                Source = $_.FullName
                Destination = [IO.Path]::GetRelativePath($contentRoot, $_.FullName).Replace('\', '/')
            }
        }
        foreach ($item in $entries | Sort-Object Destination) {
            $entry = $archive.CreateEntry($item.Destination, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $epoch
            $source = [IO.File]::OpenRead($item.Source)
            $destination = $entry.Open()
            try { $source.CopyTo($destination) }
            finally { $destination.Dispose(); $source.Dispose() }
        }
    }
    finally { $archive.Dispose() }

    $catalogEntries.Add([ordered]@{
        provider = $connector.id
        version = $Version
        downloadUri = "https://github.com/Seddryck/DubUrl/releases/download/v$Version/$archiveName"
        sha256 = Get-Sha256 $archivePath
        aliases = @($connector.defaultAliases)
        schemes = @($connector.schemes)
        dubUrlApiRange = $apiRange
        targetFrameworks = @('net8.0', 'net9.0', 'net10.0')
        runtimeIdentifiers = @($connector.runtimeIdentifiers)
    })
}

Write-Utf8Json $catalogPath ([ordered]@{ schemaVersion = 1; entries = @($catalogEntries) })
Remove-Item -LiteralPath $stagingRoot -Recurse -Force
Write-Host "Created $($catalogEntries.Count) connector packs and catalog '$catalogPath'."
