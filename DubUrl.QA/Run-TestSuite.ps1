Function Run-TestSuite {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string[]] $categories
		, [string] $config = "Release"
		, [string[]] $frameworks = @("net7.0")
	)

	Begin {
		$testSuccessful = 0
		if ($env:APPVEYOR -eq "True") {
			$adapters = "--test-adapter-path:.", "--logger:Appveyor"
		}
	}

	Process {
        $buildMsg = & dotnet build "..\..\DubUrl.QA" -c $config --nologo
		if ($lastexitcode -ne 0) {
			Write-Warning "Cannot build the Test assembly! `r`n$($buildMsg -join "`r`n")"
		} else {
			foreach ($category in $categories) {
				foreach ($framework in $frameworks) {
					Write-Host "`tRunning test-suite for $category"
					$args  = @("test", "..\..\DubUrl.QA")
					$args += @("--filter", "`"TestCategory=$($category.Split("+") -join "`"`"&`"`"TestCategory=")`"")
					$args += @("-c", $config)
					$args += @("-f", $framework)
					$args += @("--no-build", "--nologo")
					$args += $adapters
					& dotnet $args | Out-Host
					$testSuccessful += $lastexitcode
				}
			}
		}
    }

    End {
        return $testSuccessful
    }
}


