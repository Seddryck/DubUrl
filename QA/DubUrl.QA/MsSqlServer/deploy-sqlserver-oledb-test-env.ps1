[CmdletBinding()]
param(
    [string]$config='Release', [string[]]$frameworks=@('net8.0','net9.0','net10.0'),
    [hashtable]$sdkVersions=@{'net8.0'='8.0.416';'net9.0'='9.0.308';'net10.0'='10.0.401'},
    [switch]$installPrerequisites,
    [string]$providerName=''
)
$ErrorActionPreference='Stop'
if (!$IsWindows) { throw 'SQL Server OLE DB QA requires Windows x64.' }
. (Join-Path $PSScriptRoot '../Run-ProviderQaSuite.ps1')
. (Join-Path $PSScriptRoot '../Install-QaWindowsDrivers.ps1')
if ($installPrerequisites) { Install-QaWindowsDriver SqlServer }
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$project=Join-Path $root 'Providers/SqlServer/DubUrl.Provider.MsSqlServer.QA/DubUrl.Provider.MsSqlServer.QA.csproj'
$instance='DubUrlQa'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$previous=$env:DUBURL_SQLSERVER_OLEDB_QA_URL
$database='Qa'+[Guid]::NewGuid().ToString('N')
$initPath=Join-Path ([IO.Path]::GetTempPath()) ($database+'.sql')
$created=$false
Push-Location $root
try {
    & SqlLocalDB create $instance -s
    if ($LASTEXITCODE -ne 0) { throw 'SQL Server LocalDB is required and must start successfully.' }
    $sql=Join-Path $root 'Providers/SqlServer/DubUrl.Provider.MsSqlServer.QA/infrastructure/initialize.sql'
    (Get-Content $sql -Raw).Replace('DubUrl',$database) | Set-Content -LiteralPath $initPath
    & sqlcmd -S "(localdb)\$instance" -E -b -i $initPath
    if ($LASTEXITCODE -ne 0) { throw 'LocalDB fixture initialization failed.' }
    $created=$true
    $info = & SqlLocalDB info $instance
    $pipe = ($info | Select-String -Pattern 'np:\\.*').Matches.Value | Select-Object -First 1
    if (!$pipe) { throw 'LocalDB did not report a named-pipe endpoint.' }
    $endpoint=[Uri]::EscapeDataString($pipe.Trim())
    $env:DUBURL_SQLSERVER_OLEDB_QA_URL="oledb+mssql://localhost/${database}?Data%20Source=$endpoint&Integrated%20Security=SSPI"
    if ($providerName -ne 'SQLOLEDB') { $env:DUBURL_SQLSERVER_OLEDB_QA_URL += '&Use%20Encryption%20for%20Data=Optional' }
    if ($providerName) { $env:DUBURL_SQLSERVER_OLEDB_QA_URL += '&Provider='+[Uri]::EscapeDataString($providerName) }
    Run-ProviderQaSuite -project $project -provider sqlserver-oledb -config $config -frameworks $frameworks -sdkVersions $sdkVersions -testFilter 'TestCategory=OleDB'
} finally {
    if ($created) {
        & sqlcmd -S "(localdb)\$instance" -E -b -Q "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database];" | Out-Null
    }
    Remove-Item -LiteralPath $initPath -ErrorAction SilentlyContinue
    & SqlLocalDB stop $instance -k
    & SqlLocalDB delete $instance
    [Environment]::SetEnvironmentVariable('DUBURL_SQLSERVER_OLEDB_QA_URL',$previous,'Process')
    Pop-Location
}
