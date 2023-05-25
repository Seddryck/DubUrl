Param(
	[switch] $force=$false
	, $databaseService= "postgresql-x64-13"
)
Push-Location $PSScriptRoot
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Windows-Service.ps1

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

	# Starting database service
	try { $previouslyRunning = Start-Windows-Service $databaseService }
	catch {
		Write-Warning "Failure to start a Windows service: $_"
		exit 1
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	If (-not($env:PATH -like $pgPath)) {
		$env:PATH += ";$pgPath"
	}
	$env:PGPASSWORD = "Password12!"
	& psql -U "postgres" -h "localhost" -f ".\deploy-pgsql-database.sql"

	# Installing ODBC driver
	Write-host "`tDeploying PostgreSQL ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*postgres*" -Platform "64-bit"
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading PostgreSQL ODBC driver ..."
		Invoke-WebRequest "https://ftp.postgresql.org/pub/odbc/versions/msi/psqlodbc_13_02_0000-x64.zip" -OutFile "$env:temp\psqlodbc.zip"
		Write-Host "`t`tExtracting from archive PostgreSQL ODBC driver ..."
		& 7z e "$env:temp\psqlodbc.zip" -o"$env:temp" -y
		Write-Host "`t`tInstalling PostgreSQL ODBC driver ..."
		& msiexec /i "$env:temp\psqlodbc_x64.msi" /quiet /qn /norestart /log "$env:temp\install-pgsql.log" | Out-Host
		#Get-Content "$env:temp\install-pgsql.log"
		Write-Host "`t`tChecking installation ..."
		Get-OdbcDriver -Name "*postgres*"
		Write-Host "`tDeployment of PostgreSQL ODBC driver finalized."
	} else {
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*postgres*" -Platform "64-bit"
		Write-Host "`t`tSkipping installation of new drivers"
	}

	# Running QA tests
	Write-Host "Running QA tests related to PostgreSQL"
	$testSuccessful = Run-TestSuite @("Postgresql")

	# Stopping database Service
	if (!$previouslyRunning) {
		Stop-Windows-Service $databaseService
	}

	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for PostgreSQL"
}
Pop-Location
