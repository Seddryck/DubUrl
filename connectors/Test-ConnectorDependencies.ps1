Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$parent = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../.artifacts'))
$root = [IO.Path]::GetFullPath((Join-Path $parent "dependency-tests-$([Guid]::NewGuid().ToString('N'))"))
if (-not $root.StartsWith($parent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Test directory escapes the artifact root.'
}
New-Item -ItemType Directory -Path $root -Force | Out-Null
$inputPath = Join-Path $root 'ConnectorDependencies.deps.json'

function New-Fixture([string] $Driver) {
    $target = @{
        'ConnectorDependencies/1.2.3' = @{ dependencies = @{ Provider = '1.2.3'; Core = '1.2.3'; $Driver = '10.0.0' }; runtime = @{ 'ConnectorDependencies.dll' = @{} } }
        'Provider/1.2.3' = @{ dependencies = @{ Core = '1.2.3' }; runtime = @{ 'Provider.dll' = @{ assemblyVersion = '1.2.3.0' } } }
        "$Driver/10.0.0" = @{ runtimeTargets = @{ 'runtimes/win-x64/native/driver.dll' = @{ rid = 'win-x64'; assetType = 'native' } }; resources = @{ 'fr/Driver.resources.dll' = @{ locale = 'fr' } } }
    }
    return @{
        runtimeTarget = @{ name = '.NETCoreApp,Version=v8.0'; signature = '' }
        compilationOptions = @{}
        targets = @{ '.NETCoreApp,Version=v8.0' = $target; '.NETCoreApp,Version=v8.0/win-x64' = $target }
        libraries = @{ 'ConnectorDependencies/1.2.3' = @{ type = 'project' }; 'Provider/1.2.3' = @{ type = 'project' }; "$Driver/10.0.0" = @{ type = 'package'; sha512 = 'hash' } }
    }
}

try {
    foreach ($driver in @('Npgsql', 'Odbc')) {
        $fixture = New-Fixture $driver
        $fixture | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $inputPath
        $before = (Get-FileHash -LiteralPath $inputPath).Hash
        $output = Join-Path $root "$driver.deps.json"
        & (Join-Path $PSScriptRoot 'Write-ConnectorDependencies.ps1') -InputPath $inputPath -OutputPath $output -ImplementationAssembly 'Provider.dll'
        if ((Get-FileHash -LiteralPath $inputPath).Hash -ne $before) { throw 'Conversion modified publish output.' }
        $document = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json -AsHashtable
        if ($document.libraries.Contains('ConnectorDependencies/1.2.3')) { throw 'Collector library was retained.' }
        foreach ($target in $document.targets.Values) {
            if ($target.Contains('ConnectorDependencies/1.2.3')) { throw 'Collector target was retained.' }
            $provider = $target['Provider/1.2.3']
            if ($provider.dependencies.Contains('Provider') -or $provider.dependencies[$driver] -ne '10.0.0' -or $provider.dependencies.Core -ne '1.2.3') {
                throw 'Implementation dependencies were not merged correctly.'
            }
            if ($provider.runtime['Provider.dll'].assemblyVersion -ne '1.2.3.0') { throw 'Implementation identity changed.' }
            $assets = $target["$driver/10.0.0"]
            if ($assets.runtimeTargets['runtimes/win-x64/native/driver.dll'].rid -ne 'win-x64' -or $assets.resources['fr/Driver.resources.dll'].locale -ne 'fr') {
                throw 'Native or satellite asset metadata changed.'
            }
        }
    }
    $npgsql = Get-Content -LiteralPath (Join-Path $root 'Npgsql.deps.json') -Raw
    $odbc = Get-Content -LiteralPath (Join-Path $root 'Odbc.deps.json') -Raw
    if ($npgsql -notmatch 'Npgsql/10.0.0' -or $npgsql -match 'Odbc/10.0.0' -or $odbc -notmatch 'Odbc/10.0.0' -or $odbc -match 'Npgsql/10.0.0') {
        throw 'Two connectors sharing an implementation must retain separate dependency documents.'
    }
    foreach ($invalid in @('missing-implementation', 'conflicting-version')) {
        $fixture = New-Fixture 'Npgsql'
        if ($invalid -eq 'missing-implementation') { [void]$fixture.libraries.Remove('Provider/1.2.3') }
        else { $fixture.targets['.NETCoreApp,Version=v8.0']['Provider/1.2.3'].dependencies.Npgsql = '9.0.0' }
        $fixture | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $inputPath
        $rejected = $false
        try { & (Join-Path $PSScriptRoot 'Write-ConnectorDependencies.ps1') -InputPath $inputPath -OutputPath (Join-Path $root 'invalid.json') -ImplementationAssembly 'Provider.dll' }
        catch { $rejected = $true }
        if (-not $rejected) { throw "Accepted invalid dependency metadata: $invalid" }
    }
    Write-Host 'Dependency conversion passed: shared implementations, separate drivers, portable/RID targets, native/resources, unchanged input, and invalid metadata.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
