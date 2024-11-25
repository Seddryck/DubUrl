Param(
	[switch] $force=$false
	, [string] $databaseService= "MySQL80"
	, [string[]] $odbcDrivers = @("MariaDB", "MySQL")
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net6.0", "net8.0", "net9.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1
. $PSScriptRoot\..\Windows-Service.ps1

if ($force) {
	Write-Host "Enforcing QA testing for MySQL"
}

$databaseVersion = $databaseService.Substring($databaseService.Length - 2)
$mySqlPath = "C:\Program Files\MySQL\MySQL Server $($databaseVersion[0]).$($databaseVersion[1])\bin"
if (-not (Test-Path -Path $mySqlPath)) {
	$mySqlPath = $mySqlPath -replace "C:", "E:"
	If (-not (Test-Path -Path $mySqlPath)) {
		$mySqlPath = $mySqlPath -replace "E:", "C:"
		Write-Warning "MySQL installation folder '$mySqlPath' doesn't exist'"
	}
}
Write-Host "Using '$mySqlPath' as MySQL installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*mysql*")) {
	Write-Host "Deploying MySQL testing environment"

	# Starting database service or docker container
	if ($env:APPVEYOR -eq "True") {
		try { $previouslyRunning = Start-Windows-Service $databaseService }
		catch {
			Write-Warning "Failure to start a Windows service: $_"
			exit 1
		}
	} else {
		$previouslyRunning, $running = Deploy-Container -FullName "mysql"
		if (!$previouslyRunning){
			$waitForAvailable = 10
			Write-host "`tWaiting $waitForAvailable seconds for the server to be available ..."
			Start-Sleep -s $waitForAvailable
			Write-host "`tServer is expected to be available."
		}
	}

	# Deploying database based on script
	$env:MYSQL_PWD = "Password12!"
	if (Test-Path -Path $mySqlPath) {
		Write-host "`tCreating database using local client"
		if (-not($env:PATH -like $mySqlPath)) {
			$env:PATH += ";$mySqlPath"
		}
		Get-Content ".\deploy-mysql-database.sql" | & mysql --user=root
	} else {
		Write-host "`tCreating database using remote client on the docker container"
		& docker exec -it some-mysql mysql "--user=root" "--password=$($env:MYSQL_PWD)" "--execute=$(Get-Content .\deploy-mysql-database.sql)" | Out-Null
	}
	Write-host "`tDatabase created"
	
	$odbcDriversInstalled = @()
	# Installing ODBC driver
	if ($odbcDrivers -contains "MariaDB") {
		Write-host "`tDeploying MariaDB ODBC drivers"
		$drivers = Get-OdbcDriver -Name "*mariadb*" -Platform "64-bit"
		if ($drivers.Length -eq 0) {
			Write-Host "`t`tDownloading MariaDB ODBC driver ..."
			Invoke-WebRequest `
					-Uri "https://dlm.mariadb.com/2454057/Connectors/odbc/connector-odbc-3.1.17/mariadb-connector-odbc-3.1.17-win64.msi" `
					-OutFile "$env:temp\mariadb-connector-odbc.msi"
			Write-Host "`t`tMariaDB ODBC driver downloaded."
			Write-Host "`t`tInstalling MariaDB ODBC driver ..."
			& msiexec /i "$env:temp\mariadb-connector-odbc.msi" /quiet /qn /norestart /log "$env:temp\install-mariadb.log" | Out-Host
			#Get-Content "$env:temp\install-mariadb.log" | Write-Host
			Write-Host "`t`tMariaDB ODBC driver installed."
			Write-Host "`t`tChecking installation ..."
			Get-OdbcDriver -Name "*mariadb*" -Platform "64-bit"
			Write-Host "`t`tInstallation checked."
			Write-Host "`tDeployment of MariaDB ODBC driver finalized."
			$odbcDriversInstalled += "MariaDB"
		} else {
			$odbcDriversInstalled += "MariaDB"
			Write-Host "`t`tDrivers already installed:"
			Get-OdbcDriver -Name "*mariadb*" -Platform "64-bit"
			Write-Host "`t`tSkipping installation of new drivers"
		}
	} 
	if ($odbcDrivers -contains "MySQL") {
		Write-host "`tDeploying MySQL ODBC drivers"
		$drivers = Get-OdbcDriver -Name "*mysql*" -Platform "64-bit"
		if ($drivers.Length -eq 0) {
			Write-Host "`t`tDownloading MySQL ODBC driver ..."
			$response = try { 
				(Invoke-WebRequest `
					-Uri "https://cdn.mysql.com//Downloads/Connector-ODBC/8.1/mysql-connector-odbc-8.1.0-winx64.msi" `
					-OutFile "$env:temp\mysql-connector-odbc.msi" `
					-PassThru `
				).BaseResponse
			} catch { 
				Write-Host "An exception was caught: $($_.Exception.Message)"
				$_.Exception.Response 
			} 
			$statusCodeInt = [int]$response.StatusCode

			If ($statusCodeInt -eq 200) {
				Write-Host "`t`tMySQL ODBC driver downloaded."
				Write-Host "`t`tInstalling MySQL ODBC driver ..."
				& msiexec /i "$env:temp\mysql-connector-odbc.msi" /quiet /qn /norestart /log "$env:temp\install-mysql.log" | Out-Host
				#Get-Content "$env:temp\install-mysql.log"
				Write-Host "`t`tMySQL ODBC driver installed."
				Write-Host "`t`tChecking installation ..."
				Get-OdbcDriver -Name "*mysql*" -Platform "64-bit"
				Write-Host "`t`tInstallation checked."
				Write-Host "`tDeployment of MySQL ODBC driver finalized."
				$odbcDriversInstalled += "MySQL"
			} else {
				Write-Host "`t`tFailed to download MySQL ODBC driver."
				Write-Host "`tInstalling MySQL ODBC driver was interrupted."
			}
		} else {
			$odbcDriversInstalled += "MySQL"
			Write-Host "`t`tDrivers already installed:"
			Get-OdbcDriver -Name "*mysql*" -Platform "64-bit"
			Write-Host "`t`tSkipping installation of new drivers"
		}
	}

	# Running QA tests
	Write-Host "Running QA tests related to MySQL"
	$suites = @("MySQL+AdoProvider")
	foreach ($odbcDriverInstalled in $odbcDriversInstalled) {
		$suites += "MySQL+ODBC+" + $odbcDriverInstalled + "Driver"
	}
	$testSuccessful = Run-TestSuite $suites -config $config -frameworks $frameworks

	# Stopping DB Service
	if (!$previouslyRunning)
	{
		$service = Get-Service -Name $databaseService -ErrorAction SilentlyContinue
		if ($null -ne $service) { 
			Stop-Service $databaseService 
		} else {
			Remove-Container $running
		}
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
