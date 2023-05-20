Param(
	[switch] $force=$false
	, $config = "Release"
)
if ($force) {
	Write-Warning "Forcing QA testing for QuestDB"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*quest*")) {
	Write-Host "Deploying QuestDB testing environment"

	# Starting docker container for Apache Drill
	$previously_running = $false
	$running = & docker container ls --format "{{.ID}}" --filter "name=quest"
	if ($null -ne $running) {
		$previously_running = $true
		Write-Host "`tContainer is already running with ID '$running'."
	} else {
		Write-Host "`tStarting new container with mounting at $mountedFolder"
		Start-Process -FilePath ".\DubUrl.QA\QuestDB\run-questdb-docker.cmd"
		do {
			$running = & docker container ls --format "{{.ID}}" --filter "name=questdb"
			if ($null -eq $running) {
				Start-Sleep -s 1
			}
		} while($null -eq $running)
		
		Write-Host "`tContainer started with ID '$running'."
		Start-Sleep -s 10
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	If (-not($env:PATH -like $pgPath)) {
		$env:PATH += ";$pgPath"
	}
	$env:PGPASSWORD = "quest"
	& psql -U "admin" -h "localhost" -p "8812" -f ".\DubUrl.QA\QuestDB\deploy-questdb-database.sql"

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
	Write-Host "Running QA tests related to mssql"
	& dotnet build DubUrl.QA -c Release --nologo
	& dotnet test DubUrl.QA --filter TestCategory="QuestDB" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	$testSuccessful = ($lastexitcode -gt 0)

	# Stop the docker container if not previously running
	if (!$previously_running -and $null -ne $running){
		Write-Host "`tForcefully removing container '$running' ..."
		& docker rm --force $running | Out-Null
		Write-Host "`tContainer removed."
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for QuestDB"
}