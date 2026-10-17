[CmdletBinding()]
param(
    [string] $ImageMagick = 'magick',
    [string] $OutputPath = "$PSScriptRoot\..\misc\icon\duburl.ico"
)

$ErrorActionPreference = 'Stop'
$sourceDirectory = Join-Path $PSScriptRoot '..\misc\icon'
$sources = @(16, 32, 64, 128, 256) | ForEach-Object {
    Join-Path $sourceDirectory "DubUrl-icon-$_.png"
}

foreach ($source in $sources) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Missing icon source '$source'."
    }
}

& $ImageMagick @sources -define icon:auto-resize=256,128,64,32,16 $OutputPath
if ($LASTEXITCODE -ne 0) {
    throw "ImageMagick failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
    throw "Expected icon '$OutputPath' was not created."
}

Write-Host "Generated $OutputPath from the checked DubUrl artwork."
