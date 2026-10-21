# Attendance policy integration contract proposal

**Superseded implementation status, 12 October:** direct HR publication, real historical policy
resolution, Admin leave review and a provenance-bound daily time calculator now have local code.
See [current implementation](Policy-Leave-Implementation.md). The registered calculator is
`ConfiguredAttendancePolicyCalculator`; it explicitly returns `PayrollReady=false`. The original
proposal below remains design history for the separate financial/payroll adapter and real SQL
acceptance. Its references to absent policy/leave producers or unconfirmed HR ownership are historical.

Status: **design proposal only**, 12 October 2026. This document does not introduce runtime
interfaces, activate a calculator, approve policy values, change HR ownership, or relax month
locking. It records the bounded Attendance side of a contract to agree with the HR developer.

Subsequent user authorization permits completing Attendance policy locally. A typed configuration
validator now exists; it does not implement the proposed calculation/leave/payroll contract below.
Publication and leave authority questions remain separate. This historical proposal's ownership
restriction is superseded by that user authorization; its evidence and calculation guards remain.
See [Implementation](Implementation.md), [Summaries](Summaries.md), [Overtime](Overtime.md),
and [Work Ownership](../Work-Ownership.md) for current delivery and ownership boundaries.

## Existing executable contract

`Application/Attendance/DailySummaries/AttendanceCalculationContracts.cs` currently exposes:

```csharp
Task<AttendancePolicyResult> CalculateAsync(
    Guid employeeId, DateOnly date, FactualAttendance facts,
    EffectiveRosterResponse roster, CancellationToken ct);
```

The registered implementation is `UnavailableAttendancePolicyCalculator`. It reports
`policy_integration_unavailable`; payable minutes, late minutes, overtime minutes and day status
remain null. `FactualAttendance` contains total elapsed seconds/minutes, open-session count,
session IDs and an input fingerprint. Its public aggregate is not sufficient by itself to
calculate arrival lateness, partial-day leave overlap or approved financial adjustments.

Existing daily summary fingerprints and protected locked evidence must remain readable exactly
as written. This proposal is not an instruction to change serialized records or recompute old
fingerprints. Existing OT decisions have their own protected exact-tick financial evidence and
separate financial permissions. Ordinary summary/lock permissions do not grant salary access.

Local HR Policies, Leaves and PayrollHandoff currently have Domain entities and EF mappings,
but no executable Application services or corresponding repositories/controllers. Generic
`PolicyVersion.RulesJson`, an approval ID, or an empty leave table is not an integrated policy.

## Recommended boundary and alternatives

Use an explicit immutable Attendance input snapshot, populated by bounded module-specific
readers inside the caller's existing authorization transaction. HR supplies validated policy and
leave interpretations; Attendance supplies exact duty and approved OT/carry-forward references.
The calculator operates on that frozen snapshot and returns a separately versioned result.

Putting hidden database reads inside the existing calculator would require fewer interface
changes, but would make dependency completeness and source fingerprints difficult to audit.
Having Attendance interpret arbitrary HR JSON or implement HR approvals would duplicate HR
ownership and invent unsupported schema semantics. Neither is recommended.

Do not add unused runtime interfaces now. Agree the following logical shapes with the HR owner
first, then implement only the adapters with actual producers. Names below are proposed,
not currently registered services or public HTTP request types.

## Proposed protected input: AttendanceInputSnapshotV1

| Component | Required contents |
|---|---|
| Identity and date | Schema version, employee ID, business date, time-zone ID, calculation version, bounded capture instant, canonical snapshot fingerprint |
| Employment | Exact dated assignment ID/version, category, site/office, relevant approved lifecycle events, and original resource-scope proof |
| Schedule | Exact resolved shift version and assignment/temporary-change references, UTC start/end, local business-date interpretation, configured break intervals, and pinned holiday-calendar revision |
| Sessions | Canonical latest revision of each session key: ID, key, revision, business date, exact UTC start/end ticks, raw CheckIn/Checkout IDs, assessment revisions, correction/decision references, site and shift version |
| Policy | Approved policy version(s), schema version, canonical rules digest, dated assignment/end references, supported typed rule interpretation, and verified publishing/decision evidence |
| Leave | Authoritative coverage state plus approved per-day/interval leave effects, source request/version, decision, cancellation/reversal references, treatment, exact duration/allocation and applicable policy reference |
| OT | Complete relevant pending-request identities and approved award identities/decision versions, exact approved ticks, formula version, protected financial-evidence digest and source-date provenance |
| Carry-forward | Every applicable immutable ledger revision, original source date/lock/summary, target period, delta ticks, protected interpretation digest and approved financial interpretation status |
| Other effects | Only supported, explicitly approved travel/waiting/exception inputs required by the selected schema; missing required producers block calculation |

The snapshot is internal protected evidence, never a client-authored request or ordinary summary
DTO. Store salary/base amounts and resulting money in a separate protected financial envelope.
Public projections may expose safe dependency codes, source identities already authorized by
the endpoint, calculation version and nonfinancial explanations. They must not expose private
storage references, leave medical evidence, salary inputs, raw policy secrets or financial amounts.

### Time and session validation

- Require nonempty bound identities; use UTC instants with zero offset and exact ticks. Retain
  the explicit business date/time zone instead of assigning overnight time by UTC calendar date.
- Use the latest immutable revision per session key, reject duplicate revisions and overlapping
  intervals, and reject open sessions for a final financial result. Reuse the established shift-start
  business-date rule; do not invent next-day reset or rest-credit semantics.
- Validate raw punch employee/action/site bindings, latest accepted assessments, consumed passing
  CheckIn face evidence where required, and correction approval/application provenance. Manual
  approved duty remains distinguishable from face-verified raw CheckIn evidence.
- Preserve exact original session intervals. Any approved pre-shift exclusion, break deduction,
  rounding or date split is an explicit calculation line tied to a supported policy rule.
  The confirmed OT no-break rule does not silently become the normal-pay rule.
- Verify employment/site coverage over the occupied interval, including transfers and overnight
  boundaries. A current assignment must not replace missing historical evidence.

### Policy and leave validation

- Resolve policy applicability server-side using effective dates and historical population
  membership. Bind each assignment to its exact policy version and any append-only ending.
  Unknown schemas, missing versions, invalid approvals and competing applicable interpretations
  return unavailable/conflict. Do not assume employee overrides population or newer IDs win.
- Verify the correct approval request type, subject, state, terminal decision, decision actor and
  publishing time using the HR producer's agreed provenance contract. An arbitrary approved
  request cannot authenticate a policy. Approval authority/segregation comes from confirmed HR rules.
- A leave reader must distinguish **complete with no applicable leave** from **integration
  unavailable**, incomplete query, and conflicted evidence. Absence of rows must not imply absence
  of leave or authorize an absence deduction.
- Leave effects must identify exact day or time-interval allocation. Do not spread a multi-day
  request's total minutes evenly, infer a half-day interval, or infer holiday/week-off pay.
  Validate employee/date coverage, approved decision, source policy, and controlled cancellation
  or reversal. Preserve the complete effective evidence set in the fingerprint.
- Keep medical/exam attachments within HR private storage. Attendance consumes a verified
  eligibility/evidence outcome and provenance, not attachment contents or diagnoses.

### OT and calculation provenance

- Consume the existing OT service's validated immutable decision evidence; do not recompute an
  award from today's salary or replace its formula. Check request/employee/date/session bindings,
  approved ticks, source fingerprint, compensation record, source-month calendar days, PKR,
  formula version and final half-up whole-rupee amount against the retained evidence.
- Pending OT blocks readiness. Each approved award must be included exactly once in the proposed
  financial manifest. A stale or excluded award is an issue, not zero money. A later approved
  source correction needs explicit reconciliation; it cannot silently rewrite an awarded decision.
- Produce a calculation version, input digest and ordered explanatory rule lines. Separate factual
  duration, policy-interpreted payable duration and financial amount. Unknown rules produce no
  successful financial result; warnings cannot substitute for a missing mandatory dependency.
- Fingerprint canonical ordered content and schema versions, including validated source revisions,
  decisions and reversals. A changed salary version, approval, leave cancellation or OT decision
  must change readiness even when the visible aggregate remains identical. Protected evidence
  validation remains mandatory; matching hashes alone do not establish approval or authenticity.

## Pre-lock readiness and post-lock payroll processing

The current month-lock workflow correctly blocks uncalculated policy, pending OT, approved OT
without integration, and carry-forward without financial interpretation. Keep those guards until
the actual producer/consumer is implemented and verified. A feature flag cannot replace evidence.

Proposed flow:

1. Inside the existing authorized serializable transaction, resolve the complete month population
   and every required source. Build a proposed `AttendancePayrollReadinessManifestV1` containing
   the exact day summaries, policy/leave evidence digests, each approved OT decision, each
   carry-forward revision, financial calculation-envelope digest and all unresolved issue codes.
2. A functioning payroll calculation adapter validates and incorporates those inputs. This is
   **calculation readiness**, not evidence of payment, Accounts processing or an already locked
   payroll batch. With no adapter, report unavailable and retain all current lock blockers.
3. Month preview exposes a safe fingerprint and issues. Finalization re-resolves and revalidates
   that complete manifest under the same transaction, checks the expected fingerprint/version,
   and atomically creates the lock event and protected membership manifest. Any concurrent
   source change causes conflict. Existing direct single-authority locking remains unchanged.
4. After the lock exists, PayrollHandoff creates its authorized immutable lines referencing that
   exact `AttendancePeriodLockEventId` and manifest, then performs its separately approved
   verification/processing workflow. Deduplication must cover a source across batches, not only
   within one batch, unless an explicit approved reversal/adjustment references the original.
5. Accounts processing produces its own traceable confirmation. Later corrections use the
   confirmed first-later-unlocked-period adjustment flow; they never reopen the source month.

`PayrollHandoffLine` presently requires an existing lock-event ID. Therefore requiring a completed
payroll handoff line or processing record before creating that same Attendance lock would form a
dependency cycle. Readiness must precede the lock; handoff/processing must consume the lock.
Payroll approval authority and settlement meaning still require the agreed HR/Accounts boundary.

## Safe readiness diagnostics and authorization

Reuse the existing `AttendancePolicyResult.Status`/explanations and `MonthLockIssue` surfaces.
A future implemented adapter can add bounded machine-readable dependency codes with explicit
`available`, `unavailable`, `conflict` or `not_applicable` semantics. `not_applicable` requires a
verified interpretation, and `available` requires a completed producer check; neither follows
from an unconfigured adapter or an empty collection.

Examples of future codes are `policy_schema_unsupported`, `leave_integration_unavailable`,
`leave_effect_conflict`, `overtime_award_not_included`, and `payroll_calculation_unavailable`.
These are proposed contract vocabulary, not currently emitted API values. Do not add a global
public health endpoint exposing per-employee dependencies. Existing scoped day reads and All-scope
month previews already provide suitable authorization boundaries. Detailed financial explanations
need separate financial permission and DTOs.

Only known missing setup may be converted into a readiness issue. Current MonthLockService
handles the exact 409 `employee_assignment_unavailable` code, retains the employee/date cell and
blocks finalization. Access denial, session revocation, corrupt provenance, unknown failures and
storage failures continue to propagate. Never catch all conflicts and label them missing setup.

## Decisions and implementation checklist

Do not repeat the already confirmed OT formula/eligibility, direct correction/month-lock authority,
no-reopening rule, site-only scheduling or Operational/Red zone decisions. Remaining agreement:

- HR owner supplies the policy/leave contract and implementation location; this proposal does not
  authorize recreating their workflows or integrating another Git branch.
- Management supplies ambiguous normal-attendance/pay rules: exact late/half-day ranges and reset
  periods, deduction basis/divisor, normal-pay breaks, holiday/week-off and partial-day leave
  treatment, KPI/waiting effects, and worked overnight examples. SRS baseline values remain the
  baseline; unresolved section 19 choices are not defaulted.
- If office Attendance is requested, agree its schedule source separately. The currently confirmed
  site resolver returns `site_not_assigned` for staff without a site.
- HR/Accounts confirm preparation, verification and approval responsibilities and the meaning of
  pre-lock calculation readiness versus later payroll processing.

Implementation evidence required before replacing unavailable adapters:

- [ ] Agree typed schemas, producer completeness/authority guarantees and dated applicability.
- [ ] Implement bounded source readers without altering raw punches or historical HR evidence.
- [ ] Validate exact session/leave/OT bindings, conflicts, reversals and protected evidence.
- [ ] Prove missing producers and unsupported rules never yield a calculated/ready result.
- [ ] Exercise sub-minute, overnight, transfer, cancellation, changed-salary, duplicate-award and
      original/current authorization cases using approved synthetic fixtures.
- [ ] Verify pre-lock revalidation, atomic rollback, concurrent writers, durable replay, no financial
      leaks and post-lock source deduplication against approved isolated SQL/private storage.
- [ ] Preserve legacy snapshot/fingerprint readers; version any new protected manifest explicitly.
- [ ] Keep the implementation/verification checklist linked above accurate about real acceptance,
      runtime enablement and remaining dependencies. Build success alone is not this evidence.

This document changes no runtime behavior, schema, database, permissions, provider configuration
or Git integration. Implementation should proceed only for the agreed producer contracts and
confirmed rules, with the HR developer retaining ownership of HR policy/leave/payroll workflows.
