# Locked correction carry-forward

Implemented factual request/review/decision and month-posting workflow. This module consumes exact
protected direct-lock manifests and frozen summary/session evidence. A different permission-authorized
HR reviewer can approve a time or manual-duty interpretation. Approval appends one factual ledger
delta to the first available unlocked calendar month after the source month. Source locks and source
attendance never change.

`CarryForwardEvidence` verifies original input, protected proposal, approved decision, prior revision,
virtual session identities and exact factual totals. Its bounded per-operation validation graph prevents
unbounded/cyclic evidence traversal; legacy locks without protected manifests fail closed.
`CarryForwardDutyOverlap` replaces original frozen windows with the latest validated virtual windows
for overlap checks, including corrections on neighboring business dates. `CarryForwardLockReader`
returns every immutable posting to a target month for its final-lock manifest.

Self identity is server-derived. Management reads use existing correction permissions, current web MFA,
and original plus current source/target scopes. Queue cursors are encrypted, actor/filter-bound and
have a finite lifetime; authority is reevaluated for every page. Monthly totals require all bounded
posting scopes to pass before totals or paginated rows are returned.

No new role, face approval, pay rule, target-date WorkSession, open-period record or lock reopening is
invented. Policy/payable/OT integration is explicitly `pending_policy`. The unavailable production
policy adapter means production-ready source locks cannot currently be fabricated by this module.

Feature documentation: `docs/Attendance-Carry-Forward.md`.
Client examples: `docs/attendance-corrections/CarryForward-Client-Examples.md`.
Verification: `docs/attendance-corrections/CarryForward-Verification.md`.
