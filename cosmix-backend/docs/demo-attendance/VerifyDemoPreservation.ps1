$ErrorActionPreference='Stop'
$root=Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\DemoAttendance20261028'
$before=Get-Content -LiteralPath (Join-Path $root 'before-table-hashes.json') -Raw|ConvertFrom-Json
$after=Get-Content -LiteralPath (Join-Path $root 'final-table-hashes.json') -Raw|ConvertFrom-Json
$delta=Get-Content -LiteralPath (Join-Path $root 'final-table-differences.json') -Raw|ConvertFrom-Json
$one=@('[attendance].[AttendanceCorrections]','[attendance].[AttendanceSummarySessions]','[attendance].[DailyAttendanceSummaries]','[attendance].[ShiftPopulationAssignments]','[attendance].[Shifts]','[attendance].[ShiftVersions]','[attendance].[Sites]','[attendance].[WorkSessions]','[hr].[Departments]','[hr].[Designations]','[hr].[EmployeeAssignments]','[hr].[EmployeeOnboardingDecisions]','[hr].[Employees]','[hr].[EmployeeSubmissions]','[hr].[EmploymentTypes]','[workflow].[ApprovalDecisions]','[workflow].[ApprovalRequests]','[communication].[Notifications]','[jobs].[OutboxMessages]')
$normal=@('[audit].[AuditEvents]','[auth].[AuthRateBuckets]','[auth].[MobileOtpChallenges]','[auth].[OtpDeliveries]','[auth].[RefreshTokens]','[auth].[UserSessions]')
$allowed=$one+$normal+'[workflow].[IdempotencyReceipts]'
$unexpected=@($delta|Where-Object {$_.Table -notin $allowed -or $_.Delta -lt 0})
foreach($table in $one){$change=@($delta|Where-Object Table -eq $table);if($change.Count -ne 1 -or $change[0].Delta -ne 1){throw ('Unexpected business-table delta: '+$table)}}
$receipts=$delta|Where-Object Table -eq '[workflow].[IdempotencyReceipts]'
if($receipts.Delta -ne 2){throw 'Replay created unexpected receipt count'}
if($before.Count -ne 173 -or $after.Count -ne 173 -or $unexpected.Count -ne 0){$unexpected|ConvertTo-Json;throw 'Unexpected preservation delta'}
$protected=@($before|Where-Object {$_.Table -match 'Payroll|Salary|Payment|Policy|Role|Permission|RawPunch|Face|Migration|PeriodLock|Legal'})
foreach($b in $protected){$a=$after|Where-Object Table -eq $b.Table;if($a.Hash -cne $b.Hash){throw ('Protected table changed: '+$b.Table)}}
$result=[pscustomobject]@{Tables=173;UnchangedTableHashes=173-$delta.Count;ChangedTables=$delta.Count;ExpectedBusinessRowDeltasPassed=$true;UnexpectedDeltas=@();ProtectedTableHashesUnchanged=$protected.Count;IdempotencyReceiptsAdded=2;NewEmployeeCount=1;NewSessionCount=1;NewDailySummaryCount=1;NewCorrectionCount=1;PolicyWrites=0;RawPunchWrites=0;FaceWrites=0}
$result|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $root 'preservation-result.json')
$result|ConvertTo-Json -Depth 10
