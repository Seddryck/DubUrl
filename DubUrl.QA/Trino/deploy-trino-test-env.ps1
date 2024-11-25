Param(
	[switch] $force=$false
	, [string] $networkName = "trino-network"
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net6.0", "net8.0", "net9.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1

if ($force) {
	Write-Host "Enforcing QA testing for Trino"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*trino*")) {
	Write-Host "Deploying Trino testing environment"

	# Checking docker architecture
	$dockerVersion = docker version --format '{{json .}}' | ConvertFrom-Json
	$dockerServerOS = $dockerVersion.Server.Os
	Write-Host "OS/architecture for docker engine: $dockerServerOS"
	if ($dockerServerOS -ne "linux") {
		Write-Warning "Cannot run this test-suite because docker server OS/architecture is not Linux"
		exit 1
	}

	# Creating network
	$network = & docker network inspect $networkName --format "{{.ID}}" 2>$null
	if (!$network) {
		$network = & docker network create $networkName
		Write-Host "`tNetwork '$networkName' created with ID '$network'"
	} else {
		Write-Host "`tNetwork '$networkName' already existing with ID '$network'"
	}
	$containers = & docker network inspect $networkName -f '{{json .Containers}}'

	# Starting docker container for Postgresql
	$previouslyRunning, $running = Deploy-Container -FullName "postgresql" -FilePath ".\..\PostgreSQL\run-postgresql-docker.cmd"
	Connect-Network -Container $running -Network $networkName

	# Starting docker container for Trino
	$mountedFolder = ".\..\bin\$config\net6.0\Trino\.catalog"
	$previouslyRunning, $running = Deploy-Container -FullName "trino" -Arguments @($mountedFolder)
	if (!$previouslyRunning) {
		Start-Sleep -s 10
	}
	Connect-Network -Container $running -Network $networkName

	# Deploying database based on script
	Write-host "`tCreating database"
	If (-not($env:PATH -like $pgPath)) {
		$env:PATH += ";$pgPath"
	}
	$env:PGPASSWORD = "Password12!"
	& psql -U "postgres" -h "localhost" -p "5432" -f ".\..\PostgreSQL\deploy-postgresql-database.sql"
	Write-host "`tDatabase created"

	#Install ODBC drivers
	Write-host "`tDeploying Simba Trino ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*trino*" -Platform "64-bit"
	if ($drivers.Length -eq 0) {
		Write-Host "`t`tCannot download Simba Trino ODBC driver (licence file needed). Skipping installation."
		$odbcDriverInstalled = $false
	} else {
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*trino*" -Platform "64-bit"
		Write-Host "`t`tSkipping installation of new drivers"
		$odbcDriverInstalled = $true
	}

	# Running QA tests
	Write-Host "Running QA tests related to Trino"
	$suites = @("Trino+AdoProvider")
	if ($odbcDriverInstalled) {
		$suites += "Trino+ODBC"
	}
	$testSuccessful = Run-TestSuite $suites -config $config -frameworks $frameworks

	#Remove the docker containers, if not previously running
	if (!$previouslyRunning){
		$running = & docker container ls --format "{{.ID}}" --filter "name=trino"
		Remove-Container $running

		$running = & docker container ls --format "{{.ID}}" --filter "name=postgresql"
		Remove-Container $running
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
