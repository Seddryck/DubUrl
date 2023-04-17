Param(
	[switch] $force=$false
	, $config = "Release"
)
if ($force) {
	Write-Warning "Forcing QA testing for Apache Drill"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*drill*")) {
	Write-Host "Deploying Apache Drill testing environment"

	# Deploying mounted folder
	Write-host "`tCopying file to mounted folder"
	$mountedFolder = ".\DubUrl.QA\bin\$config\net6.0\.bigdata"
	if (Test-Path -Path $mountedFolder) {
		Remove-Item -Path $mountedFolder -Force -Recurse
	}
	New-Item -Path $mountedFolder -Type Directory | out-null 
	Copy-Item -Path ".\DubUrl.QA\.bigdata\*" -Destination $mountedFolder -Recurse
	Write-Host "`tFiles copied to mounted folder"

	# Starting docker container for Apache Drill
	$previously_running = $false
	$running = & docker container ls --format "{{.ID}}" --filter "name=drill"
	if ($running -ne $null) {
		$previously_running = $true
		Write-Host "`tContainer is already running with ID '$running'."
	} else {
		Write-Host "`tStarting new container with mounting at $mountedFolder"
		Start-Process -FilePath ".\DubUrl.QA\Drill\run-drill-docker.cmd" -ArgumentList @("C:\Users\cedri\Projects\DubUrl\DubUrl.QA\bin\Release\net6.0\.bigdata")
		do {
			$running = & docker container ls --format "{{.ID}}" --filter "name=drill"
			if ($running -eq $null) {
				Start-Sleep -s 1
			}
		} while($running -eq $null)
		
		Write-Host "`tContainer started with ID '$running'."
		Start-Sleep -s 10
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
		& msiexec /i "$env:temp\drill-odbc.msi" /quiet /qn /norestart /log "$env:temp\install-drill.log" | Out-Host
		Get-Content "$env:temp\install-drill.log" | Write-Host
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
	Write-Host "Building QA test-suite"
	& dotnet build DubUrl.QA -c Release --nologo | out-null
	Write-Host "Running QA tests related to Apache Drill"
	if ($odbcDriverInstalled -eq $true) {
		& dotnet test DubUrl.QA --filter "(TestCategory=Drill""&""TestCategory=ODBC)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	}

	# Stopping and removing docker container
	if ($previously_running -eq $true) {
		Write-Host "Let continue container with ID '$running'"
	} else {
		$running = & docker container ls --format "{{.ID}}" --filter "name=drill"
		if ($running -ne $null) {
			Write-Host "`tStopping container '$running' ..."
			& docker stop $running | out-null
			Write-Host "`tDeleting container '$running' ..."
			& docker rm $running | out-null
			Write-Host "`tContainer '$running' stopped and removed"
		} else {
			Write-Host "`tNo running container named 'drill' found."
		}
	}
	
} else {
	Write-Host "Skipping the deployment and run of QA testing for Apache Drill"
}