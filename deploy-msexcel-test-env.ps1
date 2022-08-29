Param(
	[switch] $force=$false
)

if ($force) {
	Write-Warning "Forcing QA testing for Microsoft Excel"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*excel*")) {
	Write-Host "Deploying Microsoft Excel testing environment"

	Write-Host "Running QA tests related to Microsoft Excel"
	& dotnet build DubUrl.QA -c Release --nologo
	& dotnet test DubUrl.QA --filter TestCategory="MsExcel" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
} else {
	Write-Host "Skipping the deployment and run of QA testing for Microsoft Excel"
}