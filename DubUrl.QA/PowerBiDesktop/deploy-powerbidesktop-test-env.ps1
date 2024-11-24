Param(
	[switch] $force=$false
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net6.0", "net8.0", "net9.0")
	, [string] $downloadUrl = "https://download.microsoft.com/download/8/8/0/880BCA75-79DD-466A-927D-1ABF1F5454B0/PBIDesktopSetup_x64.exe"
	, [string] $processName = "PBIDesktop"
)
. $PSScriptRoot\..\Run-TestSuite.ps1

if ($force) {
	Write-Host "Enforcing QA testing for Power BI Desktop"
}

if ($env:APPVEYOR -eq "True") {
	$pbiDesktopPath = "C:\Program Files\Microsoft\Power BI Desktop"
} else {
	$pbiDesktopPath = "Windows Store"
}
Write-Host "Using '$pbiDesktopPath' as Power BI Desktop installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*powerbi*") -or ($filesChanged -like "*Power BI Desktop*")) {
	Write-Host "Deploying Power BI Desktop testing environment"

	$previouslyRunning = (Get-Process $processName -ErrorAction SilentlyContinue).Length -gt 0

	#Installation of Power BI Desktop
	if ($env:APPVEYOR -eq "True") {
		Write-host "`tInstalling Power BI Desktop ..."
		Write-Host "`t`tDownloading Power BI Desktop ..."
		Invoke-WebRequest "$downloadUrl" -OutFile "$env:temp\PBISetup_x64.exe"
		Unblock-File "$env:temp\PBISetup_x64.exe"
		Write-Host "`t`tPower BI Desktop downloaded."
		Write-Host "`t`tRunning setup of Power BI Desktop ..."	
		Write-Host "`t`t`tCreating installation folder for Power BI Desktop ..."	
		if (!(Test-Path $pbiDesktopPath -PathType Container)) {
		    New-Item -ItemType Directory -Force -Path $pbiDesktopPath
			Write-Host "`t`t`tInstallation folder for Power BI Desktop created"
		} else {
			Write-Host "`t`t`tInstallation folder for Power BI Desktop already existing"
		}
		& "$env:temp\PBISetup_x64.exe" @("-quiet", "-norestart", "INSTALLLOCATION=""$pbiDesktopPath""", "ACCEPT_EULA=1", "-log", "$env:TEMP\PBIDesktop.Install.log") | Out-Host
		Write-Host "`t`tSetup executed."
		Write-Host "`t`t`tContent of folder after installation:"	
		Write-Host $(Get-ChildItem $pbiDesktopPath)
		Write-Host "----------------------------------"
		Write-Host "`t`t`tContent of installation log:"	
		Get-Content "$env:temp\PBIDesktop.Install.log" | Write-Host
		Write-Host "----------------------------------"
		Write-host "`tPower BI Desktop installed."
	} else {
		Write-host "`tAssuming that Power BI Desktop is already installed."
	}

	#Copying Power BI Model
	Write-host "`tDeploying Power BI Model ..."
	if ($previouslyRunning) {
		Write-host "`tPower BI Model already deployed."
	} else {
		foreach ($framework in $frameworks) {
		Write-host "`t`tCopying Power BI Model to ..\bin\$config\$framework\"
			Copy-Item ".\Customer.pbix" -Destination "..\bin\$config\$framework\"
		}
		Write-host "`tPower BI Model deployed."
	}

	# Opening Power BI Desktop
	Write-host "`tOpening Power BI Model with Power BI Desktop ..."
	if ($previouslyRunning) {
		Write-host "`tPower BI Model already opened."
	} else {
		Invoke-Item "..\bin\$config\$($frameworks[0])\Customer.pbix" #| Out-Null
		$startWait = Get-Date
		$previousCpuUsage = 0
		$isDisplayed = $false
		do {
			$isRunning = ((Get-Process $processName -ErrorAction SilentlyContinue).Length -gt 0)
			$wait = New-TimeSpan -Start $startWait
			if (!$isRunning) {
				if ($wait -gt (New-TimeSpan -Seconds 1)) {
					Write-Host "`t`tWaiting for Power BI Desktop since $($wait.ToString("ss")) seconds ..."
				}
				Start-Sleep -s 1
			} else {
				if (!$isDisplayed) {
					Write-Host "`t`tPower BI Desktop processes is available."
					$isDisplayed = $true
				}
				$currentCpuUsage = (Get-Process "msmdsrv" -ErrorAction SilentlyContinue | Select-Object -expand CPU)
				$isRunning = $previousCpuUsage -eq $currentCpuUsage -and $currentCpuUsage -gt 0
				if (!$isRunning) {
					if ($wait -gt (New-TimeSpan -Seconds 1)) {
						Write-Host "`t`tWaiting for msmdsrv since $($wait.ToString("ss")) seconds ..."
					}
					Start-Sleep -s 1
				}
				$previousCpuUsage = $currentCpuUsage
			}
		} while (!$isRunning -and !($wait -gt (New-TimeSpan -Seconds 60)))
		if (!$isRunning) {
			Write-Warning "`t`tWaited during $($wait.ToString("mm' minutes 'ss' seconds'")). Stopping test harness."
			exit 0
		} else {
			Write-Host "`t`tPower BI Desktop and msmdsrv processes are available: waited $($wait.ToString("ss")) seconds to get it live."
		}
		Write-host "`tPower BI Model opened."
	}

	# Running QA tests
	Write-Host "Running QA tests related to Power BI Desktop"
	$testSuccessful = Run-TestSuite @("PowerBiDesktop") -config $config -frameworks $frameworks

	# Stopping Power BI Desktop
	if (!$previouslyRunning) {
		Stop-Process -Name $processName
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
