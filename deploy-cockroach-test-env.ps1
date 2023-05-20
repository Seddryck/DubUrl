Param(
	[switch] $force=$false
)
if ($force) {
	Write-Warning "Forcing QA testing for CockRoachDB"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*cockroach*")) {
	Write-Host "Deploying CockRoachDB testing environment"

	# Starting docker container for QuestDb
	$previously_running = $false
	$running = & docker container ls --format "{{.ID}}" --filter "name=roach"
	if ($null -ne $running) {
		$previously_running = $true
		Write-Host "`tContainer is already running with ID '$running'."
	} else {
		Write-Host "`tStarting new container"
		Start-Process -FilePath ".\DubUrl.QA\CockRoach\run-cockroach-docker.cmd"
		do {
			$running = & docker container ls --format "{{.ID}}" --filter "name=roach"
			if ($null -eq $running) {
				Start-Sleep -s 1
			}
		} while($null -eq $running)
		
		Write-Host "`tContainer started with ID '$running'."

		$cmd = "/cockroach/cockroach node status --insecure"
		$startWait = Get-Date
		do {
			$response = & docker exec -it roach-single sh -c "$cmd"
			$isRunning = ($response -join " ") -notlike "ERROR: cannot dial server*"
			$wait = New-TimeSpan -Start $startWait
			if (!$isRunning) {
				if ($wait -gt (New-TimeSpan -Seconds 1)) {
					Write-Host "`t`tWaiting since $($wait.ToString("ss")) seconds ..."
				}
				Start-Sleep -s 1
			}
		} while (!$isRunning -and !($wait -gt (New-TimeSpan -Seconds 60)))
		if (!$isRunning) {
			Write-Warning "Waited during $($wait.ToString("ss")) seconds. Stopping test harness for CockRoachDB."
			exit 0
		} else {
			Write-Host "`tServer is available: waited $($wait.ToString("ss")) seconds to get it live."
		}
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	$statements = Get-Content ".\DubUrl.QA\CockRoach\deploy-cockroach-database.sql"
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
	& dotnet build DubUrl.QA -c Release --nologo
	& dotnet test DubUrl.QA --filter TestCategory="CockRoach" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	if ($lastexitcode -gt 0) {
		throw "At least one test is in error for CockRoach."
	}

	# Stop the docker container if not previously running
	if (!$previously_running -and $null -ne $running){
		Write-Host "`tStopping container '$running' ..."
		& docker stop $running
		Write-Host "`tContainer stopped."
		Write-Host "`tRemoving container '$running' ..."
		& docker rm $running
		Write-Host "`tContainer removed."
	}
} else {
	Write-Host "Skipping the deployment and run of QA testing for CockRoachDB"
}