function Assert-ProviderQaResults {
    param([Parameter(Mandatory)][string] $path, [switch] $requireAll)
    if (!(Test-Path -LiteralPath $path)) { throw 'Expected test result file is missing.' }
    [xml] $result = Get-Content -LiteralPath $path -Raw
    $counts = $result.TestRun.ResultSummary.Counters
    if (!$counts -or [int]$counts.executed -le 0) { throw 'No live tests executed.' }
    if ([int]$counts.failed -gt 0 -or [int]$counts.error -gt 0 -or [int]$counts.aborted -gt 0) { throw 'Test results contain failures.' }
    if ($requireAll -and ([int]$counts.executed -ne [int]$counts.total -or [int]$counts.passed -ne [int]$counts.total)) {
        throw 'Required suite contains skipped or unsuccessful tests.'
    }
}
Function Start-ProviderQaInfrastructure {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true)]
		[ValidateNotNullOrEmpty()]
		[string] $composeFile,

		[Parameter(Mandatory=$true)]
		[ValidateNotNullOrEmpty()]
		[string] $provider,

		[switch] $wait,

		[int] $attempts = 3,

		[int] $initialDelaySeconds = 30
	)

	Process {
		$composeArguments = @("compose", "-f", $composeFile, "up", "--detach")
		if ($wait) { $composeArguments += "--wait" }

		foreach ($attempt in 1..$attempts) {
			$composeOutput = @(& docker @composeArguments 2>&1)
			$composeExitCode = $LASTEXITCODE
			foreach ($line in $composeOutput) { Write-Host $line }
			if ($composeExitCode -eq 0) { return }

			$composeMessage = $composeOutput -join [Environment]::NewLine
			if ($composeMessage -match '(?i)(data limit exceeded|you have reached your pull rate limit)') {
				throw "$provider QA infrastructure failed to start because the container registry quota is exhausted."
			}

			if ($attempt -lt $attempts) {
				$delay = [int]($initialDelaySeconds * [math]::Pow(2, $attempt - 1))
				Write-Warning "$provider QA infrastructure failed to start (attempt $attempt of $attempts). Retrying in $delay seconds."
				Start-Sleep -Seconds $delay
			}
		}

		throw "$provider QA infrastructure failed to start after $attempts attempts."
	}
}

Function Run-ProviderQaSuite {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true)]
		[ValidateNotNullOrEmpty()]
		[string] $project,

		[Parameter(Mandatory=$true)]
		[ValidateNotNullOrEmpty()]
		[string] $provider,

		[string] $config = "Release",

		[string[]] $frameworks = @("net8.0", "net9.0", "net10.0"),

		[hashtable] $sdkVersions = @{
			"net8.0" = "8.0.416"
			"net9.0" = "9.0.308"
			"net10.0" = "10.0.401"
		},

		[string] $resultsRoot = ".test-results",
        [string] $testFilter = '',
        [string[]] $requiredCategories = @()
	)

	Process {
		$project = (Resolve-Path -LiteralPath $project).Path
		$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
		$resultsRoot = Join-Path $repositoryRoot $resultsRoot
		$failures = @()
		$globalJsonPath = Join-Path $repositoryRoot "global.json"
		$existingGlobalJson = if (Test-Path -LiteralPath $globalJsonPath) {
			Get-Content -LiteralPath $globalJsonPath -Raw
		} else {
			$null
		}

		try {
			foreach ($framework in $frameworks) {
				if (!$sdkVersions.ContainsKey($framework)) {
					$failures += "No .NET SDK version is configured for $provider QA ($framework)."
					continue
				}

				$sdkConfiguration = [ordered]@{
					sdk = [ordered]@{
						version = $sdkVersions[$framework]
						rollForward = "disable"
						allowPrerelease = $false
					}
				}
				$sdkConfiguration | ConvertTo-Json -Depth 2 | Set-Content -LiteralPath $globalJsonPath -Encoding utf8

				Write-Host "Restoring $provider QA for $framework with .NET SDK $($sdkVersions[$framework])"
				& dotnet restore $project -p:TargetFramework=$framework --force-evaluate --nologo
				if ($LASTEXITCODE -ne 0) {
					$failures += "Restore failed for $provider QA ($framework)."
					continue
				}

				Write-Host "Building $provider QA for $framework"
				& dotnet build $project `
					--configuration $config `
					--framework $framework `
					--no-restore `
					--nologo `
					-p:ContinuousIntegrationBuild=true
				if ($LASTEXITCODE -ne 0) {
					$failures += "Build failed for $provider QA ($framework)."
					continue
				}

				$frameworkResults = Join-Path $resultsRoot "$provider/$framework"
                $suites = @(@{ Name='complete'; Filter=$testFilter; Strict=![string]::IsNullOrEmpty($testFilter) })
                $manifest = Get-Content (Join-Path (Split-Path $project) 'qa-manifest.json') -Raw | ConvertFrom-Json
                if (!$testFilter -and $manifest.capabilities -contains 'Schema') {
                    $suites += @{ Name='schema'; Filter='TestCategory=Schema'; Strict=$true }
                }
                foreach ($category in $requiredCategories) {
                    $suites += @{ Name=$category.ToLowerInvariant(); Filter="TestCategory=$category"; Strict=$true }
                }
                foreach ($suite in $suites) {
                    $directory = Join-Path $frameworkResults $suite.Name
                    $result = Join-Path $directory "$($suite.Name).trx"
                    Remove-Item -LiteralPath $result -ErrorAction SilentlyContinue
                    $arguments = @('test', $project, '--configuration', $config, '--framework', $framework,
                        '--no-build', '--no-restore', '--nologo', '--logger', "trx;LogFileName=$($suite.Name).trx", '--results-directory', $directory)
                    if ($suite.Filter) { $arguments += @('--filter', $suite.Filter) }
                    & dotnet @arguments
                    if ($LASTEXITCODE -ne 0) { $failures += "$($suite.Name) tests failed for $provider ($framework)." }
                    try { Assert-ProviderQaResults -path $result -requireAll:$suite.Strict }
                    catch { $failures += "$provider ($framework) $($suite.Name): $($_.Exception.Message)" }
                }
				Write-Host "Building $provider QA bundle for $framework"
				& dotnet msbuild $project `
					-t:QaBundle `
					-p:Configuration=$config `
					-p:TargetFramework=$framework `
					-p:ContinuousIntegrationBuild=true `
					-nologo
				if ($LASTEXITCODE -ne 0) {
					$failures += "Bundle generation failed for $provider QA ($framework)."
				}
			}
		}
		finally {
			if ($null -eq $existingGlobalJson) {
				Remove-Item -LiteralPath $globalJsonPath -ErrorAction SilentlyContinue
			} else {
				Set-Content -LiteralPath $globalJsonPath -Value $existingGlobalJson -NoNewline -Encoding utf8
			}
		}

		if ($failures.Count -gt 0) {
			$failureSummary = ($failures | ForEach-Object { " - $_" }) -join [Environment]::NewLine
			throw "$provider QA failed:$([Environment]::NewLine)$failureSummary"
		}
	}
}
