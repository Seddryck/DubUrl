Param(
	[switch] $force=$false
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net6.0", "net7.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1

if ($force) {
	Write-Host "Enforcing QA testing for Microsoft Access"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*access*")) {
	Write-Host "Deploying Microsoft Access testing environment"

	# Installing ODBC driver
	Write-host "`tDeploying Microsoft Access ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*accdb*" -Platform "64-bit"
	
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading Microsoft Access ODBC driver ..."
		Invoke-WebRequest "https://download.microsoft.com/download/3/5/C/35C84C36-661A-44E6-9324-8786B8DBE231/accessdatabaseengine_X64.exe" -OutFile "$env:temp\accessdatabaseengine_X64.exe"
		Write-Host "`t`tInstalling Microsoft Access ODBC driver ..."
		& cmd /c start /wait "$env:temp\accessdatabaseengine_X64.exe" /quiet
		Write-Host "`t`tChecking installation ..."
		Get-OdbcDriver -Name "*accdb*"
		Write-Host "`tDeployment of Microsoft Access ODBC driver finalized."
	} else {
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*accdb*" -Platform "64-bit"
		Write-Host "`t`tSkipping installation of new drivers"
	}

	# Running QA tests
	Write-Host "Running QA tests related to Microsoft Access"
	$testSuccessful = Run-TestSuite @("MsAccess") -config $config -frameworks $frameworks

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
