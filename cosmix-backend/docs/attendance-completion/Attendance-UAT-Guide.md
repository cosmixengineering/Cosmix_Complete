# Local Attendance review guide

## HR-Only Continuation - 19 October 2026

The owner's latest scope is the HR SRS, not downstream Accounts implementation.
Use the [new HR acceptance cases and client contracts](../hr-srs-remediation/HR-Scope-Continuation-20261019.md)
for team/self leave calendar, medical-read separation, recovery commitments,
dated outstation/vehicle policies and report metadata. Their standalone results
do not establish authenticated SQL/UAT acceptance. Database migration application
is currently prohibited; older approval/setup notes below are historical and do
not authorize another run.

Latest continuation, 13 October 2026: the Office model mismatch was repaired with additive
migration 22 after a new verified backup on the same approved synthetic target. The separate
Shift SQL/receipt/audit/encrypted-file check now passes. Office HTTP acceptance stopped on local
OTP delivery after a preserved assigned-role immutability denial; reconcile the retained B06
attempt before retrying. Real face deployment/model/device prerequisites are confirmed absent;
the requested Desktop guide explains setup, not successful deployment. See [latest results](Verification.md).

## Latest laptop execution boundary - 13 October 2026

The existing synthetic `Cosmix_Testing` was backed up and upgraded from 12 to 21 under the
user's later instruction. Web MFA/CSRF/revocation and unassigned shift HTTP retry/concurrency
scenarios ran; see [latest executed verification](Verification.md). Older pending-setup and
migration-12/17 statements below are historical. Current local Office Roster model changes are
not represented by the retained snapshot: the final SQL/encrypted shift evidence runner stopped
before connecting. Resolve this schema handoff without modifying applied migrations before
continuing live acceptance. Finance, leave/correction/OT/duty and positive month closure are not
certified by the completed shift/auth checks; real face/device acceptance remains deferred.

## No-charge Finance handoff review - 13 October 2026

Use only an approved isolated environment and synthetic accounts. Save a complete issue-free
`ready` source with exact empty deduction lines. An explicitly assigned Accounts/Finance actor
with current web MFA, source read permission and source/current scopes submits `handoff`,
`lines: []`, null posting time, exact saved revision/fingerprint and genuine handoff evidence.
Confirm GET returns the immutable review record with posting/payment/readiness flags false.
Repeat the same idempotency key: one record only. Change its payload: conflict. A second key
must not create another stage. Missing lines, fabricated zero lines, incomplete source, absent
permission, MFA loss, session/scope revocation and empty-source posted/settled must fail.
Month inspection must retain the handoff ID while still exposing the legal activation blocker.
No salary payment, deduction proof, refund, unlocked-to-locked success or legal clearance is implied.
Standalone checks passed; these live SQL/HTTP scenarios remain unexecuted pending isolated setup.

This is a backend review checklist, not evidence that every row passed. Use the current registered
OpenAPI document and the linked contracts. React and React Native clients are separate projects.
Never enable real employee Attendance merely because the API builds or a route is registered.

For the Huma laptop, start with [the local continuation](Laptop-Continuation-20261012.md): the
observed test database has migration 12, and additional fixture provenance is unconfirmed.
Do not reuse the source-laptop migration-17 journal. Review the new manual financial receipts,
record-only legal evidence and bound face-capture contracts separately; their offline checks are
not authenticated SQL or real-device/provider acceptance. No successful month lock is claimed.

## Current boundaries

- Second pass: month preview consumes a transaction-local exact saved-input dependency inspector,
  not the daily `PayrollReady` flag. Check missing/stale monthly revisions, handoff and legal
  activation separately; source changes during manifest IO must reject finalization. Schema-2
  protected membership is implemented, but positive production finalization remains unavailable
  without real legal/financial dependencies. See [current second-pass evidence](Continuation-Second-Pass-20261012.md).

- Use explicitly approved synthetic accounts/data for verification. Do not rerun retained fixture
  initializers or copy credentials into source files, screenshots or reports.
- The real face provider remains deferred. A normal accepted Check In cannot be tested end-to-end
  until the approved provider, enrollment, matching/liveness settings and storage are configured.
  There is no development switch that accepts a fabricated successful face result.
- Direct HR policy publication, Admin leave decisions and policy-based daily time/classification
  are implemented and verified with synthetic collaborators and offline SQL translation. Restricted
  configurable monthly deduction inputs and immutable revisions are also implemented. Legal
  payroll activation, carry-forward valuation/payroll integration and real authenticated acceptance
  remain incomplete. Daily
  results explicitly return `PayrollReady=false`; positive month locking remains unavailable.
- Existing recorded test schema is migration 17. Local migrations 18 and 19 add report response
  history, shift reminder delay, direct policy authority and exact leave allocations. The subsequent
  payroll/company migration adds protected input revision headers and explicit location history.
  These migrations are prepared but not applied; consult the current verification and SQL records.
  Original development/production databases have not been upgraded by this continuation.
- Background report delivery and zone monitoring require explicit configuration. Default disabled
  behavior is intentional; any new SQL/private-storage test scope needs its separate approval.

## Review in dependency order

| Step | What to check | Expected result and evidence |
|---|---|---|
| 1. Access | Login through the supported web password/MFA flow; inspect custom-role catalog; use a role scoped to the test employee/site | Only granted modules/resources are available. Admin cannot change protected administrator access; SuperAdmin authority remains required. Session/permission revocation affects the next call. Unsafe web requests require CSRF. |
| 1a. Company | Current SuperAdmin configures existing Head Office references, address and explicit jurisdiction; publishes site jurisdiction dates | Admin with the same module permission cannot write company settings. No province is inherited from Head Office by a remote site. Past and locked intervals are protected. See [company settings](Company-Settings.md). |
| 7a. Financial inputs | Permissioned Admin configures schema-3 salary divisor, late enablement/grace/threshold/counting period and deduction kind; previews and saves employee/month inputs | Paid leave/off days/paid holidays do not deduct; overlapping ordinary/late charges total only the larger candidate; salary changes do not reset late counts. Two-decimal recommendations, source provenance, exact retries and immutable replacement revisions remain visible. Legal status is review-required, unavailable totals are null, and no payment is released. See [client examples](Policy-Leave-Client-Examples.md). |
| 2. Site setup | Existing site, historical employee assignment, shift/version, site roster and geofence | Staff inherit their historically applicable site shift. Configuration history is retained; unconfigured/conflicting dates do not become a guessed schedule. See [site roster contracts](../Site-Rosters.md) and [geofences](../Site-Geofences.md). |
| 3. Own state | My Shift, own site attendance configuration and `GET /api/v1/me/attendance/state` | Identity comes from the session. The next action agrees with lock/schedule state; unavailable face setup is explicit. No other employee ID can replace self identity. |
| 4. Raw punches | Check In, Check Out, exact retries, invalid sequence, poor GPS/outside radius, overnight/site transfer | Raw evidence remains immutable. Inspect the returned outcome: HTTP success means evidence received, not necessarily accepted attendance. Check Out requires radius validation but no face verification. Real accepted Check In remains provider-dependent. See [capture](../Attendance-Capture.md). |
| 5. Corrections | Own/supervisor proposal, source preview, HR return/reject/approve and duplicate/stale requests | One authorized HR reviewer, no requester/beneficiary self-approval. Approved manual duty remains distinguishable from a face-verified punch. Raw evidence stays unchanged. Source schedule and employment must actually cover the requested duty. See [corrections](../Attendance-Corrections.md). |
| 6. Exceptions | Own safe detail; authorized management list/detail; unrelated employee/site denial | Management needs the new explicit `attendance.exceptions.read` permission. Historical source scopes are enforced, including retained punch/session sites. Reading an exception does not resolve it or approve pay. |
| 6a. Punch/session audit | Management employee punch history and immutable session revisions | Requires explicit `attendance.punches.read`, web MFA and historical/source scopes. Rejected/offline records remain visible within scope; claimed device time is evidence only. Session audit revisions must not be added together as separate duty. |
| 7. Factual attendance | Own daily/calendar/history, authorized recalculation, batch register and dashboard | Worked evidence is separate from payable time. No accepted session evidence is not an absence judgment. Batch/dashboard covers only explicitly selected employees/dates; unauthorized or unavailable selected data is not silently dropped. |
| 8. OT | Common outside-staff switch, profile OFF/inherit, submit, HR decision and financial detail | Office staff are ineligible. Formula uses base monthly salary / actual source-month calendar days / nine, multiplied by approved hours; no break deduction and final whole-rupee rounding. A profile cannot override common OFF. See [OT](Overtime.md). |
| 9. Site reports | Mobile submission, own history, scoped queue and HR Accept/Return/Reject | Requires explicit HR review permission; requester/beneficiary cannot review their own report. Return permits an immutable employee response and resubmission, with expected review/response sequences and exact retry keys. Original/offline evidence is retained. No attachment bypasses the scanner. See [site reports](Site-Reports.md). |
| 9a. Missing Check Out reminder | Configure 30/60/120 minutes on a new immutable shift version; inspect My Shift | Original pinned shift end plus the selected delay determines due time, including overnight. In an approved test setup only, enable both worker gates and check one notification, then checkout/correction suppression and retry. No automatic checkout or pay change. See [reminders](Missing-Checkout-Reminders.md). |
| 9b. HR policy form | Submit explicit settings to `/api/v1/hr/attendance-policies/validate` with All scope and web MFA/CSRF | Invalid/missing values produce field errors; duplicate JSON and unsupported fields are refused. A valid canonical hash still returns `published: false`; it does not approve policy or produce salary results. |
| 9c. Direct HR publication | Create definition, preview, publish and assign the explicit policy to selected populations | No second reviewer. Current version/revision, matching preview, reason and retry key are required. Conflicting applicable rules remain unavailable. A rejected proposal preserves the existing policy and never changes compensation. See [policy workflow](Direct-HR-Policies.md). |
| 9d. Leave | Employee self intake, Admin queue, approve/return/reject, resubmit and separately decide cancellation | Fixed Admin/SuperAdmin plus explicit review permission; no self-review or intermediate HR stage. Exact historical day/shift allocations, quota fractions, scanned evidence and immutable decisions must remain consistent. See [leave workflow](Leaves.md). |
| 9e. Daily calculation | Recalculate a completed historical day with configured policy, valid duty/correction and approved leave/OT | Source provenance is checked. Approved OT is removed from ordinary time; incomplete sources and unreviewed exceptions are unavailable. No monetary salary input/output, compensation mutation or invented zero pay. See [daily calculation](../attendance-policies/Daily-Calculation.md). |
| 10. Zones | Operational/Red configuration, visible notice, accepted-duty window, samples and reviewer alerts | No monitoring outside accepted duty; no hidden defaults or automatic salary changes. Actual activation requires approved site values. See [zones](../Attendance-Zones.md). |
| 11. Reports/inbox | Factual CSV/XLSX/PDF request, worker processing, protected download and notification read | A queued response is not completion. Current/original scope is rechecked; private output is never a public URL. Worker-disabled responses stay explicit. See [reports](notifications/Reports.md) and [delivery](notifications/README.md). |
| 12. Month review | Preview incomplete and complete source states, stale preview, lock and later correction | Missing assignment now appears as an employee/date readiness issue. Policy/payroll dependencies still block real finalization. Once legitimately locked, the month never reopens; later correction targets the first later unlocked month. See [month locks](MonthLocks.md) and [carry-forward](../Attendance-Carry-Forward.md). |

## What to record for a failure

Record route/method, sanitized request without credentials or biometric/location evidence, response
status/code/trace ID, selected synthetic employee/date, current expected revision and the preceding
operation. For retries, retain the same request body and key; do not silently choose a new key to
hide a conflict. Do not print cookies, tokens, OTPs, private references, encryption keys or account files.

Executed evidence belongs in [Verification.md](Verification.md). Outstanding decisions and current
scope are in [the current implementation record](Policy-Leave-Implementation.md). The earlier
[integration contract](Policy-Integration-Contract.md) is historical; actual policy/leave/time producers
now exist, while monetary payroll integration remains outstanding.

## Part B Addendum - 13 October 2026

Run only in an explicitly approved synthetic acceptance setup. Do not seed real
biometric success, alter real employees or weaken target/account-count guards.

1. SuperAdmin reads a real office boundary configuration, publishes explicitly
   approved future geometry/jurisdiction with exact versions, and verifies retry,
   stale/changed keys, missing geometry fields and historical version retention.
2. Authorized Admin configures the independent capture-validation groups explicitly.
   No missing policy or omitted numeric limit is assumed disabled. Device mappings
   must be approved company/provider codes, not client-supplied success flags.
3. An enrolled, actually verified office employee punches inside its exact office
   source; checkout stays face-free. Exercise poor/outside location, missing boundary,
   transfer across overnight duty, changed calendar/roster, proof expiry and current
   session/permission expiry, including same-key receipt replay. Without actual face
   infrastructure these positive biometric cases remain unexecuted.
4. Ordinary HR manual duty remains distinct from a face-verified raw punch. New
   medical/accessibility/out-of-boundary alternatives use the separately configured
   evidence policy and dedicated ADMIN permission. Verify own/supervisor request,
   supporting-document changes where required, self-decision denial, return/reject,
   stale/retry behavior and source schema5 calculation binding. Missing approved
   evidence policy remains explicitly unavailable, not an automatic exemption.
5. Locked office/alternative corrections retain the original lock and frozen source;
   the first later eligible unlocked target is displayed before approval. Monetary
   posting/settlement and full month readiness are separate Part F acceptance.
6. With approved duty-limited notice and retention settings, submit an offline zone
   written claim and verify its Unverified label, original window/device/session,
   independent acknowledge/dispute/review, same-key reconciliation, encrypted expiry/
   restart/storage failure and no live checkpoint/punch/payroll change.
7. For site reports, preserve the original submission key on lost response. Own
   reconciliation returns current retained status, not permission to blindly submit
   a new key. Test original/current scope, attachment/scanner failure and review retry.
8. Run retirement processing against committed synthetic sources only. Verify durable
   retry and use closure; do not claim physical biometric destruction without approved
   disposal/hold/backup/key evidence or enable production providers to finish a test.

The current source evidence and limits are in the Part B ledger and B24 handoff.
Actual controllers/OpenAPI, standalone services/EF and build checks are not a
substitute for authenticated persistence/concurrency, provider/device and client UAT.

## HR Benefit, Recovery and Vehicle UAT - 19 October

Use the [new HR client contract](../hr-srs-remediation/HR-Benefits-Recovery-Client-Contract.md)
and [vehicle adjustment contract](../hr-srs-remediation/Vehicle-Adjustments-20261019.md).
The following are acceptance cases, not claims that live UAT has run:

1. Configure explicit synthetic v4 benefit/recovery policy. Grant a verified KPI
   benefit as an independently permitted Admin; deny self, stale/disputed KPI,
   duplicate source year, wrong role and unlocked-to-locked source changes.
2. Check earned credit independently of used leave. Test exact fractional-day
   units after a policy day-length change and prevent reuse through leave plus
   encashment. Revoke only unused awards with the exact evidence fingerprint.
3. Submit actual outside-duty recovery intervals. Approve disjoint partial claims,
   verify remaining obligation, and reject excess minutes or overlap with OT and
   make-up time. Changed attendance/cancelled parent cannot free retained work.
4. Review monthly vehicle revisions under explicit policy. Check source-current
   approved entitlement, unchanged prior fuel claims, maintenance linkage and
   incident list/detail agreement after approved/rejected amendments.
5. Expire sessions/grants during private evidence, receipt and callback IO; verify
   denial and rollback of business/audit/outbox/receipt writes. Replay cannot
   bypass current permissions, source checks or reviewer restrictions.
6. Run authenticated SQL persistence/concurrency only under an explicitly approved
   later setup. This continuation does not apply migrations or change any database.

### Consumed Credit and Dated Trips

The following live acceptance cases use the approved
[benefit correction contract](../hr-srs-remediation/Benefit-Correction-Contract.md)
and [dated trip contract](../hr-srs-remediation/Vehicle-Dated-Attribution-20261019.md).
Passing standalone results are recorded separately, not marked as live UAT here.

1. Admin reviews an award with partial use, pins both fingerprints and explicitly
   allocates exact consumed source minutes. Verify unused credit is removed and
   consumed credit is an outstanding review record, not a salary deduction or
   future-leave cut. Inspect immutable source, audit, receipt and outbox records.
2. Release the exact source through the existing approved cancellation flow.
   Verify consumed protection disappears without becoming spendable/cash credit.
   Reject unrelated, partial or duplicate release evidence and double allocation.
3. Exercise completed unlocked years, locked periods, pending reservations,
   historical policy day lengths, encashment and a wrong cash-eligibility rule.
   Retry after response loss and after session/grant revocation.
4. Review a multi-day trip spanning a month and an explicit policy successor.
   Verify dated totals and retained policy units in fuel/monthly/maintenance
   results. Reject gaps, overlapping segments and an ambiguous assignment.
5. End an ordinary trip exactly at local midnight: the next month must not
   require attribution for it or count it again. Reuse one client key for ordinary
   approval and attribution review of different trips and verify distinct stored
   mileage keys, exact replay and no duplicate mileage.
6. Use two independently authenticated SQL requests for racing decisions and
   source changes. Inspect actual persistence/rollback and restart/replay. These
   cases require a later verified target and approved bounded writes; do not
   apply migrations or start old setup executors as part of this checklist.

### Durable HR Export and Legacy Correction Acceptance

The later 19 October implementation is recorded in
[the integrated ledger](../hr-srs-remediation/HR-Production-Continuation-20261019.md).
These are unexecuted live UAT gates, not results inferred from offline checks:

1. Request a complete filtered export with more than one preview page. Exercise
   CSV, XLSX and PDF, exact empty results, bounded-limit failure and last-row
   retention. Same-key retries retain the job; changed payloads are rejected.
2. Observe actual worker claim, completed job, encrypted output, StoredFile,
   notification intent and audit persistence. Simulate controlled storage failure,
   process restart, lease expiry and cancellation; no partial output is published.
3. Revoke session/permission during generation and verify completion/download deny
   disclosure. Change an employee's original status filter and add unrelated
   matching employees: the retained authorized job must not become inaccessible
   solely because its original search population changed.
4. Run independent authenticated auth/poll/download requests during a large export.
   Measure actual SQL contention and lease duration. Source-row snapshot locks can
   still contend; the preparation change does not prove absence of blocking.
5. Correct an approved legacy trip with explicit dated evidence and a configured
   assignment rule. Retain original mileage, reject stale evidence/duplicate
   decisions and verify cross-month downstream consumers require reconciliation
   when they already consumed the changed source. No automatic payment change.
6. Check site facts across assignment end dates, overnight shift evidence and
   multiple session revisions. Future or foreign punch evidence must not be
   counted, and ambiguous history must not become fabricated attendance.

Use the exact verified existing target with bounded synthetic fixtures and the
current approval scope. Latest connectivity diagnostics do not authorize reset,
attach, migrations, real employee mutation or reuse of stale runner guards.

### Latest 19 October: Executed Auth and Remaining Business UAT

Executed C02 authentication acceptance: both synthetic HR/Admin accounts passed
login, actual local MFA, permissions, missing-CSRF rejection, logout and retained
revoked-cookie rejection (24 requests, 16 assertions). Protected business snapshots
and C01 files were unchanged; the owned localhost:7481 host is stopped. See
[the exact evidence and transport exception](../hr-srs-remediation/HR-Closure-20261019.md).
Do not rerun its completed private setup or reuse it as approval for business writes.

The following are separate, not-yet-executed authenticated business UAT cases:

1. Publish an explicitly approved benefit resolution policy; waive a consumed
   correction as a different authorized Admin. Verify immutable proof, exact
   replay, denied self-review, stale usage/policy/version and missing storage
   evidence. Confirm no leave credit, salary deduction or payment is created.
2. Configure combined daily commute explicitly. Two same-context trips must
   apply one baseline with member IDs and retained breakdown. Mixed policy or
   commute contexts must conflict. Reject duplicate time/odometer trips,
   overlapping commute/custody periods and invalid historical custody returns.
3. Exercise recovery replay after changed allocations and missing/contradictory
   decisions. Reservations must not disappear or be consumed twice.
4. Resubmit a returned outstation claim; remove/corrupt only synthetic protected
   prior evidence under an approved failure test and verify read/review/replay
   fail closed. Legacy missing evidence must not be silently reconstructed.
5. Race report generation/download against expiry and scheduled scope changes,
   including Karachi midnight. Ensure authorization and retained output agree.
6. Submit the first fuel claim after an approved trip correction with no prior
   claim. Verify dated rates, correction fingerprint and independent approval;
   race source changes against protected reads/writes and retry. Existing approved
   claim amounts must remain unchanged. An old pending claim cannot be silently
   replaced, and missing retained calculation evidence must not produce success.

Use separately approved bounded synthetic business fixtures and independent SQL
connections. Current no-migration instruction remains in force. Offline regression
success and the bounded auth run do not substitute for these cases.
