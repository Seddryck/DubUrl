Param(
	[switch] $force=$false
	, $config= "Release"
)
if ($force) {
	Write-Warning "Forcing QA testing for DuckDB"
}
Push-Location $PSScriptRoot

$binPath = "./../bin/$config/net6.0/"
$rootUrl = "https://github.com/duckdb/duckdb/releases/latest/download"
if (-not($env:PATH -like "7-zip")) {
	$env:PATH += ";C:\Program Files\7-Zip"
}
Write-Host "Deployment for testing in '$binPath'"

$duckPath = "C:/Program Files/DuckDB/"
If (-not (Test-Path -Path $duckPath)) {
	$duckPath = $duckPath -replace "C:", "E:"
	If (-not (Test-Path -Path $duckPath)) {
		$duckPath = $duckPath -replace "E:", "C:"
	}
}
Write-Host "Using '$duckPath' as DuckDB CLI installation folder"

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*duckdb*")) {
	Write-Host "Deploying DuckDB testing environment"

	if (-not (Test-Path -Path $duckPath\duckdb.exe)) {
		Write-Host "`tDownloading DuckDB CLI ..."
		Invoke-WebRequest "$rootUrl/duckdb_cli-windows-amd64.zip" -OutFile "$env:temp\duckdb_cli.zip"
		Write-Host "`tExtracting from archive DuckDB CLI..."		
		& 7z e "$env:temp\duckdb_cli.zip" -o"$duckPath" -y
	} else {
		Write-Host "`tDuckDB CLI already installed: skipping installation."
	}

	if (-not (Test-Path -Path $binPath\duckdb.dll)) {
		Write-Host "`tDownloading DuckDB library..."
		Invoke-WebRequest "$rootUrl/libduckdb-windows-amd64.zip" -OutFile "$env:temp\libduckdb.zip"
		Write-Host "`tExtracting from archive DuckDB library..."
		& 7z e "$env:temp\libduckdb.zip" -o"$binPath" -y
	} else {
		Write-Host "`tDuckDB library already installed: skipping installation."
	}

	# Deploying database based on script
	$databasePath = "$binPath/Customer.duckdb"
	If (Test-Path -Path $databasePath) {
		Remove-Item -Path $databasePath
	}

	Write-host "`tCreating database at $databasePath"
	If (-not($env:PATH -like $duckPath)) {
		$env:PATH += ";$duckPath"
	}
	Get-Content ".\deploy-duckdb-database.sql" | & duckdb.exe
	Write-host "`tDatabase created"

	# Installing ODBC driver
	Write-host "`tDeploying DuckDB ODBC drivers"
	$drivers = Get-OdbcDriver -Name "*DuckDB*" -Platform "64-bit"
	If ($drivers.Length -eq 0) {
		Write-Host "`t`tDownloading DuckDB ODBC driver ..."
		Invoke-WebRequest "$rootUrl/duckdb_odbc-windows-amd64.zip" -OutFile "$env:temp\duckdb_odbc.zip"
		Write-Host "`t`tExtracting from archive DuckDB ODBC driver ..."
		& 7z e "$env:temp\duckdb_odbc.zip" -o"$duckPath" -y
		Write-Host "`t`tInstalling DuckDB ODBC driver ..."
		& "$duckPath\odbc_install.exe" "/CI /Install".Split(" ") | Out-Host
		Write-Host "`t`tChecking installation ..."
		Get-OdbcDriver -Name "*DuckDB*"
		Write-Host "`tDeployment of DuckDB ODBC driver finalized."
	} else {
		Write-Host "`t`tDrivers already installed:"
		Get-OdbcDriver -Name "*DuckDB*" -Platform "64-bit"
		Write-Host "`t`tSkipping installation of new drivers"
	}

	# Running QA tests
	Write-Host "Running QA tests related to DuckDB"
	& dotnet build "..\..\DubUrl.QA" -c Release --nologo | out-null

	# No idea why but here we should split in two distinct sets of tests.
	& dotnet test "..\..\DubUrl.QA" --filter "(TestCategory=DuckDB""&""TestCategory=AdoProvider)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	$testSuccessful = ($lastexitcode -gt 0)
	& dotnet test "..\..\DubUrl.QA" --filter "(TestCategory=DuckDB""&""TestCategory=ODBC)" -c Release --test-adapter-path:. --logger:Appveyor --no-build --nologo
	$testSuccessful += ($lastexitcode -gt 0)

	# Raise failing tests
	Pop-Location
	exit $testSuccessful
} else {
	Write-Host "Skipping the deployment and run of QA testing for DuckDB"
}
Pop-Location