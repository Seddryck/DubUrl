Param(
	[switch] $force=$false
	, $databaseService= "postgresql-x64-13"
)
Push-Location $PSScriptRoot
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Windows-Service.ps1
. $PSScriptRoot\..\Docker-Container.ps1

if ($force) {
	Write-Warning "Forcing QA testing for PostgreSQL"
}

$pgPath = "C:\Program Files\PostgreSQL\$($databaseService.Split('-')[2])\bin"
If (-not (Test-Path -Path $pgPath)) {
	$pgPath = $pgPath -replace "C:", "E:"
}
Write-Host "Using '$pgPath' as PostgreSQL installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*pgsql*")) {
	Write-Host "Deploying PostgreSQL testing environment"

	# Starting database service or docker container
	if ($env:APPVEYOR -eq "True") {
		try { $previouslyRunning = Start-Windows-Service $databaseService }
		catch {
			Write-Warning "Failure to start a Windows service: $_"
			exit 1
		}
	} else {
		$previouslyRunning, $running = Deploy-Container -FullName "postgresql" -ScriptBlock {
			$response = & pg_isready -U postgres -h localhost
			return ($response -join " ") -like "*accepting connections*"
		}
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	If (-not($env:PATH -like $pgPath)) {
		$env:PATH += ";$pgPath"
	}
	$env:PGPASSWORD = "Password12!"
	& psql -U "postgres" -h "localhost" -p "5432" -f ".\deploy-pgsql-database.sql"

	# Installing ODBC driver
	. $PSScriptRoot\deploy-pgsql-odbc-driver.ps1

	# Running QA tests
	Write-Host "Running QA tests related to PostgreSQL"
	$testSuccessful = Run-TestSuite @("Postgresql")

	# Stopping database Service
	if (!$previouslyRunning) {
		if ($env:APPVEYOR -eq "True") {
			Stop-Windows-Service $databaseService
		} else {
			Remove-Container $running
		}
	}

	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for PostgreSQL"
}
Pop-Location
