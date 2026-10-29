# Direct attendance month lock implementation plan

## 28 October 2026 — persistence guard correction

New lock writes now require direct protected-manifest provenance and nonempty,
unique, in-period summary membership. Newly inserted legacy approval-backed
fixture locks are rejected; retained legacy rows remain readable and immutable.
See [diagnosis and verified limits](Locked-Summary-Diagnosis-20261028.md). This
code fix requires no migration and does not rewrite existing fixture history.

## Latest second-pass dependency integration

The second local pass replaces the daily `PayrollReady` readiness test with a transaction-local
`IMonthLockFinancialReader`. The installed reader verifies the latest protected monthly input
revision against current calculation/jurisdiction sources, checks exact handoff evidence and
reports missing/stale sources, handoff and legal activation independently. It never opens a
nested Attendance operation. Current and retained financial scopes require the separate payroll
read permissions, in addition to the global month-lock permission; no grant is added automatically.

New finalization manifests use schema 2 and retain one exact financial source revision per covered
employee, with source evidence, handoff and legal activation fingerprints. The finalizer rejects
changed financial revisions in the preview and reinspects sources after private-manifest IO before
staging the lock. Schema-1 locked evidence stays readable and is not upgraded or given invented
financial membership. A schema-2 detail verifies retained exact source membership, not today's salary.
The optional `payrollInputs` detail contains IDs/hashes only, not salaries or private storage paths.

**Still incomplete:** the installed reader does not have formally authorized legal activation evidence.
It reports `legal_payroll_activation_required`; it cannot enable positive production finalization.
No-charge handoff has its own `payroll_no_charge_handoff_required` dependency, rather than a fabricated
zero line. OT handoff and carry-forward valuation retain their existing explicit blockers. This is
implemented source/dependency inspection and protected membership infrastructure, not complete
payroll readiness or actual SQL finalization acceptance. See [second-pass evidence](Continuation-Second-Pass-20261012.md).

The sections below retain the earlier implementation-stage contract and are superseded where they
refer to daily financial flags or schema-1-only new manifests.

**Goal:** One explicitly authorized person previews and directly finalizes a complete calendar month.
There is no request/review chain and no reopening endpoint. Source months remain immutable; later
corrections use the separately confirmed first-available-unlocked-month adjustment workflow.

Daily time calculation now explicitly returns `PayrollReady=false`. Month preview reports
`attendance_financial_calculation_required` until a complete verified financial calculation exists.
This is a calculation dependency, not another approver or permission to stop salary. Null readiness
on legacy snapshots also cannot authorize a new lock; already locked evidence remains preserved.

**12 October continuation:** An exact `409 employee_assignment_unavailable` while loading one
required employee/day now becomes a preview issue with that employee/date. Other cells continue
to be inspected, required coverage remains unchanged, the issue affects the preview fingerprint,
and finalization returns `attendance_month_not_ready`. Access denial, conflicting assignment
history, corrupt evidence and unexpected errors still propagate. The earlier isolated HTTP stop
remains historical evidence; this improvement has separate synthetic service verification.

**Architecture:** Application MonthLocks owns readiness, versioned protected evidence and the direct
write under AttendanceOperation's existing serializable authorization transaction. Infrastructure
owns bounded population/summary/event queries. API delegates only authenticated binding. Domain
retains legacy approval-backed event evidence and adds an explicit direct-lock event, never a fake
approval row. Root owns the coordinated migration; no migration is applied by this work.

**Stack:** Existing .NET10/C#/EF Core SQL Server and private encrypted storage; no new packages/projects.

- [x] Define current/preview/finalize contracts, explicit global permissions, bounded complete month
  coverage and source fingerprint including roster/policy/summary and posted carry-forward values.
- [x] Retain legacy approval ID as nullable; add private evidence reference and direct-lock method
  using a preallocated event ID, actor, reason, timestamp and immutable exact summary membership.
- [x] Add readiness blockers for policy integration, missing/stale summary, missing protected inputs,
  open duty, pending/unverified source evidence, incomplete coverage, overlapping period and bounds.
  No payroll-ready finalization occurs while the policy calculator is unavailable.
- [x] Preview is read-only to business rows. Finalize requires the exact preview fingerprint, expected
  period rowversion (or explicit null for no existing period), reason and idempotency key. Recompute
  under the shared transaction; reject changed sources before creating evidence/rows.
- [x] Save direct event, exact AttendancePeriodSummary links, protected manifest, audit and receipt
  atomically. Return frozen evidence/provenance on current detail and retry. All-scope is required
  because existing periods are global; a site-scoped request must not lock unseen employees.
- [x] Share versioned lock-manifest validation with carry-forward consumers, retain supported legacy
  events only after genuine approval/decision provenance validation, and reject unverifiable rows.
- [ ] Add standalone rules/service/SQL-translation checks without test projects or SQL connections.
  Parent coordinates Release build and registered anonymous HTTP checks; authenticated SQL rollback,
  restart, concurrency and encrypted-storage acceptance remain separate.

The caller cannot choose a subset of employees, inject summary IDs or override readiness. Technical
enumeration limits fail explicitly instead of truncating a month. No guessed salary, absence, late,
overtime, settlement, lock authority role name or automatic permission grant is introduced.

## Current API and readiness contract

- GET `/api/v1/attendance/month-locks/{year}/{month}/preview`: `attendance.month-locks.read`.
- GET `/api/v1/attendance/month-locks/{year}/{month}`: same permission, validated frozen manifest.
- POST `/api/v1/attendance/month-locks/{year}/{month}/finalize`: `attendance.month-locks.finalize`,
  `Idempotency-Key`, `expectedPreviewFingerprint`, explicit nullable `expectedVersion`, and reason.

All three require current web MFA/CSRF plus All scope. The month is global; a Site/Team/Project grant
cannot finalize or read unseen staff. Role names carry no authority and no permission is auto-granted.
The direct event retains the actual actor, reason, timestamp and protected manifest with no synthetic
ApprovalRequest. There is no reopen method available for new writes, and no reopen HTTP endpoint.

Only a fully ended calendar month may finalize. Coverage includes every verified employment day,
including a partially worked departure day and suspended employment needing explicit policy treatment.
All raw/session/summary source employee-days are also included so orphan sources are never omitted.
An empty/unverifiable population, undocumented lifecycle, candidate overflow or conflicting source is
not an empty safe month. Maximums: 2,048 employee candidates, 32,768 day/source cells, 16,384 lifecycle
rows and 2,048 status rows per employee; overflow fails explicitly. This synchronous path prioritizes
correctness for low-volume administration; no throughput claim is made.

Every required day must have the latest immutable policy-calculated summary, the same live facts/
roster/policy/input hash, its protected input snapshot and exact session membership. Non-null policy
IDs alone do not establish readiness. Missing, stale, unverified or incomplete policy outcomes and
open duty block finalization. Pending corrections and unresolved raw evidence are visible blockers.
The installed unavailable policy calculator therefore cannot finalize a payroll-ready month.
Pending OT adds `pending_overtime`; approved OT adds `overtime_payroll_settlement_unavailable`
until payroll can consume its exact financial evidence. Request/approval/source identities enter
the preview fingerprint so competing OT changes cannot silently disappear during locking.

All ledger postings targeting this month—including superseded source revisions—are fingerprinted
and frozen. A factual-only carry-forward currently adds `carry_forward_financial_calculation_unavailable`:
worked-time differences do not establish payroll settlement. This is an explicit future integration
requirement, not a silently zero-valued adjustment. The locked manifest preserves all exact posting IDs,
values and fingerprints when policy integration later makes the month eligible.

Preview reports full IssueCount and the first200 deterministic issues. Finalization recomputes the
complete state in the same authorization transaction, verifies expected rowversion/fingerprint, then
stages event/membership/private evidence/audit/receipt atomically. Exact retry requires current authority.
Legacy approval-backed events are checked against exact request/decision subject, actor and time;
without a verifiable protected lock manifest they remain explicitly unavailable for new frozen reads
and carry-forward authority. Existing legacy rows are retained rather than rewritten as direct locks.

## Verification state

Executed: **34** standalone service checks and **41** actual SQL-translation/model checks, including
month-bound OT source/state/provenance. The combined **587** anonymous HTTP checks include all three
month-lock operations. These are not authenticated SQL acceptance.
No SQL database writes, migration application, employee fixtures or real financial acceptance were
performed for this module. Actual encrypted-storage rollback, concurrent locks, source races and
persisted restart replay still require the separately approved isolated SQL environment.
