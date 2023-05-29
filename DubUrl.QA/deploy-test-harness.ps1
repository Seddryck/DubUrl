[CmdletBinding(DefaultParametersetName="default")]
Param(
    [switch] $force=$false,
    [Parameter(Position = 0, ParameterSetName='single')]
    [string] $suite=$null,
    [Parameter(ParameterSetName='multiple')]
    [string[]] $suites = @()	
)

Function Deploy-TestSuite {
	[CmdletBinding()]
	Param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
		[switch] $force,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $name
	)

    Process {
        $failure = $false
        $startWait = Get-Date
        Push-Location $PSScriptRoot\$name
        try {
            Write-Host "Running test-suite for $name"
            & .\deploy-$name-test-env.ps1 -force:$force | Out-Null
            $result = $lastexitcode
            $elasped = $(New-TimeSpan -Start $startWait)
            if ($result -eq 0) {
                Write-Host "Test-suite for $name successfully run in $($elasped.ToString("ss")) seconds." -ForegroundColor green
            } else {
                Write-Host "Test-suite for $name run in $($elasped.ToString("ss")) seconds but returned some failures." -ForegroundColor red
            }
            return [PSCustomObject]@{
                Suite = $name
                HarnessFailure = $false
                TestFailure = $result
                Elapsed = $elasped
            }
        }
        catch {
            Write-Warning $_
            return [PSCustomObject]@{
                Suite = $name
                Failure = $true
                Result = if ($null -eq $result) {0}
                Elapsed = if ($null -eq $result) {New-TimeSpan -Start $startWait}
            }
        }
        finally {
            Pop-Location
        }
    }
}

if ($suites.Length -eq 0 -and "" -ne $suite -and $null -ne $suite) {
    $suites += $suite
}

if ($suites.Length -eq 0) {
    Write-Host "Executing test harness for all the test suites ..."
    $suites = Get-ChildItem -Path ".\Duburl.QA" -Filter "deploy-*-test-env.ps1" -Recurse `
        | Select-Object "Name" | ForEach{$_.Name.Substring(7,$_.Name.Substring(7).IndexOf("-"))}
}

if ($force) {
    Write-Host "Enforcing the run of test harnesses ..."
}

$results = @()
foreach ($s in $suites) {
    $results += Deploy-TestSuite -Name $s -Force:$force
}

$results | Format-Table | Out-String | Write-Host
