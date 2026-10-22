# Load from the same output directory so provider dependencies resolve together.
# Core is published as the DubUrl package; the assembly remains DubUrl.Core.dll.
[CmdletBinding()]
param()

$outputRoot = Join-Path $PSScriptRoot '../QA/DubUrl.QA/bin'
$output = @('Release', 'Debug') |
    ForEach-Object { Join-Path $outputRoot "$_/net8.0" } |
    Where-Object { Test-Path (Join-Path $_ 'DubUrl.Core.dll') } |
    Sort-Object { (Get-Item (Join-Path $_ 'DubUrl.Core.dll')).LastWriteTimeUtc } -Descending |
    Select-Object -First 1
if (-not $output) {
    throw 'Build DubUrl.QA for net8.0 before generating documentation.'
}

$assemblies = @('Antlr4.StringTemplate.dll', 'DubUrl.Core.dll') +
    @(Get-ChildItem $output -Filter 'DubUrl.Provider.*.dll' | Sort-Object Name | ForEach-Object Name) +
    @('DubUrl.OleDb.dll', 'DubUrl.Adomd.dll')
foreach ($name in $assemblies) {
    $path = (Resolve-Path (Join-Path $output $name) -ErrorAction Stop).Path
    [void][Reflection.Assembly]::LoadFrom($path)
}
