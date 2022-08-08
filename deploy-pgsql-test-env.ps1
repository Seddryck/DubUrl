Param(
	[switch] $force=$false
	, $databaseService= "postgresql-x64-13"
)

$pgPath = "C:\Program Files\PostgreSQL\$($databaseService.Split('-')[2])\bin"
If (-not (Test-Path -Path $pgPath)) {
	$pgPath = $pgPath -replace "C:", "E:"
}
Write-Host "Using '$pgPath' as PostgreSQL installation folder"

if ($force) {
	Write-Warning "Forcing QA testing for PostgreSQL"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*pgsql*")) {
	Write-Host "Deploying PostgreSQL testing environment"

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
		Write-Warning "Service $databaseService is not installed. Expecting that PostgreSQL is running on docker."
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	If (-not($env:PATH -like $pgPath)) {
		$env:PATH += ";$pgPath"
	}
	$env:PGPASSWORD = "Password12!"
	& psql -U "postgres" -h "localhost" -f ".\DubUrl.QA.Pgsql\deploy-pgsql-database.sql"

	# Installing ODBC driver
	Write-host "`tDeploying PostgreSQL ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*postgres*" -Platform "64-bit"
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading PostgreSQL ODBC driver ..."
		Invoke-WebRequest "https://ftp.postgresql.org/pub/odbc/versions/msi/psqlodbc_13_02_0000-x64.zip" -OutFile "$env:temp\psqlodbc.zip"
		Write-Host "`t`tExtracting from archive PostgreSQL ODBC driver ..."
		& 7z e "$env:temp\psqlodbc.zip" -o"$env:temp" -y
		Write-Host "`t`tInstalling PostgreSQL ODBC driver ..."
		& msiexec /i "$env:temp\psqlodbc_x64.msi" /quiet /qn /norestart /log "$env:temp\install.log" | Out-Host
		Get-Content "$env:temp\install.log"
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
	& dotnet build DubUrl.QA.Pgsql -c Release --nologo
	& dotnet test DubUrl.QA.Pgsql -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
} else {
	Write-Host "Skipping the deployment and run of QA testing for PostgreSQL"
}