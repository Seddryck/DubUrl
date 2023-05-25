Param(
	[switch] $force=$false
	, $databaseService= "MSSQL`$SQL2019"
)
Push-Location $PSScriptRoot
. $PSScriptRoot\..\Windows-Service.ps1
. $PSScriptRoot\..\Run-TestSuite.ps1

if ($force) {
	Write-Warning "Forcing QA testing for Microsoft SQL Server"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*mssql*")) {
	Write-Host "Deploying Microsoft SQL Server testing environment"
	
	# Starting database Service
	try { $previouslyRunning = Start-Windows-Service $databaseService }
	catch {
		Write-Warning "Failure to start a Windows service: $_"
		exit 1
	}

	Write-host "`tDeploying database"
	& sqlcmd -U "sa" -P "Password12!" -S ".\SQL2019" -i ".\deploy-mssql-database.sql"
	Write-host "`tDatabase deployed"
	
	# Running QA tests
	Write-Host "Running QA tests related to Microsoft SQL Server"
	$testSuccessful = Run-TestSuite @("MsSqlServer")

	# Stopping database Service
	if (!$previouslyRunning) {
		Stop-Windows-Service $databaseService
	}
	
	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for mssql"
}
Pop-Location