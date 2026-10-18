param(
    [Parameter(Mandatory = $true)]
    [string] $PackageDirectory,
    [string] $PackageId = 'DubUrl-cli',
    [string] $CommandName = 'duburl',
    [string] $BuildIdentity = 'DubUrl.Cli',
    [string] $TargetFrameworks = 'net8.0;net9.0;net10.0',
    [string] $RuntimeIdentifiers = 'win-x64;win-arm64;linux-x64;linux-musl-x64;osx-x64;osx-arm64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$expectedFrameworks = @($TargetFrameworks.Split(';', [StringSplitOptions]::RemoveEmptyEntries))
$expectedRuntimes = @($RuntimeIdentifiers.Split(';', [StringSplitOptions]::RemoveEmptyEntries))
$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -File -Filter '*.nupkg' |
    Where-Object Name -NotLike '*.symbols.nupkg')

if ($packages.Count -ne ($expectedRuntimes.Count + 1)) {
    throw "Expected $($expectedRuntimes.Count + 1) tool packages, found $($packages.Count)."
}

function Read-ZipEntry([IO.Compression.ZipArchiveEntry] $Entry) {
    $reader = [IO.StreamReader]::new($Entry.Open())
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Assert-Settings([xml] $Settings, [string] $PackageName, [string] $ExpectedEntryPoint, [switch] $Pointer) {
    if ($Settings.DotNetCliTool.GetAttribute('Version') -ne '2') {
        throw "$PackageName must use DotnetToolSettings.xml version 2."
    }
    $commands = @($Settings.DotNetCliTool.Commands.Command)
    if ($commands.Count -ne 1 -or $commands[0].GetAttribute('Name') -ne $CommandName) {
        throw "$PackageName must expose exactly the '$CommandName' command."
    }
    if ($Pointer) {
        if ($commands[0].GetAttribute('EntryPoint') -or $commands[0].GetAttribute('Runner')) {
            throw "$PackageName pointer settings must not declare an entry point or runner."
        }
        $references = @($Settings.DotNetCliTool.RuntimeIdentifierPackages.RuntimeIdentifierPackage)
        if ($references.Count -ne $expectedRuntimes.Count) {
            throw "$PackageName must reference exactly $($expectedRuntimes.Count) runtime packages."
        }
        foreach ($runtime in $expectedRuntimes) {
            $match = @($references | Where-Object {
                $_.GetAttribute('RuntimeIdentifier') -eq $runtime -and $_.GetAttribute('Id') -eq "$PackageId.$runtime"
            })
            if ($match.Count -ne 1) { throw "$PackageName has an invalid reference for '$runtime'." }
        }
    }
    elseif ($commands[0].GetAttribute('EntryPoint') -ne $ExpectedEntryPoint -or
            $commands[0].GetAttribute('Runner') -ne 'executable') {
        throw "$PackageName must run '$ExpectedEntryPoint' with the executable runner."
    }
}

$seenPointer = $false
$seenRuntimes = @{}
foreach ($package in $packages) {
    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $nuspecEntries = @($archive.Entries | Where-Object Name -Like '*.nuspec')
        if ($nuspecEntries.Count -ne 1) { throw "$($package.Name) must contain exactly one nuspec." }
        [xml] $nuspec = Read-ZipEntry $nuspecEntries[0]
        $metadata = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        $actualId = $metadata.SelectSingleNode("*[local-name()='id']").InnerText
        $types = @($metadata.SelectNodes("*[local-name()='packageTypes']/*[local-name()='packageType']") |
            ForEach-Object { $_.GetAttribute('name') })
        $settings = @($archive.Entries | Where-Object Name -EQ 'DotnetToolSettings.xml')

        if ($types -contains 'DotnetTool') {
            if ($seenPointer -or $actualId -ne $PackageId) { throw "Expected one '$PackageId' pointer package." }
            $seenPointer = $true
            $entry = @($settings | Where-Object FullName -EQ 'tools/any/any/DotnetToolSettings.xml')
            if ($settings.Count -ne 1 -or $entry.Count -ne 1) { throw "$($package.Name) has invalid pointer settings placement." }
            Assert-Settings ([xml](Read-ZipEntry $entry[0])) $package.Name '' -Pointer
            continue
        }

        if ($types -notcontains 'DotnetToolRidPackage') { throw "$($package.Name) has an unexpected package type." }
        $runtime = @($expectedRuntimes | Where-Object { $actualId -eq "$PackageId.$_" })
        if ($runtime.Count -ne 1 -or $seenRuntimes.ContainsKey($runtime[0])) { throw "Unexpected runtime package '$actualId'." }
        $runtime = $runtime[0]
        $seenRuntimes[$runtime] = $true
        if ($settings.Count -ne $expectedFrameworks.Count) { throw "$($package.Name) must contain one settings file per TFM." }
        $entryPoint = if ($runtime.StartsWith('win-')) { "$BuildIdentity.exe" } else { $BuildIdentity }
        foreach ($framework in $expectedFrameworks) {
            $path = "tools/$framework/$runtime/DotnetToolSettings.xml"
            $entry = @($settings | Where-Object FullName -EQ $path)
            if ($entry.Count -ne 1) { throw "$($package.Name) is missing '$path'." }
            Assert-Settings ([xml](Read-ZipEntry $entry[0])) $package.Name $entryPoint
        }
    }
    finally { $archive.Dispose() }
}

if (-not $seenPointer -or $seenRuntimes.Count -ne $expectedRuntimes.Count) {
    throw 'The tool package set is incomplete.'
}
Write-Host "Validated $($packages.Count) tool packages."
