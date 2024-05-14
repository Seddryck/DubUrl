# Installing ODBC driver
Write-host "`tDeploying PostgreSQL ODBC drivers"
$drivers = Get-OdbcDriver -Name "*postgres*" -Platform "64-bit"
if ($drivers.Length -eq 0) {
	Write-Host "`t`tDownloading PostgreSQL ODBC driver ..."
	Invoke-WebRequest "https://ftp.postgresql.org/pub/odbc/releases/REL-16_00_0004/psqlodbc_x64.msi" -OutFile "$env:temp\psqlodbc_x64.msi"
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
