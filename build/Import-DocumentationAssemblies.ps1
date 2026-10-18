# Load from the same output directory so provider dependencies resolve together.
# The compatibility DubUrl.dll no longer contains the introspector types.
[CmdletBinding()]
param()

$outputRoot = Join-Path $PSScriptRoot '../DubUrl.QA/bin'
$output = @('Release', 'Debug') |
    ForEach-Object { Join-Path $outputRoot "$_/net8.0" } |
    Where-Object { Test-Path (Join-Path $_ 'DubUrl.dll') } |
    Sort-Object { (Get-Item (Join-Path $_ 'DubUrl.dll')).LastWriteTimeUtc } -Descending |
    Select-Object -First 1
if (-not $output) {
    throw 'Build DubUrl.QA for net8.0 before generating documentation.'
}

$assemblies = @('Antlr4.StringTemplate.dll', 'DubUrl.Core.dll') +
    @(Get-ChildItem $output -Filter 'DubUrl.Provider.*.dll' | Sort-Object Name | ForEach-Object Name) +
    @('DubUrl.dll', 'DubUrl.OleDb.dll', 'DubUrl.Adomd.dll')
foreach ($name in $assemblies) {
    $path = (Resolve-Path (Join-Path $output $name) -ErrorAction Stop).Path
    [void][Reflection.Assembly]::LoadFrom($path)
}
