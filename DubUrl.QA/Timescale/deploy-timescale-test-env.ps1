Param(
	[switch] $force=$false
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net8.0", "net9.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1

if ($force) {
	Write-Host "Enforcing QA testing for TimescaleDB"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*timescale*")) {
	Write-Host "Deploying TimescaleDB testing environment"

	# Starting docker container
	$previouslyRunning, $running = Deploy-Container -FullName "timescale" -ScriptBlock {
		$response = & docker exec -it timescale pg_isready -U postgres -h localhost
		return ($response -join " ") -like "*accepting connections*"
	}

	# Deploying database based on script
	Write-host "`tDeploying database ..."
	$env:PGPASSWORD = "Password12!"
	& psql -U "postgres" -h "localhost" -f ".\deploy-timescale-database.sql" | Out-Null
	Write-host "`tDatabase deployed"

	# Installing ODBC driver
	. $PSScriptRoot\..\Postgresql\deploy-postgresql-odbc-driver.ps1

	# Running QA tests
	Write-Host "Running QA tests related to Timescale"
	$testSuccessful = Run-TestSuite @("Timescale") -config $config -frameworks $frameworks

	# Stop the docker container if not previously running
	if (!$previouslyRunning){
		Remove-Container $running
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
