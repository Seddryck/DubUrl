Param(
	[switch] $force=$false
	, $package= "Firebird-4.0.2.2816-0-x64"
	, $config= "Release"
	, $extension = "zip"
)
. $PSScriptRoot\..\Run-TestSuite.ps1

if ($force) {
	Write-Host "Enforcing QA testing for FirebirdSQL"
}

$binPath = "./../bin/$config/net6.0/"
$rootUrl = "https://github.com/FirebirdSQL/firebird/releases/download/"
If (-not($env:PATH -like "*7-zip*")) {
	$env:PATH += ";C:\Program Files\7-Zip"
}

$firebirdPath = "C:\Program Files\Firebird"
If (-not (Test-Path -Path $firebirdPath)) {
	$firebirdPath = $firebirdPath -replace "C:", "E:"
	If (-not (Test-Path -Path $firebirdPath)) {
		$firebirdPath = $firebirdPath -replace "E:", "C:"
	}
}
$firebirdPath = "$firebirdPath\Firebird_$($package.Split(".")[0].Split("-")[1])_$($package.Split(".")[1])"
If (-not($env:PATH -like "*$firebirdPath*")) {
	$env:PATH += ";$firebirdPath"
}
Write-Host "Using '$firebirdPath' as FirebirdSQL installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*firebird*")) {
	Write-Host "Deploying FirebirdSQL testing environment"

	if (-not (Test-Path -Path $firebirdPath\firebird.exe)) {
		$firebirdVersion = "v$($package.Split(".")[0].Split("-")[1]).$($package.Split(".")[1]).$($package.Split(".")[2])"
		Write-Host "`tDownloading FirebirdSQL $firebirdVersion ..."
		Invoke-WebRequest "$rootUrl/$firebirdVersion/$package.$extension" -OutFile "$env:temp\firebird-install.$extension"
		Unblock-File "$env:temp\firebird-install.$extension"
		Write-Host "`tInstalling FirebirdSQL ..."
		if ($extension -eq "zip") {
			Write-Host "`tDecompressing FirebirdSQL archive in $firebirdPath ..."
			& 7z x "$env:temp\firebird-install.$extension" -o"$firebirdPath" -aoa
		} else {
			Write-Host "`tRunning FirebirdSQL installer ..."
			& "$env:temp\firebird-install.exe" "/VERYSILENT /NORESTART /NOICONS /LOG=""$env:temp\firebird-log.txt""".Split(" ")
		}	
	} else {
		Write-Host "`tFirebirdSQL already installed: skipping installation."
	}

	# Starting service/executable
	if ($extension -eq "zip") {
		if (-not (Test-Path -Path $firebirdPath\firebird.exe)) {
			Write-Error "`tInstallation of FirebirdSQL failed. Cannot find firebird.exe in $firebirdPath."
		} else {
			$process = Start-Process -FilePath "$firebirdPath\firebird.exe" -ArgumentList "-a".Split(" ") -PassThru
		}
	} else {
		$retry = 0
		$maxRetry = 5
		$started = $false
		$firebirdServiceName = "FirebirdServerDefaultInstance"
		while($retry -le $maxRetry -and $started -eq $false)
		{
			try {
				$service = Get-Service -Name $firebirdServiceName -ErrorAction Stop
		
				if ($service.Status -eq "Running") {
					Write-Host "`tService '$($service.DisplayName)' already started."
				} else {
					Write-Host "`tStarting service '$($service.DisplayName)' ..."
					Start-Service -Name $firebirdServiceName
					Write-Host "`tService started."
				}
				$started = $true
			} catch {
				if ($retry -ne $maxRetry) {
					Start-Sleep -Seconds 5
				}
				$retry += 1
			}
		}

		if ($started -eq $false) {
			if (Test-Path -Path "$env:temp\firebird-log.txt") {
				Get-Content("$env:temp\firebird-log.txt") | Write-Host
			} else {
				Write-Host "`tNo log available for the installation of ForebirdSQL."
			}
			Write-Error "`tService '$firebirdServiceName' cannot be detected or started"
		}
	}
	

	# Deploying database based on script
	$databasePath = "$binPath\Customer.fdb"
	If (Test-Path -Path $databasePath) {
		Remove-Item -Path $databasePath
	}

	Write-host "`tCreating database at $databasePath"
	Get-Content ".\deploy-firebirdsql-database.sql" | & isql.exe -u SYSADMIN -p masterkey -i ".\deploy-firebirdsql-database.sql" -b -e -q

	# Installing ODBC driver
	Write-host "`tDeploying FirebirdSQL ODBC drivers"
	$odbcDriverInstalled = $false;
	$drivers = Get-OdbcDriver -Name "*firebird*" -Platform "64-bit"
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading FirebirdSQL ODBC driver ..."
		Invoke-WebRequest "https://downloads.sourceforge.net/project/firebird/firebird-ODBC-driver/2.0.5-Release/Firebird_ODBC_2.0.5.156_x64.exe?ts=gAAAAABkLAh47QYDiggM29OgN08H8hWFCY2_ph5GKLpc0ho-5aHKxXoczAWxsSMuc8MIKS55x8LtD3fAFyAX3Da2O0PDyo-4oA%3D%3D&amp;use_mirror=deac-fra&amp;r=https%3A%2F%2Ffirebirdsql.org%2F" `
		    -OutFile "$env:temp\firebird_odbc.exe"
		
		try { Write-Host "`t`tInstalling FirebirdSQL ODBC driver ..."
		    & "$env:temp\firebird_odbc.exe" "/VERYSILENT /NORESTART /NOICONS".Split(" ") | Out-Host
			$odbcDriverInstalled = $true;
	    } catch {
			Write-Host "An exception was caught: $($_.Exception.Message)"
		}

		if ($odbcDriverInstalled = $true -eq $true) {
			Write-Host "`t`tChecking installation ..."
			Get-OdbcDriver -Name "*firebird*"
			Write-Host "`tDeployment of FirebirdSQL ODBC driver finalized."	
		}
	} else {
		$odbcDriverInstalled = $true;
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*firebird*" -Platform "64-bit"
		Write-Host "`tSkipping installation of new drivers"
	}

	# Running QA tests
	Write-Host "Running QA tests related to FirebirdSQL"
	$suites = @("FirebirdSQL+AdoProvider")
	if ($odbcDriverInstalled) {
		$suites += "FirebirdSQL+ODBC"
	}
	$testSuccessful = Run-TestSuite $suites

	# Stoping service
	if ($extension -eq "zip") {
		Write-Host "Stopping executable '$($process.Name)' ..."
		Stop-Process -Id $process.Id
		Write-Host "Executable stopped."
	} else {
		$service = Get-Service -Name $firebirdServiceName
		if ($service.Status -eq "Stopped") {
			Write-Host "Service '$($service.DisplayName)' already stopped."
		} else {
			Write-Host "Stopping service '$($service.DisplayName)' ..."
			Stop-Service -Name $firebirdServiceName
			Write-Host "Service stopped."
		}
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for FirebirdSQL"
}