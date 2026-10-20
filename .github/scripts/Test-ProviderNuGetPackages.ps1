[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $NuGetDirectory,
    [Parameter(Mandatory)][string] $Version,
    [string[]] $Frameworks = @('net8.0', 'net9.0', 'net10.0'),
    [string] $SingleFileRuntime,
    [string] $OutputDirectory = '.artifacts/provider-nuget-validation'
)

$ErrorActionPreference = 'Stop'
$feed = (Resolve-Path -LiteralPath $NuGetDirectory).Path
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $root -Force | Out-Null
# Do not inherit the repository's project defaults in these consumer applications.
Set-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Value '<Project />'
Set-Content -LiteralPath (Join-Path $root 'Directory.Build.targets') -Value '<Project />'
$configuration = Join-Path $root 'NuGet.Config'
Set-Content -LiteralPath $configuration -Value @"
<configuration><config><add key="globalPackagesFolder" value="$([Security.SecurityElement]::Escape((Join-Path $root 'packages')))" /></config><packageSources><clear />
  <add key="local" value="$([Security.SecurityElement]::Escape($feed))" />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources></configuration>
"@

$archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $feed "DubUrl.$Version.nupkg"))
try {
    if (@($archive.Entries | Where-Object { $_.FullName -match '\.(dll|pdb)$' }).Count) {
        throw 'The dependency-only DubUrl package must not contain DLLs or symbols.'
    }
    $reader = [IO.StreamReader]::new($archive.GetEntry('DubUrl.nuspec').Open())
    try { [xml] $specification = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $dependencies = @($specification.SelectNodes("//*[local-name()='dependency']") | ForEach-Object { $_.id })
    if ('DubUrl.Core' -notin $dependencies -or 'DubUrl.Provider.PostgreSql' -notin $dependencies) {
        throw 'The aggregate must retain its Core and provider dependencies.'
    }
} finally { $archive.Dispose() }

$cases = @{
    core = @{
        Packages = @('DubUrl.Core'); Code = @'
try { new DubUrl.ConnectionUrl("pg://localhost/demo").Parse(); throw new Exception("Unexpected PostgreSQL provider."); }
catch (DubUrl.Rewriting.SchemeNotFoundException) { }
'@
    }
    selective = @{
        Packages = @('DubUrl.Core', 'DubUrl.Provider.PostgreSql', 'Npgsql'); Code = @'
if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name is "Npgsql" or "DubUrl.Provider.PostgreSql"))
    throw new Exception("Provider and driver must not already be loaded.");
var url = new DubUrl.ConnectionUrl("pg://localhost/demo");
using var connection = url.Connect();
if (connection.GetType().FullName != "Npgsql.NpgsqlConnection" || !url.Parse().Contains("demo"))
    throw new Exception("Automatic NuGet discovery failed.");
if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name is "DubUrl" or "DubUrl.Application"))
    throw new Exception("Selective discovery must not require aggregate or Application.");
'@
    }
    aggregate = @{
        Packages = @('DubUrl', 'Npgsql'); Code = @'
var url = new DubUrl.ConnectionUrl("pg://localhost/demo");
using var connection = url.Connect();
if (connection.GetType().FullName != "Npgsql.NpgsqlConnection") throw new Exception("Aggregate dependencies were not discovered.");
if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "DubUrl")) throw new Exception("Aggregate DLL was loaded.");
if (DubUrl.Mapping.SchemeRegistryBuilder.GetDefaultDialectRegistry().Get("mssql") is null) throw new Exception("Aggregate lost its dialects.");
'@
    }
    application = @{
        Packages = @('DubUrl.Application'); Code = @'
using var http = new System.Net.Http.HttpClient();
var service = new DubUrl.Application.ConnectorService(new DubUrl.Core.ProviderPacks.ProviderPackStore(), http);
if (!service.List().Any(entry => entry.Id == "postgresql-npgsql" && entry.Listed && !entry.Installed))
    throw new Exception("Bundled catalog is missing.");
if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name!.StartsWith("DubUrl.Provider.")))
    throw new Exception("Application must not bring provider implementations into the process.");
'@
    }
}

$previousStore = $env:DUBURL_PROVIDER_STORE
try {
    foreach ($name in ($cases.Keys | Sort-Object)) {
        $case = $cases[$name]
        $directory = Join-Path $root $name
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $env:DUBURL_PROVIDER_STORE = Join-Path $directory ('absent-store-' + [Guid]::NewGuid().ToString('N'))
        $references = foreach ($id in $case.Packages) {
            $packageVersion = if ($id -eq 'Npgsql') { '10.0.3' } else { $Version }
            '<PackageReference Include="{0}" Version="{1}" />' -f $id, $packageVersion
        }
        $project = Join-Path $directory 'Consumer.csproj'
        Set-Content -LiteralPath $project -Value @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFrameworks>$($Frameworks -join ';')</TargetFrameworks><ImplicitUsings>enable</ImplicitUsings></PropertyGroup>
  <ItemGroup>$($references -join "`n")</ItemGroup>
</Project>
"@
        Set-Content -LiteralPath (Join-Path $directory 'Program.cs') -Value $case.Code
        & dotnet restore $project --configfile $configuration --packages (Join-Path $root 'packages') --nologo
        if ($LASTEXITCODE) { throw "Restore failed: $name" }
        foreach ($framework in $Frameworks) {
            & dotnet run --project $project --framework $framework --no-restore --no-launch-profile
            if ($LASTEXITCODE) { throw "Consumer failed: $name / $framework" }
            if (Test-Path -LiteralPath $env:DUBURL_PROVIDER_STORE) { throw 'Discovery created the provider store.' }
            Write-Host "Passed NuGet consumer: $name / $framework"
        }
        if ($SingleFileRuntime -and $name -in @('selective', 'aggregate')) {
            $publish = Join-Path $directory 'single-file'
            & dotnet publish $project --framework $Frameworks[-1] --runtime $SingleFileRuntime --self-contained false -p:PublishSingleFile=true --output $publish --nologo
            if ($LASTEXITCODE) { throw "Single-file publish failed: $name" }
            if (-not (Test-Path -LiteralPath (Join-Path $publish 'Consumer.deps.json'))) { throw 'Single-file dependency metadata is missing.' }
            $executable = if ($SingleFileRuntime.StartsWith('win-')) { 'Consumer.exe' } else { 'Consumer' }
            & (Join-Path $publish $executable)
            if ($LASTEXITCODE) { throw "Single-file consumer failed: $name" }
            Write-Host "Passed single-file NuGet consumer: $name / $SingleFileRuntime"
        }
    }
} finally { $env:DUBURL_PROVIDER_STORE = $previousStore }
