[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EvidencePath,
    [Parameter(Mandatory)][string]$OutputPath
)
# Generates bounded read-only SQL from this run's private IDs. It never opens a connection.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$privateRoot = [IO.Path]::GetFullPath('C:\Users\Affan.Ahmed\AppData\Local\Cosmix\AccessAcceptance\20261009').TrimEnd('\') + '\'
foreach ($path in @($EvidencePath, $OutputPath)) {
    if (-not [IO.Path]::GetFullPath($path).StartsWith($privateRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Evidence and generated SQL must stay inside the approved private acceptance directory.'
    }
}
if (Test-Path -LiteralPath $OutputPath) { throw 'Refusing to overwrite an existing private SQL evidence script.' }
$state = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json -Depth 80 -AsHashtable
if ($state.database -cne 'Cosmix_Testing' -or $state.runId -cnotmatch '^ACC[A-Z0-9]{6,18}$' -or -not $state.completed) {
    throw 'SQL evidence generation requires a completed, bounded Cosmix_Testing acceptance run.'
}
function Id([object]$value) {
    $parsed = [guid]$value
    if ($parsed -eq [guid]::Empty) { throw 'An evidence identity is empty.' }
    return $parsed.ToString()
}
function IdList([object[]]$values) { return (@($values | ForEach-Object { "'$(Id $_)'" }) -join ',') }
$site = Id $state.siteId
$employees = @($state.employeeIds | ForEach-Object { Id $_ })
$shifts = @($state.created.shiftIds | ForEach-Object { Id $_ })
$initialVersions = @($state.created.initialVersionIds | ForEach-Object { Id $_ })
$assignments = @($state.created.assignmentIds | ForEach-Object { Id $_ })
$temporary = @($state.created.temporaryIds | ForEach-Object { Id $_ })
$amendments = @($state.created.amendmentIds | ForEach-Object { Id $_ })
$published = Id $state.created.publishedVersionId
if ($employees.Count -lt 1 -or $employees.Count -gt 10 -or $shifts.Count -ne 3 -or $initialVersions.Count -ne 3 -or
    $assignments.Count -ne 2 -or $temporary.Count -ne 3 -or $amendments.Count -ne 3) { throw 'Unexpected fixture cardinality; inspect the run before SQL acceptance.' }
$run = [string]$state.runId
$keys = @($state.receipts | ForEach-Object {
    if ($_.key -cnotmatch '^[A-Za-z0-9_.:-]{8,100}$' -or -not $_.key.StartsWith($run + '.', [StringComparison]::Ordinal)) {
        throw 'Unexpected key outside this synthetic run.'
    }
    [string]$_.key
})
if ($keys.Count -lt 12 -or $keys.Count -gt 13 -or @($keys | Sort-Object -Unique).Count -ne $keys.Count) { throw 'Unexpected successful receipt set.' }
$keySql = @($keys | ForEach-Object { "N'$_'" }) -join ','
$employeeSql = IdList $employees; $shiftSql = IdList $shifts; $initialSql = IdList $initialVersions
$assignmentSql = IdList $assignments; $temporarySql = IdList $temporary; $amendmentSql = IdList $amendments
$targets = IdList (@($shifts) + @($assignments) + @($temporary) + @($amendments))
$from = [DateOnly]::ParseExact([string]$state.effectiveFrom, 'yyyy-MM-dd')
$date = [DateOnly]::ParseExact([string]$state.businessDate, 'yyyy-MM-dd')
$replaceDate = $date.AddDays(2).ToString('yyyy-MM-dd'); $endDate = $date.AddDays(4).ToString('yyyy-MM-dd')
$versionDate = $from.AddDays(1).ToString('yyyy-MM-dd'); $fromDate = $from.ToString('yyyy-MM-dd'); $businessDate = $date.ToString('yyyy-MM-dd')
$manifestRows = 3 * $employees.Count
$sql = @"
-- Generated for one private synthetic run. Read-only; no cleanup or schema changes.
SET NOCOUNT ON;
IF DB_NAME() <> N'Cosmix_Testing' THROW 51001, 'Refusing non-testing catalog.', 1;
IF (SELECT COUNT(*) FROM attendance.Shifts WHERE Id IN ($shiftSql) AND Code LIKE N'$run.%') <> 3
    THROW 51001, 'Missing or unexpected synthetic shift identities.', 1;
IF (SELECT COUNT(*) FROM attendance.ShiftVersions WHERE ShiftId IN ($shiftSql)) <> 4
    THROW 51001, 'Unexpected immutable shift version count.', 1;
IF (SELECT COUNT(*) FROM attendance.ShiftVersions WHERE Id IN ($initialSql) AND VersionNumber=1 AND EffectiveFrom='$fromDate') <> 3
    THROW 51001, 'Initial shift versions were lost or changed.', 1;
IF NOT EXISTS (SELECT 1 FROM attendance.ShiftVersions WHERE Id='$published' AND ShiftId='$($shifts[0])' AND VersionNumber=2 AND EffectiveFrom='$versionDate')
    THROW 51001, 'Published immutable version is missing.', 1;

IF (SELECT COUNT(*) FROM attendance.ShiftPopulationAssignments WHERE SiteId='$site') <> 2
    THROW 51001, 'Unexpected site assignment cardinality.', 1;
IF (SELECT COUNT(*) FROM attendance.ShiftPopulationAssignments WHERE Id IN ($assignmentSql) AND SiteId='$site'
    AND PopulationKind=2 AND EffectiveTo IS NULL AND ApprovalRequestId IS NULL) <> 2
    THROW 51001, 'Direct original assignment rows were rewritten or misbound.', 1;
IF NOT EXISTS (SELECT 1 FROM attendance.ShiftPopulationAssignments WHERE Id='$($assignments[0])' AND ShiftId='$($shifts[0])' AND EffectiveFrom='$fromDate')
    THROW 51001, 'Original normal assignment changed.', 1;
IF NOT EXISTS (SELECT 1 FROM attendance.ShiftPopulationAssignments WHERE Id='$($assignments[1])' AND ShiftId='$($shifts[1])' AND EffectiveFrom='$replaceDate')
    THROW 51001, 'Successor normal assignment is incorrect.', 1;
IF (SELECT COUNT(*) FROM attendance.ShiftPopulationAssignmentEnds WHERE ShiftPopulationAssignmentId IN ($assignmentSql)) <> 2
    THROW 51001, 'Expected exactly two append-only assignment endings.', 1;
IF NOT EXISTS (SELECT 1 FROM attendance.ShiftPopulationAssignmentEnds WHERE ShiftPopulationAssignmentId='$($assignments[0])' AND EffectiveTo='$replaceDate' AND ApprovalRequestId IS NULL)
    OR NOT EXISTS (SELECT 1 FROM attendance.ShiftPopulationAssignmentEnds WHERE ShiftPopulationAssignmentId='$($assignments[1])' AND EffectiveTo='$endDate' AND ApprovalRequestId IS NULL)
    THROW 51001, 'Assignment ending boundaries are incorrect.', 1;

IF (SELECT COUNT(*) FROM attendance.TemporaryShiftChanges WHERE SiteId='$site') <> 3
    THROW 51001, 'Temporary changes were duplicated or removed.', 1;
IF (SELECT COUNT(*) FROM attendance.TemporaryShiftChanges WHERE Id IN ($temporarySql) AND SiteId='$site'
    AND AttendanceDate='$businessDate' AND OriginalShiftVersionId='$($initialVersions[0])' AND ApprovalRequestId IS NULL
    AND ISJSON(WrittenEvidenceReference)=1) <> 3
    THROW 51001, 'Original temporary identity/evidence was changed or fabricated.', 1;
IF (SELECT COUNT(*) FROM attendance.TemporaryShiftChangeEmployees WHERE TemporaryShiftChangeId IN ($temporarySql)) <> $manifestRows
    OR EXISTS (SELECT 1 FROM attendance.TemporaryShiftChangeEmployees WHERE TemporaryShiftChangeId IN ($temporarySql) AND EmployeeId NOT IN ($employeeSql))
    THROW 51001, 'Actual affected staff manifests differ from the exact fixture population.', 1;
IF EXISTS (SELECT TemporaryShiftChangeId FROM attendance.TemporaryShiftChangeEmployees WHERE TemporaryShiftChangeId IN ($temporarySql)
    GROUP BY TemporaryShiftChangeId HAVING COUNT(*) <> $($employees.Count))
    THROW 51001, 'A temporary parent is missing its complete atomic manifest.', 1;
IF (SELECT COUNT(*) FROM attendance.TemporaryShiftChangeAmendments WHERE Id IN ($amendmentSql)
    AND OriginalChangeId IN ($temporarySql) AND ISJSON(WrittenEvidenceReference)=1) <> 3
    THROW 51001, 'Amendment evidence is missing or misbound.', 1;
IF NOT EXISTS (SELECT 1 FROM attendance.TemporaryShiftChangeAmendments WHERE Id='$($amendments[0])' AND OriginalChangeId='$($temporary[0])' AND ReplacementChangeId='$($temporary[1])')
    OR NOT EXISTS (SELECT 1 FROM attendance.TemporaryShiftChangeAmendments WHERE Id='$($amendments[1])' AND OriginalChangeId='$($temporary[1])' AND ReplacementChangeId IS NULL)
    OR NOT EXISTS (SELECT 1 FROM attendance.TemporaryShiftChangeAmendments WHERE Id='$($amendments[2])' AND OriginalChangeId='$($temporary[2])' AND ReplacementChangeId IS NULL)
    THROW 51001, 'Explicit supersession/cancellation chain is incorrect.', 1;
IF EXISTS (SELECT 1 FROM attendance.TemporaryShiftChanges c WHERE c.Id IN ($temporarySql)
    AND NOT EXISTS (SELECT 1 FROM attendance.TemporaryShiftChangeAmendments a WHERE a.OriginalChangeId=c.Id))
    THROW 51001, 'A temporary change unexpectedly remains effective.', 1;

IF (SELECT COUNT(*) FROM workflow.IdempotencyReceipts WHERE [Key] IN ($keySql) AND CompletedAt IS NOT NULL
    AND ProtectedResultReference IS NOT NULL) <> $($keys.Count)
    THROW 51001, 'Successful SQL writes do not have exact completed retry receipts.', 1;
IF NOT EXISTS (SELECT 1 FROM workflow.IdempotencyReceipts WHERE [Key]=N'$run.version' AND ResultCode=N'attendance-scheduling-scoped-v2')
    THROW 51001, 'Publication receipt did not preserve original affected scopes.', 1;
IF EXISTS (SELECT 1 FROM workflow.IdempotencyReceipts WHERE [Key] IN (
    N'$run.stale',N'$run.no-csrf',N'$run.blocked-version',N'$run.geofence-denied',N'$run.geofence-stale'))
    THROW 51001, 'Rejected operation left a completed or partial receipt.', 1;
IF EXISTS (SELECT 1 FROM audit.AuditEvents WHERE TargetType=N'AttendanceScheduling' AND TRY_CONVERT(uniqueidentifier,TargetId) IN ($targets)
    AND EventType IN (N'shifts.create',N'shifts.versions.append',N'site-rosters.assign',N'site-rosters.replace',N'site-rosters.end',
        N'site-rosters.temporary-create',N'site-rosters.temporary-supersede',N'site-rosters.temporary-cancel')
    AND (ProtectedChangesReference IS NULL OR ISJSON(ProtectedChangesReference)<>1))
    THROW 51001, 'A scheduling mutation lacks protected audit evidence.', 1;
IF (SELECT COUNT(*) FROM audit.AuditEvents WHERE TargetType=N'AttendanceScheduling' AND TRY_CONVERT(uniqueidentifier,TargetId) IN ($targets)
    AND EventType IN (N'shifts.create',N'shifts.versions.append',N'site-rosters.assign',N'site-rosters.replace',N'site-rosters.end',
        N'site-rosters.temporary-create',N'site-rosters.temporary-supersede',N'site-rosters.temporary-cancel')) <> 12
    THROW 51001, 'Expected exactly one original audit per successful scheduling write.', 1;
"@
if ($state.created.Contains('geofenceId')) {
    $geofence = Id $state.created.geofenceId
    $sql += @"

IF NOT EXISTS (SELECT 1 FROM attendance.SiteGeofenceVersions WHERE Id='$geofence' AND SiteId='$site'
    AND VersionNumber=1 AND EffectiveFrom='$fromDate' AND Latitude=0 AND Longitude=0 AND RadiusMeters=100 AND MaximumAccuracyMeters=20)
    THROW 51001, 'Real geofence version does not match the accepted fixture.', 1;
"@
}
$sql += @"

SELECT N'$run' AS SyntheticRun, N'PASS: immutable scheduling identities, manifests, amendments, receipts and audits' AS Evidence;
SELECT EventType, COUNT(*) AS Events FROM audit.AuditEvents
    WHERE TargetType=N'AttendanceScheduling' AND TRY_CONVERT(uniqueidentifier,TargetId) IN ($targets)
    GROUP BY EventType ORDER BY EventType;
-- Private references, tokens, identity details and encrypted contents are deliberately not selected.
"@
Set-Content -LiteralPath $OutputPath -Value $sql -Encoding utf8
Write-Output 'Generated bounded read-only SQL evidence script privately. No SQL connection or execution occurred.'
