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

		[string] $resultsRoot = ".test-results"
	)

	Process {
		$project = (Resolve-Path -LiteralPath $project).Path
		$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
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
				Write-Host "Running complete $provider QA suite for $framework"
				& dotnet test $project `
					--configuration $config `
					--framework $framework `
					--no-build `
					--no-restore `
					--nologo `
					--logger "trx;LogFileName=complete.trx" `
					--results-directory (Join-Path $frameworkResults "complete")
				if ($LASTEXITCODE -ne 0) {
					$failures += "Complete suite failed for $provider QA ($framework)."
				}

				Write-Host "Running $provider Schema capability for $framework"
				& dotnet test $project `
					--configuration $config `
					--framework $framework `
					--filter "TestCategory=Schema" `
					--no-build `
					--no-restore `
					--nologo `
					--logger "trx;LogFileName=schema.trx" `
					--results-directory (Join-Path $frameworkResults "schema")
				if ($LASTEXITCODE -ne 0) {
					$failures += "Schema suite failed for $provider QA ($framework)."
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
