[CmdletBinding()]
param(
    [ValidateSet('GrantSiteB', 'Run', 'Replay')][string]$Phase = 'Run',
    [object]$AdminClient,
    [object]$SuperAdminClient,
    [Parameter(Mandatory)][string]$FixturePath,
    [Parameter(Mandatory)][string]$AccessEvidencePath,
    [Parameter(Mandatory)][string]$SchedulingEvidencePath,
    [Parameter(Mandatory)][string]$EvidencePath
)

# Root invokes this only after the main Run and restart Replay have completed.
# All database changes use actual authenticated HTTP services. No SQL/setup/credentials here.
# GrantSiteB invalidates Admin sessions; obtain a new real MFA session before Run.
# Site A keeps its original role assignment so its later revocation test remains independent.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')
$privateRoot = 'C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009'
function PrivatePath([string]$Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path)) { throw 'Require an absolute private acceptance path.' }
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($privateRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must remain inside the approved private root.' }
    $parent = Get-Item -LiteralPath ([IO.Path]::GetDirectoryName($full)) -Force
    for ($cursor = $parent; $null -ne $cursor; $cursor = $cursor.Parent) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked private paths are not permitted.' }
    }
    if (Test-Path -LiteralPath $full) {
        $item = Get-Item -LiteralPath $full -Force
        if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Evidence must be a regular private file.' }
    }
    return $full
}
$FixturePath = PrivatePath $FixturePath
$AccessEvidencePath = PrivatePath $AccessEvidencePath
$SchedulingEvidencePath = PrivatePath $SchedulingEvidencePath
$EvidencePath = PrivatePath $EvidencePath
$grantPath = PrivatePath ($EvidencePath + '.grant.json')
$fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json -Depth 30
$access = Get-Content -LiteralPath $AccessEvidencePath -Raw | ConvertFrom-Json -Depth 40
$main = Get-Content -LiteralPath $SchedulingEvidencePath -Raw | ConvertFrom-Json -Depth 80
if ($fixture.Database -cne 'Cosmix_Testing' -or $fixture.Committed -ne $true -or
    $fixture.RunId -cnotmatch '^ACC[A-Z0-9]{6,18}$' -or $main.runId -cne $fixture.RunId -or
    $main.completed -ne $true -or $main.lastPhase -cne 'Replay') { throw 'Require committed synthetic fixtures and the completed main scheduling restart Replay.' }
$runId = [string]$fixture.RunId
$siteA = ([guid]$fixture.SiteId).ToString()
$siteB = ([guid]$fixture.DeniedSiteId).ToString()
$employeeId = ([guid]$fixture.EmployeeId).ToString()
$adminId = ([guid]$fixture.AdminAccountId).ToString()
$originalGrantId = ([guid]$access.Grants.AdminRoster.assignment.id).ToString()
$roleId = ([guid]$access.Roles.RosterSite.id).ToString()
if (@($siteA, $siteB, $employeeId, $adminId, $originalGrantId, $roleId) -contains [guid]::Empty.ToString() -or $siteA -eq $siteB) { throw 'Invalid fixture identifiers.' }
$today = [DateOnly]::FromDateTime([TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow, 'Asia/Karachi').DateTime)
if ($today -ne [DateOnly]::Parse('2026-10-09')) { throw 'Review this dated acceptance scenario before another business day.' }
$from = $today.AddDays(15)
$transfer = $today.AddDays(20)
$end = $today.AddDays(25)
$holidayDate = $today.AddDays(18)
$checks = 0
function Json($Value) { ConvertTo-Json -InputObject $Value -Depth 80 -Compress }
function Check([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "Boundary acceptance failed: $Name. Private evidence retained; do not blindly rerun." }
    $script:checks++
}
function Save {
    $script:state.checks = $script:checks
    $script:state | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $script:activeEvidencePath -Encoding utf8
}
function Http([object]$Client, [string]$Method, [string]$Path, [object]$Body, [string]$Key,
    [int]$Status, [string]$Name, [string]$ErrorCode = '', [switch]$KeepReceipt) {
    if ($null -eq $Client) { throw 'A genuine authenticated client is required for this phase.' }
    $arguments = @{ Client = $Client; Method = $Method; Path = $Path }
    if ($null -ne $Body) { $arguments.Body = $Body }
    if ($Key) { $arguments.Headers = @{ 'Idempotency-Key' = $Key } }
    $script:state.pending = [ordered]@{ name = $Name; method = $Method; path = $Path; key = $Key; request = $Body }
    Save
    $response = Invoke-LocalAcceptanceRequest @arguments
    $script:state.observations += [ordered]@{ name = $Name; status = $response.StatusCode }
    Save
    Check ($response.StatusCode -eq $Status) "$Name expected HTTP $Status received $($response.StatusCode)"
    if ($ErrorCode) { Check ($response.Body.title -ceq $ErrorCode) "$Name exact conflict reason" }
    if ($KeepReceipt) {
        $script:state.receipts += [ordered]@{ name = $Name; method = $Method; path = $Path; key = $Key;
            request = $Body; expectedStatus = $Status; expected = $response.Body }
    }
    $script:state.pending = $null
    Save
    return $response.Body
}
function AllItems([object]$Client, [string]$Path) {
    $items = @()
    for ($page = 1; $page -le 10; $page++) {
        $result = Http $Client GET "${Path}?page=$page&pageSize=100" $null '' 200 'Read bounded access metadata'
        $items += @($result.items)
        if ($items.Count -ge $result.totalCount) { return $items }
    }
    throw 'Acceptance metadata exceeded its bounded inventory.'
}
function Schedule([DateOnly]$Day, [string]$Start, [string]$EndTime, [int]$Offset) {
    return [ordered]@{ effectiveFrom = $Day.ToString('yyyy-MM-dd'); startTime = $Start; endTime = $EndTime;
        endDayOffset = $Offset; scheduledMinutes = 480; paidBreakMinutes = 0; unpaidBreakMinutes = 0; applicableWeekdays = 127 }
}
function AssignmentBody($Shift, [DateOnly]$Day, [string]$Timeline, [string]$CalendarId) {
    return [ordered]@{ shiftId = $Shift.id; effectiveFrom = $Day.ToString('yyyy-MM-dd'); effectiveTo = $end.ToString('yyyy-MM-dd');
        expectedTimeline = $Timeline; expectedShiftVersion = $Shift.version; holidayCalendarId = $CalendarId;
        reason = "SYNTHETIC $runId transfer-boundary acceptance" }
}

if ($Phase -eq 'GrantSiteB') {
    if (Test-Path -LiteralPath $grantPath) { throw 'Site B grant evidence already exists; inspect it, do not recreate access.' }
    $activeEvidencePath = $grantPath
    $state = [ordered]@{ database = 'Cosmix_Testing'; runId = $runId; phase = $Phase; checks = 0; completed = $false;
        siteId = $siteB; originalSiteAGrantId = $originalGrantId; observations = @(); receipts = @(); pending = $null }
    Save
    $account = Http $SuperAdminClient GET "/api/v1/access/accounts/$adminId" $null '' 200 'Read protected Admin account version'
    $roles = @(AllItems $SuperAdminClient '/api/v1/access/roles' | Where-Object id -eq $roleId)
    Check ($roles.Count -eq 1 -and $roles[0].isActive -and -not $roles[0].isSystemRole) 'Existing immutable roster role is active and ordinary'
    Check ((Json @($roles[0].permissions | Sort-Object)) -ceq (Json @('attendance.rosters.manage', 'attendance.rosters.read'))) 'Existing role has exactly roster read/manage'
    $assignments = @(AllItems $SuperAdminClient "/api/v1/access/accounts/$adminId/assignments")
    $original = @($assignments | Where-Object id -eq $originalGrantId)
    Check ($original.Count -eq 1 -and $null -eq $original[0].revokedAt -and
        @($original[0].scopes).Count -eq 1 -and $original[0].scopes[0].targetId -eq $siteA) 'Original Site A grant is preserved for separate revocation coverage'
    Check (@($assignments | Where-Object { $null -eq $_.revokedAt -and @($_.scopes | Where-Object targetId -eq $siteB).Count -gt 0 }).Count -eq 0) 'No prior Site B access assignment'
    $body = [ordered]@{ roleId = $roleId; scopes = @(@{ kind = 5; targetId = $siteB });
        effectiveFrom = [DateTimeOffset]::UtcNow.AddSeconds(-1).ToString('O'); effectiveTo = $null;
        expectedAccountVersion = $account.version; expectedRoleVersion = $roles[0].version;
        reason = "SYNTHETIC $runId explicit Site B roster authority for retained boundary acceptance" }
    $grant = Http $SuperAdminClient POST "/api/v1/access/accounts/$adminId/assignments" $body '' 200 'SuperAdmin explicitly grants only Site B roster scope'
    Check (@($grant.assignment.scopes).Count -eq 1 -and $grant.assignment.scopes[0].targetId -eq $siteB) 'Created scope is exactly Site B'
    $state.grant = $grant
    $state.completed = $true
    Save
    Write-Output "PASS GrantSiteB: $checks HTTP checks. Reauthenticate Admin before Run; Site A grant was not changed."
    return
}

$grantEvidence = Get-Content -LiteralPath $grantPath -Raw | ConvertFrom-Json -Depth 40
if ($grantEvidence.runId -cne $runId -or -not $grantEvidence.completed -or $grantEvidence.siteId -ne $siteB) { throw 'Require completed explicit Site B grant evidence for this exact fixture.' }
$activeEvidencePath = $EvidencePath
if ($Phase -eq 'Replay') {
    $state = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json -Depth 80 -AsHashtable
    if ($state.runId -cne $runId -or -not $state.completed) { throw 'Only completed boundary evidence can be replayed.' }
    $checks = [int]$state.checks
    foreach ($receipt in @($state.receipts)) {
        $result = Http $AdminClient $receipt.method $receipt.path $receipt.request $receipt.key $receipt.expectedStatus "Replay $($receipt.name)"
        Check ((Json $result) -ceq (Json $receipt.expected)) 'Stored successful mutation result remains frozen'
    }
    $state.lastPhase = 'Replay'
    Save
    Write-Output "PASS Boundary Replay: $checks cumulative HTTP checks."
    return
}
if (Test-Path -LiteralPath $EvidencePath) { throw 'Boundary evidence already exists. Preserve partial evidence; do not rerun writes.' }
$state = [ordered]@{ database = 'Cosmix_Testing'; runId = $runId; phase = $Phase; lastPhase = 'Run'; checks = 0;
    siteA = $siteA; siteB = $siteB; employeeId = $employeeId; from = $from.ToString('yyyy-MM-dd');
    transfer = $transfer.ToString('yyyy-MM-dd'); end = $end.ToString('yyyy-MM-dd');
    completed = $false; observations = @(); receipts = @(); pending = $null; created = [ordered]@{} }
Save
$permissions = @(Http $AdminClient GET '/api/v1/me/permissions' $null '' 200 'Current Admin permissions after real reauthentication')
foreach ($permission in @('attendance.rosters.read', 'attendance.rosters.manage')) {
    $relevant = @($permissions | Where-Object permission -ceq $permission)
    Check (@($relevant | Where-Object { $_.scopeKind -eq 5 -and $_.targetId -eq $siteA -and $_.roleAssignmentId -eq $originalGrantId }).Count -eq 1) 'Site A still uses its sole original grant'
    Check (@($relevant | Where-Object { $_.scopeKind -eq 5 -and $_.targetId -eq $siteB -and $_.roleAssignmentId -eq $grantEvidence.grant.assignment.id }).Count -eq 1) 'Site B uses its separate explicit grant'
    Check ($relevant.Count -eq 2) 'No wider roster authority was introduced'
}
$employeePath = "/api/v1/hr/employees/$employeeId"
$aPath = "/api/v1/hr/sites/$siteA/shift-assignments"
$bPath = "/api/v1/hr/sites/$siteB/shift-assignments"
$jobs = Http $AdminClient GET "$employeePath/assignments/history" $null '' 200 'Read exact synthetic HR membership before transfer'
Check ($jobs.assignments.totalCount -eq 1 -and $jobs.assignments.items[0].id -eq $fixture.EmployeeAssignmentId -and
    $jobs.assignments.items[0].siteId -eq $siteA -and $null -eq $jobs.assignments.items[0].effectiveTo) 'Only the original synthetic HR membership exists'
$historyA = Http $AdminClient GET $aPath $null '' 200 'Read Site A retained earlier schedule'
$historyB = Http $AdminClient GET $bPath $null '' 200 'Read fresh Site B schedule'
Check (@($historyA.assignments | Where-Object { $null -eq $_.effectiveTo -or [DateOnly]::Parse($_.effectiveTo) -gt $from }).Count -eq 0) 'Main schedule has ended before the supplemental range'
Check (@($historyB.assignments).Count -eq 0 -and @($historyB.temporaryChanges).Count -eq 0) 'Site B has no prior scheduling rows'
$transferBody = [ordered]@{ siteId = $siteB; effectiveFrom = $transfer.ToString('yyyy-MM-dd'); expectedVersion = $jobs.version;
    reason = "SYNTHETIC $runId retained future transfer for actual SQL boundary validation" }
$null = Http $AdminClient POST "$employeePath/assignments/site" $transferBody "$runId.boundary.transfer" 200 'Append future HR site transfer through approved service' -KeepReceipt
$jobsAfter = Http $AdminClient GET "$employeePath/assignments/history" $null '' 200 'Read retained HR transfer history'
$prior = @($jobsAfter.assignments.items | Where-Object id -eq $fixture.EmployeeAssignmentId)
$next = @($jobsAfter.assignments.items | Where-Object siteId -eq $siteB)
Check ($jobsAfter.assignments.totalCount -eq 2 -and $prior.Count -eq 1 -and $prior[0].effectiveTo -eq $transfer.ToString('yyyy-MM-dd') -and
    $next.Count -eq 1 -and $next[0].effectiveFrom -eq $transfer.ToString('yyyy-MM-dd') -and $null -eq $next[0].effectiveTo) 'Transfer preserves old identity and adds one contiguous new HR row'
$state.created.membershipId = $next[0].id
$shifts = @()
foreach ($item in @(@('BNIGHT','22:00:00','06:00:00',1), @('BMID','16:00:00','00:00:00',1), @('BDAY','00:00:00','08:00:00',0))) {
    $body = [ordered]@{ code = "$runId.$($item[0])"; name = "SYNTHETIC $runId $($item[0])";
        initialSchedule = (Schedule $from $item[1] $item[2] $item[3]); reason = "SYNTHETIC $runId boundary shift" }
    $shifts += Http $AdminClient POST '/api/v1/hr/shifts' $body "$runId.boundary.$($item[0])" 201 'Create isolated boundary shift' -KeepReceipt
}
$unsafe = $shifts[0]; $midnight = $shifts[1]; $day = $shifts[2]
$state.created.shiftIds = @($shifts | ForEach-Object id)
$calendarBody = [ordered]@{ code = "$runId.BHOL"; name = "SYNTHETIC $runId pinned revision";
    effectiveFrom = $from.ToString('yyyy-MM-dd'); effectiveTo = $end.ToString('yyyy-MM-dd');
    governmentNotificationReference = "SYNTHETIC $runId notification one"; holidays = @(
        @{ date = $holidayDate.ToString('yyyy-MM-dd'); name = 'SYNTHETIC original holiday'; notificationReference = "$runId original day" },
        @{ date = $transfer.ToString('yyyy-MM-dd'); name = 'SYNTHETIC transfer holiday one'; notificationReference = "$runId transfer day" }) }
$calendar = Http $AdminClient POST '/api/v1/hr/holiday-calendars' $calendarBody "$runId.boundary.calendar" 201 'Create actual immutable holiday calendar' -KeepReceipt
$state.created.calendarId = $calendar.calendar.id
$badAssignment = AssignmentBody $unsafe $from $historyA.timeline $calendar.calendar.id
$null = Http $AdminClient POST $aPath $badAssignment "$runId.boundary.bad-assign" 409 'Actual SQL transfer blocks overlapping overnight assignment' 'roster_site_relationship_changed'
$afterDenied = Http $AdminClient GET $aPath $null '' 200 'Read Site A after rejected assignment'
Check ($afterDenied.timeline -ceq $historyA.timeline -and @($afterDenied.assignments).Count -eq @($historyA.assignments).Count) 'Rejected transfer conflict leaves SQL site history unchanged'
$aBody = AssignmentBody $midnight $from $historyA.timeline $calendar.calendar.id
$assignedA = Http $AdminClient POST $aPath $aBody "$runId.boundary.midnight" 201 'Allow duty ending exactly at transfer midnight' -KeepReceipt
$beforeRevision = Http $AdminClient GET "$employeePath/roster?businessDate=$($holidayDate.ToString('yyyy-MM-dd'))" $null '' 200 'Read original pinned holiday context'
Check ($beforeRevision.holiday.calendarId -eq $calendar.calendar.id -and $beforeRevision.holiday.calendarRevision -eq 1 -and
    @($beforeRevision.holiday.holidays).Count -eq 1 -and $beforeRevision.holiday.holidays[0].name -ceq 'SYNTHETIC original holiday') 'First revision is explicitly selected'
$revisionBody = [ordered]@{ name = "SYNTHETIC $runId changed revision"; governmentNotificationReference = "$runId notification two";
    isActive = $true; expectedVersion = $calendar.calendar.version; reason = "SYNTHETIC $runId prove pin stability"; holidays = @(
        @{ date = $transfer.ToString('yyyy-MM-dd'); name = 'SYNTHETIC transfer holiday two'; notificationReference = "$runId new transfer day" }) }
$revised = Http $AdminClient POST "/api/v1/hr/holiday-calendars/$($calendar.calendar.id)/revisions" $revisionBody "$runId.boundary.revise" 201 'Append independent holiday revision' -KeepReceipt
Check ($revised.calendar.revision -eq 2 -and $revised.calendar.supersedesCalendarId -eq $calendar.calendar.id) 'Calendar revision appends an explicit predecessor link'
$pinned = Http $AdminClient GET "$employeePath/roster?businessDate=$($holidayDate.ToString('yyyy-MM-dd'))" $null '' 200 'Resolve original assignment after calendar successor'
Check ((Json $pinned.holiday) -ceq (Json $beforeRevision.holiday)) 'Calendar successor does not rewrite the pinned old holiday context'
Check ($pinned.status -ceq 'scheduled' -and $pinned.holiday.isInformational -and $pinned.holiday.doesNotCancelDuty -and
    $null -ne $pinned.startsAt -and $null -ne $pinned.endsAt) 'Holiday context remains informational and does not cancel duty'
$state.created.calendarRevisionId = $revised.calendar.id
$bBody = AssignmentBody $day $transfer $historyB.timeline $revised.calendar.id
$assignedB = Http $AdminClient POST $bPath $bBody "$runId.boundary.site-b" 201 'Assign Site B immediately after exact midnight boundary' -KeepReceipt
$state.created.assignmentIds = @($assignedA.assignment.id, $assignedB.assignment.id)
$beforeTransfer = Http $AdminClient GET "$employeePath/roster?businessDate=$($transfer.AddDays(-1).ToString('yyyy-MM-dd'))" $null '' 200 'Resolve employee day before transfer'
$onTransfer = Http $AdminClient GET "$employeePath/roster?businessDate=$($transfer.ToString('yyyy-MM-dd'))" $null '' 200 'Resolve employee on transfer date'
Check ($beforeTransfer.siteId -eq $siteA -and $beforeTransfer.explanation.membershipRecordId -eq $fixture.EmployeeAssignmentId -and
    $onTransfer.siteId -eq $siteB -and $onTransfer.explanation.membershipRecordId -eq $next[0].id) 'SQL effective dating resolves both retained HR membership identities'
Check ([DateTimeOffset]$beforeTransfer.endsAt -eq [DateTimeOffset]$onTransfer.startsAt) 'Adjacent duties meet exactly without overnight overlap'
Check ($onTransfer.holiday.calendarId -eq $revised.calendar.id -and $onTransfer.holiday.calendarRevision -eq 2 -and
    @($onTransfer.holiday.holidays).Count -eq 1 -and $onTransfer.holiday.holidays[0].name -ceq 'SYNTHETIC transfer holiday two') 'New assignment selects the explicit successor revision'
$publication = [ordered]@{ expectedVersion = $midnight.version; schedule = (Schedule $holidayDate '22:00:00' '06:00:00' 1);
    reason = "SYNTHETIC $runId reject overnight publication across actual HR transfer" }
$null = Http $AdminClient POST "/api/v1/hr/shifts/$($midnight.id)/versions" $publication "$runId.boundary.bad-version" 409 'Real SQL publication impact query catches future HR transfer' 'roster_site_relationship_changed'
$unchanged = Http $AdminClient GET "/api/v1/hr/shifts/$($midnight.id)" $null '' 200 'Read shift after rejected publication'
Check ($unchanged.version -ceq $midnight.version -and @($unchanged.versions).Count -eq 1 -and $unchanged.versions[0].id -eq $midnight.versions[0].id) 'Rejected publication preserves stored shift version and concurrency state'
$state.completed = $true
Save
Write-Output "PASS Boundary Run: $checks real authenticated HTTP checks covering SQL membership transfer, overnight rejection, midnight adjacency and pinned calendar revisions."
