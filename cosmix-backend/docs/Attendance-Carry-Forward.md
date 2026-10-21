# Locked attendance corrections: factual carry-forward

Status: local Domain, Application, repository, API and schema configuration are implemented; coordinated
build/verification and migration-owner generation are being recorded separately. This feature authorizes no database
application, real-account change, provider call, financial calculation or Git operation.

## Confirmed behavior

- A locked source month never reopens and its raw events, session revisions, summary, lock event and
  exact summary membership stay unchanged.
- A correction carries its factual difference into the first available unlocked calendar month
  strictly after the source month. Any overlapping locked period makes that whole target month
  unavailable. No open-period rows are invented just to represent a calendar month.
- A requester submits written evidence. A different authorized HR reviewer approves, returns or
  rejects it using the existing scoped correction permissions. Current authority is required on
  every read, write and retry. The employee's own path retains server-derived identity.
- Approved manual duty may address missing/failed Check In; no raw punch, accepted face result or
  biometric provider success is fabricated. Existing provider selection remains deferred.
- Payroll/payable/overtime/late/status formulas are not implied by a factual time adjustment.

## Frozen baseline and successive interpretation

The source is the active locked period's exact `AttendancePeriodLockEvent` and
`AttendancePeriodSummary` membership, identifying one `DailyAttendanceSummary`. The implementation
must read and validate that summary's protected input, its fingerprint and its exact
`AttendanceSummarySession` links. Linked immutable WorkSession rows must match the frozen input's
employee, business date, session IDs and factual totals. Loading the employee/date's latest live
session or summary is not a substitute for that baseline.

An approved adjustment retains a complete protected virtual interpretation of those sessions, with
stable session keys and original source IDs. Later requests start from the latest approved virtual
interpretation, while still identifying the original locked baseline. The approved difference is
`afterTicks - beforeTicks`. Displayed minute difference is
`floor(afterTicks / ticksPerMinute) - floor(beforeTicks / ticksPerMinute)`; flooring a signed tick
difference would incorrectly turn small negative changes into a whole negative minute.

Virtual session changes preserve original source IDs and record the authorizing correction. Manual
additions have their own stable virtual session identity and an explicit manual origin. They never
become target-date WorkSessions. Identical interpretations are rejected as no change; a changed
interpretation with zero net minutes may still be meaningful factual evidence.

Only time changes and manual duty are included in this factual workflow. Day-status, payable,
deduction, salary and overtime decisions need their own approved policy integration.

## Storage and schema ownership

Pending requests reuse immutable `AttendanceCorrection` and `ApprovalRequest` with the distinct
request type `attendance.locked-correction`. A versioned protected request binds the source lock,
summary, exact source sessions, latest prior adjustment, proposed virtual interpretation, selected
target month, written evidence and original authorization scopes. Ordinary unlocked-correction
queues must not silently process this different interpretation workflow.

One new append-only ledger, `AttendanceCarryForwardAdjustment`, contains:

| Field group | Required meaning |
|---|---|
| Identity | ID, employee, source business date, revision, optional superseded adjustment |
| Frozen provenance | Source lock-event ID, exact source summary ID, correction ID, approval-decision ID |
| Target | First-day target month and exclusive first-day next month, with no fabricated period FK |
| Factual values | Before/after/signed delta ticks; no money or payable columns |
| Evidence | Request/source fingerprint, resulting interpretation fingerprint, protected evidence reference, applied UTC time |
| Approved query bounds | First start/last end of the after-interpretation, derived from its exact sessions and validated against protected evidence before use |

Uniqueness is required for the correction and `(EmployeeId, SourceBusinessDate, Revision)`. Restricted
foreign keys bind the actual source summary/event, correction, approval decision and prior ledger
entry. The entity constructor checks the supplied real entities agree on employee/date/source,
the distinct approved request and different reviewer, and the preceding revision. `IAppendOnlyEntity`
protects against later mutation/deletion. SQL checks require a genuine calendar target month
after the source month, nonnegative before/after totals, exact signed delta and coherent first/prior
revision identity.

The existing `AttendanceCorrection.AdjustmentPeriodLockId` remains null: a valid target calendar
month may have no period row, and a nullable unrelated period FK cannot represent that date range.
The parent owns DbContext registration, migration/designer and snapshot changes, after reviewing the
feature model/configuration. Nothing in this design applies a migration.

The migration's Down path must refuse to drop a nonempty adjustment ledger, and must also refuse if
retained `attendance.locked-correction` requests depend on the feature, instead of deleting approved
history or leaving pending requests silently unprocessable. Foreign-key delete behavior is Restrict;
no cascade or cleanup worker may erase these adjustment records. A future retention decision requires
a separate reviewed process. Rollback guards and migration generation belong to the parent owner.

## Workflow and transaction boundary

1. Preview validates the frozen baseline, overlays the latest approved virtual interpretation,
   resolves the first unlocked target month and returns explicit IDs/fingerprints and factual values.
   Preview creates no fixtures, requests or period records.
2. Submit requires the displayed source/prior/target fingerprint and immutable typed proposal plus
   bounded written evidence. Source and target employee/date scopes are resolved server-side and
   retained in protected evidence. Missing or stale source evidence fails closed.
3. Queue/history/detail use current scoped authority plus original protected scopes. Bounded opaque
   continuation has no hidden-record total and never freezes authority.
4. Approve revalidates the source lock membership, expected prior interpretation, current source and
   target scopes, historical employment/site relationships, timing/overlap checks and the first
   unlocked month. If the displayed target has changed, the request conflicts; it is not silently
   rerouted to a different month. The requester cannot decide their own request.
5. Approval decision, new ledger revision, protected evidence, static notification outbox metadata,
   audit and idempotency receipt share the existing serialized authorization transaction. No source
   summary recalculation or source/target WorkSession write takes place.
6. Return/reject/cancel change only the linked approval lifecycle; resubmission is a fresh immutable
   request with an explicit returned-request reference and refreshed source/prior/target proof.
7. Target-month factual reads show original source dates and signed approved differences separately
   from that month's actual worked sessions, with financial integration explicitly pending.

Target-month reads sum **all approved ledger deltas posted to that month**, not only the latest
revision of each source date. For example, an original +2 hours posted to November stays there after
November locks; a later -1 hour revision goes to December if that is the first remaining open month.
The combined adjustment is +1 hour. Latest-revision selection belongs to virtual interpretation and
overlap checks, not to deletion/replacement of already posted monthly differences.

Overlap validation must include latest virtual interpretations for other source dates, not just raw
WorkSessions. Otherwise two approved manual adjustments could count overlapping duty twice. Each
source's locked original windows are replaced by its latest virtual windows only in this validation
view. Candidate scans must be bounded and fail closed, and protected interval contents must be checked
before using denormalized bounds. No unrelated hidden employee/session details are returned in errors.

## Required offline verification

Meaningful checks must cover exact lock/summary membership mismatch, stale live rows ignored in favor
of pinned sources, tampered protected evidence, first unlocked month selection across partial/overlapping
locks and year boundaries, target change during review, different reviewer, stale prior revision,
same-key replay, repeated correction without double-counting, negative sub-minute deltas, overnight
site transfers, historical resignation, overlapping virtual duty across source dates, original/current
scope denial, manual interpretation without face/raw fabrication, and SQL query translation without
opening a connection. Actual SQL transaction/concurrency, storage and authenticated HTTP acceptance
remain separately authorized work.
