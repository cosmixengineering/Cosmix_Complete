# Benefit/recovery fixture manifest - 26 October 2026

## Status

Preparation only; **not an approved executable seed packet**. No fixtures,
permissions, credentials or business records were written. App/device/real-face
availability does not block these backend tests. Missing policy/work/KPI history
does. The [parent manifest](Business-SQL-Manifest-20261026.md) pins the current
existing database and universal limits.

The old20October packet cannot run unchanged: its dates and P1 grants expired.
Do not fake the clock, change the employee joining date, manufacture a completed
KPI year, or call a recovery commitment fulfilled without approved work.

## Fresh observed prerequisites

H/A identities are pinned in the parent. H's DraftRevision is **6**. Existing
assignment `20102001-0006-4000-8000-000000000006` ends2026-10-21 and remains intact.
The four existing O1 test masters can be referenced without mutation:

| Reference | ID |
| --- | --- |
| Department | 20102001-0001-4000-8000-000000000001 |
| Designation | 20102001-0002-4000-8000-000000000002 |
| Employment type | 20102001-0004-4000-8000-000000000004 |
| Branch | 20102001-0005-4000-8000-000000000005 |
| Existing active OFFICE category | 5900e85b-f549-43a1-9484-194636581585 |
| Existing active Casual leave type | 2a807abe-0401-4f24-920a-a5b71329e1d4 |

Casual is kind3, no evidence required, advance approval required, regular balance
reduced. Branch has zero assignments for other employees. Revalidate all master
active states and absence of overlapping roster/boundary before execution.

## R2: real-service recovery, without fabricated history

Preparation date26October; actual work/fulfilment test **27October after18:00
Asia/Karachi**. It cannot truthfully finish on26October using newly published
future-effective policy and real server time. Setup and commitment could run
earlier only after approval. Earliest complete path is deliberately explicit.

Exactly **eight new prerequisite rows**, zero new employees/accounts/masters:

| Source | Count | Proposed values |
| --- | ---: | --- |
| Employment assignment | 1 | H, four test masters above, OFFICE category, [2026-10-27,10-29), no site/manager |
| Holiday calendar | 1 | TEST-R2-20261026-CAL; same support interval, zero holidays |
| Shift and initial version | 2 | TEST-R2-20261026-SHIFT; from10-27,09:00-17:00 Karachi;480 minutes,zero breaks,all weekdays |
| Office roster | 1 | Pinned branch, new shift/calendar, [10-27,10-29) |
| Initial office boundary | 1 | Same branch; from10-27; test latitude24.8607/longitude67.0011,radius100m,accuracy50m,PK-SD |
| Leave policy and version | 2 | TEST-R2-20261026-POLICY; leave-settings-v4; [10-27,10-29) |

The employment seed must use existing domain construction/persistence guards.
It entails **one existing synthetic employee update**, DraftRevision6->7 and SQL
rowversion only. No other field/account/status/joining-date change. This needs
explicit approval; preparation permission does not authorize that update.
Calendar/shift/roster/boundary/policy should be created through their real APIs,
with current timestamps, not fabricated audit or approval provenance.

Policy values are synthetic test inputs, not company defaults: year startsJuly1;
AnnualDays1,SickDays0,CasualDays1; minimumservice0; standardday480; eligible
category only the pinned OFFICE ID; paidCasual, holidaysExcluded; new calendar.
Recovery: Casual/Paid; maximum60minutes per leave/day; one day before,zero after.
Fulfilment: OutsideScheduledDuty,ExactWorkedMinutes,FixedAdmin; no partial,
minimum/maximum60,maximumclaims1,submissionwindow1day; changed parent/source
blocks claims and retains allocations. Benefits/Encashment null.

**Shared impact:** OFFICE policy eligibility is not employee isolation. An
otherwise eligible office employee could select it in the support interval.
This impact, boundary configuration and revised employment must be approved;
do not present a TEST label as isolation or legal certification.

### Business sequence

1. H submits one Casual leave for28October09:00-10:00 Karachi; A approves Paid
   against the exact policy/version. Leave60minutes, no attachment/reliever.
2. **A**, not H, records a60-minute BeforeLeave commitment for27October. Existing
   fixed Admin review authority is required; custom benefit permission is not
   a substitute. Expect committed60,fulfilled0,remaining60.
3. After27October18:00, H submits one normal ManualSession correction for
   16:30-18:00 Karachi with explicitly synthetic duty evidence; A independently
   reviews it. The office correction path must produce the real closed session,
   summary and employment binding. No raw punch, face result or GPS success is
   fabricated. This is a correction-workflow test, not physical attendance proof.
4. H reads real recovery-work-source and submits only17:00-18:00 using returned
   session ID and fingerprint; A independently approves with exact version.
   Expect fulfilled60,remaining0,fulfilled status. Pending claims count zero.

Expected business additions: one leave/day/utilization/commitment/correction/
closed-session/summary/link/work-claim each, and three approval headers plus
three decisions (leave, correction, work claim). Current DTO/persistence types
must freeze the exact physical-table and evidence graph before execution.
Commitment itself does not add another approval.

Target checks: same-key retries; changed payload; stale review; self-review
denial; overlapping interval and over-fulfilment rejection; two separate HTTP
requests for the same commitment/claim cannot allocate twice; actual SQL/audit/
receipt/encrypted evidence match; revoked-session denial. No OT or make-up reward
is created merely to claim cross-module coverage.

Proposed ceilings:160HTTP requests, three synthetic actors/local-MFA sessions,
ownedlocalhost7481; private root
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026R2\`.
The private root must not exist. Freeze auxiliary row/file maxima, IDs, routes,
active permission scopes and fresh packet hash before seeking execution approval.
No generator or execution packet is presently certified by this document.

## B2: benefit correction/resolution

Current database has no complete annual KPI source graph or benefit award.
H joined15January2026. A completed annual period today cannot simply be assigned
to H without eligible historical employment/policy evidence. Do not change that
joining date or insert a bare KPI score/finalization as proof.

The minimal conceptual positive graph needs an eligible historical beneficiary,
a complete annual set of saved/current daily calculations and their actual
employment/roster/calendar/policy sources, approved annual KPI finalization,
an effective benefit policy, an award, consumption, then correction/resolution.
These are not just365 arbitrary rows: source fingerprints/decisions/evidence and
eligibility must agree. Exact row/file counts cannot be frozen from absent
history. The prior365-day proposal is **not an executable fixture manifest**.

Two honest paths remain:

1. An explicitly authorized synthetic historical corpus, separate from real
   employee history, with a reviewed generator, exact counts and protected proof
   graph. Seeded prerequisites must be labelled as such; only later actual
   HTTP business operations are counted as accepted. A new eligible synthetic
   employee would expand the existing two-test-identity scope and needs approval.
2. A retained, genuine eligible test history supplied by the owner, verified
   before use. No live employee data or evidence is inferred from a record name.

Only after the source graph is resolved: A needs a **new** explicitly permitted
temporary benefit-review assignment via SuperAdmin, not silent renewal of P1.
Then execute award120minutes -> consume60 -> correction removing unused60 and
retaining used60 for Admin review -> policy-authorized waiver/resolution. All
values are synthetic; no automatic salary deduction or future leave cut.
Do not award/consume the same minutes twice or allow the beneficiary to approve.

**This packet does not close benefit/recovery live acceptance.** It identifies
the feasible dated recovery path and the exact remaining historical-corpus
decision, without replacing them with auth-only or offline passes.
