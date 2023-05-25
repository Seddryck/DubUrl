Param(
	[switch] $force=$false
)
if ($force) {
	Write-Warning "Forcing QA testing for CockRoachDB"
}
Push-Location $PSScriptRoot
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*cockroach*")) {
	Write-Host "Deploying CockRoachDB testing environment"

	# Starting docker container
	$previouslyRunning, $running = Deploy-Container -FullName "cockroach" -NickName "roach" -ScriptBlock {
			$cmd = "/cockroach/cockroach node status --insecure"
			$response = & docker exec -it roach-single sh -c "$cmd"
			return ($response -join " ") -notlike "ERROR: cannot dial server*"
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	$statements = Get-Content ".\deploy-cockroach-database.sql"
	$cmd = "/cockroach/cockroach sql --insecure --database duburl --execute=""$statements"";"
	& docker exec -it roach-single sh -c "$cmd"

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
	Write-Host "Running QA tests related to CockRoach"
	$testSuccessful = Run-TestSuite @("CockRoach")

	# Stop the docker container if not previously running
	if (!$previouslyRunning){
		Remove-Container $running
	}

	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for CockRoachDB"
}
Pop-Location