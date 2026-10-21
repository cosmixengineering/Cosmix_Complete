Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:checks = 0
function Check([bool]$Condition, [string]$Reason) {
    if (-not $Condition) { throw $Reason }
    $script:checks++
}
function Refuses([scriptblock]$Action, [string]$Stage) {
    $failure = $null
    try { & $Action | Out-Null } catch { $failure = $_.Exception.Message }
    Check ($null -ne $failure -and $failure -ceq ('Fresh setup refused: ' + $Stage + '; values suppressed.')) ('Expected refusal at ' + $Stage)
}
$driver = Join-Path $PSScriptRoot 'Invoke-Fresh-SQL-Setup.ps1'
Check (Test-Path -LiteralPath $driver) 'Fresh setup executor is missing (expected initial RED).'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($driver, [ref]$tokens, [ref]$errors)
Check ($errors.Count -eq 0) 'Executor parses.'
# Importing defines pure helpers only; no packet inventory, process or private IO.
$importOutput = @(. $driver)
Check ($importOutput.Count -eq 0) 'Dot sourcing must be inert.'
$scope = Get-FreshSqlScope
$hash = 'A' * 64
Assert-FreshExecutorIntent $true $hash $hash $scope.Owner $scope.Repository
$script:checks++
Refuses { Assert-FreshExecutorIntent $false $hash $hash $scope.Owner $scope.Repository } 'ExecutionFlag'
foreach ($bad in @('', ('A' * 63), ('G' * 64), ('a' * 64), ('B' * 64))) {
    Refuses { Assert-FreshExecutorIntent $true $bad $hash $scope.Owner $scope.Repository } 'ApprovedPacketSha256'
}
Refuses { Assert-FreshExecutorIntent $true $hash $hash 'other\\operator' $scope.Repository } 'Operator'
Refuses { Assert-FreshExecutorIntent $true $hash $hash $scope.Owner 'C:\other' } 'Checkout'
foreach ($instance in @($scope.Instance, $scope.Instance.ToLowerInvariant())) {
    Refuses { Assert-FreshExecutorAbsence @($instance) @() } 'TargetAbsence'
}
foreach ($path in @($scope.PrivateRoot, $scope.ArtifactRoot, $scope.DataFile, $scope.LogFile)) {
    Refuses { Assert-FreshExecutorAbsence @() @($path) } 'TargetAbsence'
}
$pipe = 'np:\\.\pipe\LOCALDB#ABCDEF01\tsql\query'
$server = 'COSMIXRND\LOCALDB#ABCDEF01'
$metadata = @{ Name = $scope.Instance; Owner = $scope.Owner; State = 'Running'; InstancePipeName = '\\.\pipe\LOCALDB#ABCDEF01\tsql\query' }
Check ((Resolve-FreshExecutorPipe $metadata) -ceq $pipe) 'New instance metadata resolves exact pipe.'
foreach ($field in @('Name', 'Owner', 'State', 'InstancePipeName')) {
    $bad = $metadata.Clone(); $bad[$field] = 'wrong'
    Refuses { Resolve-FreshExecutorPipe $bad } 'NewInstanceMetadata'
}
foreach ($badPipe in @('(localdb)\MSSQLLocalDB', '(localdb)\CosmixAttendanceHuma20261013A01', 'np:\\.\pipe\LOCALDB#842DD4FB\tsql\query', 'tcp:remote', ($pipe + ';Database=master'))) {
    Refuses { Assert-FreshExecutorSqlIdentity $badPipe $server $scope.Owner 'master' } 'SqlIdentity'
}
Refuses { Assert-FreshExecutorSqlIdentity $pipe 'OTHER\LOCALDB#11111111' $scope.Owner 'master' } 'SqlIdentity'
Refuses { Assert-FreshExecutorSqlIdentity $pipe $server 'other' 'master' } 'SqlIdentity'
Refuses { Assert-FreshExecutorSqlIdentity $pipe $server $scope.Owner 'external' } 'SqlIdentity'
Assert-FreshExecutorSqlIdentity $pipe $server $scope.Owner 'master'
$script:checks++
$sqlPath = Join-Path $scope.Repository 'docs\sql\20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence.fresh-isolated.sql'
$argsForReview = @(Get-FreshExecutorSqlcmdArguments $pipe $sqlPath)
Check (($argsForReview[0..7] -join '|') -ceq ('-S|' + $pipe + '|-d|Cosmix_Testing|-E|-C|-b|-X1')) 'sqlcmd binds exact pipe/catalog and Windows auth; fail on errors and disabled scripting.'
Check ($argsForReview[-2] -ceq '-i' -and $argsForReview[-1] -ceq $sqlPath) 'Reviewed SQL only.'
Refuses { Get-FreshExecutorSqlcmdArguments $pipe 'C:\other.sql' } 'SqlArtifactPath'
foreach ($sql in @(':connect other', '  :r other.sql', '!! whoami', 'SELECT $(Target)', 'USE [master];', 'CREATE DATABASE [other];', 'SELECT * FROM OPENROWSET(''x'');')) {
    Refuses { Assert-FreshExecutorSqlText $sql } 'SqlRouting'
}
Assert-FreshExecutorSqlText 'CREATE TABLE [hr].[Example] ([Id] int);'
$script:checks++
$actualSql = [IO.File]::ReadAllText($sqlPath)
Assert-FreshExecutorSqlText $actualSql
$script:checks++
Check ((Get-FileHash -LiteralPath $sqlPath -Algorithm SHA256).Hash -ceq 'E9FB2FBBF59C96C72C7C6F1506652EFA431545B0BD93C90987AD94822D63AB03') 'Actual from-zero SQL is the reviewed artifact.'
$sqlIds = @([regex]::Matches($actualSql, "VALUES \(N'(\d{14}_[^']+)', N'10\.0\.12'\)") | ForEach-Object { $_.Groups[1].Value })
$migrationRoot = Join-Path $scope.Repository 'src\Cosmix.Infrastructure\Database\Migrations'
$sourceIds = @(Get-ChildItem -LiteralPath $migrationRoot -Filter '*.Designer.cs' | Sort-Object Name | ForEach-Object { $_.Name.Replace('.Designer.cs','') })
Check ($sqlIds.Count -eq 21 -and ($sqlIds -join '|') -ceq ($sourceIds -join '|')) 'Actual SQL history is the exact ordered source chain, not an incremental range.'
$expected = @([pscustomobject]@{ Path = 'a.cs'; Sha256 = $hash })
Assert-FreshExecutorHashes $expected $expected
$script:checks++
Refuses { Assert-FreshExecutorHashes $expected @([pscustomobject]@{ Path = 'a.cs'; Sha256 = ('B' * 64) }) } 'InputDrift'
Refuses { Assert-FreshExecutorHashes $expected @() } 'InputDrift'
Refuses { Assert-FreshExecutorHashes $expected @($expected[0], $expected[0]) } 'InputDrift'
Refuses { Assert-FreshExecutorHashes $expected @([pscustomobject]@{ Path = 'b.cs'; Sha256 = $hash }) } 'InputDrift'
# Explicit deterministic fixture bytes, never RNG or a real key/credential file.
$secrets = @{}
$index = 1
foreach ($name in @('JwtSigningKey', 'OtpVerifier', 'OtpEncryption', 'FilesKey', 'SnapshotKey')) {
    $secrets[$name] = [Convert]::ToBase64String([byte[]](1..32 | ForEach-Object { ($_ + $index) % 256 }))
    $index++
}
$config = New-FreshExecutorConfiguration $pipe $server $secrets
Check ($config.LocalTesting.Profile -ceq 'HumaAttendanceIsolatedV1') 'Fresh branch selected.'
Check ($config.LocalTesting.PrivateDirectory -ceq $scope.PrivateRoot) 'Fresh credentials directory.'
Check ($config.LocalTesting.Isolation.DataProtectionDirectory -ceq ($scope.PrivateRoot + '\keys\data-protection')) 'Fresh Data Protection binding declared.'
Check ($config.ConnectionStrings.Count -eq 1 -and $config.ConnectionStrings.CosmixDatabase.Contains('Initial Catalog=Cosmix_Testing;')) 'One exact database connection.'
Check ($config.MobileAuthentication.Crypto.VerifierKeyBase64 -ceq $secrets.OtpVerifier) 'Explicit verifier value.'
Check ($config.MobileAuthentication.Crypto.EncryptionKeyBase64 -ceq $secrets.OtpEncryption) 'Explicit encryption value.'
Check ($config.Jwt.SigningKey -ceq $secrets.JwtSigningKey) 'Explicit JWT value.'
Check ($config.PrivateFiles.KeyFile -ceq ($scope.PrivateRoot + '\keys\files.key')) 'Fresh private file key path.'
Check ($config.MobileAuthentication.Sms.Provider -ceq 'LocalTesting' -and $config.MobileAuthentication.Sms.Endpoint -ceq '') 'Local SMS only.'
foreach ($section in @('SelfHostedFace','FaceCapture','FaceTemplateStorage','FaceStorageRetention','FaceStorageRetentionJournal')) {
    Check (-not $config[$section].Enabled) ('Face provider disabled: ' + $section)
}
foreach ($key in @($secrets.Keys)) {
    $bad = $secrets.Clone(); $bad.Remove($key)
    Refuses { New-FreshExecutorConfiguration $pipe $server $bad } 'ExplicitFreshCrypto'
    $bad = $secrets.Clone(); $bad[$key] = 'SECRET_MUST_NOT_APPEAR_IN_FAILURE'
    Refuses { New-FreshExecutorConfiguration $pipe $server $bad } 'ExplicitFreshCrypto'
}
$bad = $secrets.Clone(); $bad.FilesKey = $bad.SnapshotKey
Refuses { New-FreshExecutorConfiguration $pipe $server $bad } 'ExplicitFreshCrypto'
$bad = $secrets.Clone(); $bad.Extra = $hash
Refuses { New-FreshExecutorConfiguration $pipe $server $bad } 'ExplicitFreshCrypto'
$json = $config | ConvertTo-Json -Depth 10
Check (($json | ConvertFrom-Json).LocalTesting.Isolation.Pipe -ceq $pipe) 'In-memory JSON round trip.'
Check ((Get-FreshExecutorDigest 'abc') -ceq 'BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD') 'SHA256 encoding fixed.'
Check ((Get-FreshExecutorDigest 'abc') -cne (Get-FreshExecutorDigest 'abd')) 'Packet content drift changes digest.'
Check ($null -ne (Get-Command ConvertFrom-FreshLocalDbInfo -ErrorAction SilentlyContinue)) 'New-instance LocalDB output parser is missing (new RED).'
$info = "Name: CosmixAttendanceHuma20261013A01`nVersion: 17.0.4025.3`nShared name:`nOwner: COSMIXRND\Huma.Khan`nAuto-create: No`nState: Running`nLast start time: 13/10/2026`nInstance pipe name: \\.\pipe\LOCALDB#ABCDEF01\tsql\query"
Check ((ConvertFrom-FreshLocalDbInfo $info).Pipe -ceq $pipe) 'Strict LocalDB output parser resolves the new pipe.'
$prefixedInfo = $info.Replace('Instance pipe name: ', 'Instance pipe name: np:').Replace("`n", "`r`r`n")
Check ((ConvertFrom-FreshLocalDbInfo $prefixedInfo).Pipe -ceq $pipe) 'Installed LocalDB np-prefix and CRCRLF normalize to the same exact new pipe.'
foreach ($badPipe in @('np:np:\\.\pipe\LOCALDB#ABCDEF01\tsql\query', 'np:\\remote\pipe\LOCALDB#ABCDEF01\tsql\query', 'np:\\.\pipe\LOCALDB#ABCDEF01\tsql\query;Database=master')) {
    Refuses { ConvertFrom-FreshLocalDbInfo ($info.Replace('\\.\pipe\LOCALDB#ABCDEF01\tsql\query', $badPipe)) } 'NewInstanceMetadata'
}
foreach ($bad in @(($info + "`nOwner: other"), $info.Replace('17.0.4025.3','16.0.1'), $info.Replace('Running','Stopped'), $info.Replace('Shared name:','Shared name: other'))) {
    Refuses { ConvertFrom-FreshLocalDbInfo $bad } 'NewInstanceMetadata'
}
$hostSource = [IO.File]::ReadAllText((Join-Path $scope.Repository 'src\Cosmix.Api\Development\LocalTesting.cs'))
Assert-FreshExecutorReady $hostSource
$script:checks++
Refuses { Assert-FreshExecutorReady 'WebApplication.CreateBuilder(args)' } 'ConfigurationProviderIsolation'
$childEnvironment = New-FreshChildEnvironment
Check ($childEnvironment['COSMIX_FRESH_TESTING_HOST'] -ceq 'HumaAttendanceIsolatedV1') 'Isolated child factory flag.'
Check ($childEnvironment['COSMIX_LOCAL_TESTING_SETTINGS'] -ceq $scope.Settings) 'Private child settings path.'
Check (-not $childEnvironment.Contains('ASPNETCORE_URLS') -and -not $childEnvironment.Contains('DOTNET_STARTUP_HOOKS')) 'No inherited runtime/config/URL providers.'
$managed = Get-FreshManagedSystemDirectory
Check ($managed -ceq 'C:\Users\Huma.Khan\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\CosmixAttendanceHuma20261013A01') 'Exact managed system-directory collision scope.'
Refuses { Assert-FreshExecutorAbsence @() @($managed) } 'TargetAbsence'
foreach ($path in @($scope.Settings, ($scope.PrivateRoot + '\keys\files.key'), ($scope.PrivateRoot + '\manifest\000-start.json'))) {
    Assert-FreshOwnedPath $path
    $script:checks++
}
foreach ($bad in @('C:\other', ($scope.PrivateRoot + '\..\other'), ($scope.PrivateRoot + '\keys\file.key:stream'), $scope.PrivateRoot)) {
    Refuses { Assert-FreshOwnedPath $bad } 'PrivatePathScope'
}
$budget = Get-FreshFixtureBudget
Check ($budget.Tables['auth.UserAccounts'] -eq 3 -and $budget.Tables['hr.Employees'] -eq 3) 'Seed account/employee budget is three.'
Check ($budget.Tables['auth.Permissions'] -eq 39 -and $budget.Tables['auth.RolePermissions'] -eq 92) 'Seed permissions derive from reviewed explicit lists.'
Check ($budget.Tables['audit.AuditEvents'] -eq 1 -and $budget.Tables.Count -eq 11) 'Only eleven seed tables may be nonempty.'
$schema = Get-FreshSchemaContract
Check ($schema.History.Count -eq 21 -and $schema.Tables -ccontains 'dbo.__EFMigrationsHistory') 'Whole SQL schema includes its ledger.'
foreach ($table in @($budget.Tables.Keys)) {
    Check ($schema.Tables -ccontains $table) ('Budget table exists: ' + $table)
    Assert-FreshBusinessRowCount $table 0 $budget.Tables $false
    Assert-FreshBusinessRowCount $table $budget.Tables[$table] $budget.Tables $true
    $script:checks += 2
    Refuses { Assert-FreshBusinessRowCount $table 1 $budget.Tables $false } 'BusinessRowBudget'
    Refuses { Assert-FreshBusinessRowCount $table ($budget.Tables[$table] + 1) $budget.Tables $true } 'BusinessRowBudget'
}
Assert-FreshBusinessRowCount 'attendance.RawPunchEvents' 0 $budget.Tables $true
$script:checks++
Refuses { Assert-FreshBusinessRowCount 'attendance.RawPunchEvents' 1 $budget.Tables $true } 'BusinessRowBudget'
Refuses { Assert-FreshBusinessRowCount 'unknown.Unreviewed' 1 $budget.Tables $true } 'BusinessRowBudget'
$files = @(@{ Path = $scope.DataFile; FileType = 0 }, @{ Path = $scope.LogFile; FileType = 1 })
Assert-FreshPhysicalFileMetadata $files
$script:checks++
Refuses { Assert-FreshPhysicalFileMetadata @($files[0]) } 'PhysicalFiles'
Refuses { Assert-FreshPhysicalFileMetadata @($files[0],@{ Path = 'C:\other.ldf'; FileType = 1 }) } 'PhysicalFiles'
Refuses { Assert-FreshPhysicalFileMetadata @($files[0],@{ Path = $scope.LogFile; FileType = 0 }) } 'PhysicalFiles'
Refuses { Assert-FreshExecutionContext $null } 'ExecutionContext'
Refuses { New-FreshPrivateDirectory $null $scope.PrivateRoot } 'ExecutionContext'
Refuses { Write-FreshPrivateBytes $null $scope.Settings ([byte[]]@(1,2)) } 'ExecutionContext'
Refuses { New-FreshSqlConnection $null $pipe 'master' } 'ExecutionContext'
Refuses { Invoke-FreshChild $null 'other.exe' @() } 'ExecutionContext'
Refuses { New-FreshCryptoValues $null } 'ExecutionContext'
$quoted = ConvertTo-FreshWindowsArgument 'a b'
Check ($quoted -ceq '"a b"') 'Windows argument with spaces quoted.'
Check ((ConvertTo-FreshWindowsArgument 'a"b') -ceq '"a\"b"') 'Windows embedded quote escaped.'
$api = Join-Path $scope.Repository 'src\Cosmix.Api\bin\Release\net10.0\Cosmix.Api.dll'
Assert-FreshChildInvocation 'LocalDb' @('create','CosmixAttendanceHuma20261013A01','17.0') '' $api
Assert-FreshChildInvocation 'Dotnet' @($api,'setup-local-testing') $pipe $api
Assert-FreshChildInvocation 'Sqlcmd' $argsForReview $pipe $api
$script:checks += 3
foreach ($argsBad in @(@('delete','CosmixAttendanceHuma20261013A01'),@('start','MSSQLLocalDB'),@('info','CosmixUat_d893653346'),@('create','CosmixAttendanceHuma20261013A01','16.0'))) {
    Refuses { Assert-FreshChildInvocation 'LocalDb' $argsBad '' $api } 'ChildArguments'
}
foreach ($argsBad in @(@($api,'bootstrap-superadmin'),@($api,'setup-local-testing','--urls','https://other'),@($api))) {
    Refuses { Assert-FreshChildInvocation 'Dotnet' $argsBad $pipe $api } 'ChildArguments'
}
Refuses { Assert-FreshChildInvocation 'Sqlcmd' @('-S','other','-d','Cosmix_Testing') $pipe $api } 'ChildArguments'
Refuses { Assert-FreshApiBinary ([byte[]]@(1,2,3)) } 'ApiReleaseBuildRequired'
foreach ($offset in @(0,1)) {
    $symbols = [Text.Encoding]::UTF8.GetBytes('CreateEmptyBuilder UseKestrel')
    $strings = [Text.Encoding]::Unicode.GetBytes('Fresh local-testing requires its isolated configuration provider path.')
    Assert-FreshApiBinary ([byte[]](@([byte]0) * $offset + $symbols + $strings))
    $script:checks++
}
Assert-FreshApiBinary ([IO.File]::ReadAllBytes($api))
$script:checks++
# Reflection-only inspection in the execution runtime: never call CreateDirectoryW.
if ($PSVersionTable.PSEdition -ceq 'Desktop') {
    $native = Get-FreshNativeDirectoryType
    $method = $native.GetMethod('CreateDirectory')
    Check ($method.IsStatic -and $method.ReturnType -eq [bool] -and $method.GetParameters().Count -eq 2) 'Native exclusive-directory declaration built entirely in memory.'
    $acl = Get-FreshPrivateAcl $true
    Check ($acl.AreAccessRulesProtected -and $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]).Count -eq 3) 'Directory descriptor has three protected private grants, without creating a path.'
}
$plan = @(Get-FreshExecutorStages)
Check ($plan.Count -eq 9 -and $plan[0].Stage -ceq 'ConfigurationProviderIsolation') 'Staged review begins with pre-write isolation gate.'
Check (@($plan | Where-Object { -not $_.Implemented }).Count -eq 0) 'Every planned adapter implemented.'
# Destructive/persistent operations remain forbidden, even inside execution adapters.
$commands = @($ast.FindAll({param($n) $n -is [Management.Automation.Language.CommandAst]}, $true))
foreach ($forbidden in @('Start-Process','Set-Content','Out-File','Remove-Item','Set-Item','Invoke-Sqlcmd','sqlcmd','dotnet')) {
    Check (@($commands | Where-Object { $_.GetCommandName() -ieq $forbidden }).Count -eq 0) ('No executable mutation command: ' + $forbidden)
}
$calls = @($ast.FindAll({param($n) $n -is [Management.Automation.Language.InvokeMemberExpressionAst]}, $true))
foreach ($forbidden in @('Delete','WriteAllText','WriteAllBytes','SetEnvironmentVariable')) {
    Check (@($calls | Where-Object { $_.Member.Extent.Text -ieq $forbidden }).Count -eq 0) ('No SQL/process/private/RNG mutation method: ' + $forbidden)
}
$text = [IO.File]::ReadAllText($driver)
Check ((Get-FreshExecutorDigest $text) -cne (Get-FreshExecutorDigest ($text + '# synthetic drift'))) 'Actual executor-source drift changes digest without editing any source.'
Check ($ast.EndBlock.Statements[-1].Extent.Text.StartsWith("if (`$MyInvocation.InvocationName -ne '.')")) 'Entrypoint excludes dot sourcing.'
foreach ($name in @('New-FreshPrivateDirectory','Write-FreshPrivateBytes','Write-FreshStageRecord','New-FreshCryptoValues','Invoke-FreshChild','New-FreshSqlConnection','Invoke-FreshSqlRead','Invoke-FreshExecutorExecution')) {
    $function = @($ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name}, $true))
    Check ($function.Count -eq 1 -and $function[0].Body.EndBlock.Statements[0].Extent.Text -ceq 'Assert-FreshExecutionContext $Context') ('Adapter refuses before mutation: ' + $name)
}
Check (-not $text.Contains('ReadyForSqlExecution = $true')) 'No execution readiness claim.'
Write-Output ("PASS: {0} offline executor checks; synthetic inputs/AST only. No execution mode, SQL, LocalDB inventory, private writes, RNG, host or environment changes." -f $script:checks)
