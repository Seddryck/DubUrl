Param(
	[switch] $force=$false
)

if ($force) {
	Write-Warning "Forcing QA testing for Microsoft Excel"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*excel*")) {
	Write-Host "Deploying Microsoft Excel testing environment"

	# Installing ODBC driver
	Write-host "`tDeploying Microsoft Excel ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*xlsx*" -Platform "64-bit"
	
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading Microsoft Excel ODBC driver ..."
		Invoke-WebRequest "https://download.microsoft.com/download/3/5/C/35C84C36-661A-44E6-9324-8786B8DBE231/accessdatabaseengine_X64.exe" -OutFile "$env:temp\accessdatabaseengine_X64.exe"
		Write-Host "`t`tInstalling Microsoft Excel ODBC driver ..."
		& cmd /c start /wait "$env:temp\accessdatabaseengine_X64.exe" /quiet
		Write-Host "`t`tChecking installation ..."
		Get-OdbcDriver -Name "*xlsx*"
		Write-Host "`tDeployment of Microsoft Excel ODBC driver finalized."
	} else {
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*xlsx*" -Platform "64-bit"
		Write-Host "`t`tSkipping installation of new drivers"
	}

	Write-Host "Running QA tests related to Microsoft Excel"
	& dotnet build DubUrl.QA -c Release --nologo
	& dotnet test DubUrl.QA --filter TestCategory="MsExcel" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	$testSuccessful = ($lastexitcode -gt 0)

	# Raise failing tests
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for Microsoft Excel"
}