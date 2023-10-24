function Get-GitHub-Headers {
    [CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true, ValueFromPipeline = $true, Position=0)]
        [string] $secretToken
	)
	$headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
	$headers.Add('Accept','application/vnd.github+json')
	$headers.Add('X-GitHub-Api-Version','2022-11-28')
	$headers.Add('Authorization',"Bearer $secretToken")
	return $headers
}

function Send-GitHub-Get-Request {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true)]
        [string] $owner,
		[Parameter(Mandatory=$true)]
		[string] $repository,
		[Parameter(Mandatory=$true)]
		[string[]] $segments,
		[Parameter(Mandatory=$true)]
		[System.Collections.IDictionary] $headers
	)
	Invoke-WebRequest `
		-Uri "https://api.github.com/repos/$owner/$repository/$($segments -join '/')" `
		-Headers $headers
}

function Send-GitHub-Post-Request {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true, ValueFromPipeline = $true)]
        [object] $body,
		[Parameter(Mandatory=$true)]
		[string] $owner, 
		[Parameter(Mandatory=$true)]
		[string] $repository,
		[Parameter(Mandatory=$true)]
		[string[]] $segments,
		[Parameter(Mandatory=$true)]
		[System.Collections.IDictionary] $headers
	)
	$response = Invoke-WebRequest `
					-Method POST `
					-Uri "https://api.github.com/repos/$owner/$repository/$($segments -join '/')" `
					-Headers $headers `
					-Body $($(ConvertTo-Json $body))
}

function Get-Pull-Request-Title {
    [CmdletBinding()]
	Param(
        [Parameter(Mandatory=$true, ValueFromPipeline = $true, Position=0 )]
        [object] $context
	)
	$response = Send-GitHub-Get-Request `
					-Owner $context.Owner `
					-Repository $context.Repository `
					-Segments @('issues', $context.Id) `
					-Headers $($context.SecretToken | Get-GitHub-Headers)
	return ($response.Content | ConvertFrom-Json).title 
}

function Get-Pull-Request-Labels {
    [CmdletBinding()]
	Param(
        [Parameter(Mandatory=$true, ValueFromPipeline = $true, Position=0 )]
        [object] $context
	)
	$response = Send-GitHub-Get-Request `
					-Owner $context.Owner `
					-Repository $context.Repository `
					-Segments @('issues', $context.Id, 'labels') `
					-Headers $($context.SecretToken | Get-GitHub-Headers)
	return $response.Content | ConvertFrom-Json | Select-Object -ExpandProperty name 
}

function Post-Pull-Request-Labels {
    [CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true, ValueFromPipeline = $true, Position=0)]
		[object] $context,
		[Parameter(Mandatory=$true)]
        [string[]] $labels
	)
	$body = [PSCustomObject]@{labels=$labels}
	$response = Send-GitHub-Post-Request `
					-Owner $context.Owner `
					-Repository $context.Repository `
					-Segments @('issues', $context.Id, 'labels') `
					-Headers $($context.SecretToken | Get-GitHub-Headers) `
					-Body $body
}

function Get-Expected-Labels {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true, ValueFromPipeline = $true)]
        [string] $title,
		[System.Collections.IDictionary] $matches
	)
	$labels = @()
	$tokens = $title -Split ':'
	if ($tokens.Length -lt 2) {
		return @()
	}
	
	$conventional = $tokens[0].Trim()
	if ($conventional.IndexOf('(') -gt 0) {
		$conventional = $conventional.SubString(0, $conventional.IndexOf('(') - 1).Trim()
	}

	if ($conventional.EndsWith('!')) {
		if($matches.ContainsKey('!')) {
			$labels += $matches['!']
		}
	}

	$conventional = $conventional.TrimEnd('!').Trim()
	if(-not $matches.ContainsKey($conventional)) {
		return @()
	} else {
		$labels += $matches[$conventional]
	}
	return $labels
}

function Set-Pull-Request-Expected-Labels {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true, ValueFromPipeline = $true)]
		[object] $context
	)

	$matches = @{}
	$matches.Add('!', 'breaking-change')
	$matches.Add('build', 'build')
	$matches.Add('ci', 'build')
	$matches.Add('chore', 'dependency-update')
	$matches.Add('docs', 'docs')
	$matches.Add('feat', 'new-feature')
	$matches.Add('fix', 'bug')
	$matches.Add('perf', 'enhancement')
	$matches.Add('refactor', 'none')
	$matches.Add('revert', 'none')
	$matches.Add('style', 'none')
	$matches.Add('test', 'none')

	$title = $context | Get-Pull-Request-Title
	$existing = $context | Get-Pull-Request-Labels
	$expected = $title | Get-Expected-Labels -Matches $matches
	if ($expected.Length -eq 0) {
		throw "Pull Request title is not a valid conventional commit"
	}

	$expected = $expected | ? {$_ -ne 'none'}
	$missing = $expected | ? {-not($existing -contains $_)}
	if ($missing.Length -gt 0) {
		$context | Post-Pull-Request-Labels -Labels $missing
		Write-Host "Added labels: $($missing -Join ',')"
	} else {
		Write-Host "Labels already up-to-date."
	}
}