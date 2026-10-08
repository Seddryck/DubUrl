[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $NuGetDirectory
)

$ErrorActionPreference = 'Stop'

$packages = @(
    @{ Id = 'DubUrl'; Assembly = 'DubUrl' },
    @{ Id = 'DubUrl.OleDb'; Assembly = 'DubUrl.OleDb' },
    @{ Id = 'DubUrl.Extensions'; Assembly = 'DubUrl.Extensions' },
    @{ Id = 'DubUrl.Adomd'; Assembly = 'DubUrl.Adomd' },
    @{ Id = 'DubUrl.Schema'; Assembly = 'DubUrl.Schema' },
    @{ Id = 'DubUrl.BulkCopy'; Assembly = 'DubUrl.BulkCopy' }
)
$frameworks = @('net8.0', 'net9.0')

function Assert-Condition {
    param(
        [Parameter(Mandatory)]
        [bool] $Condition,

        [Parameter(Mandatory)]
        [string] $Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$packageFiles = @(Get-ChildItem -LiteralPath $NuGetDirectory -Filter '*.nupkg' -File)
$symbolPackageFiles = @(Get-ChildItem -LiteralPath $NuGetDirectory -Filter '*.snupkg' -File)

Assert-Condition ($packageFiles.Count -eq $packages.Count) "Expected $($packages.Count) NuGet packages, found $($packageFiles.Count)."
Assert-Condition ($symbolPackageFiles.Count -eq $packages.Count) "Expected $($packages.Count) symbol packages, found $($symbolPackageFiles.Count)."

foreach ($package in $packages) {
    $packagePath = Join-Path $NuGetDirectory "$($package.Id).$Version.nupkg"
    $symbolPath = Join-Path $NuGetDirectory "$($package.Id).$Version.snupkg"

    Assert-Condition (Test-Path -LiteralPath $packagePath -PathType Leaf) "Missing package '$packagePath'."
    Assert-Condition (Test-Path -LiteralPath $symbolPath -PathType Leaf) "Missing symbol package '$symbolPath'."

    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $packagePath))
    try {
        $entries = @($archive.Entries.FullName)
        Assert-Condition ($entries -contains "$($package.Id).nuspec") "Package '$($package.Id)' has no matching nuspec."

        foreach ($framework in $frameworks) {
            $assemblyPath = "lib/$framework/$($package.Assembly).dll"
            Assert-Condition ($entries -contains $assemblyPath) "Package '$($package.Id)' has no assembly at '$assemblyPath'."
        }
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host "Validated $($packageFiles.Count) packages and $($symbolPackageFiles.Count) symbol packages for version $Version."
