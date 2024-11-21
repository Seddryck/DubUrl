Param(
	[switch] $force=$false
	, [string] $config = "Release"
	, [string[]] $frameworks = @("net6.0", "net7.0")
)
. $PSScriptRoot\..\Run-TestSuite.ps1
. $PSScriptRoot\..\Docker-Container.ps1
. $PSScriptRoot\..\Windows-Service.ps1

if ($force) {
	Write-Host "Enforcing QA testing for Vertica"
}

$filesChanged = & git diff --name-only HEAD HEAD~1
if ($force -or ($filesChanged -like "*vertica*")) {
	Write-Host "Deploying Vertica testing environment"

	# Starting docker container
	$previouslyRunning, $running = Deploy-Container -FullName "vertica"
	if (!$previouslyRunning){
		$waitForAvailable = 45
		Write-host "`tWaiting $waitForAvailable seconds for the Vertica server to be available ..."
		Start-Sleep -s $waitForAvailable
		Write-host "`tVertica Server is expected to be available."
	}

    # Replace default database
    Write-host "`tStopping default database ..."
    & docker exec -it vertica /opt/vertica/bin/admintools -t stop_db -d VMart --force
    Write-host "`tDefault database stopped"
    Write-host "`tCreating DubUrl database ..."
    & docker exec -it vertica /opt/vertica/bin/admintools -t create_db -d DubUrl -s v_vmart_node0001
    Write-host "`tDubUrl database created"

	# Deploying database based on script
	Write-host "`tCreating table using remote client on the docker container"
	& docker exec -it vertica /opt/vertica/bin/vsql "--command=$(Get-Content .\deploy-vertica-database-01.sql)"
    & docker exec -it vertica /opt/vertica/bin/vsql "--command=$(Get-Content .\deploy-vertica-database-02.sql)"
    & docker exec -it vertica /opt/vertica/bin/vsql "--command=$(Get-Content .\deploy-vertica-database-03.sql)"
	Write-host "`tTable created"
	
	# Running QA tests
	Write-Host "Running QA tests related to Vertica"
	$suites = @("Vertica+AdoProvider")
	foreach ($odbcDriverInstalled in $odbcDriversInstalled) {
		$suites += "Vertica+ODBC+" + $odbcDriverInstalled + "Driver"
	}
	$testSuccessful = Run-TestSuite $suites -config $config -frameworks $frameworks

	# Stopping DB Service
	# if (!$previouslyRunning)
	# {
    #     Remove-Container $running
	# }

	# Raise failing tests
	exit $testSuccessful
} else {
	return -1
}
