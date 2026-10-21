<#
Prepared runner, not an approval or evidence of execution. Inspect is read-only and offline.
Execute requires the separately approved manifest, completed schema/preflight receipt and owned host.
Never initialize/reset SQL, enable workers, seed duty, edit old roles or rerun a partial execution.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ManifestPath,
    [ValidateSet('Inspect','Execute')][string]$Mode = 'Inspect',
    [ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ApprovedManifestSha256,
    [switch]$SkipCertificateCheck,
    [switch]$ResumeAfterGrants
)
$ErrorActionPreference = 'Stop'
$retainedRoot = [IO.Path]::GetFullPath((Join-Path $env:USERPROFILE 'AppData\Local\Cosmix\AccessAcceptance\20261009')).TrimEnd('\')
function Require([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Private-Path([string]$Path, [bool]$Exists = $true) {
    $full = [IO.Path]::GetFullPath($Path)
    Require ($full.StartsWith($retainedRoot + '\', [StringComparison]::OrdinalIgnoreCase)) 'Path is outside retained acceptance root.'
    $item = Get-Item -LiteralPath $(if ($Exists) { $full } else { Split-Path -Parent $full }) -Force
    for ($cursor = $item; $null -ne $cursor;) {
        Require (-not ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked private paths are forbidden.'
        if ($cursor -is [IO.DirectoryInfo]) { $cursor = $cursor.Parent } else { $cursor = $cursor.Directory }
    }
    $acl = Get-Acl -LiteralPath $item.FullName
    $descriptor=[Security.AccessControl.RawSecurityDescriptor]::new($acl.GetSecurityDescriptorBinaryForm(),0)
    Require ($null -ne $descriptor.DiscretionaryAcl) 'Private path has an unrestricted null ACL.'
    $allowed = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')
    Require ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -in $allowed) 'Unexpected private path owner.'
    foreach ($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
        Require ($rule.AccessControlType -ne 'Allow' -or $rule.IdentityReference.Value -in $allowed) 'Private path ACL is too broad.'
    }
    return $full
}
function Read-PrivateJson([string]$Path, [string]$Hash = '') {
    $full = Private-Path $Path
    $item = Get-Item -LiteralPath $full
    Require (-not $item.PSIsContainer -and $item.Length -ge 1 -and $item.Length -le 1048576) 'Private JSON size/type is invalid.'
    if ($Hash) { Require ((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -ceq $Hash.ToUpperInvariant()) 'Reviewed private file hash changed.' }
    try { return (Get-Content -LiteralPath $full -Raw | ConvertFrom-Json -Depth 30) }
    catch { throw 'Private JSON could not be parsed; contents suppressed.' }
}
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\')
$manifestFull = [IO.Path]::GetFullPath($ManifestPath)
Require ($manifestFull.StartsWith($repository + '\docs\attendance-completion\', [StringComparison]::OrdinalIgnoreCase)) 'Manifest must be the reviewed workspace acceptance document.'
$manifestItem=Get-Item -LiteralPath $manifestFull -Force
Require (-not $manifestItem.PSIsContainer -and $manifestItem.Length -ge 1 -and $manifestItem.Length -le 1048576) 'Manifest size/type is invalid.'
for($cursor=$manifestItem;$null -ne $cursor;) {
    Require (-not ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint)) 'Linked manifest paths are forbidden.'
    if($cursor -is [IO.DirectoryInfo]) {$cursor=$cursor.Parent} else {$cursor=$cursor.Directory}
}
$manifestHash = (Get-FileHash -LiteralPath $manifestFull -Algorithm SHA256).Hash
$manifest = Get-Content -LiteralPath $manifestFull -Raw | ConvertFrom-Json -Depth 30
Require ($manifest.SchemaVersion -eq 1 -and $manifest.Database -ceq 'Cosmix_Testing' -and
    $manifest.ExistingRunId -ceq 'ACC202610098E9C6465' -and $manifest.RunId -cmatch '^ATT[A-Z0-9]{12,28}$') 'Manifest target/run is invalid.'
Require ([IO.Path]::GetFullPath([string]$manifest.ExistingPrivateRoot).TrimEnd('\') -ieq $retainedRoot -and
    $manifest.DataSource -ieq '(localdb)\MSSQLLocalDB') 'Manifest retained root or SQL instance is unexpected.'
$runDirectory = [IO.Path]::GetFullPath((Join-Path $retainedRoot ('runs\' + $manifest.RunId)))
Require ($manifest.BusinessDate -ceq '2026-10-09' -and $manifest.ExpectedSummaryRevision -eq 0 -and
    $manifest.MonthLockYear -eq 2026 -and $manifest.MonthLockMonth -eq 10 -and
    $manifest.WorkerEnabled -eq $false -and $manifest.HistoricalFixtureWrites -eq $false) 'This runner supports only the reviewed October factual slice.'
Require ($manifest.WebRoleProfile -ceq 'existing-three-attendance-factual-v1' -and
    [string]$manifest.WebRunnerSha256 -cmatch '^[A-F0-9]{64}$' -and
    (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash -ceq $manifest.WebRunnerSha256) 'Reviewed web role profile or runner hash differs.'
$httpHelper = Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1'
Require ([string]$manifest.HttpHelperSha256 -cmatch '^[A-F0-9]{64}$' -and
    (Get-FileHash -LiteralPath $httpHelper -Algorithm SHA256).Hash -ceq $manifest.HttpHelperSha256) 'Reviewed HTTP helper changed.'
foreach ($name in @('FixtureManifestSha256','AccessResultsSha256','SettingsSha256')) {
    Require ([string]$manifest.$name -cmatch '^[A-F0-9]{64}$') 'Reviewed input hash is missing.'
}
$fixture = Read-PrivateJson (Join-Path $retainedRoot 'attendance-manifest.json') $manifest.FixtureManifestSha256
$oldAccess = Read-PrivateJson (Join-Path $retainedRoot 'access-results.json') $manifest.AccessResultsSha256
Require ($fixture.RunId -ceq $manifest.ExistingRunId -and $fixture.Committed -eq $true -and $fixture.Database -ceq 'Cosmix_Testing') 'Retained fixture provenance mismatch.'
$settingsPath = Private-Path (Join-Path $retainedRoot 'settings.json')
$settings = Read-PrivateJson $settingsPath $manifest.SettingsSha256
Require ($settings.LocalTesting.Enabled -eq $true -and $settings.AttendanceJobs.Enabled -ne $true -and $settings.AttendanceZones.Enabled -ne $true -and
    $settings.AttendanceZones.RetentionEnabled -ne $true -and $settings.ZoneLocationStorage.Enabled -ne $true -and
    $settings.MobileAuthentication.Sms.Provider -ceq 'LocalTesting' -and
    -not $settings.MobileAuthentication.Sms.Endpoint -and -not $settings.MobileAuthentication.Sms.BearerToken) 'Guarded local mode and explicitly disabled attendance worker required.'
$connection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$connection.set_ConnectionString([string]$settings.ConnectionStrings.CosmixDatabase)
Require ($connection.get_DataSource() -ieq '(localdb)\MSSQLLocalDB' -and $connection.get_InitialCatalog() -ceq 'Cosmix_Testing' -and
    $connection.get_IntegratedSecurity() -and -not $connection.get_AttachDBFilename() -and -not $connection.get_UserID() -and -not $connection.get_Password()) 'Unexpected database configuration.'
# The original settings/key/object root are reused unchanged. The owned host must explicitly
# pin AttendanceJobs:Enabled=false; a missing old setting is not proof against environment overrides.
$roleDefinitions = @(
    @{ Key = 'EMPLOYEE'; Scope = 2; Target = [string]$fixture.EmployeeId; Account = [string]$fixture.AdminAccountId;
        Permissions = @('attendance.corrections.submit','attendance.corrections.review','attendance.corrections.decide','attendance.summaries.read','attendance.summaries.calculate','attendance.reports.read','attendance.reports.export') },
    @{ Key = 'MONTH'; Scope = 7; Target = $null; Account = [string]$fixture.AdminAccountId;
        Permissions = @('attendance.month-locks.read','attendance.month-locks.finalize') },
    @{ Key = 'SELF'; Scope = 1; Target = $null; Account = [string]$fixture.HrAccountId;
        Permissions = @('attendance.corrections.self','attendance.summaries.self') }
)
Require (@($manifest.NewRoles).Count -eq 3) 'Manifest must explicitly describe all three new roles.'
foreach($definition in $roleDefinitions) {
    $described=@($manifest.NewRoles | Where-Object CodeSuffix -ceq $definition.Key)
    Require ($described.Count -eq 1 -and
        (@($described[0].Permissions | Sort-Object) -join '|') -ceq (@($definition.Permissions | Sort-Object) -join '|') -and
        $described[0].AssignedAccount -ceq $(if($definition.Key -eq 'SELF') {'retained HrAccountId'} else {'retained AdminAccountId'}) -and
        $described[0].ScopeKind -ceq $(if($definition.Scope -eq 1) {'Self'} elseif($definition.Scope -eq 2) {'Employee'} else {'All'}) -and
        $described[0].Target -ceq $(if($definition.Scope -eq 1) {'server-derived HR employee'} elseif($definition.Scope -eq 2) {'retained EmployeeId (HR)'} else {$null})) 'Manifest role description does not match executable permissions/scopes.'
}
$executionDirectory = $runDirectory
$schemaManifestHash = $manifestHash
$priorRoles = @(); $priorAssignments = @()
if ($ResumeAfterGrants) {
    $schemaManifestHash = '5F437443725510C86FA1309639DEA6E7700CD0E3AC08D17E940FF058AC2FBCE3'
    Require ($manifest.RunId -ceq 'ATT20261012R45B3DDA713' -and
        $manifest.ReconciledWebManifestSha256 -ceq $schemaManifestHash) 'Only the exact reconciled post-grant run may continue.'
    $null = Private-Path $runDirectory
    $original = Read-PrivateJson (Join-Path $runDirectory 'approved-manifest.json')
    # Runner hashes may change for this repair; all original data/action/runtime bindings stay fixed.
    foreach ($name in @('SchemaVersion','RunId','Database','DataSource','ExistingRunId','ExistingPrivateRoot',
        'BaselineMigration','TargetMigration','MigrationScript','MigrationScriptSha256','ReconciledPriorRunId',
        'FixtureManifestSha256','AccessResultsSha256','SettingsSha256','ApiAssemblySha256','RuntimeAssemblySha256',
        'HttpHelperSha256','WebRoleProfile','BusinessDate','ExpectedSummaryRevision','MonthLockYear','MonthLockMonth',
        'WorkerEnabled','HistoricalFixtureWrites','NewRoles')) {
        Require ((ConvertTo-Json -InputObject $original.$name -Depth 20 -Compress) -ceq
            (ConvertTo-Json -InputObject $manifest.$name -Depth 20 -Compress)) 'Continuation changed an original action or runtime binding.'
    }
    $originalStart = Read-PrivateJson (Join-Path $runDirectory 'web-execution-started.json')
    Require ($originalStart.RunId -ceq $manifest.RunId -and $originalStart.ManifestSha256 -ceq $schemaManifestHash) 'Original web marker mismatch.'
    $originalSchema = Read-PrivateJson (Join-Path $runDirectory 'schema-result.json')
    Require ($originalSchema.RunId -ceq $manifest.RunId -and $originalSchema.Database -ceq 'Cosmix_Testing' -and
        $originalSchema.TargetMigration -ceq '20261012082833_RetainZoneWarningTransitions' -and
        $originalSchema.ManifestSha256 -ceq $schemaManifestHash -and $originalSchema.FixturesChanged -eq $false -and
        $originalSchema.PermissionsChanged -eq $false -and $originalSchema.KeysChanged -eq $false) 'Original completed schema receipt mismatch.'
    $originalJournalPath = Private-Path (Join-Path $runDirectory 'web-journal.jsonl')
    $originalJournalItem = Get-Item -LiteralPath $originalJournalPath
    Require (-not $originalJournalItem.PSIsContainer -and $originalJournalItem.Length -gt 0 -and
        $originalJournalItem.Length -le 1048576) 'Original journal size/type is invalid.'
    $originalJournal = @(Get-Content -LiteralPath $originalJournalPath | ForEach-Object { ConvertFrom-Json -InputObject $_ -Depth 20 })
    $priorRoles = @($originalJournal | Where-Object Kind -ceq 'RoleCreated')
    $priorAssignments = @($originalJournal | Where-Object Kind -ceq 'Assigned')
    Require ($originalJournal.Count -le 200 -and $priorRoles.Count -eq 3 -and $priorAssignments.Count -eq 3 -and
        @($originalJournal | Where-Object Kind -ceq 'Stopped').Count -eq 1 -and
        @($originalJournal | Where-Object Kind -ceq 'Passed').Count -eq 21 -and
        $originalJournal[-1].Kind -ceq 'Stopped' -and $originalJournal[-1].Metadata.Passed -eq 21 -and
        @($originalJournal | Where-Object { $_.Kind -in @('SummarySaved','Completed') }).Count -eq 0 -and
        @($originalJournal | Where-Object { $_.Kind -eq 'WriteIntent' -and $_.Metadata.Path -notlike '/api/v1/access/*' }).Count -eq 0) 'Original journal is not the exact pre-summary stop.'
    foreach ($definition in $roleDefinitions) {
        $recordedRole = @($priorRoles | Where-Object Label -ceq $definition.Key)
        $recordedAssignment = @($priorAssignments | Where-Object Label -ceq $definition.Key)
        Require ($recordedRole.Count -eq 1 -and $recordedAssignment.Count -eq 1 -and
            $recordedRole[0].Metadata.Code -ceq ($manifest.RunId+'_'+$definition.Key) -and
            $recordedAssignment[0].Metadata.RoleId -ceq $recordedRole[0].Metadata.RoleId -and
            $recordedAssignment[0].Metadata.AccountId -ceq $definition.Account -and
            $recordedAssignment[0].Metadata.Scope -eq $definition.Scope -and
            $recordedAssignment[0].Metadata.TargetId -ceq $definition.Target) 'Original recorded grant does not match the reviewed role.'
    }
    $executionDirectory = Join-Path $runDirectory 'web-continuation-1'
    if ($Mode -eq 'Inspect') { Require (-not (Test-Path -LiteralPath $executionDirectory)) 'Continuation already exists; reconcile it instead of repeating.' }
}
if ($Mode -eq 'Inspect') {
    [pscustomobject]@{ Mode = 'OfflineInspect'; ManifestSha256 = $manifestHash; RunId = $manifest.RunId;
        NewRoleCount = $(if ($ResumeAfterGrants) { 0 } else { 3 }); NewAssignmentCount = $(if ($ResumeAfterGrants) { 0 } else { 3 });
        ResumeAfterGrants = [bool]$ResumeAfterGrants; SummaryRevisionsAtMost = 1; WorkerEnabled = $false;
        HistoricalDutyOrLockFixtures = $false; SqlExecuted = $false; HttpExecuted = $false }
    return
}
Require ($ApprovedManifestSha256 -and $ApprovedManifestSha256.ToUpperInvariant() -ceq $manifestHash) 'Separate approval must name the exact reviewed manifest hash.'
$null=Private-Path $runDirectory
$upgrade = Read-PrivateJson (Join-Path $runDirectory 'schema-result.json')
Require ($upgrade.RunId -ceq $manifest.RunId -and $upgrade.Database -ceq 'Cosmix_Testing' -and
    $upgrade.TargetMigration -ceq $manifest.TargetMigration -and $upgrade.ManifestSha256 -ieq $schemaManifestHash -and
    $upgrade.FixturesChanged -eq $false -and $upgrade.PermissionsChanged -eq $false -and $upgrade.KeysChanged -eq $false) 'Completed exact schema/preflight receipt is missing or outside this narrow slice.'
$null=Private-Path $executionDirectory
$hostReceipt = Read-PrivateJson (Join-Path $executionDirectory 'host.json')
Require ($hostReceipt.RunId -ceq $manifest.RunId -and $hostReceipt.SettingsSha256 -ceq $manifest.SettingsSha256 -and
    $hostReceipt.BaseUri -ceq 'https://localhost:7249' -and $hostReceipt.AttendanceJobsEnabled -eq $false -and
    $hostReceipt.ManifestSha256 -ceq $manifestHash -and $hostReceipt.ResumeAfterGrants -eq [bool]$ResumeAfterGrants) 'Host does not match the approved run settings.'
$hostProcess = Get-Process -Id ([int]$hostReceipt.ProcessId) -ErrorAction Stop
Require ($hostProcess.ProcessName -ceq 'dotnet' -and
    [Math]::Abs(($hostProcess.StartTime.ToUniversalTime() - ([DateTimeOffset]$hostReceipt.StartedAtUtc).UtcDateTime).TotalSeconds) -lt 2) 'Owned process identity changed.'
$startedPath = Private-Path (Join-Path $executionDirectory 'web-execution-started.json') $false
$journalPath = Private-Path (Join-Path $executionDirectory 'web-journal.jsonl') $false
Require (-not (Test-Path -LiteralPath $startedPath) -and -not (Test-Path -LiteralPath $journalPath)) 'Execution already started; reconcile the retained journal instead of rerunning.'
$started = [IO.File]::Open($startedPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
try { $bytes = [Text.Encoding]::UTF8.GetBytes((@{RunId=$manifest.RunId;ManifestSha256=$manifestHash;AtUtc=[DateTimeOffset]::UtcNow.ToString('O')} | ConvertTo-Json)); $started.Write($bytes) } finally { $started.Dispose() }
$journalStream = [IO.File]::Open($journalPath,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::Read)
$journal = [IO.StreamWriter]::new($journalStream,[Text.UTF8Encoding]::new($false)); $journal.AutoFlush = $true
$checks = 0
function Record([string]$Kind,[string]$Label,$Metadata = $null) {
    $journal.WriteLine((@{Kind=$Kind;Label=$Label;AtUtc=[DateTimeOffset]::UtcNow.ToString('O');Metadata=$Metadata} | ConvertTo-Json -Depth 10 -Compress))
}
function Check([bool]$Condition,[string]$Label) { Require $Condition ("Acceptance failed: " + $Label + '; response suppressed.'); $script:checks++; Record 'Passed' $Label }
function Request([string]$Label,$Client,[string]$Method,[string]$Path,$Body,[int]$Status = 200,[string]$Code = '',[string]$Key = '',[switch]$WithoutCsrf) {
    $arguments = @{Client=$Client;Method=$Method;Path=$Path;WithoutCsrf=$WithoutCsrf}
    if ($null -ne $Body) { $arguments.Body=$Body }; if ($Key) { $arguments.Headers=@{'Idempotency-Key'=$Key} }
    if ($Method -ne 'GET') { Record 'WriteIntent' $Label @{Method=$Method;Path=$Path;IdempotencyKey=$Key} }
    $response = Invoke-LocalAcceptanceRequest @arguments
    Record 'HttpStatus' $Label @{Method=$Method;Path=$Path;ActualStatus=[int]$response.StatusCode;ExpectedStatus=$Status}
    Check ($response.StatusCode -eq $Status) "$Label HTTP $Status"
    if ($Code) { Check ($response.Body.code -ceq $Code -or $response.Body.title -ceq $Code) "$Label error code" }
    return $response.Body
}
function Pages($Client,[string]$Path) {
    $items=@(); for($page=1;$page -le 10;$page++) {
        $response=Request 'bounded metadata' $Client GET "${Path}?page=$page&pageSize=100" $null
        $items+=@($response.items); if($items.Count -ge $response.totalCount) { return $items }
    }; throw 'Metadata exceeded acceptance scan bound.'
}
. $httpHelper
$accounts=$null; $super=$null; $admin=$null; $hr=$null
try {
    $accounts=@(Read-PrivateJson (Join-Path $retainedRoot 'Test-Accounts.json'))
    Check ($accounts.Count -eq 3 -and @($accounts.Email | Sort-Object -Unique).Count -eq 3 -and
        @($accounts | Where-Object Email -notin @('superadmin@example.invalid','admin@example.invalid','hr@example.invalid')).Count -eq 0) 'exact three retained synthetic identities'
    function Login([string]$Email) { New-LocalAcceptanceSession -BaseUri https://localhost:7249 -Account ($accounts | Where-Object Email -ceq $Email) -SkipCertificateCheck:$SkipCertificateCheck }
    $super=Login 'superadmin@example.invalid'; $admin=Login 'admin@example.invalid'; $hr=Login 'hr@example.invalid'
    $oldAssignmentId=[string]$oldAccess.Grants.AdminRoster.assignment.id
    $oldGrant=@(Pages $super "/api/v1/access/accounts/$($fixture.AdminAccountId)/assignments" | Where-Object id -eq $oldAssignmentId)
    Check ($oldGrant.Count -eq 1 -and $null -ne $oldGrant[0].revokedAt -and
        $oldGrant[0].scopes[0].targetId -eq $fixture.SiteId) 'prior SiteA roster revocation remains in history'
    $prefix=[string]$manifest.RunId
    $dedicatedRoles = @(Pages $super '/api/v1/access/roles' | Where-Object { $_.code.StartsWith($prefix + '_',[StringComparison]::Ordinal) })
    if ($ResumeAfterGrants) {
        Check ($dedicatedRoles.Count -eq 3) 'exact three original dedicated roles retained'
        foreach ($definition in $roleDefinitions) {
            $recordedRole = @($priorRoles | Where-Object Label -ceq $definition.Key)[0].Metadata
            $recordedAssignment = @($priorAssignments | Where-Object Label -ceq $definition.Key)[0].Metadata
            $roles = @($dedicatedRoles | Where-Object id -ceq $recordedRole.RoleId)
            Check ($roles.Count -eq 1 -and $roles[0].code -ceq ($prefix+'_'+$definition.Key) -and
                $roles[0].isActive -eq $true -and $roles[0].isSystemRole -eq $false -and
                (@($roles[0].permissions | Sort-Object) -join '|') -ceq (@($definition.Permissions | Sort-Object) -join '|')) 'retained dedicated role has exact permissions'
            $assignments = @(Pages $super "/api/v1/access/accounts/$($definition.Account)/assignments" | Where-Object roleId -ceq $recordedRole.RoleId)
            Check ($assignments.Count -eq 1 -and $assignments[0].id -ceq $recordedAssignment.AssignmentId -and
                $assignments[0].roleCode -ceq ($prefix+'_'+$definition.Key) -and $null -eq $assignments[0].revokedAt -and
                $null -eq $assignments[0].effectiveTo -and ([DateTimeOffset]$assignments[0].effectiveFrom) -le [DateTimeOffset]::UtcNow -and
                @($assignments[0].scopes).Count -eq 1 -and $assignments[0].scopes[0].kind -eq $definition.Scope -and
                $assignments[0].scopes[0].targetId -ceq $definition.Target) 'retained dedicated assignment has exact active scope'
            Record 'ExistingGrantVerified' $definition.Key @{RoleId=$recordedRole.RoleId;AssignmentId=$recordedAssignment.AssignmentId}
        }
    } else {
        Check ($dedicatedRoles.Count -eq 0) 'new dedicated roles have no previous partial execution'
        foreach($definition in $roleDefinitions) {
            $role=Request 'create new attendance role' $super POST '/api/v1/access/roles' @{code=($prefix+'_'+$definition.Key);name=($prefix+' '+$definition.Key)} -Key "$prefix-role-$($definition.Key)"
            Record 'RoleCreated' $definition.Key @{RoleId=$role.id;Code=$role.code}
            $role=Request 'set only new role permissions' $super POST "/api/v1/access/roles/$($role.id)/permissions" @{
                permissions=$definition.Permissions;expectedRoleVersion=$role.version;reason="$prefix isolated attendance acceptance"} -Key "$prefix-permissions-$($definition.Key)"
            $account=Request 'read assignment target version' $super GET "/api/v1/access/accounts/$($definition.Account)" $null
            $assignment=Request 'assign dedicated attendance role' $super POST "/api/v1/access/accounts/$($definition.Account)/assignments" @{
                roleId=$role.id;scopes=@(@{kind=$definition.Scope;targetId=$definition.Target});effectiveFrom=[DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('O');effectiveTo=$null;
                expectedAccountVersion=$account.version;expectedRoleVersion=$role.version;reason="$prefix isolated attendance acceptance"} -Key "$prefix-assignment-$($definition.Key)"
            Record 'Assigned' $definition.Key @{RoleId=$role.id;AccountId=$definition.Account;AssignmentId=$assignment.assignment.id;Scope=$definition.Scope;TargetId=$definition.Target}
        }
        Request 'old Admin session revoked by assignment' $admin GET '/api/v1/me/permissions' $null 401 | Out-Null
        Request 'old HR session revoked by assignment' $hr GET '/api/v1/me/permissions' $null 401 | Out-Null
        $admin=Login 'admin@example.invalid'; $hr=Login 'hr@example.invalid'
    }
    $employee=[string]$fixture.EmployeeId; $date=[string]$manifest.BusinessDate
    $adminState=Request 'read known other employee identity' $super GET "/api/v1/access/accounts/$($fixture.AdminAccountId)" $null
    $other=[string]$adminState.employeeId
    Require ($other -and $other -ne $employee) 'Distinct existing denied employee required.'
    $daily="/api/v1/attendance/employees/$employee/daily"
    Request 'HR denied management daily read' $hr GET ($daily+"?businessDate=$date") $null 403 | Out-Null
    Request 'unassigned Admin employee source is unavailable; not a scope assertion' $admin GET "/api/v1/attendance/employees/$other/correction-requests/sources?businessDate=$date" $null 409 'employee_assignment_unavailable' | Out-Null
    $deniedSite = [string]$fixture.DeniedSiteId
    Require ($deniedSite -and ([guid]$deniedSite) -ne [guid]::Empty -and $deniedSite -ne [string]$fixture.SiteId) 'Distinct retained denied site required.'
    Request 'Admin denied other site correction review queue' $admin GET "/api/v1/attendance/corrections/review?from=$date&to=2026-10-10&scope=3&targetId=$deniedSite" $null 403 | Out-Null
    $before=Request 'authorized factual daily read' $admin GET ($daily+"?businessDate=$date") $null
    Check ($null -eq $before.latestSavedRevision -and $before.facts.openSessions -eq 0) 'no prior summary or open duty fixture'
    $body=@{businessDate=$date;expectedRevision=0;reason="$prefix factual-only acceptance, no pay result"}
    $key=$prefix+':summary'
    Request 'missing CSRF prevents factual write' $admin POST ($daily+'/recalculate') $body 400 'invalid_csrf_token' $key -WithoutCsrf | Out-Null
    $saved=Request 'save one factual immutable revision' $admin POST ($daily+'/recalculate') $body 200 '' $key
    Check ($saved.revision -eq 1 -and $saved.status -ceq 'factual_only' -and $null -eq $saved.policyVersionId -and
        $null -eq $saved.payableMinutes -and $null -eq $saved.lateMinutes -and $null -eq $saved.overtimeMinutes) 'factual revision has no invented financial outputs'
    Record 'SummarySaved' 'exact reviewable revision' @{Id=$saved.id;Revision=$saved.revision;EmployeeId=$employee;BusinessDate=$date}
    $replayed=Request 'same-key immutable receipt replay' $admin POST ($daily+'/recalculate') $body 200 '' $key
    Check ($replayed.id -eq $saved.id -and $replayed.revision -eq $saved.revision) 'receipt replay returns same summary'
    $changed=@{businessDate=$date;expectedRevision=0;reason="$prefix changed retry evidence"}
    Request 'changed reason cannot reuse receipt key' $admin POST ($daily+'/recalculate') $changed 409 'idempotency_key_reused' $key | Out-Null
    Request 'stale expected revision cannot append' $admin POST ($daily+'/recalculate') $body 409 'attendance_summary_revision_conflict' ($prefix+':stale') | Out-Null
    $own=Request 'HR self identity resolves stored factual day' $hr GET "/api/v1/me/attendance/daily?businessDate=$date" $null
    Check ($own.employeeId -eq $employee -and $own.latestSavedRevision.id -eq $saved.id) 'self identity matches original HR employee'
    Request 'scoped review queue' $admin GET "/api/v1/attendance/corrections/review?from=$date&to=2026-10-10&scope=2&targetId=$employee" $null | Out-Null
    Request 'carry source unavailable without real source lock' $admin GET "/api/v1/hr/employees/$employee/attendance-carry-forward/sources?businessDate=$date" $null 409 'carry_forward_source_lock_unavailable' | Out-Null
    Request 'worker-disabled export explicitly unavailable' $admin POST '/api/v1/attendance/reports/exports' @{employeeIds=@($employee);from=$date;to=$date;format=1;reason="$prefix no worker"} 503 'attendance_export_worker_not_enabled' ($prefix+':export') | Out-Null
    Request 'HR denied global month preview' $hr GET '/api/v1/attendance/month-locks/2026/10/preview' $null 403 | Out-Null
    $monthReadinessVerified = $false
    $previewPath = '/api/v1/attendance/month-locks/2026/10/preview'
    $previewResponse = Invoke-LocalAcceptanceRequest -Client $admin -Method GET -Path $previewPath
    Record 'HttpStatus' 'global month readiness is explicit' @{Method='GET';Path=$previewPath;ActualStatus=[int]$previewResponse.StatusCode;ExpectedStatus=200}
    if ($previewResponse.StatusCode -eq 409 -and
        ($previewResponse.Body.code -ceq 'employee_assignment_unavailable' -or $previewResponse.Body.title -ceq 'employee_assignment_unavailable')) {
        # Retained synthetic accounts do not all have duty assignments. Do not invent history
        # or label an unavailable global population as verified month readiness/finalization.
        Record 'NotExercised' 'global month readiness and not-ready finalization' @{
            Reason='fixture_employee_assignment_unavailable';MonthReadinessVerified=$false;FinalizeRequested=$false}
    } else {
        Check ($previewResponse.StatusCode -eq 200) 'global month readiness is explicit HTTP 200'
        $preview = $previewResponse.Body
        Check ($preview.ready -eq $false -and $preview.state -ceq 'not_ready' -and $preview.issueCount -gt 0) 'unfinished or policy-incomplete month cannot finalize'
        Request 'not-ready month remains unlocked' $admin POST '/api/v1/attendance/month-locks/2026/10/finalize' @{
            expectedPreviewFingerprint=$preview.previewFingerprint;expectedVersion=$preview.version;reason="$prefix expected not-ready probe"} 409 'attendance_month_not_ready' ($prefix+':month') | Out-Null
        $monthReadinessVerified = $true
    }
    $oldGrant=@(Pages $super "/api/v1/access/accounts/$($fixture.AdminAccountId)/assignments" | Where-Object id -eq $oldAssignmentId)
    Check ($oldGrant.Count -eq 1 -and $null -ne $oldGrant[0].revokedAt) 'old SiteA grant was not restored'
    Record 'Completed' 'narrow web acceptance' @{Passed=$checks;NewRoles=$(if ($ResumeAfterGrants) {0} else {3});NewAssignments=$(if ($ResumeAfterGrants) {0} else {3});
        ResumedAfterGrants=[bool]$ResumeAfterGrants;PreviouslyPassed=$(if ($ResumeAfterGrants) {21} else {0});SummaryRevisions=1;WorkerEnabled=$false;ManualDutyAccepted=$false;
        MonthReadinessVerified=$monthReadinessVerified;MonthLocked=$false;CarryForwardAccepted=$false}
    Write-Output "Narrow web acceptance: $checks checks passed. Month readiness/not-ready finalization verified: $monthReadinessVerified. Historical duty, lock success, carry-forward success and export generation were not exercised. Private journal retained."
} catch {
    Record 'Stopped' 'manual reconciliation required; no automatic retry' @{Passed=$checks}
    throw 'Narrow web acceptance stopped. Inspect the private journal; do not rerun or reset. Sensitive details suppressed.'
} finally {
    $accounts=$null; $super=$null; $admin=$null; $hr=$null; $settings=$null; $baselineSettings=$null; $connection=$null
    $journal.Dispose()
}
