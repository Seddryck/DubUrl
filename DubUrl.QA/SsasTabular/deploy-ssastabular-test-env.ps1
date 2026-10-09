Param(
	[string] $config = "Release",
	[string[]] $frameworks = @("net8.0", "net9.0", "net10.0")
)

. $PSScriptRoot\..\Run-ProviderQaSuite.ps1

$project = Join-Path $PSScriptRoot "..\..\DubUrl.Providers.SsasTabular.QA\DubUrl.Providers.SsasTabular.QA.csproj"
Run-ProviderQaSuite -project $project -provider "ssastabular" -config $config -frameworks $frameworks
