# Attendance Continuation: Client Contracts and UAT

15 October 2026. Backend contracts only. React/React Native implementation,
authenticated HTTP/SQL acceptance, external OTP and real face acceptance are not
established by this document. Executed results belong in Verification.md.

## Encashment

1. An authorized fixed Admin publishes an explicit applicable leave-settings-v3
   policy through the existing leave settings API. Existing v1/v2 records are not
   rewritten. Optional Encashment and Recovery settings have no implicit company
   defaults; every field in a configured section must be supplied and validated.
2. The employee uses POST /api/v1/leaves/mine/encashment with RequestedMinutes,
   LeaveYearStart, exclusive LeaveYearEnd, Reason, LeaveTypeId and PolicyVersionId.
   Use real IDs returned by the owning APIs, not sample or generated GUIDs. The
   employee identity comes from the authenticated session. The required
   Idempotency-Key must be reused only for exactly the same request.
3. The selected policy must cover the valuation date and employee category/service.
   A complete closed fiscal year, exact available leave units, finalized and
   current KPI evidence, resolved disputes and effective monthly compensation
   are required. A missing source returns an explicit conflict, not zero money
   or a default KPI. Submission reserves leave units; it does not pay the employee.
4. GET /api/v1/leaves/mine/encashment returns only the owner's applications.
   GET /api/v1/leaves/admin/encashment/queue requires fixed Admin review authority.
   The currently bounded list includes retained states/history; consumers must
   inspect State and must not assume every returned row is pending.
5. POST /api/v1/leaves/admin/encashment/{id}/decisions requires current web MFA,
   attendance.leaves.review on the active fixed role, ExpectedApprovalVersion,
   Reason and Idempotency-Key. Decision values follow ApprovalState: Approved=1,
   Returned=2, Rejected=3. The requester/beneficiary cannot decide. ApprovedAmount
   is optional and, when supplied, must equal the retained authoritative amount.
6. Approval revalidates current evidence and releases/consumes exactly the owning
   reservation. Return/rejection releases only that reservation. Immutable
   decisions, protected evidence, audit and retry receipts are retained. Legacy
   unverified amounts may be returned/rejected but cannot be approved as money.

Encashment settings explicitly configure eligible leave kinds, service/KPI
eligibility, annual minute limit, divisor, rate multiplier, currency, rounding,
valuation date and submission window. Test settings are synthetic, not company
approval or legal certification. Approved leave valuation is not payroll posting,
settlement, bank advice or payment release. Later settlement-dependent corrections
remain part of the Finance reconciliation backlog.

## Recovery Commitments

POST /api/v1/leaves/{id}/recovery-commitments requires fixed Admin review authority
and a real approved, uncancelled leave with a configured recovery policy. It binds
the real leave, policy and approval IDs and records a bounded date/minute promise.
GET on the same route is owner-only self service and supports mobile Bearer auth;
foreign/unknown parent returns 404 before loading commitment details. Admin writes
remain web/MFA with cookie CSRF where applicable.

A commitment is NOT completed work, a new attendance punch, earned leave, salary
recovery, posting or settlement. Actual fulfilment and any financial consequence
remain separate workflows. Duplicate commitments and limits must be tested across
separate HTTP requests and SQL transactions before acceptance.

## Daily and Monthly Reports

- Daily missing summaries have Status=Missing and nullable quantities; clients
  must render null as unavailable, not 0 or Absent. EarlyLeaveMinutes is unavailable
  until a retained authoritative quantity exists. There is no default eight-hour
  schedule. In/out times come only from the summary's linked work sessions.
- Unlocked days select one latest saved revision. Locked days use the current
  lock's exact retained summary membership, never a newer draft revision.
- Monthly totals count known retained summaries once. Missing calendar dates are
  not inferred absences. SummaryDays, MissingSummaryDays and Source disclose
  coverage; totals are not a certification of a complete/payable payroll month.
- CSV/XLSX carry the same missing-value and provenance distinctions. Nullable
  daily fields and the new monthly fields require client contract updates.
- Metadata SourceLockStatus is scoped_source_records_not_payroll_certification.
  It no longer claims every report is a verified production dataset. The other
  twelve report computations still need their owning H1 correctness review.

## Month Readiness and Isolated Testing

A handoff of calculation A cannot authorize calculation B. Month readiness now
exposes payroll_handoff_source_reconciliation_required and retains the conflicting
chain in its fingerprint. Missing handoff and legal activation dependencies remain.
This diagnostic does not create supersession, refunds or posting receipts.

OT readiness binds persisted request/session/approval/decision evidence, including
protected decision references. Approved OT remains blocked by
overtime_locked_handoff_unavailable until Finance consumes exact retained awards.
An approval-date query is not a substitute for the source duty month. No guard is
removed merely to make a month lock succeed.

In isolated acceptance profiles only, document-expiry and face-retirement workers
are removed alongside the other unbounded workers. Normal deployment registration
is unchanged. This is not approval to start a host or write to any database.

## Required Live Acceptance

- Employee mobile/self, fixed Admin/MFA, foreign employee, custom role, revoked
  session/permission and changed-role receipt replay.
- Submit, approve, return and reject; independent SQL verification of exactly one
  decision/reservation/release/consumption/audit/receipt per accepted operation.
- Same-key retry, changed payload, stale version and separate concurrent requests
  trying to spend the same leave balance or approve the same application.
- Missing/future salary, unfinalized/changed/disputed KPI, wrong fiscal year,
  depleted balance, private evidence failures and a valid submission reviewed
  after its request window has closed.
- Daily/monthly report parity, missing summaries, multiple revisions, locked
  retained membership, linked overnight sessions and CSV/XLSX null handling.
- Stale financial handoff, changed OT decision evidence and legal dependencies
  remain unready; no salary payment or fabricated settlement is created.

The exact approved SQL target and bounded synthetic population must be resolved
before these live tests. See SQL-Preflight-20261015.md. No current database state
is inferred from earlier laptop receipts.
