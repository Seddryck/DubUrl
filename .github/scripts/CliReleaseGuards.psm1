function Assert-TestedCommit {
    param([string] $Actual, [string] $Expected)
    if ($Actual -ne $Expected) { throw "Release checkout '$Actual' does not match tested commit '$Expected'." }
}

function Assert-VersionUnchanged {
    param([string] $Built, [string] $Recalculated)
    if ($Built -ne $Recalculated) { throw "Version drift: artifacts use '$Built', but recalculation returned '$Recalculated'." }
}

function Get-TagReservationDecision {
    param([AllowNull()][string] $ExistingCommit, [string] $ExpectedCommit)
    if ([string]::IsNullOrWhiteSpace($ExistingCommit)) { return 'create' }
    if ($ExistingCommit -eq $ExpectedCommit) { return 'reuse' }
    throw "Release tag identifies '$ExistingCommit', not '$ExpectedCommit'."
}

Export-ModuleMember -Function Assert-TestedCommit, Assert-VersionUnchanged, Get-TagReservationDecision
