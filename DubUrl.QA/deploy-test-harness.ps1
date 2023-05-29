[CmdletBinding(DefaultParametersetName="default")]
Param(
    [switch] $force=$false,
    [Parameter(Position = 0, ParameterSetName='single')]
    [string] $suite=$null,
    [Parameter(ParameterSetName='multiple')]
    [string[]] $suites = @(),
    [Parameter(ParameterSetName='exclude')]
    [string[]] $exclude = @()
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
            $displayElapsed =  if ($elasped.Minutes -gt 0) {"$($elasped.ToString("mm")) minute "}
            $displayElapsed += "$($elasped.ToString("ss")) seconds."
            if ($result -eq 0) {
                Write-Host "Test-suite for $name successfully run in $displayElapsed." -ForegroundColor green
            } else {
                Write-Host "Test-suite for $name run in $($elasped.ToString("ss")) seconds but returned some failures." -ForegroundColor red
            }
            return [PSCustomObject]@{
                Suite = $name
                HarnessFailure = $false
                TestSuiteFailure = $result
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

    if ($exclude.Length -gt 0) {
        $suites | Where-Object { $exclude -notcontains $_ }
    }
}

if ($force) {
    Write-Host "Enforcing the run of test harnesses ..."
}

$results = @()
foreach ($s in $suites) {
    $results += Deploy-TestSuite -Name $s -Force:$force
}

$results | Format-Table | Out-String | Write-Host

$failureCount = 0
$results | ForEach {$failureCount += $_.TestSuiteFailure}
$failureCount = if ($failureCount -gt 0) {1} else {0}
return $failureCount
