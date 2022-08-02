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

$assemblyPath = "DubUrl.Core\bin"
Set-Location $assemblyPath
$dllfile = "net6.0\DubUrl.dll"

If ((-not (Test-Path -Path "Release\$dllfile")) -or ("Release\$dllfile".CreationTime -lt "Debug\$dllfile".CreationTime)) {
    $directory = "Debug"    
} else {
    $directory = "Release"   
}
Add-Type -Path "$directory\$dllfile"

Set-Location "..\..\"

Write-Host "Generating JSON for ADO.Net data providers based on $assemblyPath\$directory\$dllfile"
$elapsed = Measure-Command -Expression {
    $locator = New-Object  DubUrl.Mapping.MapperLocator
    $mappers = $locator.Locate() | Sort-Object ListingPriority | Select-Object -Property @{label='Class'; expression={$_.MapperType.Name}}, @{label='Database'; expression={$_.DatabaseName}}, Aliases, ProviderInvariantName
    Write-Host  "`t$($mappers.Count) mappers identified"
    $mappers | ForEach-Object {Write-Host "`t`t$($_.Class)"}
    $mappers | ConvertTo-Json | Out-File "$destinationPath\$destinationFile"
}
Write-Host  "File created at $destinationPath in $($elapsed.TotalSeconds) seconds"


########### Check if it's useful to report a change #############

If ($hash.Hash -eq (Get-FileHash $destinationPath\$destinationFile).Hash) {
    Write-Host "No change detected in the list of providers."
    Exit 0
} else {
    Write-Host "Changes detected in the list of providers."
    Exit 1
}
