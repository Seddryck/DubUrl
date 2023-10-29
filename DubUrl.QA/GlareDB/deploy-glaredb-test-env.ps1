Param(
	[switch] $force=$false
	, [string] $databaseService= "postgresql-x64-15"
	, [string] $config= "Release"
	, [string[]] $frameworks = @("net6.0", "net7.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Windows-Service.ps1
. $PSScriptRoot\..\Docker-Container.ps1

if ($force) {
	Write-Host "Enforcing QA testing for GlareDB"
}

$rootUrl = "https://github.com/glaredb/glaredb/releases/latest/download"
if (-not($env:PATH -like "7-zip")) {
	$env:PATH += ";C:\Program Files\7-Zip"
}

$glarePath = "C:/Program Files/GlareDB/"
If (-not (Test-Path -Path $glarePath)) {
	$glarePath = $glarePath -replace "C:", "E:"
	If (-not (Test-Path -Path $glarePath)) {
		$glarePath = $glarePath -replace "E:", "C:"
	}
}
Write-Host "Using '$glarePath' as GlareDB installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*glaredb*")) {
	Write-Host "Deploying GlareDB testing environment"

	#Download GlareDB
	if (-not (Test-Path -Path $glarePath\glaredb.exe)) {
		Write-Host "`tDownloading GlareDB ..."
		Invoke-WebRequest "$rootUrl/glaredb-x86_64-pc-windows-msvc.zip" -OutFile "$env:temp\glaredb.zip"
		Write-Host "`tGlareDB downloaded."
		Write-Host "`tExtracting from archive GlareDB..."		
		& 7z e "$env:temp\glaredb.zip" -o"$glarePath" -y | Out-Null
		Write-Host "`tGlareDB extracted."		
	} else {
		Write-Host "`tGlareDB already installed: skipping installation."
	}

	# Start GlareDB server
	if ((Get-Process "glaredb" -ErrorAction SilentlyContinue).Length -eq 0) {
		Write-Host "`tStarting GlareDB Server ..."
		Start-Process "$glarePath\glaredb.exe" -ArgumentList @('server')
		Write-Host "`t`tWaiting for GlareDB Server ..."
		$startWait = Get-Date
		do {
			$isRunning = (Get-Process "glaredb" -ErrorAction SilentlyContinue).Length
			$wait = New-TimeSpan -Start $startWait
			if (!$isRunning) {
				if ($wait -gt (New-TimeSpan -Seconds 1)) {
					Write-Host "`t`tWaiting since $($wait.ToString("ss")) seconds ..."
				}
				Start-Sleep -s 1
			}
		} while (!$isRunning -and !($wait -gt (New-TimeSpan -Seconds 60)))
		if (!$isRunning) {
			Write-Warning "`t`tWaited during $($wait.ToString("ss")) seconds. Stopping test harness."
			exit 0
		} else {
			Write-Host "`t`tServer is available: waited $($wait.ToString("ss")) seconds to get it live."
		}
		$previouslyRunning = $false
		Write-Host "`tGlareDB Server started."
	} else {
		$previouslyRunning = $true
		Write-Host "`tGlareDB Server already running."
	}

	# Deploying GlareDB database based on script
	if ($previouslyRunning) {
		Write-host "`tDeploying GlareDB database ..."
		$binPath = "$PSScriptRoot\..\bin\$config\$($frameworks[0])"
		((Get-Content "$PSScriptRoot\deploy-glaredb-database.sql") -replace '<path>', "$PSScriptRoot\..\.bigdata\") `
			| Set-Content -Path "$binPath\Customer.sql" -Force
		& psql -U "glaredb" -h "localhost" -p "6543" -f "$binPath\Customer.sql" | Out-Null
		Write-host "`tGlareDB database deployed"
	}

	# Running QA tests
	Write-Host "Running QA tests related to GlareDB"
	$testSuccessful = Run-TestSuite @("GlareDB+AdoProvider") -config $config -frameworks $frameworks

	# Cleaning up
	if (-not $previouslyRunning) {
		Write-host "`tStoppping GlareDB processes ..."
		do {
			$running = Get-Process "glaredb" -ErrorAction SilentlyContinue
			if ($running) {
				Write-Host "`t`tStoppping process $($running.Id) ..."
				Stop-Process -Id $running.Id -Force -ErrorAction SilentlyContinue
				Write-Host "`t`tProcess $($running.Id) stopped."
			}
		} until($null -eq $running)
		Write-host "`tGlareDB processes stopped."
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}