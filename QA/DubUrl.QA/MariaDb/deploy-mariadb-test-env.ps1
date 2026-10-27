[CmdletBinding()]
param(
    [switch] $force,
    [string] $testFilter = '',
    [string] $config = 'Release',
    [ValidateSet('net8.0', 'net9.0', 'net10.0')]
    [string[]] $frameworks = @('net8.0', 'net9.0', 'net10.0'),
    [ValidateRange(1, 65535)] [int] $port = 3306,
    [ValidateRange(30, 1800)] [int] $readinessTimeoutSeconds = 300,
    [hashtable] $sdkVersions = @{ 'net8.0' = '8.0.416'; 'net9.0' = '9.0.308'; 'net10.0' = '10.0.401' }
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Run-ProviderQaSuite.ps1')
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$project = Join-Path $repositoryRoot 'Providers/MySql/DubUrl.Provider.MariaDb.QA/DubUrl.Provider.MariaDb.QA.csproj'
$infrastructure = Join-Path (Split-Path $project) 'infrastructure'
$composeFile = Join-Path $infrastructure 'compose.yaml'
# Isolate concurrent local runs and CI jobs, including their disposable volumes.
$compose = @('compose', '-p', ('duburl-mariadb-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)), '-f', $composeFile)
$previousEnvironment = @{}
foreach ($name in @('MARIADB_ROOT_PASSWORD', 'DUBURL_MARIADB_QA_PORT', 'DUBURL_MARIADB_QA_URL', 'ODBCSYSINI', 'ODBCINSTINI')) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$testPassword = 'QaT' + [Guid]::NewGuid().ToString('N')
$env:MARIADB_ROOT_PASSWORD = $testPassword
$env:DUBURL_MARIADB_QA_PORT = [string]$port
$env:DUBURL_MARIADB_QA_URL = "mariadb://root:${testPassword}@localhost:${port}/DubUrl"
if ($env:GITHUB_ACTIONS -eq 'true') {
    Write-Host "::add-mask::$testPassword"
    Write-Host "::add-mask::$env:DUBURL_MARIADB_QA_URL"
}

function Write-RedactedOutput($lines) {
    foreach ($line in $lines) {
        Write-Host ([string]$line).Replace($testPassword, '***')
    }
}

Push-Location $repositoryRoot
try {
    & docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0) { throw 'Docker must be running with Linux containers.' }
    & docker @compose up --detach --wait --wait-timeout $readinessTimeoutSeconds
    if ($LASTEXITCODE -ne 0) { throw 'MariaDb QA infrastructure did not become healthy before the readiness timeout.' }

    # Initialize through stdin; passwords stay out of command arguments.
    $initialization = Get-Content (Join-Path $infrastructure 'initialize.sql') -Raw
    $output = @($initialization | & docker @compose exec -T mariadb sh -c 'MYSQL_PWD="$MARIADB_ROOT_PASSWORD" mariadb -h 127.0.0.1 -u root' 2>&1)
    $initializationExitCode = $LASTEXITCODE
    Write-RedactedOutput $output
    if ($initializationExitCode -ne 0) { throw 'MariaDb QA initialization failed.' }

    foreach ($framework in $frameworks) {
        foreach ($suite in $(if ($testFilter) { @('complete') } else { @('complete','odbc') })) {
            $result = Join-Path $repositoryRoot ".test-results/mariadb/$framework/$suite/$suite.trx"
            Remove-Item -LiteralPath $result -ErrorAction SilentlyContinue
        }
    }
    # CI-only installation; local machines must already have the requested ODBC driver.
    if ($IsLinux -and $env:GITHUB_ACTIONS -eq 'true') {
        & sudo apt-get update
        if ($LASTEXITCODE -ne 0) { throw 'apt update failed.' }
        & sudo apt-get install --yes odbc-mariadb unixodbc
        if ($LASTEXITCODE -ne 0) { throw 'MariaDB ODBC installation failed.' }
    }
    if ($IsLinux -and !$testFilter) {
        $driver = (& odbcinst -q -d -n 'MariaDB Unicode' | Where-Object { $_ -like 'Driver=*' } | Select-Object -First 1)
        if (!$driver) { throw 'Install the MariaDB Unicode ODBC driver before running full local QA.' }
        $odbcDirectory=Join-Path ([IO.Path]::GetTempPath()) ('duburl-maria-odbc-'+[Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $odbcDirectory | Out-Null
        "[MariaDB ODBC 3.1 Driver]`n$driver" | Set-Content (Join-Path $odbcDirectory 'drivers.ini')
        $env:ODBCSYSINI=$odbcDirectory
        $env:ODBCINSTINI='drivers.ini'
    }
    Run-ProviderQaSuite -project $project -provider mariadb -config $config -frameworks $frameworks -sdkVersions $sdkVersions -testFilter $testFilter -requiredCategories $(if ($testFilter) { @() } else { @('ODBC') })

    foreach ($framework in $frameworks) {
        foreach ($suite in $(if ($testFilter) { @('complete') } else { @('complete','odbc') })) {
            $result = Join-Path $repositoryRoot ".test-results/mariadb/$framework/$suite/$suite.trx"
            [xml] $trx = Get-Content -LiteralPath $result -Raw
            if ([int]$trx.TestRun.ResultSummary.Counters.executed -le 0) { throw "MariaDb $suite QA did not execute any tests for $framework." }
        }
    }
}
catch {
    Write-RedactedOutput @(& docker @compose logs --no-color --tail 150 2>&1)
    throw
}
finally {
    try {
        & docker @compose down --volumes --remove-orphans
        if ($LASTEXITCODE -ne 0) { Write-Warning 'MariaDb QA cleanup failed; remove the Compose project manually.' }
    }
    finally {
        foreach ($name in $previousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
        }
        if ($odbcDirectory) {
            Remove-Item -LiteralPath (Join-Path $odbcDirectory 'drivers.ini') -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath $odbcDirectory -ErrorAction SilentlyContinue
        }
        Pop-Location
    }
}
