# Tracking And HR Reports Acceptance Checklist

Not yet executed over HTTP/SQL. Use only the approved, pinned synthetic target;
this checklist does not authorize host startup, account changes or database writes.
Face/model and external OTP delivery acceptance are excluded. Existing local
synthetic OTP delivery may be used only after the bounded auth profile is reviewed.

## Required Actors And Source Evidence

Retain exact synthetic IDs for an employee who transferred from site A to B, an
unrelated employee, a current scoped reviewer, an All-scope reviewer, a report-read
reviewer without export, and an identity/documents reviewer without medical read.
Use normal role management, persisted sessions, real MFA and CSRF; never forge a
successful session or weaken the production authentication handler.

Before each scenario record the exact actor, effective grants, employee/date/source
scope, fixture IDs and expected row IDs. SQL proof must independently verify audit
and raw-punch/receipt deltas. Empty fixtures cannot prove positive scope isolation.
Keep medical names, credentials, report bodies and private evidence references out
of public logs. Inspect only allowlisted synthetic records.

## Scenarios

| Case | Request / condition | Required outcome |
| --- | --- | --- |
| Tracking read | GET `/api/v1/hr/employees/{id}/tracking?From=2026-10-01&To=2026-10-16&PageSize=25` with current scoped web MFA | Protected Items/NextCursor only, current and historical/source scope required, successful read audit |
| Tracking unauthorized | Foreign employee/source, mobile management, missing MFA, revoked role/session | Existing 401/403/404 policy as appropriate; no leaked data or success audit |
| Retired tracking write | Authenticated, CSRF-valid POST `.../tracking/record-checkin`, including repeat and unknown employee | 410 `tracking_capture_retired`; zero punch, business receipt and success-audit changes; middleware security audits remain distinguishable |
| Standard capture alternatives | Follow existing self-capture and correction links | Existing device/face/sequence and review rules remain; no synthetic face success may establish production Check-In acceptance |
| Report preview | GET `/api/v1/hr/reports/{reportType}` for each of 14 types with documented required permissions | Only authorized selected rows; private read evidence and real actor-role audit |
| Missing source authority | Keep hr.reports.read but remove one financial, identity, medical or other required source grant | 403 before report data is queried/disclosed |
| Export separation | Valid preview actor without hr.reports.export | POST export denied; no file, success audit or hidden business change |
| Export allowed | Same source scope plus read/export and CSRF, explicit Reason | CSV and XLSX contain only the permitted filtered page; protected export audit, never a financial posting |
| Medical expiry metadata | Identity/documents/certifications/report permissions but no medical grant, then a foreign-employee medical grant | Both preview/export denied or scope excludes the employee; no medical filename/date leakage |
| Historical transfer filter | All-scope reviewer requests September punches with site A after October transfer to B | Authorized historical site-A rows remain discoverable; current-authority checks still run |
| Future transfer | Today's completed duty at A, transfer to B tomorrow | Tomorrow is not added to today's source interval; actual overnight duties retain correct historical coverage |
| Filtered source | Fuel V1 or zone Z1 filter with unrelated V2/Z2 history in the same receipt window | Unrequested sources neither leak nor incorrectly suppress the requested rows; limits apply to matching source set |
| Grant/role basis | Disjoint role A and role B assignments; only B contributes to the selected employee | Audit references a contributing role, not arbitrary first grant |
| Role/session changes | Revoke authority during generation/private IO and after a prior successful read | No response disclosure from stale authority; later request reevaluates persisted state |
| Audit report | Scoped audit permission versus All-scope audit/read permissions | Current generic metadata variant rejects scoped access; unscoped events additionally require no employee/department/site/branch selector |
| Boundaries | Bad date/page/GUID; >366 inclusive days; candidate/source/row/work bounds | Clear 400/409, never silent scope truncation or unbounded data |
| Storage failure | Controlled failure in private audit storage | No report disclosure or success-audit commit; inspect retained encrypted-orphan handling separately |

## Evidence To Retain

- Current release hashes, source snapshot, exact runtime OpenAPI operation schemas.
- Owned process receipt, target identity/history and synthetic allowlist; no secrets.
- Each request status plus private assertions of exact row membership, including
  positive nonempty scope cases and negative cases.
- Independent SQL audit/receipt/punch/notification deltas and current authorization
  state; actual separate-connection race results, not sequential retries.
- Encrypted storage evidence and restart/replay results; no claim that a synthetic
  in-memory file store verifies encryption or durable SQL transactions.

HTTP/SQL completion must be added to Verification.md with exact executed results.
Source/standalone passes alone leave these rows pending. Report numerical values
and approved payroll settlement/legal rules have their own H1/E/F acceptance.

## 15 October Continuation Acceptance

All rows below remain unexecuted over actual HTTP/SQL. The A02 setup/auth approval
request covers only its bounded one-session authentication smoke test, not these
additional fixture/business writes. Record a bounded synthetic fixture manifest
and explicit target approval before executing them. Do not invent legal clearance
to force a positive financial test through production readiness guards.

| Area | Actual acceptance still required |
| --- | --- |
| Principal-only loan | Reject nonzero extra charge through the registered API; approve a synthetic 10,000 PKR loan as exactly ten 1,000 installments; independently inspect sum/ledger/audit and reject outstanding-balance overpayment |
| Handoff separation | Processing/review alone leaves principal unchanged; exact confirmed-posting action creates one ledger row with retained source, external evidence, actual authorized actor and audit/receipt |
| Duplicate posting | Use separate HTTP connections, same key/same payload, changed payload, new key/same source, same external receipt/different source and concurrent manual repayment; prove one eligible effect and atomic failures in SQL |
| Frozen source balance | Fully manually repaid installment is skipped during preparation; partial repayment conflicts; repayment during evidence IO, after preparation and after lock blocks stale advice; historical frozen lines stay unchanged |
| Posting history | After retry-window expiry/restart, retained audit and ledger support original posting GET; revocation or tampered protected source is denied; a reversal does not permit the original line to be posted/exported again |
| ESS | Real web/mobile self identity, source permissions and revocation; exact fiscal leave minutes/fractions, cancellation/release and historical/future employment; calendar matches authoritative Attendance records without Sunday/shift guesses |
| Recovery replay | Cancel or invalidate the parent after a successful recovery commitment, then retry its original key; reject without new receipts/decisions/ledger effects; no completed work inferred from a promise |
| Report values | Nonempty fixtures for all 14 variants; independently parse CSV and XLSX, compare retained approved OT, signed leave ledger, historical labels, inclusive/exclusive dates, exact frozen money and page totals; inspect source/read/export scopes |
| Vehicles | Historical custody and transfer boundaries, service-date mileage excluding future entries, retained fuel distances/policy, missing map/commute/price, actual reviewer/self-review and future/currency-invalid incidents |
| Reminders | More than 500 records per source, two SQL producers and two dispatchers, failed page commit/storage write, expired claims and restart; one outbox/delivered notification per source/type/expiry/recipient |
| Storage/rollback | Real encrypted objects and correct owner-only paths; controlled failure before commit, restart after commit, exact replay and no partial business/audit/receipt success; retain orphan-file evidence for approved lifecycle handling |

No numeric test-policy value is a company business default. F3 adjustments and
complete G1/G2/G3 missing transactions still require implementation before their
positive acceptance cases can run. The current backend contract notes are in
Confirmed-Loan-Posting-20261015.md, ESS-Consistency-20261015.md and
Reports-Remaining-20261015.md; client screens have not been implemented/tested here.

## 20 October focused continuation

Use the latest continuation ledger rather than interpreting older pending entries
as current implementation gaps. Real acceptance below is still not established
by offline regression or generated OpenAPI checks:

- Actual fixed-role `ADMIN` with the review permission on the same assignment can
  read the leave queue; custom HR cannot. Revocation, scoped permission and
  cross-assignment privilege combination must remain denied.
- Canonical benefit award/correction/waiver evidence is readable alongside retained
  title-case historical evidence without rewriting its bytes or hashes.
- Superseded pending legacy fuel closure retains exact old quantities, source
  records, independent decision and receipt; approved claims and changed payload
  retries are rejected.
- Consumed-maintenance equivalence reconstructs exact original mileage membership
  and per-trip quantities through the consumed date. Offsetting changes, lost
  original proof, competing predecessors and authority/source changes during
  protected IO must roll back; unsupported chains remain unavailable.
- For each vehicle/benefit flow, separately verify authenticated SQL concurrency,
  durable private storage failure/restart, audit/receipt atomicity and client
  permissions using a specifically approved synthetic fixture manifest.

C03 failed before its department mutation. C04 did not execute because the
intended database was not registered on the restarted existing instance. Do not
attach, replace or migrate it as part of UAT. See `HR-Continuation-20261020.md`.

### Later 20 October vehicle continuation

- Exercise a retained approved fuel claim with an approved correction whose dated
  prices change the total. Preview must expose exact original/corrected values
  and policy lines. Positive and negative differences are HR comparisons only.
- Submit with exact fingerprint/predecessor; independently approve, return or
  reject using the persisted rowversion. Verify the tracked approval state,
  audit and receipt in SQL, not just the HTTP response.
- Repeat exact requests, then change payload/version, remove permission and
  remove original protected evidence. No duplicate review or hidden payment is
  permitted. Historical claim amount, mileage, receipt and original decision
  must be unchanged.
- Exercise successive maintenance services after equivalence and after two or
  more original services before correction. Verify every ancestor, chronology,
  original mileage membership and per-trip quantity at each cutoff.
- Missing ancestors/decisions/files, a cycle, a competing approved branch,
  changed consumed quantities or impossible approval chronology must fail
  closed. Preserve the original consumption and all historical records.

The above new flows have standalone regression evidence, not authenticated SQL
acceptance. C04's existing authorization only covers its bounded department
test; it does not authorize vehicle/HR fixture writes. Use an explicitly approved
fixture manifest and verified existing target before executing these UAT writes.
See `Vehicle-Fuel-Reconciliation-20261020.md` and
`Vehicle-Maintenance-Chain-20261020.md`.

### Latest executed acceptance - 20 October 2026

The earlier C04-not-executed note is superseded by
`HR-Business-C04-20261020.md`: 28/28 checks and 35 actual HTTP requests passed.
V1 separately verified vehicle/custody persistence but stopped before logout;
do not call it an overall pass (`HR-Vehicle-V1-20261020.md`).

R1 passed 21/21 checks in 20 actual HTTP requests. Employee-master preview,
employee-only CSV/XLSX, one durable PDF, same-key retry, SQL/encrypted-file
crosschecks and authenticated logout/revocation were exercised. The downloaded
PDF's 14 fields match CSV, with one complete, visually inspected page. Previous
155 SQL snapshots and 48 files were unchanged after host exit.
See `HR-Reports-R1-20261020.md`.

Still run with explicitly approved fixtures before broad HR acceptance:

- Benefit/recovery awards, leave consumption, correction and fulfilment under
  real SQL concurrency; genuine Admin plus explicit delegated benefit permission.
- Vehicle historical maintenance/fuel-reconciliation and configured outstation
  workflows with complete retained source evidence and required grants.
- Other report types, global queue/restart/contention and delivered notifications.
- Actual HR frontend workflows and client permission/error handling.

No migration was applied. Real face, external OTP and legal activation remain
excluded by the user's instruction. R1 proves only its exact captured-job
dispatcher, not the global worker or production deployment.
