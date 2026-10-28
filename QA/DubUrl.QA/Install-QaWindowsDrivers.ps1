function Get-VerifiedQaDownload {
    param([string]$url, [string]$path, [string]$sha256)
    Invoke-WebRequest $url -OutFile $path
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $sha256) { throw 'QA download checksum mismatch.' }
}

function Install-QaWindowsDriver {
    param([ValidateSet('SqlServer','Oracle')][string]$provider)
    if (!$IsWindows -or $env:GITHUB_ACTIONS -ne 'true') {
        throw 'Automatic driver installation is restricted to disposable GitHub Windows runners. Install prerequisites explicitly on local machines.'
    }
    if ($provider -eq 'SqlServer') {
        $msi = Join-Path $env:RUNNER_TEMP 'msoledbsql-19.4.3.msi'
        Get-VerifiedQaDownload 'https://download.microsoft.com/download/bbb0d870-7a04-44d9-af17-af28575a0ed8/amd64/1033/msoledbsql.msi' $msi 'C1D5BD79C316588FB08C852ACF76628AB662426E763B68AEF4CFA25FA1DD7BEB'
        if ((Get-AuthenticodeSignature $msi).Status -ne 'Valid') { throw 'Microsoft OLE DB installer signature is invalid.' }
        $process = Start-Process msiexec.exe -ArgumentList @('/i', $msi, '/qn', '/norestart', 'IACCEPTMSOLEDBSQLLICENSETERMS=YES') -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -notin @(0,3010)) { throw "OLE DB installation failed: $($process.ExitCode)" }
    } else {
        $zip = Join-Path $env:RUNNER_TEMP 'ODAC23.26.3Xcopy_x64.zip'
        Get-VerifiedQaDownload 'https://download.oracle.com/otn_software/odac/ODAC23.26.3Xcopy_x64.zip' $zip '500E1B3339B4C8D47BEDE1D82B83C376CA0B2003531E6237130FEC92AF36AFCD'
        $source = Join-Path $env:RUNNER_TEMP 'odac-source'
        Expand-Archive -LiteralPath $zip -DestinationPath $source
        $odacHome = 'C:\DubUrlOdac'
        Push-Location $source
        try {
            & ./install.bat oledb $odacHome DubUrlQaOdac true true
            if ($LASTEXITCODE -ne 0) { throw 'Oracle OLE DB installation failed.' }
        } finally { Pop-Location }
        $env:PATH = "$odacHome;$odacHome\bin;$env:PATH"
    }
}
