Param(
	[switch] $force=$false
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net8.0", "net9.0", "net10.0")
)

if ($env:GITHUB_ACTIONS -eq "true") {
	$ErrorActionPreference = "Stop"
	. $PSScriptRoot\..\Run-ProviderQaSuite.ps1
	$archive = Join-Path $env:RUNNER_TEMP "apache-drill.tar.gz"
	$extractRoot = Join-Path $env:RUNNER_TEMP "apache-drill"
	Invoke-WebRequest "https://archive.apache.org/dist/drill/drill-1.21.2/apache-drill-1.21.2.tar.gz" -OutFile $archive
	New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
	& tar -xzf $archive -C $extractRoot
	if ($LASTEXITCODE -ne 0) { throw "Apache Drill extraction failed." }
	$drillHome = Join-Path $extractRoot "apache-drill-1.21.2"
	New-Item -ItemType Directory -Path "C:\mnt" -Force | Out-Null
	Copy-Item (Join-Path $PSScriptRoot "..\.bigdata\Customer") "C:\mnt\Customer" -Recurse -Force

	$driverInstaller = Join-Path $env:RUNNER_TEMP "drill-odbc.msi"
	Invoke-WebRequest "http://package.mapr.com/tools/MapR-ODBC/MapR_Drill/MapRDrill_odbc_v1.3.22.1055/MapR%20Drill%201.3%2064-bit.msi" -OutFile $driverInstaller
	$driverInstall = Start-Process msiexec.exe -ArgumentList @('/i', $driverInstaller, '/quiet', '/qn', '/norestart') -Wait -WindowStyle Hidden -PassThru
	if ($driverInstall.ExitCode -ne 0) { throw "MapR Drill ODBC driver installation failed with exit code $($driverInstall.ExitCode)." }

	$drillCommand = "`"$(Join-Path $drillHome 'bin\drillbit.bat')`" start"
	$start = Start-Process cmd.exe -ArgumentList @('/c', $drillCommand) -Wait -WindowStyle Hidden -PassThru
	if ($start.ExitCode -ne 0) { throw "Apache Drill failed to start." }
	try {
		$ready = $false
		foreach ($attempt in 1..30) {
			if (Test-NetConnection localhost -Port 31010 -InformationLevel Quiet) { $ready = $true; break }
			Start-Sleep -Seconds 2
		}
		if (-not $ready) { throw "Apache Drill did not become ready." }
		$project = Join-Path $PSScriptRoot "..\..\DubUrl.Providers.Drill.QA\DubUrl.Providers.Drill.QA.csproj"
		Run-ProviderQaSuite -project $project -provider "drill" -config $config -frameworks $frameworks
	}
	finally {
		$stopCommand = "`"$(Join-Path $drillHome 'bin\drillbit.bat')`" stop"
		Start-Process cmd.exe -ArgumentList @('/c', $stopCommand) -Wait -WindowStyle Hidden | Out-Null
	}
	exit 0
}

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
