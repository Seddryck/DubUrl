Param(
	[switch] $force=$false
	, $databaseService= "MSSQL`$SQL2019"
)

if ($force) {
	Write-Warning "Forcing QA testing for Microsoft SQL Server"
}
Push-Location $PSScriptRoot

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*mssql*")) {
	Write-Host "Deploying mssql testing environment"

	$previouslyRunning = $false
	$getservice = Get-Service -Name $databaseService
	if($getservice.Status -ne 'Running'){
		Write-host "`tStarting $databaseService service"
		Start-Service $databaseService 
		Write-host "`tService started"
	} else {
		Write-host "`tService" $databaseService "already started"
		$previouslyRunning = $true
	}

	Write-host "`tDeploying database"
	& sqlcmd -U "sa" -P "Password12!" -S ".\SQL2019" -i ".\deploy-mssql-database.sql"
	Write-host "`tDatabase deployed"
	
	Write-Host "Running QA tests related to Microsoft SQL Server"
	& dotnet build "..\..\DubUrl.QA" -c Release --nologo | out-null
	& dotnet test "..\..\DubUrl.QA" --filter TestCategory="MsSqlServer" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
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
	Write-Host "Skipping the deployment and run of QA testing for mssql"
}
Pop-Location