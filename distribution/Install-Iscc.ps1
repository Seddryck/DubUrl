[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($command) { Write-Host "Inno Setup found at '$($command.Source)'."; return }

$install = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (Test-Path -LiteralPath $install) {
    Add-Content -LiteralPath $env:GITHUB_PATH -Value (Split-Path $install -Parent)
    return
}

winget install JRSoftware.InnoSetup --scope user --silent --accept-package-agreements --accept-source-agreements
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $install)) {
    throw 'Unable to install Inno Setup.'
}
Add-Content -LiteralPath $env:GITHUB_PATH -Value (Split-Path $install -Parent)
