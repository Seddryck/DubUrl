Param(
	[switch] $force=$false
	, $databaseService= "postgresql-x64-13"
)
if ($force) {
	Write-Warning "Forcing QA testing for PostgreSQL"
}
Push-Location $PSScriptRoot

$pgPath = "C:\Program Files\PostgreSQL\$($databaseService.Split('-')[2])\bin"
If (-not (Test-Path -Path $pgPath)) {
	$pgPath = $pgPath -replace "C:", "E:"
}
Write-Host "Using '$pgPath' as PostgreSQL installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*pgsql*")) {
	Write-Host "Deploying PostgreSQL testing environment"

	# Starting database service
	$previouslyRunning = $false
	$getservice = Get-Service -Name $databaseService -ErrorAction SilentlyContinue
	if ($null -ne $getservice)
	{
		if($getservice.Status -ne 'Running') {
			Write-host "`tStarting $databaseService service"
			Start-Service $databaseService 
			Write-host "`tService started"
		} else {
			Write-host "`tService" $databaseService "already started"
			$previouslyRunning = $true
		}
	} else {
		Write-Warning "Service $databaseService is not installed. Expecting that PostgreSQL is running on docker."
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
	& dotnet build "..\..\DubUrl.QA" -c Release --nologo | out-null
	& dotnet test "..\..\DubUrl.QA" --filter TestCategory="Postgresql" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	$testSuccessful = ($lastexitcode -gt 0)

	# Stopping DB Service
	$getservice = Get-Service -Name $databaseService -ErrorAction SilentlyContinue
	if ($null -ne $getservice -and !$previouslyRunning)
	{
		if($getservice.Status -ne 'Stopped') {
			Write-host "`tStopping $databaseService service"
			Stop-Service $databaseService 
			Write-host "`tService stopped"
		} else {
			Write-host "`tService" $databaseService "already stopped"
		}
	} else {
		if ($previouslyRunning) {
			Write-Warning "Service $databaseService was running before the deployment of the test harness, not stopping it."
		}
	}

	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for PostgreSQL"
}
Pop-Location
