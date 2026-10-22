[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $InputPath,
    [Parameter(Mandatory)][string] $OutputPath,
    [Parameter(Mandatory)][string] $ImplementationAssembly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$document = Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json -AsHashtable
$collector = [IO.Path]::GetFileName($InputPath).Replace('.deps.json', '')
$implementation = [IO.Path]::GetFileNameWithoutExtension($ImplementationAssembly)
$collectorKeys = @($document.libraries.Keys | Where-Object { $_.StartsWith("$collector/", [StringComparison]::Ordinal) })
$implementationKeys = @($document.libraries.Keys | Where-Object { $_.StartsWith("$implementation/", [StringComparison]::Ordinal) })
if ($collectorKeys.Count -ne 1 -or $implementationKeys.Count -ne 1) {
    throw 'Dependency metadata must contain exactly one collector and one selected implementation library.'
}
$collectorKey = $collectorKeys[0]
$implementationKey = $implementationKeys[0]
foreach ($target in $document.targets.Values) {
    if (-not $target.Contains($collectorKey)) { continue }
    if (-not $target.Contains($implementationKey)) { throw "Target is missing implementation '$implementationKey'." }
    $root = $target[$implementationKey]
    if (-not $root.Contains('dependencies')) { $root.dependencies = [ordered]@{} }
    foreach ($dependency in $target[$collectorKey].dependencies.GetEnumerator()) {
        if ($dependency.Key -eq $implementation) { continue }
        if ($root.dependencies.Contains($dependency.Key) -and $root.dependencies[$dependency.Key] -ne $dependency.Value) {
            throw "Conflicting dependency version for '$($dependency.Key)'."
        }
        $root.dependencies[$dependency.Key] = $dependency.Value
    }
    [void]$target.Remove($collectorKey)
}
[void]$document.libraries.Remove($collectorKey)
$json = $document | ConvertTo-Json -Depth 100
[IO.File]::WriteAllText($OutputPath, "$json`n", [Text.UTF8Encoding]::new($false))
