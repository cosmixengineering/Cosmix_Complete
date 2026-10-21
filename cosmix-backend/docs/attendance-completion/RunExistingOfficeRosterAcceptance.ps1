param([switch]$ContinueAfterAssignedRoleDenied)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Huma-Acceptance-Common.ps1')
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$root = 'C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting'
$originalIntent = Join-Path $root 'Attendance-Office-Acceptance-20261013-B06-intent.json'
$resourcesPath = Join-Path $root 'Attendance-Office-Acceptance-20261013-B06-resources.json'
$intent = if ($ContinueAfterAssignedRoleDenied) { Join-Path $root 'Attendance-Office-Acceptance-20261013-B06-continuation-intent.json' } else { $originalIntent }
$receipt = Join-Path $root 'Attendance-Office-Acceptance-20261013-B06-result.json'
if ((Test-Path -LiteralPath $intent) -or (Test-Path -LiteralPath $receipt)) { throw 'Office attempt already exists; preserve it, no automatic rerun.' }
if ($ContinueAfterAssignedRoleDenied -and (-not (Test-Path -LiteralPath $originalIntent) -or -not (Test-Path -LiteralPath $resourcesPath))) { throw 'Exact owned incomplete office attempt is required.' }
Assert-HumaPrivate $root
$accounts = @(Get-Content -LiteralPath (Join-Path $root 'Test-Accounts.json') -Raw | ConvertFrom-Json)
$owner = $accounts | Where-Object Email -CEQ 'superadmin@example.invalid'
$hr = $accounts | Where-Object Email -CEQ 'hr@example.invalid'
function Check([bool]$value, [string]$why) { if (-not $value) { throw $why } }
function RequireStatus($response, [int]$status) { Check ($response.StatusCode -eq $status) "Expected HTTP $status, received $($response.StatusCode)." }
$ownerSession=$null;$beforeSession=$null;$client=$null
try {
$ownerSession = New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $owner -SkipCertificateCheck
$beforeSession = New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $hr -SkipCertificateCheck
$createdShift = Get-Content -LiteralPath (Join-Path $root 'Attendance-Shift-Acceptance-20261013-B03-created.json') -Raw | ConvertFrom-Json
$savedRole = Get-Content -LiteralPath (Join-Path $root 'Attendance-Shift-Acceptance-20261013-B03-role.json') -Raw | ConvertFrom-Json
$shift = Invoke-LocalAcceptanceRequest -Client $beforeSession -Method GET -Path ('/api/v1/hr/shifts/' + $createdShift.ShiftId)
RequireStatus $shift 200
$roles = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method GET -Path '/api/v1/access/roles?search=ATTHUMA13B03&pageSize=100'
RequireStatus $roles 200
$matches = @($roles.Body.items | Where-Object { $_.id -ceq $savedRole.RoleId })
Check ($matches.Count -eq 1) 'Owned synthetic role was not uniquely returned by the registered list endpoint.'
$role = [pscustomobject]@{Body=$matches[0]}
Check ($role.Body.code -ceq 'ATTHUMA13B03') 'Only the owned synthetic role may change.'
Write-HumaNewJson $intent @{ Database='Cosmix_Testing'; RoleCode='ATTHUMA13B06'; ExistingAssignedRoleUnchanged='ATTHUMA13B03';
    Permissions=@('attendance.rosters.read','attendance.rosters.manage');
    CityCode='ATTHUMA13B06CITY'; OfficeCode='ATTHUMA13B06OFFICE';
    PlannedWrites='One separate synthetic roster role/All-scope assignment, one roster assignment and immutable ending plus auth/audit/receipts; reuse exact prior city/office on explicit continuation. No employee transfer, payroll, real face or existing role changes.';
    AtUtc=[DateTimeOffset]::UtcNow.ToString('O') }
if ($ContinueAfterAssignedRoleDenied) {
    $resources = Get-Content -LiteralPath $resourcesPath -Raw | ConvertFrom-Json
    Check ($resources.ShiftId -ceq $shift.Body.id -and $resources.RoleId -ceq $role.Body.id) 'Owned continuation source identities changed.'
    $city = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method GET -Path ('/api/v1/hr/cities/' + $resources.CityId)
    $office = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method GET -Path ('/api/v1/hr/branches/' + $resources.BranchId)
    RequireStatus $city 200;RequireStatus $office 200
    Check ($city.Body.code -ceq 'ATTHUMA13B06CITY' -and $office.Body.code -ceq 'ATTHUMA13B06OFFICE' -and
        $office.Body.cityId -ceq $city.Body.id) 'Owned synthetic office/city no longer match.'
} else {
$city = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method POST -Path '/api/v1/hr/cities' -Body @{
    code='ATTHUMA13B06CITY';name='TEST ONLY Office Roster City B06';province='Punjab' } -Headers @{'Idempotency-Key'='huma-attendance-b06-city'}
RequireStatus $city 201
$office = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method POST -Path '/api/v1/hr/branches' -Body @{
    code='ATTHUMA13B06OFFICE';name='TEST ONLY Unassigned Office B06';cityId=$city.Body.id } -Headers @{'Idempotency-Key'='huma-attendance-b06-office'}
RequireStatus $office 201
Write-HumaNewJson (Join-Path $root 'Attendance-Office-Acceptance-20261013-B06-resources.json') @{
    CityId=$city.Body.id;BranchId=$office.Body.id;ShiftId=$shift.Body.id;RoleId=$role.Body.id }
}
$path = '/api/v1/hr/branches/' + $office.Body.id + '/shift-assignments'
$denied = Invoke-LocalAcceptanceRequest -Client $beforeSession -Method GET -Path $path
RequireStatus $denied 403
$logout = Invoke-LocalAcceptanceRequest -Client $beforeSession -Method POST -Path '/api/v1/auth/web/logout'
RequireStatus $logout 204
$beforeSession=$null
$permissions = @('attendance.rosters.read','attendance.rosters.manage')
$immutable = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method POST -Path ('/api/v1/access/roles/' + $role.Body.id + '/permissions') -Body @{
    permissions=$permissions;expectedRoleVersion=$role.Body.version;reason='Negative acceptance: previously assigned synthetic role must remain immutable.' }
RequireStatus $immutable 409
Check ($immutable.Body.title -ceq 'assigned_role_is_immutable') 'Assigned-role preservation guard was not enforced.'
$newRole = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method POST -Path '/api/v1/access/roles' -Body @{code='ATTHUMA13B06';name='TEST ONLY Office Roster B06'}
RequireStatus $newRole 200
Write-HumaNewJson (Join-Path $root 'Attendance-Office-Acceptance-20261013-B06-role.json') @{RoleId=$newRole.Body.id;Version=$newRole.Body.version}
$grant = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method POST -Path ('/api/v1/access/roles/' + $newRole.Body.id + '/permissions') -Body @{
    permissions=$permissions;expectedRoleVersion=$newRole.Body.version;reason='Separate synthetic office roster role; preserve existing assigned roles. No Finance or legal authority.' }
RequireStatus $grant 200
$account = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method GET -Path '/api/v1/access/accounts/8475187F-BF3B-4F9C-A3ED-4BAC32253005'
RequireStatus $account 200
$assignment = Invoke-LocalAcceptanceRequest -Client $ownerSession -Method POST -Path '/api/v1/access/accounts/8475187F-BF3B-4F9C-A3ED-4BAC32253005/assignments' -Body @{
    roleId=$newRole.Body.id;scopes=@(@{kind=7;targetId=$null});effectiveFrom=[DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('O');effectiveTo=$null;
    expectedAccountVersion=$account.Body.version;expectedRoleVersion=$grant.Body.version;configureAsInitialAccess=$false;
    reason='Synthetic office acceptance only; original role assignments remain unchanged.'}
RequireStatus $assignment 200
$client = New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $hr -SkipCertificateCheck
    $empty = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path $path
    RequireStatus $empty 200
    Check ($empty.Body.assignments.Count -eq 0 -and $empty.Body.endings.Count -eq 0) 'Owned new office must have empty roster history.'
    $body = @{shiftId=$shift.Body.id;effectiveFrom='2026-10-14';effectiveTo=$null;holidayCalendarId=$null;
        expectedTimeline=$empty.Body.timeline;expectedShiftVersion=$shift.Body.version;reason='Explicit unassigned synthetic office schedule; no company default or employee transfer.'}
    $created = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path $path -Body $body -Headers @{'Idempotency-Key'='huma-attendance-b06-assign'}
    RequireStatus $created 201
    $retry = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path $path -Body $body -Headers @{'Idempotency-Key'='huma-attendance-b06-assign'}
    RequireStatus $retry 201
    Check ($created.Body.assignment.id -ceq $retry.Body.assignment.id -and $created.Body.timeline -ceq $retry.Body.timeline) 'Roster retry changed frozen identity/timeline.'
    $changed = $body.Clone();$changed.reason='Changed synthetic request payload'
    RequireStatus (Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path $path -Body $changed -Headers @{'Idempotency-Key'='huma-attendance-b06-assign'}) 409
    $stale = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path $path -Body $body -Headers @{'Idempotency-Key'='huma-attendance-b06-stale'}
    RequireStatus $stale 409
    Check ($stale.Body.title -ceq 'office_roster_timeline_conflict') 'Unexpected stale roster denial.'
    $ending = @{effectiveTo='2026-10-16';expectedTimeline=$created.Body.timeline;reason='Synthetic immutable end; preserve original assignment evidence.'}
    $ended = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path ($path + '/' + $created.Body.assignment.id + '/end') -Body $ending -Headers @{'Idempotency-Key'='huma-attendance-b06-end'}
    RequireStatus $ended 200
    $endRetry = Invoke-LocalAcceptanceRequest -Client $client -Method POST -Path ($path + '/' + $created.Body.assignment.id + '/end') -Body $ending -Headers @{'Idempotency-Key'='huma-attendance-b06-end'}
    RequireStatus $endRetry 200
    Check ($ended.Body.timeline -ceq $endRetry.Body.timeline) 'Ending retry changed retained history.'
    $history = Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path $path
    RequireStatus $history 200
    Check ($history.Body.assignments.Count -eq 1 -and $history.Body.endings.Count -eq 1 -and
        $history.Body.assignments[0].id -ceq $created.Body.assignment.id -and $history.Body.assignments[0].effectiveTo -ceq '2026-10-16') 'Office history was duplicated or rewritten.'
    Write-HumaNewJson $receipt @{Database='Cosmix_Testing';BranchId=$office.Body.id;AssignmentId=$created.Body.assignment.id;
        BeforeGrantDenied=403;AssignedRoleImmutable=409;RoleId=$newRole.Body.id;RoleAssignmentId=$assignment.Body.assignment.id;
        Create=201;SameKeyReplay=201;ChangedPayload=409;StaleTimeline=409;End=200;EndReplay=200;
        Assignments=1;ImmutableEnds=1;AtUtc=[DateTimeOffset]::UtcNow.ToString('O');
        Limits='Unassigned office roster only; independent SQL/encrypted evidence and real staff capture/duty/leave/payroll acceptance remain separate.'}
} finally {
    foreach ($session in @($client,$ownerSession,$beforeSession)) {
        if ($null -eq $session) { continue }
        $logout=Invoke-LocalAcceptanceRequest -Client $session -Method POST -Path '/api/v1/auth/web/logout'
        RequireStatus $logout 204
    }
}
$accounts=$null;$owner=$null;$hr=$null
Write-Output 'PASS: actual MFA-backed office roster denial/grant, creation, exact replay, changed-payload/stale rejection and immutable ending/history. Synthetic fixture retained.'
