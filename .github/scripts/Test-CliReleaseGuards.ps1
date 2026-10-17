$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'CliReleaseGuards.psm1') -Force

function Assert-Throws([scriptblock] $Action, [string] $Name) {
    try { & $Action; throw "$Name did not reject invalid state." }
    catch { if ($_.Exception.Message -eq "$Name did not reject invalid state.") { throw } }
}

Assert-TestedCommit -Actual 'abc' -Expected 'abc'
Assert-VersionUnchanged -Built '1.2.3' -Recalculated '1.2.3'
if ((Get-TagReservationDecision -ExistingCommit $null -ExpectedCommit 'abc') -ne 'create') { throw 'Missing tag must be created.' }
if ((Get-TagReservationDecision -ExistingCommit 'abc' -ExpectedCommit 'abc') -ne 'reuse') { throw 'Matching tag must be reused.' }
Assert-Throws { Assert-TestedCommit -Actual 'queued' -Expected 'tested' } 'Queued-release guard'
Assert-Throws { Assert-VersionUnchanged -Built '1.2.3' -Recalculated '1.2.4' } 'Version reservation guard'
Assert-Throws { Get-TagReservationDecision -ExistingCommit 'other' -ExpectedCommit 'tested' } 'Tag collision guard'

Write-Host 'Validated tested-commit, version-drift, existing-tag, and tag-collision release guards.'
