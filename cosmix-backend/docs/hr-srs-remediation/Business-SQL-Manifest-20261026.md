# Remaining HR business SQL tests - 26 October 2026

## Latest status at15:16PKT

The initial preparation-only approval text below is historical. V2 synthetic
history and V2Continue01 subsequently received separate bounded approvals and
were executed. The approved historical vehicle continuation passed74HTTP/63checks;
see [actual results and unchanged limits](Vehicle-V2-Continue01-Execution.md).
The twelve vehicle sources already exist: do not rerun their original generator.
Benefit/recovery fixtures and their execution approval remain separate.

## Approval and status

User approved preparation of an exact business fixture manifest. **Business
fixture execution is not approved by that preparation reply.** The separate
two-account auth-only approval does not authorize these sources, third-actor
login, permission changes or business writes. No business writes were performed.

Mobile/device/real-face testing is NOT a prerequisite for these backend tests.
Tests must use actual service calls, authenticated requests, SQL repositories and
encrypted evidence, not client UI or biometric success substitutes.

## Fresh observed target

- Server: `Software-PC1\LOCALDB#48AA6F9F`; PID 26564.
- Explicit pipe: `np:\\.\pipe\LOCALDB#48AA6F9F\tsql\query`.
- Catalog `Cosmix_Testing`, ONLINE; owner/login `COSMIXRND\Huma.Khan`.
- Family: `D9C34627-18A2-41CF-A804-821E39516303`.
- Files: `C:\Users\Huma.Khan\Cosmix_Testing.mdf` and
  `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`.
- All **26** applied migration IDs match current source, ending in
  `20261022133924_AddComplianceWorkstreams`; none applied by this task.
- Three existing accounts. Only explicitly approved synthetic identities may be
  used. Zero nonterminal OTP deliveries, active custom triggers or external SQL
  dependencies were observed.

The named-instance lookup failed in the tool context; direct connection to the
running SSMS-context pipe succeeded. This supersedes the earlier unavailable
catalog result, not any old test outcome. Do not recreate/attach the database.

| Existing source | Actual rows |
| --- | ---: |
| Leave requests / ledger / recovery commitments | 0 / 0 / 0 |
| Vehicle trips / fuel claims / service records | 0 / 0 / 0 |
| Vehicles / custody | 1 / 1; preserve V1 |
| Outstation deployments | 1; preserve O1 |
| Policies / versions | 1 / 1; O1 allowance, not leave or vehicle policy |
| Employee assignments | 1; O1 HR assignment, 2026-09-01 to 2026-10-21 exclusive |

P1 temporary benefit review, vehicle policy and deployment grants expired
2026-10-21T11:09:40.0589427Z. Fixed HR/Admin leave permissions remain. Existing
HR/Admin/SuperAdmin have All-scoped vehicle trip/fuel/maintenance/custody/manage
permissions. No existing grant was renewed or changed.

## Actor bindings

- H: `hr@example.invalid`, account `8475187f-bf3b-4f9c-a3ed-4bac32253005`,
  employee `35d19922-70a8-4c80-8578-e1029360e9fc`.
- A: `admin@example.invalid`, account `94a83b62-5b56-4450-9175-2a7a9e3261bf`,
  employee `7c505086-87ea-4fd2-980e-823ecccbe36a`.
- S: existing `superadmin@example.invalid`; exact account identity and private
  credential source must be pinned before a three-actor execution packet.

H is the beneficiary/original claimant. A may propose a historical correction;
S independently reviews it. Never allow A to approve A's own proposal or H to
approve a benefit/work claim for H.

## V2: minimal historical vehicle scope

This deliberately removes the extra ordinary-trip vehicle from the old proposal.
It targets one legacy changed-amount reconciliation and one retained maintenance
chain per kind. Fresh private root proposed:
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026V2\`.
No use of old encrypted keys or alteration of V1/O1 records.

### Source rows and dates

Exactly **one new API-created Motorcycle**, registration `TEST-HR-V2-20261026`.
Exactly **12 additional synthetic baseline SQL rows**:

| Table/source | Count | Test-only values |
| --- | ---: | --- |
| Policy definition | 1 | VehicleFuel; `TEST-HR-V2-20261026-POLICY` |
| Policy versions | 3 | V1 [2026-09-01,09-16), V2 [09-16,10-26), V3 [10-26,10-27) |
| Closed custody | 1 | H; 2026-09-14T00:00Z to 09-21T18:00Z; odometer 1000 to 1200 |
| Legacy trip | 1 | SiteToSite; 2026-09-15T18:00Z to 09-16T01:00Z; odometer/map 200 km |
| Original mileage | 1 | 200 approved/outstation km, zero commute/monthly adjustment; retained schema-3 calculation |
| Original fuel claim | 1 | Retained schema-1 calculation: 10 litres at 100 PKR = 1000 PKR |
| Original approval headers | 2 | Trip and fuel; synthetic baseline, not newly executed HTTP approvals |
| Original approval decisions | 2 | Exact source/header/actor/time/private-reference bindings |

Current APIs cannot create these old published policies or legacy cross-day
approvals. These rows require an explicitly approved **synthetic legacy corpus**;
they must never be represented as real historical work or successful legacy API
execution. No new employment, employees, roles, grants or commute rows in V2.
The observed O1 employment spans the historical trip dates and remains unchanged.
V1's later custody stays intact; do not modify it to force ordinary issue APIs
to insert an older closed custody.

Rules: 20 km/litre; price 100 PKR in V1 and 200 PKR in V2/V3; eligibility only H;
Motorcycle; workshop `TEST-HR-V2-WORKSHOP`; tuning/overhaul thresholds 50 km,
EveryThreshold, fixed test reimbursements 50/75 PKR; dated attribution
OncePerTripDay/PerSegmentTwoDecimalsToEven, reviewer codes ADMIN/SUPERADMIN.
No monthly adjustment, incident or changed-threshold configuration. Canonical
rules must be validated by the existing codec, not hand-guessed enum numbers.

**Policy impact requiring explicit approval:** vehicle policy selection is global
by date, not employee-isolated. H-only eligibility is not a database-isolation
guarantee. These date windows, especially current-day V3, can affect other users'
policy lookups. Do not execute while unrelated vehicle business is active.
If this impact is not approved, V2 cannot run on this shared catalog. Do not
remove the current-policy guard or silently use a replacement database.

### Actual business commands and expected deltas

1. H requests and A approves two tuning and two overhaul services before trip
   correction: service dates September 17 and 18, odometer 1200, 200 eligible km.
2. A submits a trip correction; S approves. Two exact 100-km segments split at
   2026-09-15T19:00Z (Karachi midnight), retaining the total 200 km.
3. A submits fuel reconciliation; S approves. Dated fuel = 5 litres x 100 plus
   5 litres x 200 = **1500 PKR**, compared with original 1000, difference **+500**.
   This is an HR comparison only, not salary, settlement or payment.
4. H requests and A approves two further services per kind, September 19 and 20.
   Each kind's retained consumption is 50, 100, 150, 200 km. Replay earlier
   submissions and decisions after successors exist.

API additions: **8 service rows, 10 approval headers, 10 decisions** (eight
services, correction, reconciliation). Correction/reconciliation use existing
generic approval records, not invented new tables. Original trip/mileage/fuel
rows remain immutable. Exactly 21 successful business write commands including
vehicle creation; successful retries reuse receipts. Proposed ceilings: 160 HTTP
requests, three local-MFA sessions, no automatic retry of a partial run.

Required assertions: exact-key replay; changed-payload conflict; stale version;
self-review denial; two simultaneous submissions with distinct keys produce only
one pending correction/reconciliation; concurrent service decisions produce one
decision and one consumption; SQL audit/receipt/evidence consistency; original
source hashes unchanged; logout/revoked-cookie rejection. Rejected requests must
not create extra business rows. Do not tamper with retained protected history to
manufacture a negative test.

### Not yet an executable packet

The 12-row baseline graph is explicit, but the fixture generator must first prove
the exact encrypted evidence/purpose/reference graph using real constructors and
storage; it must enumerate auxiliary audit/outbox/receipt/file maxima and exact
IDs. No generator has been run. Missing original proof is a stop, not permission
to patch validators or flip approval flags. Do not request execution against an
unreviewed or unbounded packet. If the date rolls past 26 October, revise V3 and
obtain approval; never fake the clock.

## Benefit and recovery scope

See [the dedicated fixture manifest](Benefit-Recovery-Fixture-Manifest-20261026.md).
The expired20October proposals cannot be executed as-is. A recovery commitment
alone is not work fulfilment. A fabricated annual KPI score is not valid benefit
history. Do not count either shortcut as closing the remaining business tests.

## Universal execution limits

- No migration, schema change, database repair, cleanup or modification of old
  evidence. No real employee, face, SMS, legal activation or payment operation.
- Only owned HTTPS localhost:7481; no global workers. Preserve snapshots of all
  non-target SQL data and prior evidence before/after the approved batch.
- Verify exact process/owner/family/files/schema and synthetic identities again
  before writes. New PID/pipe or changed schema requires a reviewed binding.
- App/device acceptance is separate. A successful business test proves only the
  executed scenario, not every HR workflow or production readiness.
