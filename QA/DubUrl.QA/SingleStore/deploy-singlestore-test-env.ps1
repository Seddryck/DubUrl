[CmdletBinding()]
param(
    [switch] $force,
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
$project = Join-Path $repositoryRoot 'Providers/MySql/DubUrl.Provider.SingleStore.QA/DubUrl.Provider.SingleStore.QA.csproj'
$infrastructure = Join-Path (Split-Path $project) 'infrastructure'
$composeFile = Join-Path $infrastructure 'compose.yaml'
# Isolate concurrent local runs and CI jobs, including their disposable volumes.
$compose = @('compose', '-p', ('duburl-singlestore-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)), '-f', $composeFile)
$previousEnvironment = @{}
foreach ($name in @('ROOT_PASSWORD', 'DUBURL_SINGLESTORE_QA_PORT', 'DUBURL_SINGLESTORE_QA_URL')) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$testPassword = 'QaT' + [Guid]::NewGuid().ToString('N')
$env:ROOT_PASSWORD = $testPassword
$env:DUBURL_SINGLESTORE_QA_PORT = [string]$port
$env:DUBURL_SINGLESTORE_QA_URL = "singlestore://root:${testPassword}@localhost:${port}/DubUrl"
if ($env:GITHUB_ACTIONS -eq 'true') {
    Write-Host "::add-mask::$testPassword"
    Write-Host "::add-mask::$env:DUBURL_SINGLESTORE_QA_URL"
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
    if ($LASTEXITCODE -ne 0) { throw 'SingleStore QA infrastructure did not become healthy before the readiness timeout.' }

    # MYSQL_PWD is inherited inside the container; credentials never appear in CLI arguments.
    $initialization = Get-Content (Join-Path $infrastructure 'initialize.sql') -Raw
    $output = @($initialization | & docker @compose exec -T singlestore sh -c 'MYSQL_PWD="$ROOT_PASSWORD" singlestore -h 127.0.0.1 -u root' 2>&1)
    $initializationExitCode = $LASTEXITCODE
    Write-RedactedOutput $output
    if ($initializationExitCode -ne 0) { throw 'SingleStore QA initialization failed.' }

    foreach ($framework in $frameworks) {
        foreach ($suite in @('complete')) {
            $result = Join-Path $repositoryRoot ".test-results/singlestore/$framework/$suite/$suite.trx"
            Remove-Item -LiteralPath $result -ErrorAction SilentlyContinue
        }
    }
    Run-ProviderQaSuite -project $project -provider singlestore -config $config -frameworks $frameworks -sdkVersions $sdkVersions

    foreach ($framework in $frameworks) {
        foreach ($suite in @('complete')) {
            $result = Join-Path $repositoryRoot ".test-results/singlestore/$framework/$suite/$suite.trx"
            [xml] $trx = Get-Content -LiteralPath $result -Raw
            if ([int]$trx.TestRun.ResultSummary.Counters.executed -le 0) { throw "SingleStore $suite QA did not execute any tests for $framework." }
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
        if ($LASTEXITCODE -ne 0) { Write-Warning 'SingleStore QA cleanup failed; remove the Compose project manually.' }
    }
    finally {
        foreach ($name in $previousEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
        }
        Pop-Location
    }
}
