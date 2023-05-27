# Installing ODBC driver
Write-host "`tDeploying PostgreSQL ODBC drivers"
$drivers = Get-OdbcDriver -Name "*postgres*" -Platform "64-bit"
if ($drivers.Length -eq 0) {
	Write-Host "`t`tDownloading PostgreSQL ODBC driver ..."
	Invoke-WebRequest "https://ftp.postgresql.org/pub/odbc/versions/msi/psqlodbc_13_02_0000-x64.zip" -OutFile "$env:temp\psqlodbc.zip"
	Write-Host "`t`tExtracting from archive PostgreSQL ODBC driver ..."
	& 7z e "$env:temp\psqlodbc.zip" -o"$env:temp" -y | Out-Null
	Write-Host "`t`tInstalling PostgreSQL ODBC driver ..."
	& msiexec /i "$env:temp\psqlodbc_x64.msi" /quiet /qn /norestart /log "$env:temp\install-pgsql.log" | Out-Host
	#Get-Content "$env:temp\install-pgsql.log"
	Write-Host "`t`tChecking installation ..."
	Get-OdbcDriver -Name "*postgres*"
	Write-Host "`tDeployment of PostgreSQL ODBC driver finalized."
} else {
	Write-Host "`t`tDrivers already installed:"
	Get-OdbcDriver -Name "*postgres*" -Platform "64-bit"
	Write-Host "`t`tSkipping installation of new drivers"
}