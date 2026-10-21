Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1')
$checks = 0
function Check([bool]$Value) { if (-not $Value) { throw 'SQL binding contract failed.' }; $script:checks++ }
$pipe = 'np:\\.\pipe\LOCALDB#842DD4FB\tsql\query'
$text = New-HumaSqlConnectionString $pipe
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($text)
Check ($builder.get_DataSource() -ceq $pipe)
Check ($builder.get_InitialCatalog() -ceq 'Cosmix_Testing')
Check ($builder.get_IntegratedSecurity() -and $builder.get_Encrypt() -and $builder.get_TrustServerCertificate())
Check (-not $builder.get_Pooling() -and $builder.get_ConnectTimeout() -eq 15)
Check ($builder.get_UserID() -ceq '' -and $builder.get_Password() -ceq '')
foreach ($source in @('', '(localdb)\MSSQLLocalDB', 'tcp:remote', ($pipe + ';Database=master'), 'np:\\.\pipe\LOCALDB#52E8DC1B\tsql\query')) {
    $rejected = $false
    try { New-HumaSqlConnectionString $source | Out-Null }
    catch { $rejected = $_.Exception.Message -ceq 'Only the reviewed existing test pipe is allowed.' }
    Check $rejected
}
Write-Output "PASS: $checks existing SQL connection binding checks; no SQL connection or writes."
