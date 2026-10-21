Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:checks = 0
function Check([bool]$Value, [string]$Message) {
    if (-not $Value) { throw $Message }
    $script:checks++
}
$helper = Join-Path $PSScriptRoot 'Prepare-Fresh-SQL-Packet.ps1'
Check (Test-Path -LiteralPath $helper) 'Fresh read-only packet helper is missing.'
. $helper
$paths = Get-FreshSqlScope
Check ($paths.Instance -ceq 'CosmixAttendanceHuma20261013A01') 'Exact proposed instance pinned.'
Check ($paths.Database -ceq 'Cosmix_Testing') 'Existing local fixture catalog contract preserved in a NEW instance.'
Check ($paths.PrivateRoot -ceq 'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01') 'Fresh private root pinned.'
Check ($paths.DataFile -ceq ($paths.PrivateRoot + '\sql\Cosmix_Testing.mdf')) 'Exact MDF pinned.'
Check ($paths.LogFile -ceq ($paths.PrivateRoot + '\sql\Cosmix_Testing_log.ldf')) 'Exact LDF pinned.'
Assert-FreshSqlAbsence @('MSSQLLocalDB', 'CosmixUat_d893653346') @()
$script:checks++
foreach ($collision in @('CosmixAttendanceHuma20261013A01','cosmixattendancehuma20261013a01')) {
    try { Assert-FreshSqlAbsence @($collision) @(); throw 'Expected instance-collision refusal.' }
    catch { Check ($_.Exception.Message -eq 'Proposed isolated instance already exists; stop without reuse.') 'Instance collision fails closed.' }
}
foreach ($path in @($paths.PrivateRoot,$paths.DataFile,$paths.LogFile,$paths.ArtifactRoot)) {
    try { Assert-FreshSqlAbsence @() @($path); throw 'Expected path-collision refusal.' }
    catch { Check ($_.Exception.Message -eq 'Proposed isolated path already exists; stop without overwrite.') 'Any proposed path collision fails closed.' }
}
$creation = Get-FreshDatabaseCreationSql
Check ($creation.Contains('CREATE DATABASE [Cosmix_Testing]')) 'Exact creation DDL included for review, not executed.'
Check ($creation.Contains($paths.DataFile) -and $creation.Contains($paths.LogFile)) 'No default shared physical-file location.'
Check ($creation.Contains('@ApprovedServer') -and $creation.Contains('ORIGINAL_LOGIN()')) 'Parameterized actual-server/operator check precedes creation.'
Check ($creation.IndexOf('THROW', [StringComparison]::Ordinal) -lt $creation.IndexOf('CREATE DATABASE', [StringComparison]::Ordinal)) 'Creation guards precede DDL.'
Check ($creation.Contains("DB_NAME() <> N'master'")) 'Only new-instance master management connection creates the catalog.'
Check (-not $creation.Contains('DROP DATABASE')) 'No destructive cleanup in creation operation.'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($helper, [ref]$tokens, [ref]$errors)
Check ($errors.Count -eq 0) 'Packet script parses.'
$commands = @($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst]}, $true))
foreach ($forbidden in @('sqlcmd','Invoke-Sqlcmd','Remove-Item','Set-Content','Out-File','Start-Process','Stop-Process','Set-Acl','Set-Item')) {
    Check (-not ($commands | Where-Object {$_.GetCommandName() -ieq $forbidden})) ('No write/SQL/process command: '+$forbidden)
}
$methodCalls = @($ast.FindAll({param($node) $node -is [Management.Automation.Language.InvokeMemberExpressionAst]}, $true))
Check (-not ($methodCalls | Where-Object {$_.Member.Extent.Text -match '^(Open|ExecuteNonQuery|ExecuteReader|WriteAllText|WriteAllBytes|CreateDirectory|Delete)$'})) 'No hidden .NET SQL/filesystem write operations.'
Write-Output ("PASS: {0} fresh packet checks; synthetic inventory and source review, no SQL or private writes." -f $script:checks)
