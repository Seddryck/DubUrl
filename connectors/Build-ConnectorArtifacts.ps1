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
$project = Join-Path $repositoryRoot 'Providers/Build/ProviderBinding.Build.csproj'
$matrixPath = Join-Path $PSScriptRoot 'connector-matrix.json'
$catalogPath = Join-Path $outputRoot 'connector-catalog.json'
$noticePath = Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md'
$epoch = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem


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

function Write-Utf8Json([string] $Path, [object] $Value) {
    $json = $Value | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText($Path, "$json`n", [Text.UTF8Encoding]::new($false))
}

New-CleanDirectory $outputRoot
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json
if ($matrix.schemaVersion -ne 1) { throw 'Expected connector matrix schema version 1.' }
$technologies = @{}
foreach ($technology in $matrix.technologies) { $technologies[$technology.name] = $technology }
$ready = @($matrix.connectors | Where-Object status -eq 'ready' | Sort-Object id)
if (@($ready.assemblyName | Select-Object -Unique).Count -ne $ready.Count) {
    throw 'Provider binding assembly names must be unique.'
}
$definitionsPath = Join-Path $stagingRoot 'connector-catalog.json'
Invoke-Checked dotnet @(
    'run', '--project', (Join-Path $PSScriptRoot 'Catalog/ConnectorCatalog.Build.csproj'), '--configuration', 'Release', '--framework', 'net8.0', '--', $matrixPath, $definitionsPath, $Version
)
$definitions = (Get-Content -LiteralPath $definitionsPath -Raw | ConvertFrom-Json -AsHashtable).entries
$catalogEntries = [Collections.Generic.List[object]]::new()

foreach ($connector in $ready) {
    if (-not $technologies.ContainsKey($connector.technology)) {
        throw "Connector '$($connector.id)' uses unknown technology '$($connector.technology)'."
    }
    $technology = $technologies[$connector.technology]
    $assemblyName = $connector.assemblyName
    if ($assemblyName -cnotmatch '^DubUrl\.Provider\.[A-Za-z0-9]+\.[A-Za-z0-9]+$') {
        throw "Connector '$($connector.id)' must name its binding DubUrl.Provider.<Database>.<Driver>."
    }
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
        "-p:ConnectorAssemblyName=$assemblyName",
        "-p:ConnectorFamily=$($connector.family)",
        "-p:ConnectorTechnology=$($technology.Key)",
        "-p:ConnectorDatabaseName=$($connector.database)"
    )

    # The same generated inventory is embedded in Core for its load-context sharing policy.
    $sharedDependenciesPath = Join-Path $repositoryRoot 'DubUrl.Core/obj/Release/net8.0/CoreDependencyPaths.txt'
    if (-not (Test-Path -LiteralPath $sharedDependenciesPath -PathType Leaf)) {
        throw "Core's shared dependency inventory is missing: $sharedDependenciesPath"
    }
    $excluded = @('DubUrl.Core.dll', 'DubUrl.Core.pdb', 'DubUrl.Core.xml')
    $excluded += @(Get-Content -LiteralPath $sharedDependenciesPath | ForEach-Object { $_.Replace('\', '/') })
    foreach ($file in Get-ChildItem -LiteralPath $publishRoot -Recurse -File) {
        $relative = [IO.Path]::GetRelativePath($publishRoot, $file.FullName).Replace('\', '/')
        if ($relative -in $excluded -or $file.Extension -in @('.pdb', '.xml')) { continue }
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

    $moduleFile = "$assemblyName.dll"
    if (-not (Test-Path -LiteralPath (Join-Path $contentRoot $moduleFile) -PathType Leaf)) {
        throw "Connector '$($connector.id)' publish output is missing module '$moduleFile'."
    }
    $manifest = @($definitions | Where-Object { $_.provider -ceq $connector.id })
    if ($manifest.Count -ne 1) { throw "Missing or ambiguous connector definition '$($connector.id)'." }
    $manifest = $manifest[0]
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

    # Enrich the exact archive definition after its immutable archive hash is available.
    $catalogEntry = [ordered]@{}
    foreach ($key in $manifest.Keys) { $catalogEntry[$key] = $manifest[$key] }
    $catalogEntry.downloadUri = "https://github.com/Seddryck/DubUrl/releases/download/v$Version/$archiveName"
    $catalogEntry.sha256 = Get-Sha256 $archivePath
    $catalogEntries.Add($catalogEntry)
}

Write-Utf8Json $catalogPath ([ordered]@{ schemaVersion = 1; entries = @($catalogEntries) })
Remove-Item -LiteralPath $stagingRoot -Recurse -Force
Write-Host "Created $($catalogEntries.Count) connector packs and catalog '$catalogPath'."
