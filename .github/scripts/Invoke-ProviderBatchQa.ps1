param(
    [Parameter(Mandatory)]
    [ValidateSet('mssqlserver', 'postgresql', 'mysql')]
    [string] $Provider,

    [Parameter(Mandatory)]
    [string[]] $Frameworks
)

$ErrorActionPreference = 'Stop'
$providerRoot = (Get-Location).Path
$coreRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $providerRoot 'QA/DubUrl.QA/Run-ProviderQaSuite.ps1')

$fixture = switch ($Provider) {
    'mssqlserver' {
        @{
            Project = 'SqlServer/DubUrl.Provider.MsSqlServer.QA'
            Port = 1433
            Scheme = 'mssql'
            User = 'sa'
            Database = 'master'
            Options = '?TrustServerCertificate=true'
            PasswordVariable = 'MSSQL_SA_PASSWORD'
            UrlVariable = 'DUBURL_SQLSERVER_BATCH_QA_URL'
            Test = 'CreateBatch_SQLServer_Live'
        }
    }
    'postgresql' {
        @{
            Project = 'PostgreSql/DubUrl.Provider.PostgreSql.QA'
            Port = 5432
            Scheme = 'pgsql'
            User = 'postgres'
            Database = 'postgres'
            PasswordVariable = 'POSTGRES_PASSWORD'
            UrlVariable = 'DUBURL_POSTGRESQL_BATCH_QA_URL'
            Test = 'CreateBatch_PostgreSQL_Live'
            HealthCheck = '["CMD", "pg_isready", "-h", "127.0.0.1", "-U", "postgres"]'
        }
    }
    'mysql' {
        @{
            Project = 'MySql/DubUrl.Provider.MySql.QA'
            Port = 3306
            Scheme = 'mysql'
            User = 'root'
            Database = 'DubUrl'
            PasswordVariable = 'MYSQL_ROOT_PASSWORD'
            UrlVariable = 'DUBURL_MYSQL_BATCH_QA_URL'
            Test = 'CreateBatch_MySQL_Live'
            HealthCheck = '["CMD", "mysqladmin", "ping", "--protocol=TCP", "-h", "127.0.0.1"]'
        }
    }
}

$composeFile = Join-Path $providerRoot "Providers/$($fixture.Project)/infrastructure/compose.yaml"
$overrideFile = New-TemporaryFile
$composeProject = "duburl-batch-$([guid]::NewGuid().ToString('N').Substring(0, 12))"
$composeArgs = @('compose', '--project-name', $composeProject, '-f', $composeFile, '-f', $overrideFile.FullName)
$previousUrl = [Environment]::GetEnvironmentVariable($fixture.UrlVariable)
$infrastructureStarted = $false

try {
    Push-Location $coreRoot
    try {
        $testProject = 'DubUrl.Core.Testing/DubUrl.Core.Testing.csproj'
        dotnet build $testProject --configuration Release --nologo
        if ($LASTEXITCODE) { throw 'Could not build the Core batch tests.' }

        # Use the existing service with a private, dynamically allocated IPv4 port.
        $override = @"
services:
  ${Provider}:
    ports: !override ["127.0.0.1::$($fixture.Port)"]
"@
        if ($fixture.HealthCheck) {
            $override += "`n    healthcheck:`n      test: $($fixture.HealthCheck)"
        }
        Set-Content -LiteralPath $overrideFile.FullName -Value $override
        $infrastructureStarted = $true
        docker @composeArgs up --detach --wait --wait-timeout 180 $Provider
        if ($LASTEXITCODE) { throw "Could not start the $Provider batch fixture." }

        $binding = docker @composeArgs port $Provider $fixture.Port
        if ($LASTEXITCODE -or $binding -notmatch '^127\.0\.0\.1:(\d+)$') {
            throw "Could not resolve the $Provider batch fixture port."
        }
        $port = $Matches[1]
        $password = docker @composeArgs exec -T $Provider printenv $fixture.PasswordVariable
        if ($LASTEXITCODE -or [string]::IsNullOrWhiteSpace($password)) {
            throw "Could not read the $Provider batch fixture credential."
        }
        $escapedPassword = [Uri]::EscapeDataString($password)
        $url = "$($fixture.Scheme)://$($fixture.User):${escapedPassword}@127.0.0.1:${port}/$($fixture.Database)$($fixture.Options)"
        if ($env:GITHUB_ACTIONS -eq 'true') {
            Write-Host "::add-mask::$password"
            Write-Host "::add-mask::$escapedPassword"
            Write-Host "::add-mask::$url"
        }
        [Environment]::SetEnvironmentVariable($fixture.UrlVariable, $url)

        foreach ($framework in $Frameworks) {
            $results = Join-Path $providerRoot ".test-results/$Provider/batch/$framework"
            $result = Join-Path $results 'batch.trx'
            if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
            dotnet test $testProject --configuration Release --framework $framework --no-build --no-restore --nologo `
                --filter "Name=$($fixture.Test)" --logger 'trx;LogFileName=batch.trx' --results-directory $results
            if ($LASTEXITCODE) { throw "The $Provider batch test failed on $framework." }
            Assert-ProviderQaResults -path $result -requireAll
            [xml] $trx = Get-Content -LiteralPath $result -Raw
            $cases = @($trx.TestRun.Results.UnitTestResult)
            if ($cases.Count -ne 1 -or $cases[0].testName -ne $fixture.Test -or $cases[0].outcome -ne 'Passed') {
                throw "Expected $($fixture.Test) to pass exactly once on $framework."
            }
        }
    }
    finally { Pop-Location }
}
finally {
    [Environment]::SetEnvironmentVariable($fixture.UrlVariable, $previousUrl)
    if ($infrastructureStarted) { docker @composeArgs down --volumes }
    Remove-Item -LiteralPath $overrideFile.FullName
}
