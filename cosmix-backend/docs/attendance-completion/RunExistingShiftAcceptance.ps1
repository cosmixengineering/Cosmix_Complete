Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1')
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting'
$intent = Join-Path $root 'Attendance-Shift-Acceptance-20261013-B03-intent.json'
$receipt = Join-Path $root 'Attendance-Shift-Acceptance-20261013-B03-result.json'
if ((Test-Path -LiteralPath $intent) -or (Test-Path -LiteralPath $receipt)) { throw 'Prior shift attempt exists; preserve/reconcile it, no automatic rerun.' }
Assert-HumaPrivate $root
$accounts = @(Get-Content -LiteralPath (Join-Path $root 'Test-Accounts.json') -Raw | ConvertFrom-Json)
$owner = $accounts | Where-Object Email -CEQ 'superadmin@example.invalid'
$hr = $accounts | Where-Object Email -CEQ 'hr@example.invalid'
function Check([bool]$value,[string]$why) { if (-not $value) { throw $why } }
function RequireStatus($response,[int]$status) { Check ($response.StatusCode -eq $status) "Expected HTTP $status, received $($response.StatusCode)." }
$admin = New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $owner -SkipCertificateCheck
$hrId = '8475187F-BF3B-4F9C-A3ED-4BAC32253005'
$account = Invoke-LocalAcceptanceRequest -Client $admin -Method GET -Path ('/api/v1/access/accounts/' + $hrId)
RequireStatus $account 200
Write-HumaNewJson $intent @{ Database='Cosmix_Testing'; TargetAccount=$hrId; Profile='Synthetic HR shift tests only';
    RoleCode='ATTHUMA13B03'; Permissions=@('attendance.shifts.read','attendance.shifts.manage','attendance.rosters.self');
    ShiftCode='ATTHUMA13B03'; SyntheticSchedule='2026-10-14 09:00-18:00, 540 minutes, zero breaks, seven days; unassigned test fixture, not an office/company default.';
    AtUtc=[DateTimeOffset]::UtcNow.ToString('O') }
$role = Invoke-LocalAcceptanceRequest -Client $admin -Method POST -Path '/api/v1/access/roles' -Body @{ code='ATTHUMA13B03'; name='TEST ONLY Attendance HR Shift B03' }
RequireStatus $role 200
Write-HumaNewJson (Join-Path $root 'Attendance-Shift-Acceptance-20261013-B03-role.json') @{ RoleId=$role.Body.id; Version=$role.Body.version }
$grant = Invoke-LocalAcceptanceRequest -Client $admin -Method POST -Path ('/api/v1/access/roles/' + $role.Body.id + '/permissions') -Body @{
    permissions=@('attendance.shifts.read','attendance.shifts.manage','attendance.rosters.self'); expectedRoleVersion=$role.Body.version; reason='Explicit synthetic Attendance acceptance role; no Finance or legal activation permissions.' }
RequireStatus $grant 200
$assignment = Invoke-LocalAcceptanceRequest -Client $admin -Method POST -Path ('/api/v1/access/accounts/' + $hrId + '/assignments') -Body @{
    roleId=$role.Body.id; scopes=@(@{kind=7;targetId=$null}); effectiveFrom=[DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('O');
    effectiveTo=$null; expectedAccountVersion=$account.Body.version; expectedRoleVersion=$grant.Body.version;
    configureAsInitialAccess=$false; reason='Authorized synthetic HR shift acceptance, separate role only; existing assignments preserved.' }
RequireStatus $assignment 200
$client = New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $hr -SkipCertificateCheck
$body = @{ code='ATTHUMA13B03'; name='TEST ONLY Synthetic Site Shift B03'; reason='Synthetic test fixture only, not a company or office schedule.';
    initialSchedule=@{effectiveFrom='2026-10-14';startTime='09:00:00';endTime='18:00:00';endDayOffset=0;scheduledMinutes=540;paidBreakMinutes=0;unpaidBreakMinutes=0;applicableWeekdays=127} }
$key = 'huma-attendance-20261013-b03-create'
$created = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/hr/shifts' -Body $body -Headers @{ 'Idempotency-Key'=$key }
RequireStatus $created 201
Write-HumaNewJson (Join-Path $root 'Attendance-Shift-Acceptance-20261013-B03-created.json') @{ ShiftId=$created.Body.id; Version=$created.Body.version; AssignmentId=$assignment.Body.assignment.id }
$replay = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/hr/shifts' -Body $body -Headers @{ 'Idempotency-Key'=$key }
RequireStatus $replay 201
Check ($replay.Body.id -ceq $created.Body.id -and $replay.Body.version -ceq $created.Body.version) 'Retry changed immutable create result.'
$changed = $body.Clone(); $changed.name='TEST ONLY Changed Payload'
$conflict = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path '/api/v1/hr/shifts' -Body $changed -Headers @{ 'Idempotency-Key'=$key }
RequireStatus $conflict 409
$update = @{name='TEST ONLY Synthetic Site Shift B03 Renamed';expectedVersion=$created.Body.version;reason='Synthetic shift rename with exact source version.'}
$updated = Invoke-LocalAcceptanceRequest -Client $client -Method PUT -Path ('/api/v1/hr/shifts/' + $created.Body.id) -Body $update -Headers @{ 'Idempotency-Key'='huma-attendance-20261013-b03-rename' }
RequireStatus $updated 200
$stale = Invoke-LocalAcceptanceRequest -Client $client -Method PUT -Path ('/api/v1/hr/shifts/' + $created.Body.id) -Body $update -Headers @{ 'Idempotency-Key'='huma-attendance-20261013-b03-stale' }
RequireStatus $stale 409
Check ($stale.Body.title -ceq 'scheduling_version_conflict') 'Unexpected stale-version denial.'
$detail = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path ('/api/v1/hr/shifts/' + $created.Body.id)
RequireStatus $detail 200
Check ($detail.Body.versions.Count -eq 1 -and $detail.Body.name -ceq $update.name) 'Immutable schedule/history changed on rename or retry.'
foreach ($session in @($client,$admin)) {
    $logout=Invoke-LocalAcceptanceRequest -Client $session -Method POST -Path '/api/v1/auth/web/logout'
    RequireStatus $logout 204
}
Write-HumaNewJson $receipt @{ Database='Cosmix_Testing'; RoleId=$role.Body.id; AssignmentId=$assignment.Body.assignment.id; ShiftId=$created.Body.id;
    Create=201; Retry=201; RetrySameIdentity=$true; ChangedPayload=409; Rename=200; StaleVersion=409; Detail=200; ScheduleVersions=1;
    AtUtc=[DateTimeOffset]::UtcNow.ToString('O'); Limits='Unassigned synthetic shift only; no punch/leave/payroll/face/concurrency acceptance claimed.' }
$accounts=$null;$owner=$null;$hr=$null;$client=$null;$admin=$null
Write-Output 'PASS: real authenticated Shift create, exact retry, changed-payload conflict, rename, stale-version rejection and retained schedule detail. Synthetic fixture retained; no default/company schedule changed.'
