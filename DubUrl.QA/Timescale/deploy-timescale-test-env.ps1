Param(
	[switch] $force=$false
)
Push-Location $PSScriptRoot
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1

if ($force) {
	Write-Warning "Forcing QA testing for TimescaleDB"
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
	. $PSScriptRoot\..\Postgresql\deploy-pgsql-odbc-driver.ps1

	# Running QA tests
	Write-Host "Running QA tests related to Timescale"
	$testSuccessful = Run-TestSuite @("Timescale")

	# Stop the docker container if not previously running
	if (!$previouslyRunning){
		Remove-Container $running
	}

	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for TimescaleDB"
}
Pop-Location