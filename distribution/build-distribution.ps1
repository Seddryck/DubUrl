[CmdletBinding()]
param(
    [ValidateSet('Publish', 'Archive', 'Bundle', 'Pack', 'Distribute')]
    [string] $Mode = 'Distribute',
    [string] $Project = '../DubUrl.Cli/DubUrl.Cli.csproj',
    [string] $InstallerScript = './duburl.iss',
    [string] $Version = $(if ($env:GitVersion_SemVer) { $env:GitVersion_SemVer } else { '0.0.0-local' }),
    [string[]] $Frameworks,
    [string[]] $Runtimes,
    [string] $Configuration = 'Release',
    [string] $OutputRoot = './bin',
    [string] $InnoSetupCompiler = 'ISCC.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $Project))
$InstallerScript = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $InstallerScript))
$OutputRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $OutputRoot))
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$PublishRoot = Join-Path $OutputRoot 'publish'
$ArchiveRoot = Join-Path $OutputRoot 'archives'
$InstallerRoot = Join-Path $OutputRoot 'installers'
$PackageRoot = Join-Path $OutputRoot 'packages'
$StagingRoot = Join-Path $OutputRoot 'staging'
$BuildIdentity = 'DubUrl.Cli'
$CommandName = 'duburl'

function Get-DeclaredList([string] $Path, [string] $PropertyName) {
    [xml] $document = Get-Content -LiteralPath $Path -Raw
    $nodes = @($document.SelectNodes("/Project/PropertyGroup/$PropertyName"))
    if ($nodes.Count -eq 0) { throw "Could not find $PropertyName in '$Path'." }
    return @($nodes[-1].InnerText.Split(';', [StringSplitOptions]::RemoveEmptyEntries))
}

if (-not $Frameworks -or $Frameworks.Count -eq 0) {
    $Frameworks = Get-DeclaredList (Join-Path $RepositoryRoot 'Directory.Build.props') 'TargetFrameworks'
}
if (-not $Runtimes -or $Runtimes.Count -eq 0) {
    $Runtimes = Get-DeclaredList $Project 'RuntimeIdentifiers'
}

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    Write-Host "> $Command $($Arguments -join ' ')" -ForegroundColor DarkGray
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command '$Command' failed with exit code $LASTEXITCODE." }
}

function Assert-File([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$Description does not exist: $Path" }
}

function Assert-Directory([string] $Path, [string] $Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) { throw "$Description does not exist: $Path" }
}

function New-CleanDirectory([string] $Path) {
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Get-PublishDirectory([string] $Framework, [string] $Runtime) {
    Join-Path $PublishRoot "$Framework/$Runtime"
}

function Get-ExecutableName([string] $Runtime, [string] $Name) {
    if ($Runtime.StartsWith('win-', [StringComparison]::OrdinalIgnoreCase)) { "$Name.exe" } else { $Name }
}

function Invoke-Publish {
    Invoke-Checked dotnet @('restore', $Project, '--locked-mode')
    foreach ($framework in $Frameworks) {
        foreach ($runtime in $Runtimes) {
            $output = Get-PublishDirectory $framework $runtime
            New-CleanDirectory $output
            Invoke-Checked dotnet @(
                'publish', $Project, '--configuration', $Configuration, '--framework', $framework,
                '--runtime', $runtime, '--output', $output, '--no-self-contained', '--no-restore',
                "-p:Version=$Version"
            )
            Assert-File (Join-Path $output (Get-ExecutableName $runtime $BuildIdentity)) 'Published executable'
        }
    }
}

function Invoke-Archive {
    New-Item -ItemType Directory -Path $ArchiveRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $StagingRoot -Force | Out-Null
    foreach ($framework in $Frameworks) {
        foreach ($runtime in $Runtimes) {
            $publish = Get-PublishDirectory $framework $runtime
            Assert-Directory $publish 'Publish directory'
            $stage = Join-Path $StagingRoot "$framework-$runtime"
            New-CleanDirectory $stage
            Copy-Item -Path (Join-Path $publish '*') -Destination $stage -Recurse -Force
            $sourceExecutable = Join-Path $stage (Get-ExecutableName $runtime $BuildIdentity)
            $commandExecutable = Join-Path $stage (Get-ExecutableName $runtime $CommandName)
            Assert-File $sourceExecutable 'Published executable'
            Move-Item -LiteralPath $sourceExecutable -Destination $commandExecutable -Force
            if ($runtime.StartsWith('linux-') -and -not $IsWindows) {
                Invoke-Checked chmod @('+x', $commandExecutable)
            }

            $baseName = "duburl-$Version-$framework-$runtime".ToLowerInvariant()
            if ($runtime.StartsWith('linux-')) {
                $archive = Join-Path $ArchiveRoot "$baseName.tar.gz"
                Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
                Invoke-Checked tar @('-czf', $archive, '-C', $stage, '.')
            }
            else {
                $archive = Join-Path $ArchiveRoot "$baseName.zip"
                Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
                Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -CompressionLevel Optimal
            }
            Assert-File $archive 'Distribution archive'
        }
    }
}

function Get-InstallerArchitecture([string] $Runtime) {
    switch ($Runtime) {
        'win-x64' { 'x64compatible' }
        'win-arm64' { 'arm64' }
        default { throw "No installer architecture mapping exists for '$Runtime'." }
    }
}

function Invoke-Bundle {
    Assert-File $InstallerScript 'Inno Setup script'
    New-Item -ItemType Directory -Path $InstallerRoot -Force | Out-Null
    $framework = $Frameworks[-1]
    foreach ($runtime in $Runtimes | Where-Object { $_ -like 'win-*' }) {
        $publish = Get-PublishDirectory $framework $runtime
        Assert-File (Join-Path $publish "$BuildIdentity.exe") 'Published Windows executable'
        $name = "duburl-$Version-$framework-$runtime-setup".ToLowerInvariant()
        Invoke-Checked $InnoSetupCompiler @(
            "/DAppVersion=$Version", "/DTargetFramework=$framework", "/DRuntimeIdentifier=$runtime",
            "/DTargetArchitecture=$(Get-InstallerArchitecture $runtime)", "/DPublishDirectory=$publish",
            "/DOutputDirectory=$InstallerRoot", "/DOutputBaseFilename=$name",
            "/DIconFile=$(Join-Path $RepositoryRoot 'misc/icon/duburl.ico')", $InstallerScript
        )
        Assert-File (Join-Path $InstallerRoot "$name.exe") 'Windows installer'
    }
}

function Invoke-Pack {
    $packageFrameworks = Get-DeclaredList (Join-Path $RepositoryRoot 'Directory.Build.props') 'TargetFrameworks'
    $packageRuntimes = Get-DeclaredList $Project 'RuntimeIdentifiers'
    New-Item -ItemType Directory -Path $PackageRoot -Force | Out-Null
    Invoke-Checked dotnet @('restore', $Project, '--locked-mode')
    Invoke-Checked dotnet @(
        'pack', $Project, '--configuration', $Configuration, '--output', $PackageRoot,
        '--no-restore', "-p:PackageVersion=$Version", "-p:Version=$Version"
    )
    & (Join-Path $RepositoryRoot '.github/scripts/Test-CliToolPackages.ps1') `
        -PackageDirectory $PackageRoot `
        -TargetFrameworks ($packageFrameworks -join ';') `
        -RuntimeIdentifiers ($packageRuntimes -join ';')
}

if (-not (Test-Path -LiteralPath $Project -PathType Leaf)) { throw "CLI project '$Project' does not exist." }
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

switch ($Mode) {
    'Publish' { Invoke-Publish }
    'Archive' { Invoke-Archive }
    'Bundle' { Invoke-Bundle }
    'Pack' { Invoke-Pack }
    'Distribute' { Invoke-Publish; Invoke-Archive; Invoke-Bundle; Invoke-Pack }
}

if (Test-Path -LiteralPath $StagingRoot) { Remove-Item -LiteralPath $StagingRoot -Recurse -Force }
Write-Host "Distribution operation '$Mode' completed. Output: $OutputRoot" -ForegroundColor Green
