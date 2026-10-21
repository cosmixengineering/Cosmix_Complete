Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:checks = 0
function Check([bool]$Value,[string]$Message) { if (-not $Value) { throw $Message }; $script:checks++ }
function Refuses([scriptblock]$Action) { $failed=$false; try { & $Action | Out-Null } catch { $failed=$true }; Check $failed 'Unsafe input was accepted.' }
$driver=Join-Path $PSScriptRoot 'Invoke-A02Setup.ps1'
Check (Test-Path -LiteralPath $driver) 'A02 executable packet missing (initial RED).'
. $driver
$scope=Get-FreshSqlScope
Check ($scope.Instance -ceq 'CosmixAttendanceHuma20261015A02') 'Wrong instance.'
Check ($scope.Profile -ceq 'HumaAttendanceIsolatedA02') 'Wrong isolated profile.'
Check ($scope.PrivateRoot -ceq 'C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261015A02') 'Wrong private root.'
$pipe='np:\\.\pipe\LOCALDB#ABCDEF02\tsql\query'
$server='COSMIXRND\LOCALDB#ABCDEF02'
Assert-FreshExecutorSqlIdentity $pipe $server $scope.Owner 'master'
$script:checks++
foreach($bad in @('(localdb)\MSSQLLocalDB','(localdb)\CosmixAttendanceHuma20261013A01','tcp:remote','np:\\.\pipe\LOCALDB#842DD4FB\tsql\query',($pipe+';Database=other'))) {
    Refuses { Assert-FreshExecutorSqlIdentity $bad $server $scope.Owner 'master' }
}
foreach($value in @('other','Cosmix_Development')) { Refuses { Assert-FreshExecutorSqlIdentity $pipe $server $scope.Owner $value } }
Refuses { Assert-FreshExecutorSqlIdentity $pipe 'OTHER\LOCALDB#11111111' $scope.Owner 'Cosmix_Testing' }
Refuses { Assert-FreshExecutorSqlIdentity $pipe $server 'other' 'Cosmix_Testing' }
Refuses { Assert-FreshExecutorAbsence @($scope.Instance) @() }
Refuses { Assert-FreshExecutorAbsence @() @($scope.PrivateRoot) }
$hash='A'*64
Assert-FreshExecutorIntent $true $hash $hash $scope.Owner $scope.Repository
Refuses { Assert-FreshExecutorIntent $false $hash $hash $scope.Owner $scope.Repository }
Refuses { Assert-FreshExecutorIntent $true ('B'*64) $hash $scope.Owner $scope.Repository }
foreach($text in @(':connect other','USE master;','CREATE DATABASE other;','SELECT $(Target)','SELECT * FROM OPENROWSET(''x'');')) { Refuses { Assert-FreshExecutorSqlText $text } }
$budget=Get-FreshFixtureBudget
Check ($budget.Tables['auth.Permissions'] -eq 39) 'Fixed permission budget changed.'
Check ($budget.HrPermissionCodes.Count -eq 20) 'Explicit HR grant budget changed.'
Check ($budget.Tables['auth.RolePermissions'] -eq 98) 'Initial exact grants changed.'
Check ($budget.Tables['auth.UserAccounts'] -eq 3) 'Account budget changed.'
Refuses { Assert-FreshBusinessRowCount 'auth.RolePermissions' 99 $budget.Tables $true }
Refuses { Assert-FreshBusinessRowCount 'attendance.AttendancePunches' 1 $budget.Tables $true }
Refuses { New-FreshCryptoValues $null }
Refuses { New-FreshSqlConnection $null $pipe 'Cosmix_Testing' }
Refuses { Invoke-FreshChild $null 'dotnet' @('anything') }
. (Join-Path $PSScriptRoot 'Invoke-A02Auth.ps1')
Assert-A02AuthIntent $true $hash $hash 'APPROVE A02 SETUP AND ONE HR WEB MFA ON 7481 ONLY'
$script:checks++
Refuses { Assert-A02AuthIntent $false $hash $hash 'APPROVE A02 SETUP AND ONE HR WEB MFA ON 7481 ONLY' }
Refuses { Assert-A02AuthIntent $true $hash ('B'*64) 'APPROVE A02 SETUP AND ONE HR WEB MFA ON 7481 ONLY' }
Refuses { Assert-A02AuthIntent $true $hash $hash 'APPROVE OLD DATABASE' }
Refuses { Write-A02AuthReceipt 'auth-attempt.json' @{} }
$sample="Name: CosmixAttendanceHuma20261015A02`nVersion: 17.0.4025.3`nShared name:`nOwner: COSMIXRND\Huma.Khan`nAuto-create: No`nState: Running`nLast start time: synthetic`nInstance pipe name: np:\\.\pipe\LOCALDB#ABCDEF02\tsql\query"
Check ((ConvertFrom-FreshLocalDbInfo $sample).Pipe -ceq $pipe) 'SqlLocalDB np prefix must normalize once.'
Refuses { ConvertFrom-FreshLocalDbInfo ($sample.Replace('Running','Stopped')) }
Assert-FreshChildInvocation 'Dotnet' @((Join-Path $scope.ReleaseRoot 'Cosmix.Api.dll'),'setup-local-testing') '' (Join-Path $scope.ReleaseRoot 'Cosmix.Api.dll')
$script:checks++
Refuses { Assert-FreshChildInvocation 'Dotnet' @('C:\other.dll','setup-local-testing') '' 'C:\other.dll' }
$contract=Get-FreshSchemaContract
Check ($contract.History.Count -eq 24) 'Exact current schema chain required.'
$sourceIds=@(Get-ChildItem -LiteralPath (Join-Path $scope.Repository 'src\Cosmix.Infrastructure\Database\Migrations') -Filter '*.Designer.cs' | Sort-Object Name | ForEach-Object { $_.Name.Replace('.Designer.cs','') })
Check (($sourceIds -join '|') -ceq ($contract.History.MigrationId -join '|')) 'Generated SQL differs from source chain.'
. (Join-Path $PSScriptRoot 'Invoke-A02Auth.ps1') -Execute -ApprovedManifestSha256 $hash -AuthApproval 'APPROVE A02 SETUP AND ONE HR WEB MFA ON 7481 ONLY'
Check ([bool]$ExecuteAuth) 'Setup helper import must not discard the explicit auth execution flag.'
Refuses { Set-A02ReleaseDirectory 'C:\other\bin\Cosmix.Api\release' }
Refuses { Set-A02ReleaseDirectory 'D:\CosmixVerification\hr-backend-continuation-20261015\..\other\bin\Cosmix.Api\release' }
Refuses { Set-A02ReleaseDirectory 'D:\CosmixVerification\hr-backend-continuation-20261015\posting-build\bin\Cosmix.Api\debug' }
$authBudget=Get-A02AuthBudget
Check ($authBudget.BaseUri -ceq 'https://localhost:7481/' -and $authBudget.Actor -ceq 'hr@example.invalid') 'Auth scope changed.'
Check ($authBudget.MaximumPasswordChallenges -eq 1 -and $authBudget.MaximumOtpVerifications -eq 1 -and $authBudget.MaximumOwnedHosts -eq 1) 'One-session budget changed.'
Check ($authBudget.MobileOtpChallenges -eq 1 -and $authBudget.OtpDeliveries -eq 1 -and $authBudget.UserSessions -eq 1 -and $authBudget.RefreshTokens -eq 1) 'Auth SQL row budget changed.'
Check ($authBudget.MaximumAuthRateBuckets -eq 20 -and $authBudget.MaximumAdditionalAuditEvents -eq 12 -and $authBudget.MaximumDataProtectionKeys -eq 1) 'Auxiliary auth write budget changed.'
Check ($authBudget.ImmutableBusinessAndGrants -and $authBudget.RealProviderCalls -eq 0 -and $authBudget.BusinessRequests -eq 0) 'Business/provider scope expanded.'
Write-Output "PASS: $script:checks A02 offline target/intent/grant/mutation-context checks. No SQL, process, private-material or instance writes."
