$ErrorActionPreference = 'Stop'
$fixtureEngine = $null
$fixtureDatabase = $null
try {
    $fixtureInput = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $fixtureEngine = New-Object -ComObject DAO.DBEngine.120
    # dbVersion120 (128) + dbEncrypt (2); never print the input or native errors.
    $fixtureDatabase = $fixtureEngine.CreateDatabase($fixtureInput.path, (';LANGID=0x0409;CP=1252;COUNTRY=0;pwd=' + $fixtureInput.password), 130)
    $fixtureDatabase.Execute('CREATE TABLE [FilePasswordProbe] ([Value] INTEGER)')
    $fixtureDatabase.Execute('INSERT INTO [FilePasswordProbe] ([Value]) VALUES (1724)')
    $fixtureDatabase.Close()
} catch {
    exit 1
} finally {
    if ($null -ne $fixtureDatabase) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($fixtureDatabase) }
    if ($null -ne $fixtureEngine) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($fixtureEngine) }
}
