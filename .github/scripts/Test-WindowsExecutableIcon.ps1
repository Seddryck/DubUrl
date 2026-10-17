param([Parameter(Mandatory = $true)][string] $Executable)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Windows executable icon validation must run on Windows.' }
if (-not (Test-Path -LiteralPath $Executable -PathType Leaf)) { throw "Executable '$Executable' does not exist." }

Add-Type -AssemblyName System.Drawing.Common
$icon = [Drawing.Icon]::ExtractAssociatedIcon((Resolve-Path -LiteralPath $Executable))
try {
    if ($null -eq $icon -or $icon.Width -le 16 -or $icon.Height -le 16) {
        throw "Executable '$Executable' does not contain the expected multi-resolution icon resource."
    }
    Write-Host "Validated embedded icon in '$Executable' ($($icon.Width)x$($icon.Height))."
}
finally {
    if ($null -ne $icon) { $icon.Dispose() }
}
