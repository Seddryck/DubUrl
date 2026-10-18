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
    @{ Id = 'DubUrl.Schema.Documents'; Assembly = 'DubUrl.Schema.Documents' },
    @{ Id = 'DubUrl.Application'; Assembly = 'DubUrl.Application' },
    @{ Id = 'DubUrl.BulkCopy'; Assembly = 'DubUrl.BulkCopy' },
    @{ Id = 'DubUrl.Connector.PostgreSql.Npgsql'; Assembly = 'DubUrl.Connector.PostgreSql.Npgsql' }
)
$frameworks = @('net8.0', 'net9.0', 'net10.0')

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

        if ($package.Id -eq 'DubUrl.Schema.Documents') {
            $schemaPath = 'schemas/schema-document/v1/schema.json'
            Assert-Condition ($entries -contains $schemaPath) "Package '$($package.Id)' has no schema at '$schemaPath'."
        }

        if ($package.Id -eq 'DubUrl.Connector.PostgreSql.Npgsql') {
            $nuspecEntry = $archive.GetEntry("$($package.Id).nuspec")
            $reader = [IO.StreamReader]::new($nuspecEntry.Open())
            try {
                [xml] $nuspec = $reader.ReadToEnd()
                $dependency = @($nuspec.package.metadata.dependencies.group.dependency | Where-Object id -eq 'DubUrl')
                Assert-Condition ($dependency.Count -eq 3) "Connector package must declare DubUrl once for each target framework."
                foreach ($item in $dependency) {
                    Assert-Condition ($item.version -eq "[$Version,1.0.0)") "Connector package has unexpected DubUrl dependency range '$($item.version)'."
                }
            }
            finally {
                $reader.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

Write-Host "Validated $($packageFiles.Count) packages and $($symbolPackageFiles.Count) symbol packages for version $Version."
