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

	$getservice = Get-Service -Name $databaseService
	if($getservice.Status -ne 'Running'){
		Start-Service $databaseService 
		Write-host "`tStarting" $databaseService "service"
	} else {
		Write-host "`tService" $databaseService "already started"
	}
	Write-host "`tDeploying database"
	& sqlcmd -U "sa" -P "Password12!" -S ".\SQL2019" -i ".\DubUrl.QA.Mssql\deploy-mssql-database.sql"
	
	Write-Host "Running QA tests related to mssql"
	& dotnet build DubUrl.QA.Mssql -c Release --nologo
	& dotnet test DubUrl.QA.Mssql -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
} else {
	Write-Host "Skipping the deployment and run of QA testing for mssql"
}