Function Start-Windows-Service {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $serviceName
	)

	Begin {
        $previouslyRunning = $false
    }

	Process{
        $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
		if ($null -eq $service) {
			throw "`Service '$serviceName' not found!"
		} else {
			if($service.Status -ne 'Running') {
				Write-host "`tStarting '$serviceName' service ..."
				Start-Service $serviceName -ErrorAction SilentlyContinue
				Write-host "`tService started"

				if ((Get-Service -Name $serviceName).Status -ne 'Running') {
					throw "`Service '$serviceName' not able to start!"
				}
			} else {
				Write-host "`tService '$serviceName' already started"
				$previouslyRunning = $true
			}
		}
    }

    End{
        return $previouslyRunning
    }
}

Function Stop-Windows-Service {
	[CmdletBinding()]
	Param(
		[Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string] $serviceName
	)

	Process {
        $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
		if ($null -eq $service) {
			throw "`Service '$serviceName' not found!"
		} else {
			if($service.Status -ne 'Stopped') {
				Write-host "`tStopping '$serviceName' service ..."
				Stop-Service $serviceName 
				Write-host "`tService stopped"
			} else {
				Write-host "`tService '$serviceName' already stopped"
			}
		}
    }
}




