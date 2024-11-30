Param(
	[switch] $force=$false
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net8.0", "net9.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1

if ($force) {
	Write-Host "Enforcing QA testing for QuestDB"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*quest*")) {
	Write-Host "Deploying QuestDB testing environment"

	# Starting docker container
	$previouslyRunning, $running = Deploy-Container -FullName "questdb" -NickName "quest"
	if (!$previouslyRunning) {
		$waitForAvailable = 10
		if ($env:APPVEYOR -eq "True") {
			$waitForAvailable = 30
		}
		Write-host "`tWaiting $waitForAvailable seconds for the server to be available ..."
		Start-Sleep -s $waitForAvailable
		Write-host "`tServer is expected to be available."
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	if (-not($env:PATH -like $pgPath)) {
		$env:PATH += ";$pgPath"
	}
	$env:PGPASSWORD = "quest"
	& psql -U "admin" -h "localhost" -p "8812" -f ".\deploy-questdb-database.sql"
	Write-host "`tDatabase created"

	# Installing ODBC driver
	. $PSScriptRoot\..\Postgresql\deploy-postgresql-odbc-driver.ps1

	# Running QA tests
	Write-Host "Running QA tests related to QuestDB"
	$testSuccessful = Run-TestSuite @("QuestDB") -config $config -frameworks $frameworks

	# Stop the docker container if not previously running
	if (!$previouslyRunning){
		Remove-Container $running
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
