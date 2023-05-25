Param(
	[switch] $force=$false
	, $config = "Release"
)
Push-Location $PSScriptRoot
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1

if ($force) {
	Write-Warning "Forcing QA testing for QuestDB"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*quest*")) {
	Write-Host "Deploying QuestDB testing environment"

	# Starting docker container
	$previouslyRunning, $running = Deploy-Container -FullName "questdb" -NickName "quest"
	if (!$previouslyRunning) {
		Start-Sleep -s 10
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	if (-not($env:PATH -like $pgPath)) {
		$env:PATH += ";$pgPath"
	}
	$env:PGPASSWORD = "quest"
	& psql -U "admin" -h "localhost" -p "8812" -f ".\deploy-questdb-database.sql"
	Write-host "`tDatabase created"

	# Installing ODBC driver
	Write-host "`tDeploying PostgreSQL ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*postgres*" -Platform "64-bit"
	if ($drivers.Length -eq 0) {
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
	Write-Host "Running QA tests related to QuestDB"
	$testSuccessful = Run-TestSuite @("QuestDB")

	# Stop the docker container if not previously running
	if (!$previouslyRunning){
		Remove-Container $running
	}

	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for QuestDB"
}
Pop-Location