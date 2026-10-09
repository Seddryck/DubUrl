Param(
	[string] $config = "Release",
	[string[]] $frameworks = @("net8.0", "net9.0", "net10.0")
)

. $PSScriptRoot\..\Run-ProviderQaSuite.ps1

$project = Join-Path $PSScriptRoot "..\..\DubUrl.Providers.SsasMultidim.QA\DubUrl.Providers.SsasMultidim.QA.csproj"
Run-ProviderQaSuite -project $project -provider "ssasmultidim" -config $config -frameworks $frameworks
