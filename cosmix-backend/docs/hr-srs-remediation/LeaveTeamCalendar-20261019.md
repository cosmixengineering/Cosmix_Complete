# Scoped Leave Calendar Handoff

SRS section 13 and ROLE-002/003. HR-only calendar, not payroll, Accounts posting,
settlement, leave approval, entitlement calculation or legal certification.

## Files Owned

- `src/Cosmix.Application/HR/Leaves/LeaveTeamCalendarContracts.cs`
- `src/Cosmix.Application/HR/Leaves/LeaveTeamCalendarService.cs`
- `src/Cosmix.Application/HR/Leaves/LeaveTeamCalendarProjection.cs`
- `src/Cosmix.Api/Controllers/HR/LeaveTeamCalendarController.cs`
- `docs/hr-srs-remediation/VerifyLeaveTeamCalendar.cs`
- This note.

All are new files. No existing files, financial code, shared suites, repositories,
DbContext, migrations, DI or permission catalogue were edited by this worker.

## Behavior

- `GET /api/v1/leaves/team-calendar`: explicit 1-25 distinct `EmployeeIds`,
  inclusive `From`/`To` covering at most 31 dates, `Page` 1-1000000,
  `PageSize` 1-100. Empty selection is invalid, never implicit all employees.
- Uses real `ILeaveRepository.OverlappingAsync` reads, after authorizing every
  selected employee against `IRosterRepository.ResourcesAsync` for both today's
  Asia/Karachi date and the complete requested interval. No repository extension.
- Every historical slice must be authorized; one permitted membership alternative
  within a slice suffices. Mixed authorized/unauthorized selections fail entirely
  before leave reads, even if unauthorized employees have no leave rows.
- Requires current web MFA plus `attendance.leaves.team-calendar.read`. HR All,
  Department, Team, Site, Project, Employee and Self grants retain existing scope
  semantics. This does not grant Admin leave review or medical evidence access.
- `GET /api/v1/leaves/mine/calendar` uses `attendance.leaves.self` and only the
  session employee. It accepts dates/pagination but no employee selector and works
  for authorized mobile self sessions. Own history does not require team authority.
- Items contain exactly `EmployeeId`, `Date`, `Status`; envelope is existing
  `PagedResult<T>`. No names, leave category/type, reason, contact, reliever,
  private evidence, balances, money, approval IDs or request IDs are returned.
- Only explicit retained request days are shown, not every day between start/end.
  Distinct `(employee,date,status)` entries do not disclose duplicate application
  counts. Different application statuses can coexist on a date; no inferred
  attendance/absence or duration. Sort is date, employee GUID, ordinal status.
- Statuses: `pending`, `approved`, `returned`, `rejected`, `cancelled`,
  `cancellation_pending`, `superseded`. Pending cancellation is not a completed
  cancellation. Returned revisions with a successor are identified as superseded.
- Checks approval identity, stage/request type, submission/closure timing and
  matching decisions; cancellation ownership/approval links; exact day identity,
  date coverage, unique days and aggregate retained minutes. Inconsistent source
  state fails with 409 instead of inferred status or a fabricated date allocation.
- At most 1000 source leave requests across the selected population, retaining the
  existing repository's 1000-per-employee/100-per-request cancellation bounds.
  Existing scope limits remain 4096 groups / 16384 alternatives. Limits fail
  explicitly, never truncate silently or widen scope.
- Selection is captured before asynchronous IO. Rechecks current session,
  effective-dated grants and original plus freshly resolved current/historical
  scopes after reads. Uses the existing authorization transaction and mandatory
  attendance access audit; audit persistence failure prevents a response.

## Parent Integration

Exact shared wiring (parent confirmed added during this task):

```csharp
services.AddScoped<LeaveTeamCalendarService>();
// AccessPermissionCatalog.All:
.. LeaveTeamCalendarPermissions.Definitions,
```

Permission definition: `attendance.leaves.team-calendar.read`, `Sensitive=false`,
`Management=true`, `SelfService=false`. Existing `AttendanceOperation`,
`ILeaveRepository`, `IRosterRepository`, `IAccountAccessReader`,
`ISchedulingJournalRepository` and `TimeProvider` registrations are reused.
No default role grants, company rules, migration, seed or database operation.
Parent reported adding OpenAPI assertions; this worker did not modify them.

## Verification

Parent integration completed on 19 October 2026: the fresh `hr-scope-build`
Release passed; this suite passed **58/58**, shared composition **50/50**, and
generated OpenAPI **184/184**. See [parent evidence](HR-Scope-Continuation-20261019.md)
for exact frozen paths/logs. HTTP binding and authenticated SQL acceptance remain
unexecuted. The worker-only notes below describe its earlier handoff checkpoint.

No product build, SQL connection, API host, migration, provider call or Git write
was performed. The suite uses real calendar service and AttendanceOperation with
strict synthetic repository/session/audit IO; unexpected private-file IO fails.
Syntax-only parsing of all five new C# files completed with zero syntax errors,
using PowerShell's loaded Roslyn parser, without compilation or emitted files.
Parent must build the integrated product and run the suite alone:

```powershell
& .\docs\attendance-gap-c\VerifyManaged.ps1 `
  -ReleaseDirectory '<PARENT_FINAL_RELEASE_DIRECTORY_ON_D>' `
  -Sources @('docs/hr-srs-remediation/VerifyLeaveTeamCalendar.cs')
```

The pre-feature frozen release cannot exercise the new calendar types/permission.
No runtime pass is claimed here. Cases cover sparse/exact dates, DTO privacy,
pagination/deduplication, transfer/history scope, all slices versus alternatives,
mixed selections, missing/malformed/oversize scopes, self and web/mobile boundaries,
grant expiry/revocation, read-time scope changes, audit failure, approval and
cancellation status/tamper, contaminated rows, source bounds and caller mutation.
SQL translation and HTTP binding remain parent integration acceptance work.

## Deliberate Boundary

This is a bounded explicit-selection calendar, like the existing factual attendance
batch; it does not enumerate every employee in a team. Callers must supply employee
IDs from their authorized selection workflow. Nothing bypasses server scope checks.
Automatic team population discovery would require separately coordinated repository
work and is not silently represented as delivered by this endpoint.
