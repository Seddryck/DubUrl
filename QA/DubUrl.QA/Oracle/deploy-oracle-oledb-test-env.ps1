[CmdletBinding()]
param(
    [string]$config='Release', [string[]]$frameworks=@('net8.0','net9.0','net10.0'),
    [hashtable]$sdkVersions=@{'net8.0'='8.0.416';'net9.0'='9.0.308';'net10.0'='10.0.401'},
    [switch]$installPrerequisites,
    [string]$oracleHome=$env:ORACLE_HOME
)
$ErrorActionPreference='Stop'
if (!$IsWindows) { throw 'Oracle OLE DB QA requires Windows x64.' }
. (Join-Path $PSScriptRoot '../Run-ProviderQaSuite.ps1')
. (Join-Path $PSScriptRoot '../Install-QaWindowsDrivers.ps1')
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$project=Join-Path $root 'Providers/Oracle/DubUrl.Provider.Oracle.QA/DubUrl.Provider.Oracle.QA.csproj'
$previous=$env:DUBURL_ORACLE_OLEDB_QA_URL
$password='QaA'+[Guid]::NewGuid().ToString('N')
$testPassword='QaT'+[Guid]::NewGuid().ToString('N')
$user='QA'+[Guid]::NewGuid().ToString('N').Substring(0,12).ToUpperInvariant()
$created=$false
$response=$null
$installLog=$null
Push-Location $root
try {
    if ($installPrerequisites) {
        if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Automatic database installation is restricted to disposable GitHub runners.' }
        Write-Host "::add-mask::$password"
        Write-Host "::add-mask::$testPassword"
        $zip=Join-Path $env:RUNNER_TEMP 'oracle-free-23.26.3.zip'
        Get-VerifiedQaDownload 'https://download.oracle.com/otn-pub/otn_software/db-free/oracle-ai-database-free-26ai-23.26.3.windows.x64.zip' $zip '178D6CFD5A8109EA9D26894A597116DB4D95320CD8861D16CA10D4D8C384DAB3'
        $source=Join-Path $env:RUNNER_TEMP 'oracle-free-setup'
        Expand-Archive -LiteralPath $zip -DestinationPath $source
        $setup=(Get-ChildItem $source -Recurse -Filter setup.exe | Select-Object -First 1).FullName
        if (!$setup) { throw 'Oracle installer is missing.' }
        $response=Join-Path $env:RUNNER_TEMP 'DubUrlFreeInstall.rsp'
        "INSTALLDIR=C:\DubUrlOracle`r`nPASSWORD=$password`r`nLISTENER_PORT=1521`r`nCHAR_SET=AL32UTF8`r`nDB_DOMAIN=`r`nSOFTWARE_ONLY=FALSE`r`nMODIFY_DIRECTORY_PERMISSIONS=FALSE" | Set-Content -LiteralPath $response -Encoding ascii
        $installLog=Join-Path $env:RUNNER_TEMP 'DubUrlFreeInstall.log'
        Write-Host 'Installing Oracle Free.'
        $process=Start-Process -FilePath $setup -WorkingDirectory (Split-Path $setup) -ArgumentList @('/s',"/v`"RSP_FILE=$response`"","/v`"/L*v $installLog`"",'/v"/qn"') -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -notin @(0,3010)) { throw "Oracle Free installation failed: $($process.ExitCode)" }
        $oracleHome='C:\DubUrlOracle\dbhomeFree'
        Write-Host 'Installing Oracle OLE DB.'
        Install-QaWindowsDriver Oracle
    } else {
        $password=$env:DUBURL_ORACLE_QA_ADMIN_PASSWORD
        if (!$password -or !$oracleHome) { throw 'Set oracleHome and DUBURL_ORACLE_QA_ADMIN_PASSWORD for a local Oracle Free instance.' }
    }
    $sqlplus=Join-Path $oracleHome 'bin/sqlplus.exe'
    if (!(Test-Path -LiteralPath $sqlplus)) { throw "Oracle Free installer did not create $sqlplus." }
    Write-Host 'Waiting for Oracle Free SQL readiness.'
    $deadline=(Get-Date).AddMinutes(20)
    do {
        if (!(Test-Path -LiteralPath $sqlplus)) { Start-Sleep -Seconds 5; continue }
        $output=@("whenever sqlerror exit failure`nconnect system/`"$password`"@localhost:1521/FREEPDB1`nset heading off pagesize 0`nselect 1 from dual;`nexit" | & $sqlplus -L -s /nolog 2>&1)
        $ready=$LASTEXITCODE -eq 0 -and ($output -match '^\s*1\s*$')
        if (!$ready) { Start-Sleep -Seconds 5 }
    } while (!$ready -and (Get-Date) -lt $deadline)
    if (!$ready) {
        $codes=[regex]::Matches(($output -join ' '),'(?:ORA|SP2)-\d+') | ForEach-Object Value | Select-Object -Unique
        throw "Oracle Free did not accept a SQL query within 20 minutes. Error codes: $($codes -join ', ')"
    }
    $init="whenever sqlerror exit failure`nset echo off verify off`nconnect system/`"$password`"@localhost:1521/FREEPDB1`ncreate user $user identified by `"$testPassword`" default tablespace users quota 20M on users;`ngrant create session to $user;`ncreate table $user.`"Customer`" (`"CustomerId`" number(9), `"FullName`" varchar2(50), `"BirthDate`" date);`ninsert into $user.`"Customer`" values (1,'Nikola Tesla',date '1856-07-10');`ninsert into $user.`"Customer`" values (2,'Albert Einstein',date '1879-03-14');`ncommit;`nexit"
    $created=$true
    $output=@($init | & $sqlplus -L -s /nolog 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'Oracle OLE DB fixture initialization failed.' }
    $env:DUBURL_ORACLE_OLEDB_QA_URL="oledb+oracle://${user}:${testPassword}@localhost:1521/FREEPDB1"
    if ($env:GITHUB_ACTIONS -eq 'true') { Write-Host "::add-mask::$env:DUBURL_ORACLE_OLEDB_QA_URL" }
    Run-ProviderQaSuite -project $project -provider oracle-oledb -config $config -frameworks $frameworks -sdkVersions $sdkVersions -testFilter 'TestCategory=OleDB'
} finally {
    if ($installLog -and (Test-Path -LiteralPath $installLog)) {
        Get-Content -LiteralPath $installLog | Where-Object { $_ -match 'Installation success or error status:|Return value 3|Error \d{4}' } | ForEach-Object { $_.Replace($password,'[redacted]').Replace($testPassword,'[redacted]') } | Write-Host
        Remove-Item -LiteralPath $installLog
    }
    if ($created) { "whenever sqlerror exit failure`nconnect system/`"$password`"@localhost:1521/FREEPDB1`ndrop user $user cascade;`nexit" | & $sqlplus -L -s /nolog | Out-Null }
    if ($response) { Remove-Item -LiteralPath $response -ErrorAction SilentlyContinue }
    [Environment]::SetEnvironmentVariable('DUBURL_ORACLE_OLEDB_QA_URL',$previous,'Process')
    Pop-Location
}
