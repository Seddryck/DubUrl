Param(
	[string] $config = "Release",
	[string[]] $frameworks = @("net8.0", "net9.0", "net10.0")
)

. $PSScriptRoot\..\Run-ProviderQaSuite.ps1

$project = Join-Path $PSScriptRoot "..\..\Providers\Adomd\DubUrl.Provider.SsasTabular.QA\DubUrl.Provider.SsasTabular.QA.csproj"
Run-ProviderQaSuite -project $project -provider "ssastabular" -config $config -frameworks $frameworks
