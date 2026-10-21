# ESS Leave, Calendar and Assignment Consistency

Task D, 15 October 2026. Existing shared checkout; no Git synchronization,
commit, branch or worktree. Existing financial ESS changes are retained.

## Readiness

Parent integrated verification now passes: ESS consistency 37/37 and financial
boundary 21/21, with a zero-warning/error Release build. Latest combined runner:
`D:\CosmixVerification\part-c-parent-20261013\managed-ad7fea45209f43d1a9136175630b644e`.
The statements below about final verification pending describe the earlier agent
handoff; they are superseded for these two standalone suites only. Actual
authenticated HTTP/SQL and client acceptance remain pending.

Source and standalone verifier prepared for the parent's next full build slot.
Final Release/behavioral verification is **pending**, not a completed acceptance
claim. The parent requested that this agent stop further builds because of
concurrent compiler pressure; the just-started retry was interrupted.

No Program, DI, DbContext, migrations, permission catalog, shared Leave files or
global progress documents were edited. Existing registrations already provide
LeaveService, AttendanceSummaryService and EmployeeSelfService. No shared code
registration edit is required. Parent owns global progress/client handoff and
the unrelated principal-only, zero-interest/zero-charge financial work.

## Implemented Behavior

- Profile resolves employment through OfficeRosterRepository.EmploymentAsync,
  the dated reader used by Attendance. Start is inclusive, end exclusive, and
  the injected clock uses RosterSchedule.Today (Asia/Karachi). Future assignments
  cannot replace current employment. Overlap returns a null ActiveAssignment
  with AssignmentStatus=conflict; no membership returns unavailable. AssignmentAsOf
  states the business date. Profile reads run within the authorization transaction.
- Calendar delegates to AttendanceSummaryService.CalendarAsync with self identity.
  Its per-day roster, accepted session revisions, factual calculation, policy
  explanations and locked private evidence remain authoritative. There is no
  individual-shift fallback, Sunday convention, raw session summation or arbitrary
  latest saved summary. Missing frozen evidence keeps the shared 409 error.
- Leave discovery and balances delegate to LeaveService.TypesAsync,
  SelfPoliciesAsync and BalanceAsync. A unique applicable policy is read; multiple
  candidates require explicit selection. Missing/conflicting evidence produces
  null quota context and explicit statuses/reasons, never CL/SL defaults.
- Exact numerator/denominator quota values, selected fiscal boundaries, usage
  basis, service eligibility and paid-kind configuration are preserved. Shared
  ledger provenance, cancellations/releases, original policy denominators,
  encashment reservations and fiscal-transition guards remain in the Leave reader.
  ESS adds no entitlement, ledger write or reservation for pending applications.
- Request discovery is employee-filtered and bounded to 20 recent / 5 upcoming
  IDs. Approved cancellations and supersessions are filtered before the upcoming
  limit. DetailAsync supplies exact requested minutes, day intervals, decisions,
  cancellations and supersession metadata. Pending counts exclude unsubmitted
  and superseded requests. An unsubmitted recent row with missing approval fails
  closed via the existing leave_approval_unavailable error.
- Session and ESS grants are checked before reads and again before returning
  profile, calendar and leave results. Authoritative self permissions are also
  rechecked after calendar/leave IO. Shared source read audit failures propagate.
  Existing financial transaction, permission, audit, salary and ledger behavior
  is preserved.

## Client Contract Changes

`GET /api/v1/self-service/attendance` keeps its year/month parameters. Invalid
values now fail validation instead of silently using the current month. Days
are the existing AttendanceDayResponse contract, including BusinessDate, Facts,
Roster, Policy, IsLocked and LatestSavedRevision. Monthly classification counts
and overtime are nullable when any day lacks complete calculated classification.
TotalWorkedMinutes remains factual time, not payable time. Old flattened shift
titles/punch-time fields are removed rather than guessed from another source.

`GET /api/v1/self-service/leaves` adds AsOf and Status. Each balance entry carries
Status, optional Balance (LeaveSelfBalanceResponse), ApplicablePolicies and
Reasons. Old integer EntitledDays/UsedDays/PendingDays/RemainingDays are removed.
RecentRequests and UpcomingApprovedLeaves now use the shared LeaveResponse with
RequestedMinutes and exact Days; the old calendar-span DaysCount is removed.
Quota visibility does not promise service eligibility, paid treatment or approval.

`GET /api/v1/self-service/leaves/balance?leaveTypeId=...&policyVersionId=...&asOf=...`
allows explicit policy/date selection using LeaveSelfBalanceRequest. It returns
the shared LeaveSelfBalanceResponse without reinterpreting its fractions.
AsOf selects applicability/year; ledger usage is currently recorded usage for
that year, including future approved leave, not a historical ledger snapshot.
Fiscal/entitlement context is usable only with verified policy evidence.

Calendar requires `attendance.summaries.self` in addition to the existing
`ess.attendance.read` or `auth.self.read` boundary. Leave endpoints similarly
require `attendance.leaves.self` plus `ess.leaves.read` or `auth.self.read`.
No new or automatic grants were created. Existing consumers/provisioning need
parent review of these explicit source permissions and changed response shapes.

The overview makes bounded independently audited source reads; its type balances
are not advertised as one atomic cross-type ledger snapshot. Each balance retains
the source EvaluatedAtUtc. Upcoming means StartDate on/after today's local date;
ongoing leave remains in request history.

## Verification Evidence

Baseline Release succeeded with 0 warnings/errors:
`D:\CosmixVerification\ess-consistency-20261015-d-a71\red\bin\Cosmix.Api\release`.

Initial actual-repository verifier: **2/7 passing, five expected failures** for
future assignment, inclusive assignment end, hidden overlap, invented casual
entitlement and assumed Sunday rest. Retained source, dependency hashes and logs:
`D:\CosmixVerification\part-c-parent-20261013\managed-dcacef4ac27b4c6a9ba9588965a5d995`.
An earlier harness constructor used an invalid casual leave advance-approval
flag; it was corrected before this failure-first run and is not product evidence.

The expanded VerifyEssLeaveConsistency.cs is prepared, **not yet executed against
the final source**. It exercises real ESS, LeaveService, AttendanceSummaryService,
AttendanceFacts, OfficeRosterRepository and ESS repository predicates with
synthetic collaborators. Cases include exact thirds and signed release, fiscal
year/transition, ambiguous policy selection, no evidence, session/grant/audit
denial, per-date roster transfer, corrected session revisions, locked evidence,
cancelled-before-limit selection and exact two-hour request allocations. Its SQL
connection interceptor denies every connection attempt; no tracked writes are
permitted. Synthetic queries do not prove SQL translation or database races.

One post-edit build attempt failed in concurrently edited HrReportService.cs
(nullable/formatted-value compile errors); another returned nondiagnostic
MSB6006/csc exit 1. The next retry was stopped when the parent reserved the build
slot. No passing final build or final verifier count is claimed. Scoped
`git diff --check` passed.

Parent verification after a successful unique-output Release build:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory $parentReleaseDirectory -Sources @(
  'docs/hr-srs-remediation/VerifyEssLeaveConsistency.cs',
  'docs/hr-srs-remediation/VerifyEssFinancialBoundary.cs',
  'docs/attendance-completion/VerifyLeaveSelfReads.cs',
  'docs/attendance-completion/VerifyLeaveQueries.cs',
  'docs/attendance-completion/VerifyFacts.cs',
  'docs/attendance-completion/VerifySummaries.cs',
  'docs/hr-srs-remediation/VerifyRemediationContracts.cs'
)
```

No SQL/instance/seed/account/credential/host write, real provider call, legal
activation, payroll posting, new test project or migration occurred. Real
authenticated HTTP/SQL, independent-connection races, encrypted-storage acceptance
and client integration remain outside this evidence.

## Exact Changed Files

1. src/Cosmix.Application/HR/EmployeeSelfService/EmployeeSelfService.cs
2. src/Cosmix.Application/HR/EmployeeSelfService/EmployeeSelfServiceContracts.cs
3. src/Cosmix.Application/HR/EmployeeSelfService/IEmployeeSelfServiceRepository.cs
4. src/Cosmix.Infrastructure/Repositories/HR/EmployeeSelfServiceRepository.cs
5. src/Cosmix.Api/Controllers/EmployeeSelfService/EmployeeSelfServiceController.cs
6. docs/hr-srs-remediation/VerifyEssLeaveConsistency.cs (new)
7. docs/hr-srs-remediation/ESS-Consistency-20261015.md (new)

Preexisting dirty portions of these files are not claimed as this task's edits.
