<#
Real localhost HTTP acceptance; run only after the separately approved isolated setup.
Creates run-prefixed roles and explicit assignments on the three synthetic test accounts.
Does not initialize/migrate SQL, start a host, alter credentials or revoke these grants.
Keep -PassThruClients results in memory. Never format/serialize the Clients property.
On a partial failure retain RunJournal and reconcile by this RunId; do not blindly rerun.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][uri]$BaseUri,
    [Parameter(Mandatory)][string]$AccountsPath,
    [Parameter(Mandatory)][string]$FixtureManifestPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9_.-]{5,50}$')][string]$RunId,
    [switch]$SkipCertificateCheck,
    [switch]$PassThruClients,
    [Collections.Generic.List[object]]$RunJournal = [Collections.Generic.List[object]]::new()
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'LocalAcceptanceHttp.ps1')
Assert-LocalAcceptanceUri $BaseUri
$approvedRoot = [IO.Path]::GetFullPath((Join-Path $env:USERPROFILE 'AppData\Local\Cosmix\AccessAcceptance\20261009')).TrimEnd('\') + '\'
function Read-PrivateAcceptanceJson([string]$Path) {
    try {
        $full = [IO.Path]::GetFullPath($Path)
        if (-not $full.StartsWith($approvedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'outside root' }
        $item = Get-Item -LiteralPath $full -Force
        if ($item.PSIsContainer -or $item.Length -lt 1 -or $item.Length -gt 1048576) { throw 'invalid file' }
        for ($cursor = $item; $null -ne $cursor;) {
            if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'linked path' }
            if ($cursor -is [IO.DirectoryInfo]) { $cursor = $cursor.Parent } else { $cursor = $cursor.Directory }
        }
        return (Get-Content -LiteralPath $full -Raw | ConvertFrom-Json -Depth 20)
    } catch { throw 'Private acceptance JSON is unavailable or outside the approved local root; details suppressed.' }
}
$accounts = @(Read-PrivateAcceptanceJson $AccountsPath)
$manifest = Read-PrivateAcceptanceJson $FixtureManifestPath
if ($manifest.Database -cne 'Cosmix_Testing' -or $manifest.RunId -cne $RunId -or $manifest.Committed -ne $true) { throw 'Fixture manifest target/run does not match this invocation or is not committed.' }
try {
    $adminId = [Guid]::Parse([string]$manifest.AdminAccountId)
    $superId = [Guid]::Parse([string]$manifest.SuperAdminAccountId)
    $hrId = [Guid]::Parse([string]$manifest.HrAccountId)
    $siteId = [Guid]::Parse([string]$manifest.SiteId)
} catch { throw 'Fixture manifest is missing a required synthetic account or site identifier.' }
if (@($adminId,$superId,$hrId,$siteId) -contains [Guid]::Empty -or @($adminId,$superId,$hrId | Select-Object -Unique).Count -ne 3) {
    throw 'Fixture manifest account/site identifiers are invalid.'
}
if ($accounts.Count -ne 3 -or @($accounts | Where-Object { $_.Email -notin @('superadmin@example.invalid','admin@example.invalid','hr@example.invalid') }).Count -ne 0 -or
    @($accounts.Email | Select-Object -Unique).Count -ne 3) { throw 'Exactly the three synthetic fixture credentials are required.' }
function Credential([string]$Email) { return ($accounts | Where-Object { $_.Email -ceq $Email } | Select-Object -First 1) }
$checks = [Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "Authenticated access acceptance failed: $Name. Sensitive response data suppressed." }
    $checks.Add($Name)
}
function Request([string]$Name, $Client, [string]$Method, [string]$Path, $Body, [int]$Status = 200,
    [string]$Code = '', [string]$IdempotencyKey = '', [switch]$WithoutCsrf) {
    $arguments = @{ Client = $Client; Method = $Method; Path = $Path; WithoutCsrf = $WithoutCsrf }
    if ($null -ne $Body) { $arguments.Body = $Body }
    if ($Method -ceq 'POST' -and $Path -match '^/api/v1/access/(roles(?:/[A-Fa-f0-9-]+/permissions)?|accounts/[A-Fa-f0-9-]+/assignments)$') {
        if (-not $IdempotencyKey) { $IdempotencyKey = [Guid]::NewGuid().ToString('N') }
        $arguments.Headers = @{ 'Idempotency-Key' = $IdempotencyKey }
    }
    $response = Invoke-LocalAcceptanceRequest @arguments
    Check ($response.StatusCode -eq $Status) "$Name (expected HTTP $Status; received $($response.StatusCode))"
    if ($Code) { Check ($response.Body.code -ceq $Code -or $response.Body.title -ceq $Code) "$Name code" }
    return $response.Body
}
function Roles($Client) {
    $result = @()
    for ($page = 1; $page -le 10; $page++) {
        $response = Request 'role metadata read' $Client GET "/api/v1/access/roles?page=$page&pageSize=100" $null
        $result += @($response.items)
        if ($result.Count -ge $response.totalCount) { return $result }
    }
    throw 'Role inventory exceeded this bounded acceptance runner.'
}
function AccountState($Client, [Guid]$Id) { return (Request 'account metadata read' $Client GET "/api/v1/access/accounts/$Id" $null) }
function Assign($Client, [Guid]$AccountId, $Role, [int]$Scope, $TargetId, [string]$Label) {
    $account = AccountState $Client $AccountId
    $currentRole = @(Roles $Client | Where-Object { $_.id -eq $Role.id })
    Check ($currentRole.Count -eq 1) 'exact selected role exists'
    $body = @{
        roleId = [string]$Role.id
        scopes = @(@{ kind = $Scope; targetId = $TargetId })
        effectiveFrom = [DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('O')
        effectiveTo = $null
        expectedAccountVersion = [string]$account.version
        expectedRoleVersion = [string]$currentRole[0].version
        reason = "$RunId synthetic acceptance: $Label"
    }
    $key = [Guid]::NewGuid().ToString('N')
    $result = Request $Label $Client POST "/api/v1/access/accounts/$AccountId/assignments" $body 200 '' $key
    $RunJournal.Add([pscustomobject]@{ Kind = 'Assignment'; Label = $Label; AccountId = $AccountId; AssignmentId = $result.assignment.id; RoleId = $Role.id; Scope = $Scope; TargetId = $TargetId })
    return [pscustomobject]@{ Result = $result; Request = $body; IdempotencyKey = $key }
}

$admin = New-LocalAcceptanceSession -BaseUri $BaseUri -Account (Credential 'admin@example.invalid') -SkipCertificateCheck:$SkipCertificateCheck
$super = New-LocalAcceptanceSession -BaseUri $BaseUri -Account (Credential 'superadmin@example.invalid') -SkipCertificateCheck:$SkipCertificateCheck
$oldHr = New-LocalAcceptanceSession -BaseUri $BaseUri -Account (Credential 'hr@example.invalid') -SkipCertificateCheck:$SkipCertificateCheck
$catalog = @(Request 'fixed Admin catalog access after real MFA' $admin GET '/api/v1/access/permissions' $null)
$adminBefore = @(Request 'initial Admin current permissions' $admin GET '/api/v1/me/permissions' $null)
Check (@($adminBefore | Where-Object { $_.permission -like 'attendance.shifts.*' -or $_.permission -like 'attendance.rosters.*' }).Count -eq 0) 'first-grant precondition: Admin does not own scheduling permissions'
Check (@($catalog | Where-Object { $_.code -eq 'attendance.rosters.manage' }).Count -eq 1) 'supported scheduling permission is in the catalog'
$prefix = $RunId.ToUpperInvariant()
Check (@(Roles $super | Where-Object { $_.code.StartsWith($prefix + '_', [StringComparison]::Ordinal) }).Count -eq 0) 'fresh role prefix; no silent rerun'
Request 'missing CSRF denies role creation' $admin POST '/api/v1/access/roles' @{ code = "${prefix}_NO_CSRF"; name = "$RunId synthetic no-CSRF probe" } 400 'invalid_csrf_token' -WithoutCsrf | Out-Null
Request 'reserved fixed role name rejected' $admin POST '/api/v1/access/roles' @{ code = "${prefix}_RESERVED"; name = 'Admin' } 400 'reserved_system_role' | Out-Null
$roles = @{}
$roleDefinitions = @(
    @{ Key = 'ShiftAll'; Code = 'SHIFT_ALL'; Permissions = @('attendance.shifts.read','attendance.shifts.manage') },
    @{ Key = 'RosterSite'; Code = 'ROSTER_SITE'; Permissions = @('attendance.rosters.read','attendance.rosters.manage') },
    @{ Key = 'MyShiftSelf'; Code = 'MY_SHIFT'; Permissions = @('attendance.rosters.self') },
    @{ Key = 'RosterReadSite'; Code = 'ROSTER_READ'; Permissions = @('attendance.rosters.read') }
)
foreach ($definition in $roleDefinitions) {
    $role = Request 'explicit empty custom role creation' $admin POST '/api/v1/access/roles' @{
        code = "${prefix}_$($definition.Code)"; name = "$RunId synthetic $($definition.Key)"
    }
    $RunJournal.Add([pscustomobject]@{ Kind = 'Role'; RoleId = $role.id; Code = $role.code })
    if ($definition.Key -eq 'ShiftAll') {
        Request 'omitted grant reason rejected by JSON contract' $admin POST "/api/v1/access/roles/$($role.id)/permissions" @{
            permissions = $definition.Permissions; expectedRoleVersion = $role.version
        } 400 | Out-Null
        Request 'control-character grant reason rejected' $admin POST "/api/v1/access/roles/$($role.id)/permissions" @{
            permissions = $definition.Permissions; expectedRoleVersion = $role.version; reason = "invalid`nreason"
        } 400 'invalid_access_reason' | Out-Null
        Request 'unknown catalog permission rejected' $admin POST "/api/v1/access/roles/$($role.id)/permissions" @{
            permissions = @('attendance.unapproved'); expectedRoleVersion = $role.version; reason = "$RunId synthetic unknown-code probe"
        } 400 'unsupported_permission' | Out-Null
    }
    $roles[$definition.Key] = Request 'fixed Admin first grant without owning business permission' $admin POST "/api/v1/access/roles/$($role.id)/permissions" @{
        permissions = $definition.Permissions; expectedRoleVersion = $role.version; reason = "$RunId synthetic explicit $($definition.Key) role"
    }
    if ($definition.Key -eq 'ShiftAll') {
        Request 'stale grant snapshot conflicts' $admin POST "/api/v1/access/roles/$($role.id)/permissions" @{
            permissions = @('attendance.rosters.self'); expectedRoleVersion = $role.version; reason = "$RunId synthetic stale-version probe"
        } 409 'access_version_conflict' | Out-Null
    }
}
$superState = AccountState $admin $superId
$protectedAttempt = @{
    roleId = $roles.ShiftAll.id; scopes = @(@{ kind = 7; targetId = $null })
    effectiveFrom = [DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('O'); effectiveTo = $null
    expectedAccountVersion = $superState.version; expectedRoleVersion = $roles.ShiftAll.version
    reason = "$RunId synthetic protected-target denial"
}
Request 'Admin cannot change another protected SuperAdmin account' $admin POST "/api/v1/access/accounts/$superId/assignments" $protectedAttempt 403 'access_denied' | Out-Null
$adminShift = Assign $super $adminId $roles.ShiftAll 7 $null 'SuperAdmin assigns Shift-All to protected Admin'
$adminRoster = Assign $super $adminId $roles.RosterSite 5 $siteId.ToString() 'SuperAdmin assigns scoped Roster to protected Admin'
Request 'role assignment invalidates prior Admin session' $admin GET '/api/v1/me/permissions' $null 401 | Out-Null
$admin = New-LocalAcceptanceSession -BaseUri $BaseUri -Account (Credential 'admin@example.invalid') -SkipCertificateCheck:$SkipCertificateCheck
$hrBefore = AccountState $admin $hrId
$missingReason = @{
    roleId = $roles.MyShiftSelf.id; scopes = @(@{ kind = 1; targetId = $null })
    effectiveFrom = [DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('O'); effectiveTo = $null
    expectedAccountVersion = $hrBefore.version; expectedRoleVersion = $roles.MyShiftSelf.version
}
Request 'omitted assignment reason rejected by JSON contract' $admin POST "/api/v1/access/accounts/$hrId/assignments" $missingReason 400 | Out-Null
$hrSelf = Assign $admin $hrId $roles.MyShiftSelf 1 $null 'Admin explicitly assigns HR self-service'
$replay = Request 'identical assignment key replays committed result' $admin POST "/api/v1/access/accounts/$hrId/assignments" $hrSelf.Request 200 '' $hrSelf.IdempotencyKey
Check ($replay.assignment.id -ceq $hrSelf.Result.assignment.id -and $replay.accountVersion -ceq $hrSelf.Result.accountVersion) 'assignment replay retains original identity and versions'
$changedRetry = $hrSelf.Request.Clone()
$changedRetry.reason = "$RunId changed retry purpose"
Request 'changed assignment payload cannot reuse key' $admin POST "/api/v1/access/accounts/$hrId/assignments" $changedRetry 409 'idempotency_key_reused' $hrSelf.IdempotencyKey | Out-Null
Request 'new assignment key with old versions conflicts' $admin POST "/api/v1/access/accounts/$hrId/assignments" $hrSelf.Request 409 'access_version_conflict' | Out-Null
$hrRead = Assign $admin $hrId $roles.RosterReadSite 5 $siteId.ToString() 'Admin explicitly assigns HR site-scoped roster read'
Request 'role assignment invalidates prior HR session' $oldHr GET '/api/v1/me/permissions' $null 401 | Out-Null
$hr = New-LocalAcceptanceSession -BaseUri $BaseUri -Account (Credential 'hr@example.invalid') -SkipCertificateCheck:$SkipCertificateCheck
Request 'custom HR role cannot administer access' $hr GET '/api/v1/access/permissions' $null 403 'access_denied' | Out-Null
$selfPermissions = @(Request 'fresh HR permission projection' $hr GET '/api/v1/me/permissions' $null)
Check (@($selfPermissions | Where-Object { $_.permission -eq 'attendance.rosters.self' -and $_.scopeKind -eq 1 -and $_.targetId -eq $hrBefore.employeeId }).Count -eq 1) 'HR My Shift is capped to its actual employee'
Check (@($selfPermissions | Where-Object { $_.permission -eq 'attendance.rosters.read' -and $_.scopeKind -eq 5 -and $_.targetId -eq $siteId.ToString() }).Count -eq 1) 'HR roster read retains the explicit site scope'
$adminPermissions = @(Request 'fresh Admin permission projection' $admin GET '/api/v1/me/permissions' $null)
Check (@($adminPermissions | Where-Object { $_.permission -eq 'attendance.shifts.manage' -and $_.scopeKind -eq 7 }).Count -eq 1) 'Admin shift administration has explicit All scope'
Check (@($adminPermissions | Where-Object { $_.permission -eq 'attendance.rosters.manage' -and $_.scopeKind -eq 5 -and $_.targetId -eq $siteId.ToString() }).Count -eq 1) 'Admin roster administration retains the explicit site scope'
$currentShiftRole = @(Roles $admin | Where-Object { $_.id -eq $roles.ShiftAll.id })[0]
Request 'an assigned custom role remains immutable' $admin POST "/api/v1/access/roles/$($currentShiftRole.id)/permissions" @{
    permissions = @('attendance.rosters.self'); expectedRoleVersion = $currentShiftRole.version; reason = "$RunId synthetic immutable-role probe"
} 409 'assigned_role_is_immutable' | Out-Null
$fixedRole = @(Roles $admin | Where-Object { $_.isSystemRole -eq $true })[0]
Check ($null -ne $fixedRole) 'fixed role metadata is present'
Request 'fixed role permission list remains immutable' $super POST "/api/v1/access/roles/$($fixedRole.id)/permissions" @{
    permissions = @('attendance.rosters.self'); expectedRoleVersion = $fixedRole.version; reason = "$RunId synthetic fixed-role probe"
} 409 'system_role_is_immutable' | Out-Null

$summary = [pscustomobject]@{
    RunId = $RunId; Passed = $checks.Count; Checks = $checks.ToArray(); Journal = $RunJournal.ToArray()
    GrantsRetained = $true; CleanupExecuted = $false; SqlAtomicityIndependentlyVerified = $false
}
if ($PassThruClients) {
    return [pscustomobject]@{
        Summary = $summary
        Clients = @{ Admin = $admin; SuperAdmin = $super; Hr = $hr }
        Grants = @{ AdminShift = $adminShift.Result; AdminRoster = $adminRoster.Result; HrSelf = $hrSelf.Result; HrRosterRead = $hrRead.Result }
        Roles = $roles
    }
}
return $summary
