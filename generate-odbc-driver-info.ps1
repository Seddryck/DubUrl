#requires -PSEdition Core

$destinationPath = ".\Docs\_data"
$destinationFile = "odbc.json"


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
$dllfile = "net8.0\DubUrl.dll"

If ((-not (Test-Path -Path "Release\$dllfile")) -or ("Release\$dllfile".CreationTime -lt "Debug\$dllfile".CreationTime)) {
    $directory = "Debug"    
} else {
    $directory = "Release"   
}
Add-Type -Path "$directory\$dllfile"

Set-Location "..\..\"

Write-Host "Generating JSON for Odbc driver locators based on $assemblyPath\$directory\$dllfile"
$elapsed = Measure-Command -Expression {
    $locator = New-Object  DubUrl.Locating.OdbcDriver.DriverLocatorIntrospector
    $driverLocators = $locator.Locate() | Sort-Object ListingPriority | Select-Object -Property @{label='Class'; expression={$_.DriverLocatorType.Name}}, @{label='Database'; expression={$_.DatabaseName}}, Aliases, NamePattern, Options, Slug, MainColor, SecondaryColor
    Write-Host  "`t$($driverLocators.Count) mappers identified"
    $driverLocators | ForEach-Object {Write-Host "`t`t$($_.Class)"}
    $driverLocators | ConvertTo-Json | Out-File "$destinationPath\$destinationFile"
}
Write-Host  "File created at $destinationPath\$destinationFile in $($elapsed.TotalSeconds) seconds"


########### Check if it's useful to report a change #############

If ($hash.Hash -eq (Get-FileHash $destinationPath\$destinationFile).Hash) {
    Write-Host "No change detected in the list of Odbc driver locators."
    Exit 0
} else {
    Write-Host "Changes detected in the list of Odbc driver locators."
    Exit 1
}
