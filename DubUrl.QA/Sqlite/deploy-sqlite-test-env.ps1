Param(
	[switch] $force=$false
	, $config= "Release"
	, $extension = "zip"
)
if ($force) {
	Write-Warning "Forcing QA testing for Sqlite"
}
$binPath = "./../bin/$config/net6.0/"
If (-not($env:PATH -like "*7-zip*")) {
	$env:PATH += ";C:\Program Files\7-Zip"
}

$sqlitePath = "C:\Program Files\Sqlite"
If (-not (Test-Path -Path $sqlitePath)) {
	$sqlitePath = $sqlitePath -replace "C:", "E:"
	If (-not (Test-Path -Path $sqlitePath)) {
		$sqlitePath = $sqlitePath -replace "E:", "C:"
	}
}
If (-not($env:PATH -like "*$sqlitePath*")) {
	$env:PATH += ";$sqlitePath"
}
Write-Host "Using '$sqlitePath' as Sqlite installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*sqlite*")) {
	Write-Host "Deploying Sqlite testing environment"

	if (-not (Test-Path -Path $sqlitePath\sqlite3.exe)) {
		Write-Host "`tDownloading Sqlite ..."
		Invoke-WebRequest "https://www.sqlite.org/2023/sqlite-tools-win32-x86-3410200.zip" -OutFile "$env:temp\sqlite-install.$extension"
		Unblock-File "$env:temp\sqlite-install.$extension"
		Write-Host "`tInstalling Sqlite ..."
		if ($extension -eq "zip") {
			Write-Host "`tDecompressing Sqlite archive in $sqlitePath ..."
			& 7z e "$env:temp\sqlite-install.$extension" -o"$sqlitePath" -aoa
		} else {
			Write-Error "Not managed"
		}	
	} else {
		Write-Host "`tSqlite already installed: skipping installation."
	}

	# Deploying database based on script
	$databasePath = "$binPath\Customer.db"
	If (Test-Path -Path $databasePath) {
		Remove-Item -Path $databasePath
	}

	Write-host "`tCreating database at $databasePath"
	Get-Content ".\deploy-sqlite-database.sql" | & sqlite3.exe

	# Installing ODBC driver
	Write-host "`tDeploying Sqlite ODBC drivers"
	$odbcDriverInstalled = $false;
	$drivers = Get-OdbcDriver -Name "*sqlite*" -Platform "64-bit"
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading Sqlite ODBC driver ..."
		Invoke-WebRequest "http://www.ch-werner.de/sqliteodbc/sqliteodbc_w64.exe" `
		    -OutFile "$env:temp\sqlite_odbc.exe"
		
		Write-Warning "Cannot silently install Sqlite ODBC driver. Proceed to manual installation."
	} else {
		$odbcDriverInstalled = $true;
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*sqlite*" -Platform "64-bit"
		Write-Host "`tSkipping installation of new drivers"
	}

	# Running QA tests
	Write-Host "Running QA tests related to Sqlite"
	& dotnet build "..\..\DubUrl.QA" -c Release --nologo | out-null

	# To avoid to run the two test-suites in parallel
	& dotnet test "..\..\DubUrl.QA" --filter "(TestCategory=Sqlite""&""TestCategory=AdoProvider)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	$testSuccessful = ($lastexitcode -gt 0)
	if ($odbcDriverInstalled -eq $true) {
		& dotnet test "..\..\DubUrl.QA" --filter "(TestCategory=Sqlite""&""TestCategory=ODBC)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
		$testSuccessful += ($lastexitcode -gt 0)
	}

	# Raise failing tests
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for Sqlite"
}