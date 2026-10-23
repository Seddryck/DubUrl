[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Version,

    [Parameter(Mandatory)]
    [string] $NuGetDirectory
)

$ErrorActionPreference = 'Stop'

$packages = @(
    @{ Id = 'DubUrl'; Assembly = 'DubUrl.Core' },
    @{ Id = 'DubUrl.Provider.Access'; Assembly = 'DubUrl.Provider.Access' },
    @{ Id = 'DubUrl.Provider.Db2'; Assembly = 'DubUrl.Provider.Db2' },
    @{ Id = 'DubUrl.Provider.Drill'; Assembly = 'DubUrl.Provider.Drill' },
    @{ Id = 'DubUrl.Provider.DuckDb'; Assembly = 'DubUrl.Provider.DuckDb' },
    @{ Id = 'DubUrl.Provider.Excel'; Assembly = 'DubUrl.Provider.Excel' },
    @{ Id = 'DubUrl.Provider.FirebirdSql'; Assembly = 'DubUrl.Provider.FirebirdSql' },
    @{ Id = 'DubUrl.Provider.MySql'; Assembly = 'DubUrl.Provider.MySql' },
    @{ Id = 'DubUrl.Provider.Odbc'; Assembly = 'DubUrl.Provider.Odbc' },
    @{ Id = 'DubUrl.Provider.Oracle'; Assembly = 'DubUrl.Provider.Oracle' },
    @{ Id = 'DubUrl.Provider.PostgreSql'; Assembly = 'DubUrl.Provider.PostgreSql' },
    @{ Id = 'DubUrl.Provider.Snowflake'; Assembly = 'DubUrl.Provider.Snowflake' },
    @{ Id = 'DubUrl.Provider.Sqlite'; Assembly = 'DubUrl.Provider.Sqlite' },
    @{ Id = 'DubUrl.Provider.SqlServer'; Assembly = 'DubUrl.Provider.SqlServer' },
    @{ Id = 'DubUrl.Provider.Teradata'; Assembly = 'DubUrl.Provider.Teradata' },
    @{ Id = 'DubUrl.Provider.Text'; Assembly = 'DubUrl.Provider.Text' },
    @{ Id = 'DubUrl.Provider.Trino'; Assembly = 'DubUrl.Provider.Trino' },
    @{ Id = 'DubUrl.OleDb'; Assembly = 'DubUrl.OleDb' },
    @{ Id = 'DubUrl.Extensions'; Assembly = 'DubUrl.Extensions' },
    @{ Id = 'DubUrl.Adomd'; Assembly = 'DubUrl.Adomd' },
    @{ Id = 'DubUrl.Schema'; Assembly = 'DubUrl.Schema' },
    @{ Id = 'DubUrl.Schema.Documents'; Assembly = 'DubUrl.Schema.Documents' },
    @{ Id = 'DubUrl.Application'; Assembly = 'DubUrl.Application' },
    @{ Id = 'DubUrl.BulkCopy'; Assembly = 'DubUrl.BulkCopy' }
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
    if ($package.Assembly) {
        Assert-Condition (Test-Path -LiteralPath $symbolPath -PathType Leaf) "Missing symbol package '$symbolPath'."
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $packagePath))
    try {
        $entries = @($archive.Entries.FullName)
        Assert-Condition ($entries -contains "$($package.Id).nuspec") "Package '$($package.Id)' has no matching nuspec."

        foreach ($framework in $frameworks) {
            $assemblyPath = "lib/$framework/$($package.Assembly).dll"
            Assert-Condition ($entries -contains $assemblyPath) "Package '$($package.Id)' has no assembly at '$assemblyPath'."
        }
        $reader = [IO.StreamReader]::new($archive.GetEntry("$($package.Id).nuspec").Open())
        try { [xml] $specification = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $dependencies = @($specification.SelectNodes("//*[local-name()='dependency']") | ForEach-Object { $_.id })
        Assert-Condition ('DubUrl.Core' -notin $dependencies) "Package '$($package.Id)' depends on the retired DubUrl.Core package."
        if ($package.Id -eq 'DubUrl') {
            Assert-Condition (@($dependencies | Where-Object { $_ -like 'DubUrl.Provider.*' }).Count -eq 0) 'DubUrl must not bring provider implementations.'
        }
        if ($package.Id -eq 'DubUrl.Schema.Documents') {
            $schemaPath = 'schemas/schema-document/v1/schema.json'
            Assert-Condition ($entries -contains $schemaPath) "Package '$($package.Id)' has no schema at '$schemaPath'."
        }
        if ($package.Id -eq 'DubUrl') {
            Assert-Condition ($entries -contains 'buildTransitive/DubUrl.targets') "DubUrl package is missing its single-file dependency discovery target."
        }

    }
    finally {
        $archive.Dispose()
    }
}

Write-Host "Validated $($packageFiles.Count) packages and $($symbolPackageFiles.Count) symbol packages for version $Version."
