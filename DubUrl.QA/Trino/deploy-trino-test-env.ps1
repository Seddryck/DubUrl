Param(
	[switch] $force=$false
	, $config = "Release"
	, $networkName = "trino-network"
)
Push-Location $PSScriptRoot
. $PSScriptRoot\..\Run-TestSuite.ps1

if ($force) {
	Write-Warning "Forcing QA testing for Trino"
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
	$containerName = "postgresql"
	Write-Host "`tStarting container '$containerName' ..."
	$running = & docker container ls --format "{{.ID}}" --filter "name=$containerName"
	if ($running) {
		Write-Host "`tContainer '$containerName' is already running with ID '$running'."
	} else {
		Start-Process -FilePath ".\..\PostgreSQL\run-postgresql-docker.cmd"
		do {
			$running = & docker container ls --format "{{.ID}}" --filter "name=$containerName"
			if (!$running) {
				Start-Sleep -s 1
			}
		} while(!$running)
		Write-Host "`tContainer '$containerName' started with ID '$running'."
	}
	if ($containers.Contains($running)) {
		Write-Host "`tContainer '$running' already connected to network '$networkName'."
	} else {
		Write-Host "`tConnecting container '$running' to network '$networkName' ..."
		& docker network connect $networkName $running
		Write-Host "`tContainer'$running' connected to network '$networkName'."
	}

	# Starting docker container for Trino
	$containerName = "trino"
	$running = & docker container ls --format "{{.ID}}" --filter "name=$containerName"
	if ($running) {
		$previously_running = $true
		Write-Host "`tContainer '$containerName' is already running with ID '$running'."
	} else {
		$mountedFolder = ".\..\bin\$config\net6.0\.catalog"
		Write-Host "`tStarting new container '$containerName' with mounting at $mountedFolder"
		Start-Process -FilePath ".\run-trino-docker.cmd" -ArgumentList @($mountedFolder)
		do {
			$running = & docker container ls --format "{{.ID}}" --filter "name=$containerName"
			if (!$running) {
				Start-Sleep -s 1
			}
		} while(!$running)
		
		Write-Host "`tContainer '$containerName' started with ID '$running'."
		Start-Sleep -s 10
	}
	if ($containers.Contains($running)) {
		Write-Host "`tContainer '$running' already connected to network '$networkName'."
	} else {
		Write-Host "`tConnecting container '$running' to network '$networkName' ..."
		& docker network connect $networkName $running
		Write-Host "`tContainer'$running' connected to network '$networkName'."
	}

	# Deploying database based on script
	Write-host "`tCreating database"
	If (-not($env:PATH -like $pgPath)) {
		$env:PATH += ";$pgPath"
	}
	$env:PGPASSWORD = "Password12!"
	& psql -U "postgres" -h "localhost" -p "5432" -f ".\..\PostgreSQL\deploy-pgsql-database.sql"
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
	$testSuccessful = Run-TestSuite $suites

	#Stop the docker container if not previously running
	if (!$previously_running){
		$running = & docker container ls --format "{{.ID}}" --filter "name=trino"
		if ($null -ne $running) {
			Write-Host "`tForcefully removing container '$running' ..."
			& docker rm --force $running | Out-Null
			Write-Host "`tContainer removed."
		}

		$running = & docker container ls --format "{{.ID}}" --filter "name=postgresql"
		if ($null -ne $running) {
			Write-Host "`tForcefully removing container '$running' ..."
			& docker rm --force $running | Out-Null
			Write-Host "`tContainer removed."
		}
	}

	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for Trino"
}
Pop-Location