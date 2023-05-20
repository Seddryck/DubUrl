Param(
	[switch] $force=$false
	, $config = "Release"
	, $networkName = "trino-network"
)
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
	$containers = docker network inspect $networkName -f '{{json .Containers}}'

	# Starting docker container for Postgresql
	$containerName = "postgresql"
	$running = & docker container ls --format "{{.ID}}" --filter "name=$containerName"
	if ($running) {
		Write-Host "`tContainer '$containerName' is already running with ID '$running'."
	} else {
		Start-Process -FilePath ".\DubUrl.QA\PostgreSQL\run-postgresql-docker.cmd"
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
		Write-Host "`tContainer '$containerName' is already running with ID '$running'."
	} else {
		$mountedFolder = ".\DubUrl.QA\bin\$config\net6.0\.catalog"
		Write-Host "`tStarting new container '$containerName' with mounting at $mountedFolder"
		Start-Process -FilePath ".\DubUrl.QA\Trino\run-trino-docker.cmd" -ArgumentList @($mountedFolder)
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
	& psql -U "postgres" -h "localhost" -p "5432" -f ".\DubUrl.QA\PostgreSQL\deploy-pgsql-database.sql"

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
	& dotnet build DubUrl.QA -c Release --nologo
	& dotnet test DubUrl.QA --filter "(TestCategory=Trino""&""TestCategory=AdoProvider)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	$testSuccessful = ($lastexitcode -gt 0)
	if ($odbcDriverInstalled -eq $true) {
		& dotnet test DubUrl.QA --filter "(TestCategory=Trino""&""TestCategory=ODBC)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
		$testSuccessful += ($lastexitcode -gt 0)
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for Trino"
}