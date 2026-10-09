Param(
	[switch] $force=$false,
	[string] $config = "Release",
	[string[]] $frameworks = @("net8.0", "net9.0", "net10.0")
)

if ($env:GITHUB_ACTIONS -ne "true") {
	Write-Warning "The provider-owned CrateDB environment is currently configured for GitHub Actions."
	return -1
}

$ErrorActionPreference = "Stop"
. $PSScriptRoot\..\Run-ProviderQaSuite.ps1
& sudo apt-get update
if ($LASTEXITCODE -ne 0) { throw "Unable to update the package index." }
& sudo apt-get install --yes postgresql-client
if ($LASTEXITCODE -ne 0) { throw "Unable to install the PostgreSQL client." }

$project = Join-Path $PSScriptRoot "..\..\DubUrl.Providers.CrateDb.QA\DubUrl.Providers.CrateDb.QA.csproj"
$infrastructure = Join-Path $PSScriptRoot "..\..\DubUrl.Providers.CrateDb.QA\infrastructure"
$composeFile = Join-Path $infrastructure "compose.yaml"
& docker compose -f $composeFile up --detach --wait
if ($LASTEXITCODE -ne 0) { throw "CrateDB QA infrastructure failed to start." }
try {
	& psql -U crate -h localhost -p 5432 -d crate -f (Join-Path $infrastructure "initialize.sql")
	if ($LASTEXITCODE -ne 0) { throw "CrateDB QA database initialization failed." }
	Run-ProviderQaSuite -project $project -provider "cratedb" -config $config -frameworks $frameworks
}
finally {
	& docker compose -f $composeFile down --volumes
}
