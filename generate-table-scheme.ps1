$destinationFile = ".\README.md"

########## Substitution function #############

function Substitute-Table {
    [CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true, ValueFromPipeline = $true)]
        [string] $original, 
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $identifier, 
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $table
	)
    
    $text = ""
    [bool] $skip = $false
    foreach ($line in $original.Split("`r`n")) {
        $i+=1
        if($line -eq "<!-- END $identifier TABLE -->") {
            $skip = $false
            Write-Host  "`t`tPrevious content skipped between lines $j and $i"
        }

        if (-not $skip) {
            $text += $line + "`r`n"
        }

        if ($line -eq "<!-- START $identifier TABLE -->"){
            $skip = $true
            $text += $table 
            Write-Host  "`t`tNew content inserted after line $i"
            $j = $i+1
        } 
    }
    return $text
} 

########### Create a markdown table ##########

function Write-Table {
    [CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true, ValueFromPipeline = $true)]
        [ValidateNotNullOrEmpty()]
        [object[]]$mappers, 
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string[]] $keys
	)
    
    Write-Host  "`t$($mappers.Count) mappers to transform in markdown table"
    $items = @()
    $columns = @{}

    foreach($mapper in $mappers) {
        $dbName = "![$($mapper.Database)]"
        $dbName += "(https://img.shields.io/badge/"
        $dbName += [URI]::EscapeUriString($mapper.Database)
        $dbName += "-" + $mapper.MainColor -replace '#', ''
        $dbName += "?logo=$($mapper.Slug)"
        $dbName += "&logoColor=$($mapper.SecondaryColor -replace '#', '')"
        $dbName += "&style=flat-square"
        $dbName += ")"
        
        foreach ($prop in $mapper.PSObject.Properties)
        {
            if (-not($keys -Contains $prop)) {
                $mapper.PSObject.Properties.Remove($prop)
            }
        }
        $item = $mapper
        $item.Database = $dbName
        $item.Aliases = $mapper.Aliases -join ', '

        $items += $item
        $item.PSObject.Properties | %{
            if((-not $columns.ContainsKey($_.Name) -or $columns[$_.Name] -lt $_.Value.ToString().Length) -and ($null -ne $_.Value)) {
                $columns[$_.Name] = $_.Value.ToString().Length
            }
        }
        Write-Host "`t`t$($mapper.Class)"
    }

    foreach($key in $($columns.Keys)) {
        $columns[$key] = [Math]::Max($columns[$key], $key.Length)
    }   

    $table=""
    $header = @()
    foreach($key in $keys) {
        $header += ('{0,-' + $columns[$key] + '}') -f ($key -creplace '([A-Z])', ' $1').Trim()
    }
    $table += '|' + ($header -join ' | ') + "|`r`n"

    $separator = @()
    foreach($key in $keys) {
        $separator += '-' * $columns[$key]
    }
    $table += '|' + ($separator -join ' | ') + "|`r`n"

    foreach($item in $items) {
        $values = @()
        foreach($key in $keys) {
            $values += ('{0,-' + $columns[$key] + '}') -f ($item.($key)).Replace("|", "\|")
        }
        $table += '|' + ($values -join ' | ') + "|`r`n"
    }
    Write-Host  "`tCreated markdown table with $($table.Split("`r`n").GetUpperBound(0)) lines and a width of $($table.Split("`r`n")[0].Length) chars"
    return $table
}

########### MAIN ##########

Write-Host "Creating new version of $destinationFile for markdown tables ..."
$elapsed = Measure-Command -Expression {
    $text = (Get-Content -Path $destinationFile) -join "`r`n"

    $table = Get-Content -Path ".\Docs\_data\natives.json" `
        | ConvertFrom-Json -NoEnumerate `
        | Write-Table -Keys @("Database", "Aliases", "ProviderInvariantName")
    $text = $text | Substitute-Table -Identifier "ADONET" -Table $table

    $table = Get-Content -Path ".\Docs\_data\adomd.json" `
        | ConvertFrom-Json -NoEnumerate `
        | Write-Table -Keys @("Database", "Aliases", "ProviderInvariantName")
    $text = $text | Substitute-Table -Identifier "ADOMD" -Table $table
    
    $table = Get-Content -Path ".\Docs\_data\odbc.json" `
        | ConvertFrom-Json -NoEnumerate `
        | Write-Table -Keys @("Database", "Aliases", "NamePattern")
    $text = $text | Substitute-Table -Identifier "ODBC" -Table $table
    
    $table = Get-Content -Path ".\Docs\_data\oledb.json" `
        | ConvertFrom-Json -NoEnumerate `
        | Write-Table -Keys @("Database", "Aliases", "NamePattern")
    $text = $text | Substitute-Table -Identifier "OLEDB" -Table $table
    
    $text | Out-File -Path $destinationFile
}
Write-Host "New version of $destinationFile created in $($elapsed.TotalSeconds) seconds."