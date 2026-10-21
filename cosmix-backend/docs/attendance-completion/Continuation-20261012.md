# Attendance completion continuation — 12 October 2026

User instruction: complete remaining Attendance in this existing local checkout. Preserve all
current work. No Git/GitHub mutations, policy-feature integration from another checkout, test
projects, real-account changes, or additional SQL fixture writes without their separate approval.

## Current authorized work

1. Make month readiness usable with known missing historical staff setup: report the affected
   employee/day, retain coverage, and keep finalization blocked. Unexpected failures still propagate.
2. Align mobile next-action guidance with current lock/schedule restrictions.
3. Add explicitly scoped exception queue/detail and own safe exception detail. Read-only access is
   separately permissioned; this does not invent a new review/approval chain.
4. Add factual multi-employee dashboard using existing report scopes, bounded explicit employee/date
   filters and the same factual summary inputs. Unknown pay/absence remains unknown.
5. Correct known overlapping-schedule exception classification without choosing a conflicting roster.
   Add scoped management punch and immutable session-revision audit reads with safe DTOs and
   protected continuation; current assessment does not replace original raw evidence.
6. Document exact Attendance-side policy/leave/payroll input and readiness requirements. Preserve
   the missing HR implementation and avoid making payroll settlement a prerequisite of its own lock.
7. Run meaningful affected standalone checks, registered anonymous HTTP checks and Release build.
   Propose the next bounded authenticated SQL acceptance run separately; do not reuse old journals.

## Decisions requested at this historical checkpoint

The user subsequently confirmed HR-only site-report decisions, the reminder delay in the shift
form, and implementation of Attendance policy in this local checkout. See
[the approved follow-up](Approved-Workflow-Continuation.md) for current work and verification.
The following list records the earlier questions, not the latest authority state.

- Site-report decision authority and whether Accept/Return/Reject are wanted. Received reports are
  currently immutable; a complete returned-report response flow must accompany any such decision.
- Missing-Check-Out/excessive-duty configuration: per site or company-wide; numerical values remain
  explicit configuration and must not cause automatic checkout or salary deductions.
- Whether the user will bring the separate HR policy implementation locally or now authorizes its
  local implementation. Existing instruction forbids duplication/integration without direction.
- Face provider remains explicitly deferred. No real face/liveness success can be established.

## Evidence ledger

- Month preview regression reproduced (`employee_assignment_unavailable` aborted preview), then
  fixed. Standalone service checks passed; final current count is recorded in Verification.md.
- Mobile guidance regression reproduced (locked duty advertised a face challenge), then fixed.
  Capture service harness: 89 passed, synthetic collaborators only.
- No schema/migration/database write has been performed in this continuation.
- Exception read service: 54 passed; exact repository SQL translation: 35 passed.
- Management audit reads: 46 service/authorization and 12 SQL translation checks passed.
- Factual dashboard/batch: 54 passed. Capture rules: 15; month-lock service: 40;
  correction regression: 99. All are standalone synthetic/offline checks, not SQL acceptance.
- Final Release build: 0 warnings/errors. Fresh anonymous HTTP/OpenAPI/traffic sweep:
  697 passed across 114 Attendance and 58 existing employee operations. Initial sandbox host
  attempt failed on Data Protection/localhost access; the same bounded check passed outside it.
- Independent source reviews found no actionable defects in the added reads and two fixes.

The historical 587 HTTP / 59 SQL-private-evidence checks predate these additions. They are not
automatically attributed to the new code. Full production Attendance remains dependent on approved
provider, policy and operating configuration plus authenticated/device/load acceptance.
