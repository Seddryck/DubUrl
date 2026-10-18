#requires -PSEdition Core

$destinationPath = ".\Docs\_data"
$destinationFile = "natives.json"


########### Check if it's useful to make changes to doc or readme #############
Set-Location ./

$hash = 0
If(Test-Path -LiteralPath $destinationPath\$destinationFile -PathType leaf) {
    $hash = Get-FileHash $destinationPath\$destinationFile
    Write-Debug "Previous hash for $destinationPath\$destinationFile is $($hash.Hash)"
}


########### Generate JSON file #############

& "$PSScriptRoot/build/Import-DocumentationAssemblies.ps1"

$elapsed = Measure-Command -Expression {
    $introspector = New-Object  DubUrl.Mapping.NativeMapperIntrospector
    $mappers = $introspector.Locate() | Sort-Object ListingPriority | Select-Object -Property @{label='Class'; expression={$_.MapperType.Name}}, @{label='Database'; expression={$_.DatabaseName}}, Aliases, ProviderInvariantName, Slug, MainColor, SecondaryColor
    Write-Host  "`t$($mappers.Count) mappers identified"
    $mappers | ForEach-Object {Write-Host "`t`t$($_.Class)"}
    $mappers | ConvertTo-Json | Out-File "$destinationPath\$destinationFile"
}
Write-Host  "File created at $destinationPath\$destinationFile in $($elapsed.TotalSeconds) seconds"


########### Check if it's useful to report a change #############

If ($hash.Hash -eq (Get-FileHash $destinationPath\$destinationFile).Hash) {
    Write-Host "No change detected in the list of providers."
    Exit 0
} else {
    Write-Host "Changes detected in the list of providers."
    Exit 1
}
