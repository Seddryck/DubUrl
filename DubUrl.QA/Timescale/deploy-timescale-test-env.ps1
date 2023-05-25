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
	Write-host "`tCreating database"
	$env:PGPASSWORD = "Password12!"
	& psql -U "postgres" -h "localhost" -f ".\deploy-timescale-database.sql"

	# Installing ODBC driver
	Write-host "`tDeploying PostgreSQL ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*postgres*" -Platform "64-bit"
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading PostgreSQL ODBC driver ..."
		Invoke-WebRequest "https://ftp.PostgreSQL.org/pub/odbc/versions/msi/psqlodbc_13_02_0000-x64.zip" -OutFile "$env:temp\psqlodbc.zip"
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