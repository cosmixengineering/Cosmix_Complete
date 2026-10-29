# Factual attendance and policy integration

Implemented locally: factual daily views, own calendar, scoped employee calendar, immutable
calculation revisions/history and configured daily time interpretation using real policy and leave
readers. See [the current continuation](Policy-Leave-Implementation.md) for verification and limits.
No branch was fetched or merged. Ordinary financial payroll calculation remains separate.

HR day/calendar reads tolerate only missing employment assignment as an explicit
setup-required read state: `roster.status=employee_assignment_unavailable` and
`policy.status=schedule_configuration_required`, retaining factual sessions and
null classified/payable values. This does not activate attendance or payroll and
does not change strict recalculation/self APIs. See the
[28 October follow-up](HR-Read-Availability-20261028.md).

| Route | Permission / scope |
|---|---|
| GET `/api/v1/me/attendance/daily?businessDate=YYYY-MM-DD` | `attendance.summaries.self`; session employee only |
| GET `/api/v1/me/attendance/calendar?from=YYYY-MM-DD&to=YYYY-MM-DD` | Same; inclusive range up to 31 days |
| GET `/api/v1/me/attendance/daily/history` | Same; businessDate, page/pageSize 1–100 |
| GET `/api/v1/attendance/employees/{employeeId}/daily` | `attendance.summaries.read`; web MFA, every historical source scope |
| GET `/api/v1/attendance/employees/{employeeId}/calendar` | Same, bounded range |
| GET `/api/v1/attendance/employees/{employeeId}/daily/history` | Same, bounded pagination |
| POST `/api/v1/attendance/employees/{employeeId}/daily/recalculate` | `attendance.summaries.calculate`; web MFA/CSRF, unlocked date |

Recalculate body:

```json
{
  "businessDate": "2026-10-09",
  "expectedRevision": 0,
  "reason": "Review completed factual work sessions"
}
```

Supply the real current revision (zero only before the first save), `Idempotency-Key`, HTTPS
web cookies and `X-CSRF-TOKEN`. No permission is automatically granted. Changed input under
the same key conflicts; stale revision conflicts; revoked original/current scope denies replay.

Facts select the latest immutable revision of each session, reject overlapping/foreign/duplicate
revision data and add closed elapsed time. Open sessions contribute no fabricated completed
duration. Business dates remain the shift-start date, including overnight elapsed time. Whole
minutes are rounded down **after** summing exact elapsed durations; worked seconds and exact
source session revisions remain in protected inputs. Scheduled paid/unpaid breaks do not
automatically deduct factual elapsed time. Per-session allocation minutes are independently
floored explanatory values; sub-minute remainder is retained in original timestamps.

No sessions still means factual `no_accepted_session_evidence`. Only complete verified daily inputs
may classify a closed scheduled day under its explicit policy. `ConfiguredAttendancePolicyCalculator`
now consumes historical policy, pinned roster/holiday, approved leave, raw/corrected duty evidence and
verified OT decisions. Missing/conflicting inputs retain null calculated fields. `InputProvenance`
binds these sources to the revision; legacy snapshots omit it and retain their original fingerprints.
Time results explicitly set `PayrollReady=false`; no salary is changed, withheld or zeroed. The
confirmed OT amount formula remains in its protected feature. Ordinary deductions, payroll settlement
and continuous-duty reset rules are not inferred from daily time results.

Recalculation refuses open sessions and locked dates. It saves an append-only revision, protected
input snapshot, exact session links, audit and receipt under the existing shared SQL authorization
transaction. Unchanged inputs retain their previous revision. Locked reads follow the period's
pinned summary, verify the protected snapshot's complete fingerprint and return its frozen inputs;
missing/ambiguous locked evidence fails closed. Direct locking is implemented but remains blocked
without verified financial readiness; reopening is forbidden. Factual CSV exports cite a saved revision
only when its complete input fingerprint matches the exported facts.

The existing recalculate operation delegates its append step to an assembly-internal method while
retaining its live authorization transaction, scope, idempotency, audit, lock and policy checks.
The append step opens no nested transaction and performs no commit. It is not a public authorization
bypass or a new endpoint. The approved correction workflow reuses this step atomically.

Queries resolve historical employment, Team and Project membership before returning data. Employee,
Site, Department, Team, Project and All grants use the same server-resolved timeline as rosters:
every date slice must be covered, and any equivalent membership within that slice can authorize it.
A Team on one day cannot substitute for the next day's Team. Retained work-session sites are checked
against employment on each source's own business date; a mismatched source adds an independent
Employee/Site requirement, rather than borrowing membership from another date. Site-report queue
permissions remain their existing exact Site/All requirements.

New protected calculation receipts retain the complete original scope groups, and retries require
both the current resolved groups and the original groups. Existing flat receipts retain their ALL
requirements. Empty/malformed scope groups fail closed even with an All grant. This change adds no
permission grants or schema changes. Raw GPS, face templates and sensitive employee fields are absent.

Requires local migration `20261009083807_AddAttendanceCaptureAndFactualSummaries`; **not applied**.
It makes factual policy/pay fields nullable, adds protected input provenance and leaves earlier
applied migrations intact. The migration's Down refuses evidence loss and fabricated fallback values.

Executed: `VerifyFacts.cs` 11 pure factual checks; `VerifySummaries.cs` 61 real Application-service
checks with in-memory collaborators, including permissions, replay, stale writes, snapshot substitution,
locked reads, invalid policy output, historical Team/Project alternatives and original/current receipt
scope checks. `VerifyAttendanceScopes.cs` passed 23 evaluator checks; `VerifyAttendanceScopeQueries.cs`
passed 8 exact EF SQL translation checks, with an interceptor confirming zero database connections.
The query checks cover the source's own business date, employee, nullable site identity, exclusive
employment ending and bounded source selection. These are not authenticated SQL, rollback, provider
or payroll UAT.
