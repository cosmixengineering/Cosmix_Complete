# B2 HR Report Repository Scope

Repository-owned changes are limited to `HrReportRepository.cs`, its partial
`HrReportScopeRepository.cs`, `VerifyHrReportQueries.cs`, and this document.
The parent owns authorization, service contracts, permission definitions and service tests.

## Boundary

All fourteen report reads require the server-created `HrReportSelection`. Each root
employee/source query intersects `selection.EmployeeIds` before projection, materialization,
aggregation or pagination. This includes the four document-expiry queries, monthly summaries,
leave aggregation and payroll lines. No unscoped overload remains.

Selection and report execution must share the parent's existing serializable authorization
transaction. The parent supplies normalized dates, effective `ForSession` grants and the full
required-permission list, then rechecks the session and grants after generation and audit IO.
Selections are internal capabilities, not HTTP input or reusable authorization receipts.

Candidate SQL applies an employee/department/site/team/project grant prefilter for every required
permission using current business-date authority. Caller row filters are separate: daily and
monthly assignment filters use their report dates, KPI department filtering uses the same latest
assignment as its projection, and punch/site-report/OT site filters use the original source site.
A transfer does not hide historical site-A records from an actor authorized for both current
and historical scope. Remaining common assignment filters retain their report/current semantics.
Team/project existence is deliberately a superset: `IRosterRepository.ResourcesAsync`
remains authoritative for effective dates, early termination, active groups, overlapping history
and alternative memberships. Every selected employee must pass `HrReportAccess.Allows` for
current membership and every historical slice. All required permissions must cover all slices.

An unauthorized historical/source slice excludes the entire employee before report pagination.
There is no partial-history total. The selector authorizes a contiguous superset of relevant
source periods, so intermediate transfers also need authorization. This conservative behavior
can omit otherwise individually readable rows; narrowing employee/date filters can help.

Original source sites are independently checked. If roster history does not match the original
site throughout the source period, an additional `(EmployeeId, original SiteId)` resource must
be authorized. Current department/team/project/site authority cannot substitute for that original
location. Explicit employee-wide or All grants can cover it. Current authorization and timestamp
sources use the established `RosterSchedule.Today` business-date converter and its historical
timezone rules, rather than UTC dates or an added offset. Retained business dates are also checked.
For timestamp intervals `[start,end)`, the last occupied business date is derived from `end - 1 tick`;
only the roster call converts the inclusive occupied-date range to an exclusive date bound.
Thus an interval ending at local midnight does not require tomorrow's assignment, while genuine
overnight duty still requires both occupied dates. Payroll batch/month exclusive ends are likewise
converted once. This does not introduce a timezone setting or alter source records.

`ReadRoleAssignmentIds` contains distinct report-read role assignments that actually match
resources of selected employees, including historical and original source resources. Rejected
candidates contribute no basis. Unscoped audit also contributes its All-scope read assignment.
The parent validates and resolves the selected audit role from these current assignments.

## Source Coverage

| Report | Additional protected footprint |
| --- | --- |
| Daily / monthly attendance | Full normalized day/month, work-session dates, timestamps and original sites; retained summary-session links |
| Punch audit | Receipt window, retained business date, device-capture date and original punch site |
| Operational zones | Receipt/capture dates; both report assignment/version location and retained event-version location |
| Site reporting | Receipt/capture dates and original site |
| Late / KPI | Full year, complete assessment periods extending outside the year, latest assignment used by the existing projection |
| Overtime | Request window, actual duty interval/site, linked work-session business date/site and latest compensation effective date |
| Meal allowance | Claim window, complete eligibility interval/original assignment site and linked session date/site |
| Leave | Full year and complete overlapping fiscal leave-year intervals |
| Employee master / document expiry | Current membership; document expiry threshold does not replace the current scope date |
| Fuel / mileage | Claim window, trip interval and selected claimant's latest vehicle service date |
| Payroll handoff | Entire overlapping batch periods and referenced overtime, allowance, fuel, payroll-input-month and loan schedule periods |
| Audit | All required permissions require All scope because arbitrary event target/actor metadata cannot be proven employee-local |

Source-footprint queries apply the corresponding report's filters before the row limit: original
site for punch/site/OT, vehicle for fuel, assignment-version zone for zones, latest-review status
for site reports, approval status (including the existing Pending fallback) for OT, allowance
category and leave type. Latest compensation and vehicle-service sources are correlated to the
filtered parent rows. Daily/master status and assignment filters narrow employee candidates before
their source queries. Filters that the report itself does not use are not invented as source rules.

Payroll provenance that is unknown, missing, or tied to another employee yields an explicit
`hr_report_source_interval_invalid` conflict. It is not silently omitted. The five recognized
source types are `attendance.overtime`, `attendance.allowance`, `hr.vehicle-fuel.claim`,
`attendance.payroll-input` and `hr.advance-loan.schedule`.

Fuel claims only join trips belonging to the claimant. Vehicle service lookup is restricted to
the same selected employee; sharing a vehicle does not authorize another claimant's service data.

Audit employee-tagged events must match selected IDs. Unscoped events require
`IncludeUnscopedAudit` **and** no employee/department/site/branch filter. The repository enforces
this filter condition even if passed an inconsistent selection. `SelectScopeAsync` rejects
non-All audit access, including employee-tagged events with arbitrary metadata.

## Limits

Limits raise HTTP 409 via the existing `AuthenticationFailure` contract; authorization never
silently truncates candidates or source history.

| Limit | Value | Conflict code |
| --- | ---: | --- |
| Candidate employees after SQL prefilter | 256 | `hr_report_candidate_limit` |
| Source metadata rows across all footprint queries | 8,192 | `hr_report_source_limit` |
| Roster resolutions, cached by employee/interval | 1,024 | `hr_report_scope_work_limit` |
| Total resolved alternatives | 65,536 | `hr_report_scope_work_limit` |
| Scopes / alternatives in one resolution | 4,096 / 16,384 | `hr_report_scope_limit` |
| Source/merged historical interval day difference | 3,660 | `hr_report_source_interval_limit` |
| Monthly summaries, leave entries, each document/certification/insurance branch | 16,384 | `hr_report_row_limit` |

The existing roster resolver retains its own timeline/conflict limits. Invalid scope data or
cross-employee resources conflict. Current employee scope is checked separately from old source
periods, avoiding an unnecessary interval between an old report and today. Report page sizes
remain parent-validated (1..1,000). Existing pagination and numerical defects remain H1 work.

## Offline Verification

`VerifyHrReportQueries.cs` uses an EF query-provider replacement over synthetic model rows for
repository behavior, and the real SQL Server provider for `ToQueryString` translation. Connection
interceptors throw on any attempted database connection. It does not create a test project,
call SQL, perform writes, invoke external providers or claim authenticated SQL acceptance.

The baseline audit DLLs reproduced **56 failures / 86 checks**, including foreign-row exposure
and empty-selection exposure in every report and missing scoped method contracts. Frozen RED
runner: `D:\CosmixVerification\part-c-parent-20261013\managed-6020cb3609fd475bb8106c1ad9089246\VerifyHrReportQueries.dll`.
The older common verifier stops its stderr pipeline on the first RED failure; direct execution
of this DLL produces the complete failure list and exits 1.

Final Release solution build passed with **zero warnings and zero errors**. The final standalone
verifier passed **158/158 checks**, including all actual report SQL expressions, read-role basis,
empty-selection/foreign-row isolation in all fourteen reports, all document branches, audit
unscoped-filter conditions, original punch/OT/site locations, extended KPI/OT/fiscal/batch periods,
unknown payroll provenance, candidate/source overflow and zero attempted SQL connections.

Frozen GREEN runner and retained source/logs:
`D:\CosmixVerification\part-c-parent-20261013\managed-504c4acf854346f78ee891df55525603\VerifyHrReportQueries.dll`.
Earlier green runs (131 and 138 checks) preceded the final audit-role and actual-query SQL checks.

## Independent Review Follow-Up

The parent-owned final pre-review Release at
`D:\CosmixVerification\hr-remediation-ab-final-20261015\release\bin\Cosmix.Api\release`
reproduced **9 failures / 168 checks** after adding regressions and before the P2 fixes.
Retained RED runner/source:
`D:\CosmixVerification\part-c-parent-20261013\managed-1eb716b73fd7479b97c2f91a9876f8ae\VerifyHrReportQueries.dll`.
Failures covered historical site filters after transfer (punch, site and OT), daytime and
midnight-exclusive OT boundaries, vehicle and zone filters, and OT/site review statuses. The
existing genuine-overnight denial continued to pass.

The corrected Release built with zero warnings/errors in the repository-specific artifact path.
Expanded verification passed **255/255**, including current authorization across local midnight,
allowance categories, filtered-source overflow, actual query predicates, and SQL `TOP` translation
before bounded client-side business-date conversion. Frozen GREEN runner/source:
`D:\CosmixVerification\part-c-parent-20261013\managed-6d9b67dbf9404408b74aa1b6011b2c25\VerifyHrReportQueries.dll`.
After that pass, the large excluded-zone stress fixture was replaced with an equivalent excluded
allowance-category fixture to avoid repeated correlated-query compilation in the synthetic provider.
The parent then ran the exact final source against
`D:\CosmixVerification\hr-remediation-ab-reviewed-20261015\release`:
**255/255 repository checks passed**, within ten suites / 1,467 passing checks.
Final frozen runner: `D:\CosmixVerification\part-c-parent-20261013\managed-d45fa1c4195a4c789f71914baffc98c0`.
Source hashes were unchanged across the final combined verification. Medical permission changes
are parent-owned and verified in the same run. See Verification.md.

Build output is isolated at `D:\CosmixVerification\hr-remediation-report-scope-20261015`.
Use the existing `docs/attendance-gap-c/VerifyManaged.ps1` runner with its `ReleaseDirectory`
pointing at `bin\Cosmix.Api\release` there and `Sources` set to
`docs/hr-srs-remediation/VerifyHrReportQueries.cs`. The runner freezes managed dependencies and
retains source, input hashes, compiler logs and execution logs in its own unique D-drive folder.

No Git writes, shared DI/schema/application/service/auth edits or test projects are part of
this repository slice. Concurrent parent/other-agent changes must be preserved. Offline checks
do not establish SQL execution plans, production load behavior or end-to-end export acceptance.
