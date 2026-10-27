$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Run-ProviderQaSuite.ps1')
$path=Join-Path ([IO.Path]::GetTempPath()) ('qa-results-'+[Guid]::NewGuid().ToString('N')+'.trx')
try {
    foreach($case in @(
        @{Total=2;Executed=2;Passed=2;Failed=0;Strict=$true;Expected=$true},
        @{Total=2;Executed=1;Passed=1;Failed=0;Strict=$true;Expected=$false},
        @{Total=2;Executed=1;Passed=1;Failed=0;Strict=$false;Expected=$true},
        @{Total=0;Executed=0;Passed=0;Failed=0;Strict=$false;Expected=$false},
        @{Total=2;Executed=2;Passed=1;Failed=1;Strict=$false;Expected=$false}
    )) {
        "<TestRun><ResultSummary><Counters total='$($case.Total)' executed='$($case.Executed)' passed='$($case.Passed)' failed='$($case.Failed)' /></ResultSummary></TestRun>" | Set-Content -LiteralPath $path
        $accepted=$true
        try { Assert-ProviderQaResults -path $path -requireAll:$case.Strict } catch { $accepted=$false }
        if($accepted -ne $case.Expected) { throw "Result validation mismatch: $($case | ConvertTo-Json -Compress)" }
    }
    Write-Host 'Five QA-result validation cases passed.'
} finally { Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue }
