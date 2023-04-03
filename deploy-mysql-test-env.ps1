Param(
	[switch] $force=$false
	, $databaseService= "MySQL57"
)
if ($force) {
	Write-Warning "Forcing QA testing for MySQL"
}

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
	$getservice = Get-Service -Name $databaseService -ErrorAction SilentlyContinue
	if ($getservice -ne $null)
	{
		if($getservice.Status -ne 'Running') {
			Start-Service $databaseService 
			Write-host "`tStarting" $databaseService "service"
		} else {
			Write-host "`tService" $databaseService "already started"
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
		Get-Content ".\DubUrl.QA\MySQL\deploy-mysql-database.sql" | & mysql --user=root
	} else {
		Write-host "`tSkipping database creation"
	}

	# Installing ODBC driver
	Write-host "`tDeploying MySQL ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*mysql*" -Platform "64-bit"
	$OdbcDriverInstalled = $false
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading MySQL ODBC driver ..."
		$response = try { 
			(Invoke-WebRequest `
			    -Uri "https://dev.mysql.com/get/Downloads/Connector-ODBC/8.0/mysql-connector-odbc-8.0.32-winx64.msi" `
			    -OutFile "$env:temp\mysql-connector-odbc.msi" `
				-ErrorAction Stop
			).BaseResponse
		} catch [System.Net.WebException] { 
			Write-Verbose "An exception was caught: $($_.Exception.Message)"
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
			$OdbcDriverInstalled = $true
		}
		
	} else {
		$OdbcDriverInstalled = $true
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*mysql*" -Platform "64-bit"
		Write-Host "`t`tSkipping installation of new drivers"
	}

	# Running QA tests
	Write-Host "Running QA tests related to MySQL"
	& dotnet build DubUrl.QA -c Release --nologo
	& dotnet test DubUrl.QA --filter "(TestCategory=MySQL""&""TestCategory=AdoProvider)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	If ($OdbcDriverInstalled -eq $true) {
		& dotnet test DubUrl.QA --filter "(TestCategory=MySQL""&""TestCategory=ODBC)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	}
} else {
	Write-Host "Skipping the deployment and run of QA testing for MySQL"
}