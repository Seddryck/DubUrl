Function Run-TestSuite {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string[]] $categories
		, [string] $config = "Release"
	)

	Begin {
		$testSuccessful = 0
		if ($env:APPVEYOR -eq "True") {
			$adapters = "--test-adapter-path:.", "--logger:Appveyor"
		}
	}

	Process {
        & dotnet build "..\..\DubUrl.QA" -c $config --nologo | Out-Null
		foreach ($category in $categories) {
			Write-Host "`tRunning test-suite for $category"
			$args  = @("test", "..\..\DubUrl.QA")
			$args += "--filter"
			$args += "`"TestCategory=$($category.Split("+") -join "`"`"&`"`"TestCategory=")`""
			$args += @("-c", $config, "--no-build", "--nologo")
			$args += $adapters
			& dotnet $args | Out-Host
			$testSuccessful += ($lastexitcode -gt 0)
		}
    }

    End {
        return $testSuccessful
    }
}


