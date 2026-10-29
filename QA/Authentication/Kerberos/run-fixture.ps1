[CmdletBinding()]
param(
    [ValidateSet('net8.0', 'net9.0', 'net10.0')]
    [string[]]$Frameworks = @('net8.0', 'net9.0', 'net10.0'),
    [string]$ResultsDirectory,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$project = 'duburl-kerberos-' + [guid]::NewGuid().ToString('N').Substring(0, 12)
if (-not $ResultsDirectory) { $ResultsDirectory = Join-Path $taskRoot ".artifacts/kerberos/$project" }
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
if ((Test-Path -LiteralPath $ResultsDirectory) -and @(Get-ChildItem -LiteralPath $ResultsDirectory -Force).Count) {
    throw 'Use a new or empty fixture results directory.'
}
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$compose = @('compose', '-p', $project, '-f', (Join-Path $PSScriptRoot 'compose.yaml'))
$savedTestDir = $env:DUBURL_KERBEROS_TEST_DIR
$savedResults = $env:DUBURL_KERBEROS_RESULTS
$env:DUBURL_KERBEROS_RESULTS = $ResultsDirectory
$env:DUBURL_KERBEROS_TEST_DIR = Join-Path $taskRoot "DubUrl.Core.Testing/bin/Debug/$($Frameworks[0])"
$reports = @()
function Invoke-FixtureDocker {
    param([string[]]$DockerArguments)
    & docker @DockerArguments
    if ($LASTEXITCODE -ne 0) { throw "Fixture Docker operation failed (exit $LASTEXITCODE)." }
}
try {
    if (-not $SkipBuild) {
        foreach ($framework in $Frameworks) {
            & dotnet build (Join-Path $taskRoot 'DubUrl.Core.Testing/DubUrl.Core.Testing.csproj') -f $framework --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw 'Fixture test bundle build failed.' }
        }
    }
    Invoke-FixtureDocker ($compose + @('build', 'kdc', 'mariadb', 'client'))
    Invoke-FixtureDocker ($compose + @('up', '-d', '--wait', '--wait-timeout', '120', 'kdc', 'mariadb'))
    Invoke-FixtureDocker ($compose + @('exec', '-T', 'mariadb', 'bash', '/fixture/policy.sh'))
    Invoke-FixtureDocker ($compose + @('up', '-d', 'singlestore'))
    $readyCommand = 'export MYSQL_PWD="$(cat /realm/singlestore-root.secret)"; singlestore -h127.0.0.1 -uroot --ssl-ca=/realm/ca.crt --ssl-mode=VERIFY_IDENTITY -Nse "SELECT 1" >/dev/null 2>&1'
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & docker @compose exec -T singlestore bash -c $readyCommand 2>$null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'The isolated SingleStore server did not become ready; no successful Kerberos login is inferred.' }
    Invoke-FixtureDocker ($compose + @('exec', '-T', 'singlestore', 'bash', '/fixture/policy.sh'))
    Invoke-FixtureDocker ($compose + @('run', '--rm', '--no-deps', '--user', '0', '--entrypoint', 'bash', 'client', '-c', 'chown 10001:10001 /results'))
    foreach ($framework in $Frameworks) {
        $env:DUBURL_KERBEROS_TEST_DIR = Join-Path $taskRoot "DubUrl.Core.Testing/bin/Debug/$framework"
        foreach ($scenario in @('positive', 'missing', 'wrong')) {
            Invoke-FixtureDocker ($compose + @('run', '--rm', '--no-deps', '-e', 'DUBURL_KERBEROS_SINGLESTORE_READY=1', '-e', "DUBURL_KERBEROS_TFM=$framework", 'client', $scenario))
            $path = Join-Path $ResultsDirectory "kerberos-$framework-$scenario.trx"
            [xml]$report = Get-Content -LiteralPath $path
            $counters = $report.TestRun.ResultSummary.Counters
            $skipped = @($report.TestRun.Results.UnitTestResult | Where-Object outcome -eq 'NotExecuted')
            if ([int]$counters.total -ne 6 -or [int]$counters.passed -ne 4 -or [int]$counters.failed -ne 0 -or $skipped.Count -ne 2 -or
                @($skipped | Where-Object { $_.testName -notmatch '"MYSQL"' }).Count) {
                throw 'Unexpected fixture result. Missing/skipped MariaDB or SingleStore cases are not acceptance evidence.'
            }
            $reports += [pscustomobject]@{ Framework = $framework; Scenario = $scenario; Passed = 4; BlockedMySql = 2; Report = $path }
        }
    }
    [pscustomobject]@{
        Project = $project
        Platform = 'Linux x64 containers'
        MariaDb = '11.4.13; gssapi-only account, required TLS and certificate verification'
        SingleStore = '9.1.1; authentication_gss account, required TLS and certificate verification'
        MySql = 'Blocked: the Enterprise-only server Kerberos plugin is unavailable; no Community/MariaDB substitute used'
        Runs = $reports
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.json')
    $reports | Format-Table Framework, Scenario, Passed, BlockedMySql
    Write-Host "Fixture reports: $ResultsDirectory"
}
finally {
    # Only this newly generated Compose project's resources are removed. No prune/global cleanup.
    & docker @compose down --volumes --remove-orphans
    if ($LASTEXITCODE -ne 0) { Write-Warning "Fixture cleanup failed for $project; inspect that exact Compose project." }
    & docker image rm "$project-kdc" "$project-client" "$project-mariadb" 2>$null | Out-Null
    $env:DUBURL_KERBEROS_TEST_DIR = $savedTestDir
    $env:DUBURL_KERBEROS_RESULTS = $savedResults
}
