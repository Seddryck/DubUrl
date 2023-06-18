Param(
	[switch] $force=$false
	, [string] $databaseService= "MSSQL`$SQL2019"
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net6.0", "net7.0")
)
. $PSScriptRoot\..\Windows-Service.ps1
. $PSScriptRoot\..\Docker-Container.ps1
. $PSScriptRoot\..\Run-TestSuite.ps1

if ($force) {
	Write-Host "Enforcing QA testing for Microsoft SQL Server"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*mssql*")) {
	Write-Host "Deploying Microsoft SQL Server testing environment"
	
	# Starting database Service
	if ($env:APPVEYOR -eq "True") {
		try { $previouslyRunning = Start-Windows-Service $databaseService }
		catch {
			Write-Warning "Failure to start a Windows service: $_"
			exit 1
		}
	} else {
		$previouslyRunning, $running = Deploy-Container -FullName "mssqlserver" -Nickname "mssql"
		if (!$previouslyRunning){
			Start-Sleep -s 10
		}
	}

	# Deploying database based on script
	Write-host "`tDeploying database ..."
	if ($env:APPVEYOR -eq "True") {
		Write-host "`t`tUsing local client ..."
		& sqlcmd -U "sa" -P "Password12!" -S ".\SQL2019" -i ".\deploy-mssqlserver-database.sql" | Out-Null
	} else {
		Write-host "`t`tCopying deployment script on container ..."
		& docker cp "./deploy-mssqlserver-database.sql" mssql:"./deploy-mssqlserver-database.sql" 
		Write-host "`t`tScript copied"
		Write-host "`t`tUsing remote client on the docker container ..."
		& docker exec -it mssql /opt/mssql-tools/bin/sqlcmd "-Usa" "-PPassword12!" "-i./deploy-mssqlserver-database.sql" | Out-Null
	}
	Write-host "`tDatabase deployed"
	
	# Copying correct config
	Write-Host "`t`tConfiguring URI for instance ..."
	foreach ($framework in $frameworks)
	{
		$filePath = "$PSScriptRoot\..\bin\$config\$framework\Instance.txt"
		$serverUrl = if ($env:APPVEYOR -eq "True") { "localhost/SQL2019" } else { "localhost" }
		$serverUrl | Set-Content -NoNewline -Force $filePath
		Write-Host "`t`tConfigure value '$serverUrl' into $filePath"
	}
	Write-Host "`t`tInstance URI configured."
	

	# Running QA tests
	$testSuccessful = Run-TestSuite @("MsSqlServer") -config $config -frameworks $frameworks

	# Stopping database Service
	if (!$previouslyRunning) {
		if ($env:APPVEYOR -eq "True") {
			Stop-Windows-Service $databaseService
		} else {
			Remove-Container $running
		}
	}
	
	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}