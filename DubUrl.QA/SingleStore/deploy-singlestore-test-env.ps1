Param(
	[switch] $force=$false
	, [string[]] $odbcDrivers = @("MariaDB", "MySQL")
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net6.0", "net7.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1
. $PSScriptRoot\..\Windows-Service.ps1

if ($force) {
	Write-Host "Enforcing QA testing for SingleStore"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*singlestore*")) {
	Write-Host "Deploying SingleStore testing environment"

	# Starting docker container
	$previouslyRunning, $running = Deploy-Container -FullName "singlestore"
	if (!$previouslyRunning){
		$waitForAvailable = 3
		Write-host "`tWaiting $waitForAvailable seconds for the SingleStore server to be available ..."
		Start-Sleep -s $waitForAvailable
		Write-host "`tSingleStore Server is expected to be available."
	}

	# Deploying database based on script
	Write-host "`tCreating database using remote client on the docker container"
	& docker exec -it singlestoredb-dev singlestore -pPassword12! "--execute=$(Get-Content .\deploy-singlestore-database.sql)" | Out-Null
	Write-host "`tDatabase created"
	
	$odbcDriversInstalled = @()
	# Installing ODBC driver
	# Should call a single script to install MariaDB and MySQL ODBC drivers

	# Running QA tests
	Write-Host "Running QA tests related to SingleStore"
	$suites = @("SingleStore+AdoProvider")
	foreach ($odbcDriverInstalled in $odbcDriversInstalled) {
		$suites += "SingleStore+ODBC+" + $odbcDriverInstalled + "Driver"
	}
	$testSuccessful = Run-TestSuite $suites -config $config -frameworks $frameworks

	# Stopping DB Service
	if (!$previouslyRunning)
	{
        Remove-Container $running
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
