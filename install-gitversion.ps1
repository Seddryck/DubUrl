$ErrorActionPreference = "Stop"

$minimumVersion = [Version]"6.0.0"
$dotnetToolsPath = Join-Path $env:USERPROFILE ".dotnet\tools"

function Get-GitVersionVersion {
    param(
        [Parameter(Mandatory)]
        [string] $Command
    )

    try {
        $versionOutput = (& $Command /version 2>&1 | Out-String).Trim()

        if ($LASTEXITCODE -ne 0) {
            return $null
        }

        $versionMatch = [regex]::Match($versionOutput, "(?<!\d)(\d+\.\d+\.\d+)(?!\d)")

        if (-not $versionMatch.Success) {
            return $null
        }

        return [Version]$versionMatch.Groups[1].Value
    }
    catch {
        return $null
    }
}

$gitVersion = Get-Command gitversion -ErrorAction SilentlyContinue
$installedVersion = if ($gitVersion) {
    Get-GitVersionVersion -Command $gitVersion.Source
}

if ($installedVersion -and $installedVersion -ge $minimumVersion) {
    Write-Host "GitVersion $installedVersion is already installed."
    Write-Host "Path: $($gitVersion.Source)"
    exit 0
}

if ($installedVersion) {
    Write-Host "GitVersion $installedVersion is older than the required version $minimumVersion. Reinstalling..."
}
elseif ($gitVersion) {
    Write-Host "The installed GitVersion version could not be determined. Reinstalling..."
}
else {
    Write-Host "GitVersion is not installed. Installing..."
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue

if (-not $dotnet) {
    throw ".NET SDK is not installed or not available in PATH. Install the .NET SDK first."
}

$globalTools = dotnet tool list --global

if ($LASTEXITCODE -ne 0) {
    throw "Failed to list globally installed .NET tools."
}

if ($globalTools | Select-String -Pattern "^gitversion\.tool\s" -Quiet) {
    dotnet tool update --global GitVersion.Tool
}
else {
    dotnet tool install --global GitVersion.Tool
}

if ($LASTEXITCODE -ne 0) {
    throw "Failed to install GitVersion.Tool."
}

$gitVersionExe = Join-Path $dotnetToolsPath "dotnet-gitversion.exe"

if (-not (Test-Path $gitVersionExe)) {
    throw "dotnet-gitversion.exe was not found after installing GitVersion.Tool."
}

$installedVersion = Get-GitVersionVersion -Command $gitVersionExe

if (-not $installedVersion -or $installedVersion -lt $minimumVersion) {
    throw "GitVersion.Tool $installedVersion was installed, but version $minimumVersion or later is required."
}

$shimPath = Join-Path $dotnetToolsPath "gitversion.cmd"

@"
@echo off
"%USERPROFILE%\.dotnet\tools\dotnet-gitversion.exe" %*
"@ | Set-Content -Path $shimPath -Encoding ASCII

$env:Path = "$dotnetToolsPath;$env:Path"

if (Get-Command Set-AppveyorBuildVariable -ErrorAction SilentlyContinue) {
    Set-AppveyorBuildVariable -Name "PATH" -Value $env:Path
}

Write-Host "GitVersion $installedVersion installed successfully."
Write-Host "Path: $gitVersionExe"
