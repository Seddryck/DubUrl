Param(
	[switch] $force=$false
)
if ($force) {
	Write-Host "Enforcing QA testing for CockRoachDB"
}
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*cockroach*")) {
	Write-Host "Deploying CockRoachDB testing environment"

	# Starting docker container
	$previouslyRunning, $running = Deploy-Container -FullName "cockroach" -NickName "roach" -ScriptBlock {
		$cmd = "/cockroach/cockroach node status --insecure"
		try {
			$response = & docker exec -it roach-single sh -c "$cmd"
			return ($response -join " ") -notlike "ERROR: cannot dial server*"
		} catch {
			Write-Warning $_
			Start-Sleep -Seconds 1
			return $false
		}
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	$statements = Get-Content ".\deploy-cockroach-database.sql"
	$cmd = "/cockroach/cockroach sql --insecure --database duburl --execute=""$statements"";"
	& docker exec -it roach-single sh -c "$cmd"
	Write-host "`tDatabase created."

	# Installing ODBC driver
	. $PSScriptRoot\..\Postgresql\deploy-postgresql-odbc-driver.ps1

	# Running QA tests
	Write-Host "Running QA tests related to CockRoach"
	$testSuccessful = Run-TestSuite @("CockRoach+AdoProvider", "CockRoach+ODBC")

	# Stop the docker container if not previously running
	if (!$previouslyRunning){
		Remove-Container $running
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}