Param(
	[switch] $force=$false
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net8.0", "net9.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1

if ($force) {
	Write-Host "Enforcing QA testing for Apache Drill"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*drill*")) {
	Write-Host "Deploying Apache Drill testing environment"

	# Deploying mounted folder
	foreach ($framework in $frameworks)
	{
		$mountedFolder = ".\..\bin\$config\$framework\.bigdata"
		Write-host "`tCopying file to mounted folder '$mountedFolder' ..."
		if (Test-Path -Path $mountedFolder) {
			Remove-Item -Path $mountedFolder -Force -Recurse
		}
		New-Item -Path $mountedFolder -Type Directory | out-null 
		Copy-Item -Path ".\..\.bigdata\*" -Destination $mountedFolder -Recurse
		Write-Host "`tFiles copied to mounted folder."
	}
	
	# Starting docker container for Apache Drill
	$previouslyRunning, $running = Deploy-Container -FullName "drill" -Arguments @("$PSScriptRoot\..\bin\$config\net6.0\.bigdata")
	if (!$previouslyRunning) {
		$waitForAvailable = 10
		if ($env:APPVEYOR -eq "True") {
			$waitForAvailable = 50
		}
		Write-host "`tWaiting $waitForAvailable seconds for the server to be available ..."
		Start-Sleep -s $waitForAvailable
		Write-host "`tServer is expected to be available."
	}

	$odbcDriverInstalled = $false
	# Installing ODBC driver
	Write-host "`tDeploying MapR Drill ODBC driver"
	$drivers = Get-OdbcDriver -Name "*drill*" -Platform "64-bit"
	if ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading MapR Drill ODBC driver ..."
		Invoke-WebRequest `
				-Uri "http://package.mapr.com/tools/MapR-ODBC/MapR_Drill/MapRDrill_odbc_v1.3.22.1055/MapR%20Drill%201.3%2064-bit.msi" `
				-OutFile "$env:temp\drill-odbc.msi"
		Write-Host "`t`tInstalling MapR Drill ODBC driver ..."
		& msiexec /i "$env:temp\drill-odbc.msi" /quiet /qn /norestart /log "$env:temp\install-drill.log" | Out-Null
		#Get-Content "$env:temp\install-drill.log" | Write-Host
		Write-Host "`t`tChecking installation ..."
		Get-OdbcDriver -Name "*drill*" -Platform "64-bit"
		Write-Host "`tDeployment of MapR Drill ODBC driver finalized."
		$odbcDriverInstalled = $true
	} else {
		$odbcDriverInstalled = $true
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*drill*" -Platform "64-bit"
		Write-Host "`t`tSkipping installation of new drivers"
	}

	# Running QA tests
	Write-Host "Running QA tests related to Drill"
	$suites = @("Drill+AdoProvider")
	if ($odbcDriverInstalled) {
		$suites += "Drill+ODBC"
	}
	$testSuccessful = Run-TestSuite $suites -config $config -frameworks $frameworks

	# Stop the docker container if not previously running
	if (!$previouslyRunning){
		Remove-Container $running
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
