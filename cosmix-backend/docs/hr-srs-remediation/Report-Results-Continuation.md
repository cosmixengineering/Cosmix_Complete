# H1 Bounded Attendance Report Continuation

Latest parent integration result: reviewed Release passes 153/153 report-result
checks, including pre-pagination totals for 150 employees and pages of 100/50/0.
The report boundary suite passes 266 checks; all-query suite passes 258. Exact
final evidence: D:/CosmixVerification/part-c-parent-20261013/managed-96d7fc12a0004bc490000929fcf90324.
The later total-count correction supersedes the earlier 135-check results/hashes
below. Source aggregate identity and final build are retained in Verification.md.

15 October 2026. Bounded implementation and offline verification complete.
Ownership is limited to the daily and monthly
attendance reads in `HrReportRepository.cs`, a new report helper partial,
`VerifyHrReportResults.cs`, and this record. No shared contracts, service,
scope repository, DI, schema, host, SQL, provider or Git writes are authorized.

## Integration Coordination

- Parent owns the combined Release build. This slice uses the frozen baseline
  `D:\CosmixVerification\hr-remediation-c-20261015\baseline\bin\Cosmix.Api\release`
  with `docs/attendance-gap-c/VerifyManaged.ps1` for RED reproduction.
- Parent removed the obsolete daily status prefilter from
  `HrReportScopeRepository.FilterEmployees`. The authoritative row filter now
  runs after retained-summary selection, before daily pagination.
- Parent approved and implemented nullable daily ScheduledMinutes, WorkedMinutes,
  LateMinutes, EarlyLeaveMinutes, OvertimeMinutes and ExceptionCount, plus monthly
  SummaryDays, MissingSummaryDays and Source, including the export columns.
  This agent consumes those contracts but did not edit shared DTOs or exports.
- Parent replaced the unconditional verified-production metadata with
  `scoped_source_records_not_payroll_certification`. This agent did not edit
  service or metadata contracts.

## Execution Ledger

- Read AGENTS, architecture, H1/B2 plan and B2 scope/authorization records.
- Root cause: monthly read sums every revision and subtracts reported days from
  calendar days to invent absence. Daily read supplies 480 scheduled minutes for
  missing summaries and chooses an unrelated earliest session.
- Existing `AttendanceSummaryRepository.LatestAsync` establishes authoritative
  current lock membership for locked periods, latest revision otherwise.
- Ruling: reuse retained schedule minutes and summary-session identities; do not
  resolve a fresh schedule or infer attendance/payroll outcomes at report time.
- Initial RED: 36 failures / 90 checks against the frozen baseline, runner
  `D:\CosmixVerification\part-c-parent-20261013\managed-74f228261b8a485ca3c37a6eefeccab8`.
- Contract-expanded RED: 42 failures / 81 checks, runner
  `D:\CosmixVerification\part-c-parent-20261013\managed-930d434f0e1847338f396094444279bd`.
  The check count changes because SQL assertions follow observed production
  queries; early scenario failures also stop that scenario's remaining checks.
- Production implementation prepared; parent combined build caught contextual
  keyword `from` in two LINQ query expressions. Renamed helper parameters to
  fromDate/toDate in the owned partial, then held production edits for rebuild.
- Final suite adds scope-selection/status and corrupt membership regressions and
  gives the superseded daily revision 300 worked minutes to reproduce incorrect
  worked totals as well as policy totals.
- Final exact-source RED: **51 failures / 89 checks** on the specified baseline;
  final GREEN: **135/135**, zero failures, against the parent's integrated Release
  at `D:\CosmixVerification\hr-remediation-c-20261015\integrated\bin\Cosmix.Api\release`.
  All checks ran real repository methods against synthetic EF query roots; actual
  captured queries translated with the SQL Server provider's ToQueryString.
  Connection interceptors reported zero SQL connection attempts.
- Final RED source/compiler/runtime/input-hash evidence:
  `D:\CosmixVerification\part-c-parent-20261013\managed-a96f611860cc4102afde4d7cf8fa066d`.
- Final GREEN source/compiler/runtime/input-hash evidence:
  `D:\CosmixVerification\part-c-parent-20261013\managed-7000909db9234877bbb526fb0270538b`.
- Prior integrated GREEN before the nonzero obsolete-work fixture was 135/135,
  runner `D:\CosmixVerification\part-c-parent-20261013\managed-f8500aa4f4134405b3937981ec169a4f`.
- Owned-file diff/whitespace check passed. Final review in this slice was author
  self-review; parent owns the combined build, wider regression suites and review.
  No SQL execution, private-evidence validation, HTTP/client/provider acceptance,
  project creation, schema, DI or Git writes were performed by this slice.

Reproduce with the existing runner (substitute the baseline path for RED):

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:\CosmixVerification\hr-remediation-c-20261015\integrated\bin\Cosmix.Api\release' `
  -Sources @('docs/hr-srs-remediation/VerifyHrReportResults.cs')
```

Stable final source SHA-256 values:

| File | SHA-256 |
| --- | --- |
| src/Cosmix.Infrastructure/Repositories/HR/HrReportRepository.cs | 93DB46C7CFC2ED8C53B3F397F2D972D83B8B41E89205B1CD8F74857FF93C1370 |
| src/Cosmix.Infrastructure/Repositories/HR/HrAttendanceReportResultsRepository.cs | 796400A670DEE40FD287D37E6C1EF485F82B9FFFCEAD86DBA4FFAB5950CBBD51 |
| docs/hr-srs-remediation/VerifyHrReportResults.cs | 23D9FCF152A553E68230F71B15F66FBC5957EF090C41A8BD8003739A3E304939 |

## Result Semantics

- Unlocked days use the maximum saved Revision for each EmployeeId/AttendanceDate,
  independently of CalculatedAtUtc. Superseded revisions are excluded in SQL
  before the row cap. Ambiguous latest revisions conflict.
- Locked days use the current locked event's exact AttendancePeriodSummary
  membership. Missing membership for a saved day, cross-employee membership,
  duplicate membership or overlapping locked periods conflict. Monthly days with
  no saved summary remain missing; they never become absences. Daily reads of a
  locked day without a retained summary retain the existing unavailable conflict.
- Daily schedule minutes come from the selected saved summary and require a
  retained ShiftVersionId. There is no live roster recalculation, 480-minute
  default, elapsed-to-payable conversion or employee-status-to-day-status mapping.
- Missing daily summaries return Missing and null quantities. Factual-only
  summaries retain worked minutes but preserve null policy quantities. EarlyLeave
  is null because this saved summary does not expose that quantity. Explicit
  retained zero values remain zero.
- Daily UTC times use only the selected summary's exact AttendanceSummarySession
  links: first start and final end across all retained sessions. An open retained
  session leaves LastOutUtc null. No stale or unlinked session is substituted.
- Source identifies summary ID/revision, policy and shift IDs, and whether the
  authority is locked-retained or latest-saved. It does not certify current input
  freshness, protected-file integrity, posting, settlement or payment.
- Monthly PresentDays counts observed Present/HalfDay/Hourly dates, not payroll
  equivalent full days. AbsentDays counts only explicit retained Absent outcomes.
  Leave and rest/holiday counts likewise come only from retained statuses.
- Monthly totals sum only retained quantities and round hours once to two places.
  Null payable/OT quantities do not borrow worked time. SummaryDays and
  MissingSummaryDays describe calendar coverage, including pre-hire or otherwise
  unreported dates; they do not assert those dates were scheduled working days.
  Source also reports policy-quantity coverage separately and exact source IDs.
- Employee, department, site and branch filters intersect the authorized employee
  selection before materialization. Pagination orders by EmployeeNumber then Id;
  daily status filtering occurs before Skip/Take. Existing B2 bounds still fail
  explicitly instead of silently truncating input sources.

## Remaining H1 Work

The other twelve reports are untouched. Their B2 authorization evidence does not
prove numerical or format correctness. Separate H1 fixtures and remediation are
still needed for every row below; these are observations/acceptance gaps, not a
claim to have completed a twelve-report audit.

| Report | Remaining value/acceptance gap |
| --- | --- |
| punch-audit | Missing assessment still defaults to Verified; channel/site inference and page offsets need repair/evidence. |
| operational-zones | Inferred zone labels/colors and review status need authoritative-source fixtures and pagination evidence. |
| site-reporting | Review/response selection, date boundaries and page offsets need independent expected-row evidence. |
| late-and-kpi | Proposed assessments are not finalized KPI outcomes; effective assignment, disciplinary selection and revisions need evidence. |
| overtime | ApprovedMinutes still comes from RequestedMinutes; rate uses latest compensation/request month rather than retained award provenance. |
| meal-allowance | Claimed amounts and first settlement record need authoritative decision/settlement and revision fixtures. |
| leave | Fixed /480 day conversion, absolute-value utilization/reversals, fiscal grouping and invented carry-forward/zero fields remain. |
| employee-master | Effective-date assignment selection, null identity fields and page offsets need H1 evidence. |
| document-expiry | Expiry cutoffs, days-until-expiry, each source branch and merged pagination need H1 evidence. |
| fuel-and-mileage | Missing consumption still defaults to 28 km/l; historical service/claim valuation and page offsets need evidence. |
| payroll-handoff | Exact source revisions, financial totals and batch-period boundaries need H1 evidence; batch locking is not payment. |
| audit | Date/category filtering, stable pagination and export formatting need H1 evidence. |

Actual CSV/XLSX/PDF parsing, export audit/private storage, authenticated HTTP/SQL,
database execution plans and load behavior are not verified by this slice.
