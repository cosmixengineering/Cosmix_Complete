[CmdletBinding()]
param(
    [Parameter(Mandatory)][object]$Client,
    [object]$AdminClient,
    [object]$DeniedClient,
    [object]$SelfClient,
    [Parameter(Mandatory)][string]$FixturePath,
    [Parameter(Mandatory)][string]$EvidencePath,
    [ValidateSet('Run', 'Resume', 'Replay', 'DeniedReplay')][string]$Phase = 'Run'
)

# Explicitly invoked acceptance only. This script does not initialize a database, grant access,
# create an account, obtain credentials, start a host, or execute SQL. Root owns those steps.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot '../access-provisioning/LocalAcceptanceHttp.ps1')

$privateRoot = [IO.Path]::GetFullPath('C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009')
function Assert-PrivatePath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($privateRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Acceptance fixture/evidence paths must remain inside the approved private acceptance directory.'
    }
    return $full
}
$FixturePath = Assert-PrivatePath $FixturePath
$EvidencePath = Assert-PrivatePath $EvidencePath
$fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json -Depth 30
if ($fixture.database -cne 'Cosmix_Testing' -or $fixture.runId -cnotmatch '^ACC[A-Z0-9]{6,18}$') {
    throw 'Expected an explicit Cosmix_Testing fixture with a bounded synthetic ACC run ID.'
}
$siteId = ([guid]$fixture.siteId).ToString()
$employeeIds = @($fixture.employeeIds | ForEach-Object { ([guid]$_).ToString() })
if ($siteId -eq [guid]::Empty.ToString() -or $employeeIds.Count -lt 1 -or $employeeIds.Count -gt 10 -or
    @($employeeIds | Sort-Object -Unique).Count -ne $employeeIds.Count -or $employeeIds -contains [guid]::Empty.ToString()) {
    throw 'Use one real synthetic fixture site and one to ten distinct synthetic employee IDs.'
}
$from = [DateOnly]::ParseExact([string]$fixture.effectiveFrom, 'yyyy-MM-dd')
$date = [DateOnly]::ParseExact([string]$fixture.businessDate, 'yyyy-MM-dd')
$today = [DateOnly]::FromDateTime([TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTimeOffset]::UtcNow, 'Asia/Karachi').DateTime)
if ($from -le $today -or $date -lt $from.AddDays(3) -or $date -gt $today.AddDays(60)) {
    throw 'Use future start/day dates within sixty days, with businessDate at least three days after effectiveFrom.'
}
$runId = [string]$fixture.runId
$sitePath = "/api/v1/hr/sites/$siteId"
$checks = 0
function Check([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "Acceptance check failed: $Name" }
    $script:checks++
}
function Json([object]$Value) { ConvertTo-Json -InputObject $Value -Depth 80 -Compress }
function Save-Evidence {
    $script:state.checks = $script:checks
    $script:state | ConvertTo-Json -Depth 80 | Set-Content -LiteralPath $EvidencePath -Encoding utf8
}
function Keep-Receipt([string]$Name, [string]$Kind, [string]$Method, [string]$Path, [string]$Key,
    [object]$Body, [int]$Status, [object]$Result) {
    $script:state.receipts += [ordered]@{ name = $Name; clientKind = $Kind; method = $Method; path = $Path;
        key = $Key; request = (Json $Body | ConvertFrom-Json -Depth 80 -AsHashtable); expectedStatus = $Status;
        expected = (Json $Result | ConvertFrom-Json -Depth 80 -AsHashtable) }
}
function Http([string]$Method, [string]$Path, [object]$Body, [string]$Key, [int]$Status,
    [string]$Name, [string]$Kind = 'Scheduler', [switch]$KeepReceipt, [switch]$WithoutCsrf) {
    $selected = switch ($Kind) { 'Admin' { $AdminClient } 'Denied' { $DeniedClient } 'Self' { $SelfClient } default { $Client } }
    if ($null -eq $selected) { throw 'This check requires a genuine current fixed Admin/SuperAdmin client.' }
    $headers = @{}
    if ($Key) { $headers['Idempotency-Key'] = $Key }
    $requestArguments = @{ Client = $selected; Method = $Method; Path = $Path; Headers = $headers }
    if ($null -ne $Body) { $requestArguments.Body = $Body }
    if ($WithoutCsrf) { $requestArguments.WithoutCsrf = $true }
    $result = Invoke-LocalAcceptanceRequest @requestArguments
    Check ($result.StatusCode -eq $Status) "$Name (expected HTTP $Status, got $($result.StatusCode))"
    $script:state.observations += [ordered]@{ name = $Name; status = $result.StatusCode }
    if ($KeepReceipt) {
        Keep-Receipt $Name $Kind $Method $Path $Key $Body $Status $result.Body
    }
    Save-Evidence
    return $result.Body
}
function Concurrent-Write([string]$Path, [object]$Body, [string]$Key, [int]$Status) {
    Assert-LocalAcceptanceUri ([uri]$Client.BaseUri)
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.CookieContainer = $Client.WebSession.Cookies
    $handler.UseCookies = $true
    $handler.AllowAutoRedirect = $false
    # Limited to the already verified HTTPS localhost endpoint and the existing test-only certificate option.
    if ($Client.SkipCertificateCheck) { $handler.ServerCertificateCustomValidationCallback = [Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator }
    $httpClient = [Net.Http.HttpClient]::new($handler)
    $httpClient.Timeout = [TimeSpan]::FromSeconds(45)
    $requests = @(); $responses = @()
    try {
        $payload = Json $Body
        foreach ($index in 1..2) {
            $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Post, ([uri]::new([uri]$Client.BaseUri, $Path)))
            $request.Content = [Net.Http.StringContent]::new($payload, [Text.Encoding]::UTF8, 'application/json')
            [void]$request.Headers.TryAddWithoutValidation('Idempotency-Key', $Key)
            [void]$request.Headers.TryAddWithoutValidation([string]$Client.CsrfHeaderName, [string]$Client.CsrfToken)
            $requests += $request
        }
        # Both network requests start before either response is awaited.
        $firstPending = $httpClient.SendAsync($requests[0])
        $secondPending = $httpClient.SendAsync($requests[1])
        [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($firstPending, $secondPending)).GetAwaiter().GetResult()
        $responses = @($firstPending.GetAwaiter().GetResult(), $secondPending.GetAwaiter().GetResult())
        $bodies = @()
        foreach ($response in $responses) {
            Check ([int]$response.StatusCode -eq $Status) "Concurrent exact-key request expected HTTP $Status, got $([int]$response.StatusCode)"
            $bodies += ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json -Depth 80)
        }
        Check ((Json $bodies[0]) -ceq (Json $bodies[1])) 'Concurrent identical requests return the same committed result'
        Keep-Receipt 'Concurrent future site assignment' Scheduler POST $Path $Key $Body $Status $bodies[0]
        $script:state.observations += [ordered]@{ name = 'Two real concurrent identical requests'; status = $Status; requests = 2 }
        Save-Evidence
        return $bodies[0]
    }
    finally {
        foreach ($response in $responses) { $response.Dispose() }
        foreach ($request in $requests) { $request.Dispose() }
        $httpClient.Dispose()
    }
}
function Schedule([DateOnly]$Day, [string]$Start = '09:00:00', [string]$End = '17:00:00') {
    return [ordered]@{ effectiveFrom = $Day.ToString('yyyy-MM-dd'); startTime = $Start; endTime = $End;
        endDayOffset = 0; scheduledMinutes = 480; paidBreakMinutes = 0; unpaidBreakMinutes = 0; applicableWeekdays = 127 }
}

if ($Phase -in @('Replay', 'DeniedReplay')) {
    $state = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json -Depth 80 -AsHashtable
    if ($state.database -cne 'Cosmix_Testing' -or $state.runId -cne $runId -or $state.siteId -ne $siteId -or
        -not $state.completed) { throw 'Replay requires the completed private evidence for this exact fixture run.' }
    $checks = [int]$state.checks
    foreach ($item in @($state.receipts)) {
        if ($Phase -eq 'DeniedReplay') {
            # Keep ShiftsManage on the new session: these should fail specifically on revoked roster scope.
            if ($item.clientKind -ne 'Scheduler' -or
                ($item.path -notlike "$sitePath/*" -and $item.path -notmatch '^/api/v1/hr/shifts/[^/]+/versions$')) { continue }
            $null = Http $item.method $item.path $item.request $item.key 403 "Denied replay: $($item.name)"
        } else {
            $actual = Http $item.method $item.path $item.request $item.key $item.expectedStatus "Restart replay: $($item.name)" $item.clientKind
            Check ((Json $actual) -ceq (Json $item.expected)) "Frozen encrypted-disk result: $($item.name)"
        }
    }
    $state.lastPhase = $Phase
    Save-Evidence
    Write-Output "PASS ${Phase}: $checks cumulative checks. Evidence retained privately; no credentials or protected references emitted."
    return
}

if ($Phase -eq 'Resume') {
    $state = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json -Depth 80 -AsHashtable
    if ($state.database -cne 'Cosmix_Testing' -or $state.runId -cne $runId -or $state.siteId -ne $siteId -or
        $state.completed -or @($state.receipts).Count -ne 4 -or @($state.created.shiftIds).Count -ne 3 -or
        @($state.created.assignmentIds).Count -ne 1 -or $state.created.Contains('temporaryIds')) {
        throw 'Resume only supports this exact checkpoint: three committed shifts and one assignment, with no temporary writes.'
    }
    $checks = [int]$state.checks
    $history = Http GET "$sitePath/shift-assignments" $null '' 200 'Verify narrow pre-temporary resume checkpoint'
    Check (@($history.assignments).Count -eq 1 -and @($history.temporaryChanges).Count -eq 0 -and
        @($history.temporaryAmendments).Count -eq 0 -and $history.assignments[0].id -eq $state.created.assignmentIds[0]) 'Actual persisted timeline matches the only allowed resume checkpoint'
    $shifts = @('BASE','ALT','THIRD' | ForEach-Object {
        $receipt = @($state.receipts | Where-Object key -eq "$runId.shift.$_")
        if ($receipt.Count -ne 1) { throw 'Resume shift receipt is missing or duplicated.' }
        $receipt[0].expected
    })
    $base = $shifts[0]; $alternate = $shifts[1]; $third = $shifts[2]
    $assignmentReceipt = @($state.receipts | Where-Object key -eq "$runId.assign")
    if ($assignmentReceipt.Count -ne 1) { throw 'Resume assignment receipt is missing or duplicated.' }
    $assignBody = $assignmentReceipt[0].request
    $assigned = $assignmentReceipt[0].expected
    $state.lastPhase = 'Resume'
} else {
if (Test-Path -LiteralPath $EvidencePath) { throw 'Evidence already exists. Use the explicit supported Resume checkpoint or Replay; never overwrite a partial run.' }
$state = [ordered]@{ database = 'Cosmix_Testing'; runId = $runId; siteId = $siteId; employeeIds = $employeeIds;
    effectiveFrom = $from.ToString('yyyy-MM-dd'); businessDate = $date.ToString('yyyy-MM-dd'); created = [ordered]@{};
    observations = @(); receipts = @(); checks = 0; completed = $false; lastPhase = 'Run' }
Save-Evidence

$history = Http GET "$sitePath/shift-assignments" $null '' 200 'Read fresh site timeline'
Check (@($history.assignments).Count -eq 0 -and @($history.temporaryChanges).Count -eq 0) 'Dedicated site has no existing scheduling rows'
$shifts = @()
foreach ($item in @(@('BASE', '09:00:00', '17:00:00'), @('ALT', '10:00:00', '18:00:00'), @('THIRD', '11:00:00', '19:00:00'))) {
    $body = [ordered]@{ code = "$runId.$($item[0])"; name = "SYNTHETIC $runId $($item[0])";
        initialSchedule = (Schedule $from $item[1] $item[2]); reason = "SYNTHETIC isolated acceptance $runId" }
    $shifts += Http POST '/api/v1/hr/shifts' $body "$runId.shift.$($item[0])" 201 "Create synthetic $($item[0]) shift" -KeepReceipt
}
$base = $shifts[0]; $alternate = $shifts[1]; $third = $shifts[2]
$state.created.shiftIds = @($shifts | ForEach-Object { $_.id })
$state.created.initialVersionIds = @($shifts | ForEach-Object { $_.versions[0].id })
$assignBody = [ordered]@{ shiftId = $base.id; effectiveFrom = $from.ToString('yyyy-MM-dd'); effectiveTo = $null;
    holidayCalendarId = $null; expectedTimeline = $history.timeline; expectedShiftVersion = $base.version;
    reason = "SYNTHETIC site schedule $runId" }
$assigned = Concurrent-Write "$sitePath/shift-assignments" $assignBody "$runId.assign" 201
$state.created.assignmentIds = @($assigned.assignment.id)
}
$again = Http POST "$sitePath/shift-assignments" $assignBody "$runId.assign" 201 'Immediate assignment retry'
Check ((Json $again) -ceq (Json $assigned)) 'Assignment retry returns frozen original result'
$changedAssign = [ordered]@{}; foreach ($entry in $assignBody.GetEnumerator()) { $changedAssign[$entry.Key] = $entry.Value }
$changedAssign.reason = "SYNTHETIC changed payload $runId"
$null = Http POST "$sitePath/shift-assignments" $changedAssign "$runId.assign" 409 'Changed payload cannot reuse assignment key'
$null = Http POST "$sitePath/shift-assignments" $assignBody "$runId.stale" 409 'New key cannot reuse stale site timeline'
$null = Http POST "$sitePath/shift-assignments" $assignBody "$runId.no-csrf" 400 'Real cookie write rejects missing CSRF' -WithoutCsrf

$preview = Http GET "$sitePath/temporary-shift-changes/preview?businessDate=$($date.ToString('yyyy-MM-dd'))&replacementShiftVersionId=$($alternate.versions[0].id)" $null '' 200 'Preview actual SQL staff membership'
Check ((Json @($preview.affectedEmployeeIds | Sort-Object)) -ceq (Json @($employeeIds | Sort-Object))) 'Preview affected manifest is exactly the fixture staff set'
$temporaryBody = [ordered]@{ businessDate = $date.ToString('yyyy-MM-dd'); replacementShiftVersionId = $alternate.versions[0].id;
    expectedTimeline = $preview.timeline; expectedStaff = $preview.staffToken; writtenEvidence = "SYNTHETIC original written evidence $runId";
    reason = "SYNTHETIC temporary date $runId" }
$first = Http POST "$sitePath/temporary-shift-changes" $temporaryBody "$runId.temp.first" 201 'Create actual temporary change and manifest' -KeepReceipt
$superPreview = Http GET "$sitePath/temporary-shift-changes/$($first.change.id)/supersede/preview?replacementShiftVersionId=$($third.versions[0].id)" $null '' 200 'Preview immutable supersession'
$supersedeBody = [ordered]@{ replacementShiftVersionId = $third.versions[0].id; expectedTimeline = $superPreview.timeline;
    expectedStaff = $superPreview.staffToken; writtenEvidence = "SYNTHETIC superseding written evidence $runId"; reason = "SYNTHETIC supersession $runId" }
$superseded = Http POST "$sitePath/temporary-shift-changes/$($first.change.id)/supersede" $supersedeBody "$runId.temp.super" 200 'Supersede with new parent and exact staff manifest' -KeepReceipt
Check ($superseded.original.status -eq 'superseded' -and $superseded.replacement.id -ne $first.change.id) 'Supersession returns explicit old/new identities'
$state.created.temporaryIds = @($first.change.id, $superseded.replacement.id)
$state.created.amendmentIds = @($superseded.amendment.id)
$resolved = Http GET "/api/v1/hr/employees/$($employeeIds[0])/roster?businessDate=$($date.ToString('yyyy-MM-dd'))" $null '' 200 'Resolve actual historical site membership'
Check ($resolved.explanation.temporaryChangeId -eq $superseded.replacement.id -and $resolved.shiftVersionId -eq $third.versions[0].id) 'Effective roster follows superseding change'
$cancelPreview = Http GET "$sitePath/temporary-shift-changes/$($superseded.replacement.id)/cancel/preview" $null '' 200 'Preview restoration of actual base'
$cancelBody = [ordered]@{ expectedTimeline = $cancelPreview.timeline; expectedStaff = $cancelPreview.staffToken;
    writtenEvidence = "SYNTHETIC cancellation written evidence $runId"; reason = "SYNTHETIC cancellation $runId" }
$cancelled = Http POST "$sitePath/temporary-shift-changes/$($superseded.replacement.id)/cancel" $cancelBody "$runId.temp.cancel" 200 'Cancel successor without deleting its evidence' -KeepReceipt
$state.created.amendmentIds += $cancelled.amendment.id
Check ($cancelled.original.status -eq 'cancelled' -and $null -eq $cancelled.replacement) 'Cancellation has explicit terminal history'
$restored = Http GET "/api/v1/hr/employees/$($employeeIds[0])/roster?businessDate=$($date.ToString('yyyy-MM-dd'))" $null '' 200 'Resolve restored base schedule'
Check ($restored.shiftVersionId -eq $base.versions[0].id -and $restored.explanation.source -eq 'site') 'Cancellation restores exact base version'

$freshPreview = Http GET "$sitePath/temporary-shift-changes/preview?businessDate=$($date.ToString('yyyy-MM-dd'))&replacementShiftVersionId=$($alternate.versions[0].id)" $null '' 200 'Preview fresh root after cancellation'
$temporaryBody.expectedTimeline = $freshPreview.timeline; $temporaryBody.expectedStaff = $freshPreview.staffToken
$fresh = Http POST "$sitePath/temporary-shift-changes" $temporaryBody "$runId.temp.fresh" 201 'Create a fresh temporary root on cancelled date' -KeepReceipt
$state.created.temporaryIds += $fresh.change.id
$publishBody = [ordered]@{ expectedVersion = $base.version; schedule = (Schedule ($from.AddDays(1)) '08:00:00' '16:00:00');
    reason = "SYNTHETIC assigned-site publication $runId" }
$null = Http POST "/api/v1/hr/shifts/$($base.id)/versions" $publishBody "$runId.blocked-version" 409 'Active temporary original pin blocks publication'
$freshCancelPreview = Http GET "$sitePath/temporary-shift-changes/$($fresh.change.id)/cancel/preview" $null '' 200 'Preview fresh root cancellation'
$freshCancelBody = [ordered]@{ expectedTimeline = $freshCancelPreview.timeline; expectedStaff = $freshCancelPreview.staffToken;
    writtenEvidence = "SYNTHETIC publication preparation $runId"; reason = "SYNTHETIC fresh cancellation $runId" }
$freshCancelled = Http POST "$sitePath/temporary-shift-changes/$($fresh.change.id)/cancel" $freshCancelBody "$runId.temp.fresh-cancel" 200 'Cancel fresh root before publication' -KeepReceipt
$state.created.amendmentIds += $freshCancelled.amendment.id
$published = Http POST "/api/v1/hr/shifts/$($base.id)/versions" $publishBody "$runId.version" 201 'Publish safely to an assigned site through scoped authority' -KeepReceipt
Check (@($published.versions).Count -eq 2 -and $published.versions[0].id -eq $base.versions[0].id) 'Publication preserves initial immutable version'
$state.created.publishedVersionId = $published.versions[1].id
$publishedRoster = Http GET "/api/v1/hr/employees/$($employeeIds[0])/roster?businessDate=$($date.ToString('yyyy-MM-dd'))" $null '' 200 'Resolve published site version'
Check ($publishedRoster.shiftVersionId -eq $published.versions[1].id) 'Site inherits the published future base version'
if ($null -ne $SelfClient) {
    $mine = Http GET "/api/v1/me/shift?businessDate=$($date.ToString('yyyy-MM-dd'))" $null '' 200 'Read self schedule through real server-side employee identity' Self
    Check ($mine.employeeId -eq $fixture.employeeId -and $mine.shiftVersionId -eq $published.versions[1].id) 'Self endpoint resolves the authenticated HR employee and actual site version'
    # Unknown query fields cannot choose another employee; this endpoint takes only businessDate.
    $spoofed = Http GET "/api/v1/me/shift?businessDate=$($date.ToString('yyyy-MM-dd'))&employeeId=$siteId" $null '' 200 'Attempted self employee selection stays bound to session' Self
    Check ($spoofed.employeeId -eq $fixture.employeeId) 'Client employee ID cannot change self identity'
}
if ($null -ne $DeniedClient) {
    $deniedSiteId = ([guid]$fixture.deniedSiteId).ToString()
    $null = Http GET "/api/v1/hr/sites/$deniedSiteId/shift-assignments" $null '' 403 'HR cannot read another site timeline' Denied
    $null = Http GET "/api/v1/hr/employees/$siteId/roster?businessDate=$($date.ToString('yyyy-MM-dd'))" $null '' 403 'HR self permission does not authorize arbitrary employee roster reads' Denied
}

$history = Http GET "$sitePath/shift-assignments" $null '' 200 'Read exact timeline before normal replacement'
$replaceDate = $date.AddDays(2); $endDate = $date.AddDays(4)
$replaceBody = [ordered]@{ shiftId = $alternate.id; effectiveFrom = $replaceDate.ToString('yyyy-MM-dd'); effectiveTo = $null;
    holidayCalendarId = $null; expectedTimeline = $history.timeline; expectedShiftVersion = $alternate.version;
    reason = "SYNTHETIC normal replacement $runId" }
$replaced = Http POST "$sitePath/shift-assignments/$($assigned.assignment.id)/replace" $replaceBody "$runId.replace" 200 'Append normal ending and successor atomically' -KeepReceipt
$state.created.assignmentIds += $replaced.assignment.id
$endBody = [ordered]@{ effectiveTo = $endDate.ToString('yyyy-MM-dd'); expectedTimeline = $replaced.timeline;
    reason = "SYNTHETIC controlled ending $runId" }
$ended = Http POST "$sitePath/shift-assignments/$($replaced.assignment.id)/end" $endBody "$runId.end" 200 'Append controlled future ending' -KeepReceipt
$unassigned = Http GET "/api/v1/hr/employees/$($employeeIds[0])/roster?businessDate=$($endDate.ToString('yyyy-MM-dd'))" $null '' 200 'Resolve exclusive assignment end'
Check ($unassigned.status -eq 'unassigned') 'Ending leaves future site explicitly unassigned'
$history = Http GET "$sitePath/shift-assignments" $null '' 200 'Read final complete immutable site history'
Check (@($history.assignments).Count -eq 2 -and @($history.temporaryChanges).Count -eq 3 -and @($history.temporaryAmendments).Count -eq 3) 'Exact expected immutable site rows are visible'
Check (@($history.temporaryChanges | Where-Object status -eq 'active').Count -eq 0) 'Every temporary root is explicitly terminal'

if ($null -ne $AdminClient) {
    $configuration = Http GET "$sitePath/geofences?asOfDate=$($from.ToString('yyyy-MM-dd'))" $null '' 200 'Read real geofence concurrency state' Admin
    Check ($configuration.latestVersionNumber -eq 0) 'Dedicated fixture site has no geofence versions'
    $geofenceBody = [ordered]@{ effectiveFrom = $from.ToString('yyyy-MM-dd'); latitude = 0; longitude = 0; radiusMeters = 100;
        maximumAccuracyMeters = 20; expectedSiteVersion = $configuration.siteVersion; expectedLatestVersionNumber = 0;
        managementApproved = $true; approvalReference = "SYNTHETIC isolated fixture attestation $runId"; reason = "SYNTHETIC geofence $runId" }
    if ($null -ne $DeniedClient) {
        $null = Http POST "$sitePath/geofences/versions" $geofenceBody "$runId.geofence-denied" 403 'HR has no fixed Admin geofence authority' Denied
    }
    $geofence = Http POST "$sitePath/geofences/versions" $geofenceBody "$runId.geofence" 201 'Fixed Admin creates actual future geofence' Admin -KeepReceipt
    $state.created.geofenceId = $geofence.boundary.id
    $effective = Http GET "$sitePath/geofences?asOfDate=$($from.ToString('yyyy-MM-dd'))" $null '' 200 'Read exact effective geofence' Admin
    Check ($effective.boundary.id -eq $geofence.boundary.id) 'Geofence applies at its explicit future boundary'
    $null = Http POST "$sitePath/geofences/versions" $geofenceBody "$runId.geofence-stale" 409 'New key cannot bypass geofence concurrency' Admin
}
$state.completed = $true
Save-Evidence
Write-Output "PASS Run: $checks real authenticated scheduling/geofence HTTP checks. Fixture IDs and replay evidence retained privately. SQL persistence is checked separately."
