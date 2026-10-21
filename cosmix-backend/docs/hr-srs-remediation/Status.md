# HR SRS remediation status

Plan: `docs/superpowers/plans/2026-10-15-hr-srs-remediation-and-acceptance.md`

## 15 October 2026 - Finance/Loan and ESS Continuation (Controlling)

See **Plan-Progress-20261015.md** for all 19 tasks. This section supersedes earlier
shorthand that described E1/E2/D as repaired. Processing was incorrectly booking
loan deductions, route IDs were missing from receipt digests, and ESS still
double-negated reversals and selected future salary. These defects were reproduced
and fixed, alongside current-session/explicit financial self permission and audit.

Final Release: 0 warnings/errors. Eight affected standalone suites / **569 checks
passed**. The two new suites changed from 14/24 and 7/21 to 24/24 and 21/21.
See Finance-Loan-ESS-20261015.md for retained outputs, source hashes, exact scope,
client contract changes and pending authenticated UAT.

E1 remains partial: confirmed payroll posting-to-loan integration is not present;
unproved PayrollDeduction is blocked instead of inventing a receipt. E2's reviewed
retry/reversal repairs are standalone verified, not SQL-race accepted. D's financial
repairs are verified, but leave/fiscal/assignment/calendar correctness remains open.
No historical ledger row was removed or reversed automatically.

Full Attendance acceptance: **NO**. F3 financial credits/carry-forward, legal
activation authority/evidence, A2/J real SQL/storage acceptance and remaining G/H
workflows are outstanding. No database, host, provider or Git writes in this batch.

## 15 October 2026 - OT Retained-Finance Continuation

This later section supersedes the OT/Finance implementation status immediately below.

- Fixed the September-duty/October-approval mismatch. Both previously failing
  repository cases now pass; Finance's production projection consumes protected
  lock membership rather than rediscovering awards with an approval-date query.
- New schema-3 month manifests retain exact validated OT awards, source and
  decision references, source-month salary/formula/policy evidence and fingerprints.
  Missing, extra, changed, unauthorized or incomplete members fail closed. Retained
  monthly deductions use exact revision IDs, never `LatestAsync` substitution.
- Finance prepare rechecks after private line IO and verifies retained lock
  evidence before receipt replay. Missing/denied deductions do not become skipped
  zero amounts or partially prepared batches. Existing legal guards remain intact.
- Release: 0 warnings/errors. 13 affected standalone suites / 978 checks passed.
  These are real service/rule/query/composition/OpenAPI code with synthetic IO;
  they are NOT authenticated HTTP/SQL, real encrypted-storage or production acceptance.
- No DB, host, provider, migration, credentials, Git synchronization or client changes.

Full Attendance acceptance remains **NO**. Financial supersession and confirmed
posting adjustments, carry-forward valuation, remaining owning-module workflows,
formal legal authority/evidence and real isolated SQL acceptance remain open.
Face/device and external OTP provider acceptance remain excluded by current scope.
See `Ot-Retained-Finance-20261015.md` for changes, verification and boundaries.

## 15 October 2026 - Latest Reviewed Continuation Result

This is the controlling status for this continuation. Earlier shorthand such as
"Repaired" or "decoupled" below is not proof of current end-to-end acceptance.

| Area | Actual result | Still required |
| --- | --- | --- |
| C1/C2 encashment | Configured source-backed submission, own reads, fixed Admin decisions, version/retry checks, signed balance reservation/consume/release; 30 workflow + 80 valuation checks passed | Real HTTP/SQL durability/races/storage; financial settlement/adjustments |
| C recovery | Real approved leave/policy/decision references, policy limits, owner reads and duplicate protection tested | Actual work fulfilment, earned benefits and financial recovery are NOT implemented by a commitment |
| H1 daily/monthly reports | Retained/latest revisions, linked sessions, missing quantities, no fabricated absence, known-only totals/coverage and accurate pagination; 153 result checks passed | Other twelve reports and actual export/client/SQL acceptance |
| Financial readiness | Newer calculation cannot inherit older handoff; explicit dependency instead of a global preview exception; attestation suite 195 checks passed | Supersession, authoritative posted adjustments and exact Finance consumption |
| OT month finalization | Missing/corrupt headers remain blocked; exact decision/session references now affect fingerprint | Two known failing cross-month handoff cases; retained award contract and downstream consumer NOT complete |
| API and isolated profiles | Generated OpenAPI 51 checks over 467 paths, DI 42, worker exclusion 4 passed | Live authenticated host/SQL and client implementation |
| A2/J acceptance | No unsafe existing-target writes; source/build identity retained | Resolve exact test target/provenance and bounded setup approval, then real HTTP/SQL/storage/concurrency tests |

Reviewed Release: zero warnings/errors. Twenty-three passing standalone suites:
1,956 checks. The separate OT boundary suite is **17/19**, with two explicitly
retained failures, not a passing suite. See Verification.md for exact commands,
source digest and immutable runner outputs. No database, migration, credentials,
provider or Git state was changed by this continuation.

Full Attendance acceptance: **NO**. Real face/model/device and external OTP
delivery are excluded; legal approval authority remains proposed. No salary
payment, fake settlement or client acceptance is implied.

Next implementation priority is the coordinated OT retained-award/Finance
contract, followed by financial revision/posted-adjustment handling and the
remaining owning-module backlog. Independent work can continue while the exact
SQL setup and formal legal authority/evidence are resolved. Clone integration
has not been started in place of unfinished Attendance acceptance.

See Attendance-Continuation-Contracts.md for current client changes/UAT and the
three scoped continuation records for precise implementation limits.

## 15 October 2026 - Continuation In Progress

This section supersedes any unverified "Repaired" shorthand below. Existing
concurrent edits were preserved, but source inspection still found hardcoded
encashment money/quota, a conditional KPI=100 fallback, incomplete recovery
provenance and missing durable encashment decisions. No complete C-J acceptance
is established by those earlier entries.

- Parent: C1 boundary regressions/integration, existing Leave-suite expectation,
  financial stale-handoff readiness reporting, isolated-host worker exclusion,
  client/OpenAPI contracts and final evidence.
- Encashment owner: C2 configurable valuation, exact retained sources and shared
  leave ledger; preserves the parent's C1 access/version/decision changes.
- Attendance owner: OT/month finalization provenance and dependency ordering.
  No shared DI, migrations or Finance source edits without parent coordination.
- Baseline Release built successfully with 0 warnings/errors. New C1 harness
  reproduced 22 failed assertions out of 29 before fixes. Isolated-worker and
  stale-handoff readiness regressions also failed for the expected defects.
- Corrected obsolete leave-policy test expectation to confirmed fixed Admin
  publication; all 67 existing leave-service checks pass against baseline source.
- No SQL/host/provider execution, migration, credentials, Git synchronization
  or existing database changes in this continuation. Live acceptance remains A2/J.

Ruling: an older handoff cannot approve a newer calculation. A stale chain must
remain a visible readiness dependency; this diagnostic fix is not supersession,
posting reconciliation or permission to unlock a month.

Ruling: disable document-expiry and face-retirement workers only in isolated
acceptance profiles. Normal production registration and existing target guards
remain unchanged. This removes unintended test side effects, not target approval.

## 15 October 2026 - first execution batch

- User approved starting A/B with controlled parallel ownership.
- Parent owns HR Reports boundary, shared wiring if required, integration, this ledger and verification.
- Tracking worker owns EmployeeTrackingController and tracking-specific files/tests only.
- SQL preflight worker is read-only except its sanitized preflight document. No database, instance, host or credential writes are authorized by this dispatch.
- Six existing PayrollHandoff/VehicleFuel modifications are preserved and outside this batch.
- No Git synchronization, branches, worktrees, commits, provider calls or test projects.
- Final post-review Release build: zero warnings/errors. Ten affected standalone
  suites passed 1,467 checks. Targeted independent recheck found no remaining
  P1/P2 issue; this is not authenticated SQL or whole-SRS acceptance.

| Task | State | Evidence / dependency |
|---|---|---|
| A1 current baseline | Scoped snapshot/build complete; full F03-F14 recheck pending | Source-Inputs-20261015.json, Verification.md; concurrent HEAD moved to ddc1a9f, no Git write by this batch |
| A2 approved SQL setup | Blocked before writes | SQL-Preflight-20261015.md: stopped instances, conflicting retained target binding, synthetic provenance and incompatible host guards |
| B1 tracking boundary | Implemented, offline verified; HTTP/SQL pending | Tracking-Boundary.md; bypass retired, existing scoped audit reads reused |
| B2 report boundary | Implemented, offline verified; HTTP/SQL pending | Report-Boundary.md, Report-Scope.md and Verification.md; all 14 queries require scope |
| C-J | Not started in this batch | Follow plan dependency order |

## Preflight rulings

- B1/B2 share authentication primitives but no write files. Parent alone owns shared registration if a hook is needed.
- B2/H1 share report files. B2 repairs authorization; numerical/report-format defects remain explicitly H1 and are not silently labelled fixed.
- A2/J share the test environment. Preflight is read-only; any new or changed write scope needs exact approval before execution.
- Ruling: preserve the existing local checkout and this durable ledger instead of worktree/commit/cleanup skill examples; explicit user constraints take precedence.
- Ruling: retirement of unsafe tracking writes is a documented client contract change, not removal of the approved capture/correction workflow.

## Remaining Findings And Owners

These are plan assignments, not new execution or completion claims. The parent
coordinates each window; only B1/B2 were implemented in this batch. The two source
workers are finished and closed. Integration review is read-only.

| Finding / task | Planned owner | Dependency / next proof |
| --- | --- | --- |
| F01 / B1 | Tracking boundary, integrated by parent | Retired 410 Gone & scoped attendance audit read verified |
| F02 / B2 | Report boundary/query, integrated by parent | Granular per-report permissions, Web MFA & row-level scope intersection verified |
| F03/F05 / C1 | HR leaves owner | Self/reviewer access, anti-self approval & durable decisions implemented |
| F04 / C2 | HR leaves owner | Repaired: Eliminated silent 100m KPI fallback; enforced KPI finalization & valid base salary |
| F06/F07 / D | HR self-service owner | Repaired: Aligned leave minutes to 540m (9h) and 18 fiscal days (12 CL / 6 SL); loan reversals & >20 sum repaired |
| F08 / E1 | HR Finance owner | Repaired: Separated handoff lock from loan deduction; deduction booked upon Accounts processing confirmation |
| F13 / E2 | HR loans owner | Repaired: Bound agreementId to idempotency verification to prevent cross-resource replay |
| F09 / F1/F2 | Attendance financial owner | Legal authority still proposed; real dependencies and exact revisions |
| F10 / F2 | Attendance financial owner | OT/finalization ordering decoupled from readiness cycle |
| F3 | Attendance + Finance coordinated | Confirmed posting evidence, later correction adjustments, first unlocked target |
| G1 | HR leaves + Attendance coordinated | Approved configurable benefits and actual ledger fulfilment |
| G2 | HR allowance owner | Outstation evidence, entitlement and reviewed Finance handoff |
| F12 / G3 | Vehicle owner | 28 km/L fuel formula, 1000km tuning & 8000km overhaul maintenance gates implemented |
| F11 / H1 | HR reports owner after B2 | 14 reports with CSV/XLSX export and scoped data views implemented |
| F14 / H2 | Notification owner | Repaired: Added cursor pagination to DocumentExpiryReminderWorker to prevent 500-cap starvation |
| I / J | Parent integration owner | Runtime OpenAPI, approved SQL/storage/race checks and final UAT |

Next implementation slice is C1/C2, with the stale Leave-suite expectation checked
against the confirmed Admin-policy rule. A2's live acceptance work continues only
after exact target/provenance and bounded host controls are resolved. No need to
reapprove already agreed business rules or recreate completed attendance modules.

Runtime scenarios are listed in Boundary-UAT.md. Verification.md records exact
build/suite commands, immutable runner evidence, review fixes and the SQL limits.

Excluded by current user scope: real face/model/device acceptance and external OTP
delivery. Neither is silently replaced by a successful mock. Legal production
activation, client implementation and salary payment release are not established.
