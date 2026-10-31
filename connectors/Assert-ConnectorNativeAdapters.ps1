[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Provider,
    [Parameter(Mandatory)][string] $DriverAssembly,
    [Parameter(Mandatory)][string[]] $ArchivePaths
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$encryptedAdapter = 'DubUrl.Provider.DuckDb.Native.dll'
foreach ($path in $ArchivePaths) {
    if ($path -cmatch '^DubUrl\.Provider\.[A-Za-z0-9]+\.[A-Za-z0-9]+\.dll$' -and
        $path -cne $DriverAssembly -and
        -not ($Provider -ceq 'duckdb-native' -and $path -ceq $encryptedAdapter)) {
        throw "Connector '$Provider' still contains a per-driver binding DLL: $path"
    }
}
if ($Provider -ceq 'duckdb-native') {
    if ($ArchivePaths -cnotcontains $encryptedAdapter) {
        throw 'The native DuckDB connector must include its encrypted-connection adapter.'
    }
} elseif ($ArchivePaths -ccontains $encryptedAdapter) {
    throw 'Only the native DuckDB connector may carry the encrypted-connection adapter.'
}
