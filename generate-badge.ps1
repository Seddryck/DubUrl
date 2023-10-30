$destinationFile = ".\README.md"
$rootUrl = "https://seddryck.github.io/DubUrl/docs"

########### Create a markdown table ##########
Write-Host "Creating new version of $destinationFile for badges ..."
$elapsed = Measure-Command -Expression {
    $badges = @()
    $badges += [PSCustomObject]@{
        Label = "Mappers for ADO.Net Provider"
        Count = (Get-Content -Path ".\Docs\_data\natives.json" | ConvertFrom-Json).Count
        Status = "implemented"
        Url = "/native-ado-net-providers"
    }
    $badges += [PSCustomObject]@{
        Label = "Mappers for ODBC drivers"
        Count = (Get-Content -Path ".\Docs\_data\odbc.json" | ConvertFrom-Json).Count
        Status = "implemented"
        Url = "/odbc-driver-locators"
    }
    $badges += [PSCustomObject]@{
        Label = "Mappers for OLE DB providers"
        Count = (Get-Content -Path ".\Docs\_data\oledb.json" | ConvertFrom-Json).Count
        Status = "implemented"
        Url = "/oledb-provider-locators"
    }
    $badges += [PSCustomObject]@{
        Label = "Mappers for ADOMD.NET providers"
        Count = (Get-Content -Path ".\Docs\_data\adomd.json" | ConvertFrom-Json).Count
        Status = "implemented"
        Url = "/adomd-providers"
    }
    $textBadges = ""
    foreach($badge in $badges) {
        $textBadges += "[![$($badge.Label) $($badge.Status) badge](https://img.shields.io/badge/$($badge.Label.Replace(" ", "%20"))-$($badge.Count)%20$($badge.Status)-green)]($rootUrl$($badge.Url))`r`n"
    }
    if ($textBadges.Length -ge 2) {
        $textBadges.Substring(0,$textBadges.Length-2)
    }

    Write-Host  "`tCreated $($badges.Length) badges"

    ########### Update the sub-part of the readme ##########

    Write-Host  "`tReplacing content in $destinationFile ..."
    $text = ""
    [bool] $skip = $false
    ForEach ($line in Get-Content -Path $destinationFile) {
        $i+=1
        if($line -eq "<!-- END BADGES -->") {
            $skip = $false
            Write-Host  "`t`tPrevious content skipped between lines $j and $i"
        }

        if (-not $skip) {
            $text += $line + "`r`n"
        }

        if ($line -eq "<!-- START BADGES -->"){
            $skip = $true
            $text += $textBadges
            Write-Host  "`t`tNew content inserted after line $i"
            $j = $i+1
        } 
    }
    $text | Out-File -Path $destinationFile
    Write-Host  "`tNew content written"
}
Write-Host "New version of $destinationFile created in $($elapsed.TotalSeconds) seconds"