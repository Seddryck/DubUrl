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
			& docker @composeArguments
			if ($LASTEXITCODE -eq 0) { return }

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

		[string] $resultsRoot = ".test-results"
	)

	Process {
		$project = (Resolve-Path -LiteralPath $project).Path
		$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
		$resultsRoot = Join-Path $repositoryRoot $resultsRoot

		foreach ($framework in $frameworks) {
			Write-Host "Restoring $provider QA for $framework"
			& dotnet restore $project -p:TargetFramework=$framework --force-evaluate --nologo
			if ($LASTEXITCODE -ne 0) {
				throw "Restore failed for $provider QA ($framework)."
			}

			Write-Host "Building $provider QA for $framework"
			& dotnet build $project `
				--configuration $config `
				--framework $framework `
				--no-restore `
				--nologo `
				-p:ContinuousIntegrationBuild=true
			if ($LASTEXITCODE -ne 0) {
				throw "Build failed for $provider QA ($framework)."
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
				throw "Complete suite failed for $provider QA ($framework)."
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
				throw "Schema suite failed for $provider QA ($framework)."
			}

			Write-Host "Building $provider QA bundle for $framework"
			& dotnet msbuild $project `
				-t:QaBundle `
				-p:Configuration=$config `
				-p:TargetFramework=$framework `
				-p:ContinuousIntegrationBuild=true `
				-nologo
			if ($LASTEXITCODE -ne 0) {
				throw "Bundle generation failed for $provider QA ($framework)."
			}
		}
	}
}
