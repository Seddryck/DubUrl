# Offline integration tests: mock GitHub only; exercise real ZIP extraction and file copying.
$ErrorActionPreference = 'Stop'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "duburl-install-test-$([guid]::NewGuid())"
New-Item -ItemType Directory -Path $testRoot | Out-Null
try {
    function Assert-Test($Condition, $Message) {
        if (-not $Condition) { throw $Message }
    }
    function Assert-Fails($Action, $Pattern) {
        try { & $Action } catch {
            Assert-Test ($_.Exception.Message -like $Pattern) "Unexpected failure: $_"
            return
        }
        throw "Expected failure matching: $Pattern"
    }
    function gh {
        $global:LASTEXITCODE = 0
        if ($args[0] -eq 'api') {
            $endpoint = [string]$args[1]
            Assert-Test ($endpoint.StartsWith("/repos/$($scenario.Repository)/", [StringComparison]::Ordinal)) "Repository casing changed: $endpoint"
            switch -Wildcard ($endpoint) {
                '*/pulls/*' {
                    if ($mode -eq 'merged') {
                        '{"number":7,"state":"closed","merged_at":"2026-01-01","merge_commit_sha":"merge"}'
                    } elseif ($mode -eq 'closed') {
                        '{"number":7,"state":"closed","merged_at":null}'
                    } else {
                        '{"number":7,"state":"open","head":{"sha":"head"}}'
                    }
                }
                '*/actions/workflows/*' {
                    Assert-Test ($endpoint.Contains("/$($scenario.Workflow)/runs?")) "Wrong workflow: $endpoint"
                    Assert-Test ($endpoint.Contains('head_sha=head&status=success')) 'Missing head/success filter.'
                    '{"workflow_runs":[{"id":42,"conclusion":"success","created_at":"2026-01-01"}]}'
                }
                '*/actions/runs/42/artifacts*' {
                    @{artifacts=@(@{name=$scenario.ArtifactName; expired=($mode -eq 'expired')})} | ConvertTo-Json -Depth 5
                }
                '*/releases[?]*' { '[{"tag_name":"test","draft":false}]' }
                '*/commits/test' { '{"sha":"merge"}' }
                '*/releases/latest' { '{"tag_name":"test"}' }
                default { throw "Unexpected API endpoint: $endpoint" }
            }
        } elseif ($args[0] -in @('release', 'run')) {
            $repo = $args[[Array]::IndexOf($args, '--repo') + 1]
            Assert-Test ($repo -ceq $scenario.Repository) "Repository casing changed: $repo"
            if ($args[0] -eq 'release') {
                $pattern = $args[[Array]::IndexOf($args, '--pattern') + 1]
                Assert-Test ($pattern -ceq $expectedPattern) "Wrong archive pattern: $pattern"
            } else {
                $artifact = $args[[Array]::IndexOf($args, '--name') + 1]
                Assert-Test ($artifact -ceq $scenario.ArtifactName) "Wrong artifact: $artifact"
            }
            $download = $args[[Array]::IndexOf($args, '--dir') + 1]
            Copy-Item -LiteralPath $archive -Destination $download
        } else { throw "Unexpected GitHub command: $args" }
    }

    $scenarios = @(
        @{ProductName='DubUrl'; Repository='Seddryck/DubUrl'; Workflow='ci-release.yml'; ArtifactName='cli-distribution-net10.0-win-x64'},
        @{ProductName='SampleCli'; Repository='ExampleOwner/SampleCLI'; Workflow='Build-Custom.yml'; ArtifactName='Custom-Zip'; ArchivePattern='portable-*-windows.zip'; ConfigurationFiles=@('appsettings.json', 'preferences.json')}
    )
    foreach ($scenario in $scenarios) {
        $prefix = $scenario.ProductName.ToLowerInvariant()
        $source = Join-Path $testRoot "$prefix-source"
        New-Item -ItemType Directory -Path $source | Out-Null
        Set-Content (Join-Path $source "$prefix.exe") 'new executable'
        Set-Content (Join-Path $source 'appsettings.json') '{"indent":2}'
        Set-Content (Join-Path $source "$prefix.config.json") '{"indent":2}'
        $expectedPattern = if ($scenario.ArchivePattern) { $scenario.ArchivePattern } else { "$prefix-*-net10.0-win-x64.zip" }
        $archive = Join-Path $testRoot ($expectedPattern.Replace('*', 'test'))
        if ($scenario.ConfigurationFiles) {
            Set-Content (Join-Path $source 'preferences.json') '{"custom":true}'
        }
        Compress-Archive -Path (Join-Path $source '*') -DestinationPath $archive
        $destination = Join-Path $testRoot "$prefix-installed"
        $mode = 'release'
        # Exercise actual DubUrl defaults rather than explicitly supplying them.
        $parameters = if ($prefix -eq 'duburl') { @{} } else { $scenario }
        & "$PSScriptRoot/install-local.ps1" -InstallDirectory $destination @parameters
        $config = Join-Path $destination "$prefix.config.json"
        Assert-Test ((Get-Content $config -Raw).Trim() -eq '{"indent":2}') 'Packaged defaults missing.'
        Assert-Fails { & "$PSScriptRoot/install-local.ps1" -InstallDirectory $destination @parameters } '*must be empty*'
        [IO.File]::WriteAllText($config, '{"indent":"tab"}')
        $before = [Convert]::ToBase64String([IO.File]::ReadAllBytes($config))
        [IO.File]::WriteAllText((Join-Path $destination 'appsettings.json'), '{"user":true}')
        if ($scenario.ConfigurationFiles) {
            [IO.File]::WriteAllText((Join-Path $destination 'preferences.json'), '{"user":"custom"}')
        }
        Set-Content (Join-Path $destination "$prefix.exe") 'old executable'
        Set-Content (Join-Path $destination 'unrelated.txt') 'keep'
        & "$PSScriptRoot/install-local.ps1" -InstallDirectory $destination -Force @parameters
        Assert-Test ($before -ceq [Convert]::ToBase64String([IO.File]::ReadAllBytes($config))) 'Existing configuration changed.'
        Assert-Test ((Get-Content (Join-Path $destination "$prefix.exe") -Raw).Trim() -eq 'new executable') 'Executable not updated.'
        Assert-Test ((Get-Content (Join-Path $destination 'appsettings.json') -Raw) -ceq '{"user":true}') 'DubUrl appsettings changed.'
        if ($scenario.ConfigurationFiles) {
            Assert-Test ((Get-Content (Join-Path $destination 'preferences.json') -Raw) -ceq '{"user":"custom"}') 'Custom configuration changed.'
        }
        Assert-Test ((Get-Content (Join-Path $destination 'unrelated.txt') -Raw).Trim() -eq 'keep') 'Unrelated file changed.'
        Remove-Item -LiteralPath $config
        Remove-Item -LiteralPath (Join-Path $destination 'appsettings.json')
        & "$PSScriptRoot/install-local.ps1" -InstallDirectory $destination -Release test -Force @parameters
        Assert-Test (Test-Path $config) 'Missing configuration not supplied.'
        Assert-Test ((Get-Content (Join-Path $destination 'appsettings.json') -Raw).Trim() -ceq '{"indent":2}') 'Missing appsettings not supplied.'

        $mode = 'open'
        & "$PSScriptRoot/install-local.ps1" -InstallDirectory $destination -PullRequest 7 -Force @parameters
        $mode = 'merged'
        & "$PSScriptRoot/install-local.ps1" -InstallDirectory $destination -PullRequest 7 -Force @parameters
        $mode = 'closed'
        Assert-Fails { & "$PSScriptRoot/install-local.ps1" -InstallDirectory $destination -PullRequest 7 -Force @parameters } '*closed without being merged*'
        $mode = 'expired'
        Assert-Fails { & "$PSScriptRoot/install-local.ps1" -InstallDirectory $destination -PullRequest 7 -Force @parameters } '*has no unexpired artifact*'
        Write-Host "Passed: $($scenario.ProductName) defaults/overrides, release and PR paths, casing, overwrite protection, and configuration preservation."
    }
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Test cleanup path is outside temporary directory.'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
