$ErrorActionPreference = 'Stop'
$workspace = $env:GITHUB_WORKSPACE
if ([string]::IsNullOrWhiteSpace($workspace)) { throw 'GITHUB_WORKSPACE is required.' }

$feed = Join-Path $workspace 'artifacts/core-candidate-feed'
$tempRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$packages = Join-Path $tempRoot ('dqa-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$coreRoot = Join-Path $workspace '.qa-core'
$version = $env:QA_CORE_VERSION
$candidateProjects = [ordered]@{
    'DubUrl.Parser' = 'DubUrl.Parser/DubUrl.Parser.csproj'
    'DubUrl' = 'DubUrl.Core/DubUrl.Core.csproj'
}

foreach ($id in $candidateProjects.Keys) {
    $project = Join-Path $coreRoot $candidateProjects[$id]
    dotnet pack $project -c Release "-p:Version=$version" -p:PackageIcon= -p:ContinuousIntegrationBuild=true -o $feed --nologo
    if ($LASTEXITCODE -or !(Test-Path -LiteralPath (Join-Path $feed "$id.$version.nupkg"))) {
        throw "Could not pack candidate package '$id'."
    }
}

# Exact package ID mappings take precedence over wildcard mappings.
$escapedFeed = [Security.SecurityElement]::Escape($feed)
$schemaFeed = Join-Path $workspace 'artifacts/schema-feed'
New-Item -ItemType Directory -Force $schemaFeed | Out-Null
$escapedSchemaFeed = [Security.SecurityElement]::Escape($schemaFeed)
$config = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="candidate" value="$escapedFeed" />
    <add key="schema" value="$escapedSchemaFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="candidate">
      <package pattern="DubUrl" />
      <package pattern="DubUrl.Parser" />
    </packageSource>
    <packageSource key="schema"><package pattern="DubUrl.Schema" /><package pattern="DubUrl.Schema.Documents" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
</configuration>
"@
Set-Content -LiteralPath (Join-Path $workspace 'NuGet.Config') -Value $config

# A fresh global package folder prevents restore from reusing the published package with the same ID and version.
$env:NUGET_PACKAGES = $packages
Add-Content -LiteralPath $env:GITHUB_ENV -Value "NUGET_PACKAGES=$packages"
& (Join-Path $workspace 'build/Initialize-SchemaQa.ps1')

$qaProject = Join-Path $workspace 'QA/DubUrl.Provider.QA'
if (!(Test-Path -LiteralPath $qaProject)) {
    $qaProject = Join-Path $workspace 'QA/DubUrl.ProviderTesting'
}
dotnet restore $qaProject --nologo
if ($LASTEXITCODE) { throw 'Could not restore the candidate QA dependencies.' }

foreach ($id in $candidateProjects.Keys) {
    $packed = Join-Path $feed "$id.$version.nupkg"
    $packageId = $id.ToLowerInvariant()
    $restored = Join-Path $packages "$packageId/$version/$packageId.$version.nupkg"
    if ((Get-FileHash -LiteralPath $packed).Hash -ne (Get-FileHash -LiteralPath $restored).Hash) {
        throw "Restored package '$id' does not match the candidate."
    }
}
