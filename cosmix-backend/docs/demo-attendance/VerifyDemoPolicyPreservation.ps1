$ErrorActionPreference='Stop'
$root=Join-Path $env:LOCALAPPDATA 'Cosmix\LocalTesting\DemoAttendancePolicy20261028'
$before=Get-Content -LiteralPath (Join-Path $root 'before-table-hashes.json') -Raw|ConvertFrom-Json
$after=Get-Content -LiteralPath (Join-Path $root 'after-table-hashes.json') -Raw|ConvertFrom-Json
$delta=Get-Content -LiteralPath (Join-Path $root 'after-table-differences.json') -Raw|ConvertFrom-Json
$one=@('[attendance].[AttendanceCorrections]','[attendance].[AttendanceSummarySessions]','[attendance].[DailyAttendanceSummaries]','[attendance].[ShiftPopulationAssignmentEnds]','[attendance].[ShiftPopulationAssignments]','[attendance].[WorkSessions]','[hr].[HolidayCalendars]','[hr].[Policies]','[hr].[PolicyAssignments]','[hr].[PolicyVersions]','[workflow].[ApprovalDecisions]','[workflow].[ApprovalRequests]','[communication].[Notifications]','[jobs].[OutboxMessages]')
$normal=@('[audit].[AuditEvents]','[auth].[AuthRateBuckets]','[auth].[MobileOtpChallenges]','[auth].[OtpDeliveries]','[auth].[RefreshTokens]','[auth].[UserSessions]')
$allowed=$one+$normal+'[workflow].[IdempotencyReceipts]'
foreach($name in $one){$d=@($delta|Where-Object Table -eq $name);if($d.Count -ne 1 -or $d[0].Delta -ne 1){throw ('Unexpected demo delta: '+$name)}}
if(@($delta|Where-Object {$_.Table -notin $allowed -or $_.Delta -lt 0}).Count -ne 0){throw 'Unexpected table changes'}
if(($delta|Where-Object Table -eq '[workflow].[IdempotencyReceipts]').Delta -ne 2){throw 'Duplicate approval/summary receipt'}
if($before.Count -ne 173 -or $after.Count -ne 173){throw 'Schema table count changed'}
foreach($table in @('[hr].[Employees]','[hr].[EmployeeAssignments]','[attendance].[RawPunchEvents]')){
 $b=$before|Where-Object Table -eq $table;$a=$after|Where-Object Table -eq $table
 if(!$b -or !$a -or $a.Hash -cne $b.Hash){throw ('Protected record table changed: '+$table)}
}
$protected=@($before|Where-Object {$_.Table -match 'Payroll|Salary|Payment|Role|Permission|RawPunch|Face|Migration|PeriodLock|Legal|Employees|EmployeeAssignments'})
foreach($b in $protected){$a=$after|Where-Object Table -eq $b.Table;if($a.Hash -cne $b.Hash){throw ('Protected table changed: '+$b.Table)}}
$result=[pscustomobject]@{Tables=173;UnchangedTables=173-$delta.Count;ChangedTables=$delta.Count;UnexpectedChanges=0;EmployeesUnchanged=33;ProtectedHashesUnchanged=$protected.Count;NewPolicy=1;NewPolicyVersion=1;EmployeeOnlyAssignment=1;NewHolidayCalendar=1;AppendedRosterEndAndReplacement=2;NewCorrection=1;NewSession=1;NewSummary=1;NewIdempotencyReceipts=2;MigrationsApplied=0;Payments=0;RealFaceWrites=0}
$result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'preservation-result.json')
$result|ConvertTo-Json
