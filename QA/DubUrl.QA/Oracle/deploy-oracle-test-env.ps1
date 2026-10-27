[CmdletBinding()]
param(
    [string] $config = 'Release',
    [ValidateSet('net8.0', 'net9.0', 'net10.0')]
    [string[]] $frameworks = @('net8.0', 'net9.0', 'net10.0'),
    [ValidateRange(1, 65535)] [int] $port = 1521,
    [ValidateRange(30, 1800)] [int] $readinessTimeoutSeconds = 960,
    [hashtable] $sdkVersions = @{ 'net8.0' = '8.0.416'; 'net9.0' = '9.0.308'; 'net10.0' = '10.0.401' }
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Run-ProviderQaSuite.ps1')
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$project = Join-Path $repositoryRoot 'Providers/Oracle/DubUrl.Provider.Oracle.QA/DubUrl.Provider.Oracle.QA.csproj'
$infrastructure = Join-Path (Split-Path $project) 'infrastructure'
$composeFile = Join-Path $infrastructure 'compose.yaml'
# Isolate concurrent local runs and CI jobs, including their disposable volumes.
$compose = @('compose', '-p', ('duburl-oracle-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)), '-f', $composeFile)
$previousEnvironment = @{}
foreach ($name in @('ORACLE_PWD', 'DUBURL_ORACLE_QA_PORT', 'DUBURL_ORACLE_QA_URL')) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$adminPassword = 'QaA' + [Guid]::NewGuid().ToString('N')
$testPassword = 'QaT' + [Guid]::NewGuid().ToString('N')
$env:ORACLE_PWD = $adminPassword
$env:DUBURL_ORACLE_QA_PORT = [string]$port
$env:DUBURL_ORACLE_QA_URL = "oracle://duburl_qa:${testPassword}@localhost:${port}/FREEPDB1"
if ($env:GITHUB_ACTIONS -eq 'true') {
    Write-Host "::add-mask::$adminPassword"
    Write-Host "::add-mask::$testPassword"
    Write-Host "::add-mask::$env:DUBURL_ORACLE_QA_URL"
}

function Write-RedactedOutput($lines) {
    foreach ($line in $lines) {
        Write-Host ([string]$line).Replace($adminPassword, '***').Replace($testPassword, '***')
    }
}

Push-Location $repositoryRoot
try {
    & docker info --format '{{.OSType}}'
    if ($LASTEXITCODE -ne 0) { throw 'Docker must be running with Linux containers.' }
    & docker @compose up --detach --wait --wait-timeout $readinessTimeoutSeconds
    if ($LASTEXITCODE -ne 0) { throw 'Oracle QA infrastructure did not become healthy before the readiness timeout.' }

    # Pass the generated password on stdin, never as a command argument or stored file.
    $initialization = "define 1 = $testPassword`n" + (Get-Content (Join-Path $infrastructure 'initialize.sql') -Raw)
    $output = @($initialization | & docker @compose exec -T oracle sqlplus -L -s / as sysdba 2>&1)
    $initializationExitCode = $LASTEXITCODE
    Write-RedactedOutput $output
    if ($initializationExitCode -ne 0) { throw 'Oracle QA initialization failed.' }

    foreach ($framework in $frameworks) {
        foreach ($suite in @('complete', 'schema')) {
            $result = Join-Path $repositoryRoot ".test-results/oracle/$framework/$suite/$suite.trx"
            Remove-Item -LiteralPath $result -ErrorAction SilentlyContinue
        }
    }
    Run-ProviderQaSuite -project $project -provider oracle -config $config -frameworks $frameworks -sdkVersions $sdkVersions

    foreach ($framework in $frameworks) {
        foreach ($suite in @('complete', 'schema')) {
            $result = Join-Path $repositoryRoot ".test-results/oracle/$framework/$suite/$suite.trx"
            [xml] $trx = Get-Content -LiteralPath $result -Raw
            if ([int]$trx.TestRun.ResultSummary.Counters.executed -le 0) { throw "Oracle $suite QA did not execute any tests for $framework." }
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
        if ($LASTEXITCODE -ne 0) { Write-Warning 'Oracle QA cleanup failed; remove the Compose project manually.' }
    }
    finally {
        foreach ($name in $previousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
        }
        Pop-Location
    }
}
