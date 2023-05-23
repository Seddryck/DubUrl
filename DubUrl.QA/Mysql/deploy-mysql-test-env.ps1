Param(
	[switch] $force=$false
	, $databaseService= "MySQL57"
	, $odbcDriver= "MariaDB"
)
if ($force) {
	Write-Warning "Forcing QA testing for MySQL"
}
Push-Location $PSScriptRoot

$databaseVersion = $databaseService.Substring($databaseService.Length - 2)
$mySqlPath = "C:\Program Files\MySQL\MySQL Server $($databaseVersion[0]).$($databaseVersion[1])\bin"
If (-not (Test-Path -Path $mySqlPath)) {
	$mySqlPath = $mySqlPath -replace "C:", "E:"
	If (-not (Test-Path -Path $mySqlPath)) {
		$mySqlPath = $mySqlPath -replace "E:", "C:"
	}
}
Write-Host "Using '$mySqlPath' as MySQL installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*mysql*")) {
	Write-Host "Deploying MySQL testing environment"

	# Starting database service
	$previouslyRunning = $false
	$getservice = Get-Service -Name $databaseService -ErrorAction SilentlyContinue
	if ($null -ne $getservice)
	{
		if($getservice.Status -ne 'Running') {
			Start-Service $databaseService 
			Write-host "`tStarting" $databaseService "service"
		} else {
			Write-host "`tService" $databaseService "already started"
			$previouslyRunning = $true
		}
	} else {
		Write-Warning "Service $databaseService is not installed. Expecting that MySQL is running on docker."
	}

	# Deploying database based on script
	If (Test-Path -Path $mySqlPath) {
		Write-host "`tCreating database"
		If (-not($env:PATH -like $mySqlPath)) {
			$env:PATH += ";$mySqlPath"
		}
		$env:MYSQL_PWD = "Password12!"
		Get-Content ".\deploy-mysql-database.sql" | & mysql --user=root
		Write-host "`tDatabase created"
	} else {
		Write-host "`tSkipping database creation"
	}

	$odbcDriverInstalled = $false
	# Installing ODBC driver
	if ($odbcDriver -eq "MariaDB") {
		Write-host "`tDeploying MariaDB ODBC drivers"
		$drivers = Get-OdbcDriver -Name "*mariadb*" -Platform "64-bit"
		if ($drivers.Length -eq 0) {
			Write-Host "`t`tDownloading MariaDB ODBC driver ..."
			Invoke-WebRequest `
					-Uri "https://dlm.mariadb.com/2454057/Connectors/odbc/connector-odbc-3.1.17/mariadb-connector-odbc-3.1.17-win64.msi" `
					-OutFile "$env:temp\mariadb-connector-odbc.msi"
			Write-Host "`t`tInstalling MariaDB ODBC driver ..."
			& msiexec /i "$env:temp\mariadb-connector-odbc.msi" /quiet /qn /norestart /log "$env:temp\install-mariadb.log" | Out-Host
			#Get-Content "$env:temp\install-mariadb.log" | Write-Host
			Write-Host "`t`tChecking installation ..."
			Get-OdbcDriver -Name "*mariadb*" -Platform "64-bit"
			Write-Host "`tDeployment of MariaDB ODBC driver finalized."
			$odbcDriverInstalled = $true
		} else {
			$odbcDriverInstalled = $true
			Write-Host "`t`tDrivers already installed:"
			Get-OdbcDriver -Name "*mariadb*" -Platform "64-bit"
			Write-Host "`t`tSkipping installation of new drivers"
		}
	} else {
		Write-host "`tDeploying MySQL ODBC drivers"
		$drivers = Get-OdbcDriver -Name "*mysql*" -Platform "64-bit"
		if ($drivers.Length -eq 0) {
			Write-Host "`t`tDownloading MySQL ODBC driver ..."
			$response = try { 
				(Invoke-WebRequest `
					-Uri "https://dev.mysql.com/get/Downloads/Connector-ODBC/8.0/mysql-connector-odbc-8.0.32-winx64.msi" `
					-OutFile "$env:temp\mysql-connector-odbc.msi" `
				).BaseResponse
			} catch { 
				Write-Host "An exception was caught: $($_.Exception.Message)"
				$_.Exception.Response 
			} 
			$statusCodeInt = [int]$response.BaseResponse.StatusCode

			If ($statusCodeInt -eq 200) {
				Write-Host "`t`tInstalling MySQL ODBC driver ..."
				& msiexec /i "$env:temp\mysql-connector-odbc.msi" /quiet /qn /norestart /log "$env:temp\install-mysql.log" | Out-Host
				#Get-Content "$env:temp\install-mysql.log"
				Write-Host "`t`tChecking installation ..."
				Get-OdbcDriver -Name "*mysql*" -Platform "64-bit"
				Write-Host "`tDeployment of MySQL ODBC driver finalized."
				$odbcDriverInstalled = $true
			} else {
				Write-Host "`t`tInstalling MySQL ODBC driver was interrupted."
			}
		} else {
			$odbcDriverInstalled = $true
			Write-Host "`t`tDrivers already installed:"
			Get-OdbcDriver -Name "*mysql*" -Platform "64-bit"
			Write-Host "`t`tSkipping installation of new drivers"
		}
	}

	# Running QA tests
	Write-Host "Running QA tests related to MySQL"
	& dotnet build "..\..\DubUrl.QA" -c Release --nologo | out-null
	& dotnet test "..\..\DubUrl.QA" --filter "(TestCategory=MySQL""&""TestCategory=AdoProvider)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	$testSuccessful = ($lastexitcode -gt 0)
	if ($odbcDriverInstalled -eq $true) {
		& dotnet test "..\..\DubUrl.QA" --filter "(TestCategory=MySQL""&""TestCategory=ODBC)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
		$testSuccessful += ($lastexitcode -gt 0)
	}

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
	Write-Host "Skipping the deployment and run of QA testing for MySQL"
}
Pop-Location