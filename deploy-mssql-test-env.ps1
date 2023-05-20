Param(
	[switch] $force=$false
	, $databaseService= "MSSQL`$SQL2019"
)

if ($force) {
	Write-Warning "Forcing QA testing for mssql"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*mssql*")) {
	Write-Host "Deploying mssql testing environment"

	$previouslyRunning = $false
	$getservice = Get-Service -Name $databaseService
	if($getservice.Status -ne 'Running'){
		Start-Service $databaseService 
		Write-host "`tStarting" $databaseService "service"
	} else {
		Write-host "`tService" $databaseService "already started"
		$previouslyRunning = $true
	}
	
	Write-host "`tDeploying database"
	& sqlcmd -U "sa" -P "Password12!" -S ".\SQL2019" -i ".\DubUrl.QA\MsSqlServer\deploy-mssql-database.sql"
	
	Write-Host "Running QA tests related to mssql"
	& dotnet build DubUrl.QA -c Release --nologo
	& dotnet test DubUrl.QA --filter TestCategory="MsSqlServer" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo

	# Stopping DB Service
	$getservice = Get-Service -Name $databaseService -ErrorAction SilentlyContinue
	if ($null -ne $getservice -and !$previouslyRunning)
	{
		if($getservice.Status -ne 'Stopped') {
			Stop-Service $databaseService 
			Write-host "`tStopping" $databaseService "service"
		} else {
			Write-host "`tService" $databaseService "already stopped"
		}
	} else {
		if ($previouslyRunning) {
			Write-Warning "Service $databaseService was running before the deployment of the test harness, not stopping it."
		}
	}
} else {
	Write-Host "Skipping the deployment and run of QA testing for mssql"
}