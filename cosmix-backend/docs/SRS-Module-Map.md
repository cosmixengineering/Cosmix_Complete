# SRS module map

## Latest HR/mobile pilot check - 20 October 2026

The subsequent `hr-pilot-build09` Release passed with zero warnings/errors.
Historical service receipt replay after a valid successor was repaired;
vehicle/fuel/benefit/recovery/KPI suites passed **1,749/1,749** and separate
capture/enrollment protection suites passed **232/232**. These are standalone
synthetic checks, not additional business SQL acceptance.

The user now requests real-face pilot testing. Existing local face configuration
is enabled. Fresh strict model/codec/private-configuration readiness and
adapter-protected CompreFace health checks passed; this is not proof of
real-person/device acceptance. External OTP is still unavailable for a new employee and legal
activation remains excluded. See [current pilot report and precise remaining
gates](hr-srs-remediation/HR-Mobile-Pilot-Readiness-20261020.md).
Do not describe the whole HR backend as production-ready or only awaiting SMS.

## Latest bounded outstation verification - 20 October 2026

O1Continue01 exercised one synthetic deployment and one policy-valued claim
with independent Admin approvals, concurrent same-key requests, duplicate and
stale-version rejection, immutable response replay, MFA, CSRF and logout:
38 named checks passed across 45 HTTP requests. A separate SELECT-only
reconciliation passed exact persisted business/auth/receipt checks and
32 SQL-correlated audit expectations. The original runner's final verifier
timeout remains recorded; its failure evidence was not overwritten.

Nine existing synthetic source rows and employee revision 6 were preserved.
No migration, payment, real face/OTP call or legal activation occurred.
This covers only the single-policy/no-home-break outstation lifecycle, not
benefit/recovery fulfilment, historical vehicle chains or all HR acceptance.
See [executed O1 evidence and limits](hr-srs-remediation/HR-Outstation-O1-20261020.md).

## Latest Verified Update - 20 October 2026

C04 authenticated SQL acceptance passed 28/28 checks with 35 HTTP requests,
including department concurrency, CSRF, role checks and revocation. This
supersedes earlier unavailable-target notes, but does not accept every HR workflow.
See [actual C04 evidence](hr-srs-remediation/HR-Business-C04-20261020.md).

Frozen build09 passed Release with zero warnings/errors. Its vehicle regressions
passed 340/340, fuel reconciliation 42/42, and generated MVC/OpenAPI contracts
328/328. Historical maintenance now composes unchanged per-trip quantities with
explicit retained `CarryConsumedKilometers` transitions and monthly
`MaintenanceEffect=None`, without rewriting original consumption or evidence.
Changed historical quantities and selected `ReduceOutstation`/monthly aggregation
history remain restricted. These vehicle checks used synthetic collaborators,
not live vehicle SQL. See [supported cases and limits](hr-srs-remediation/Vehicle-Remaining-Decision-Matrix-20261020.md).

The separately approved V1 asset/custody SQL batch executed 36 actual HTTP
requests with 27 preceding checks passing, then stopped at a test-reader audit
correlation assumption. Post-stop read-only verification confirmed the exact
vehicle/returned custody, three receipts/audits and six encrypted evidence
objects. Logout/revocation was not reached; no overall V1 pass is claimed.
See [V1 actual result and reader defects](hr-srs-remediation/HR-Vehicle-V1-20261020.md).
No migration was applied. Benefit permission provisioning and
broader business SQL acceptance remain separate prerequisites; real face, external
OTP and legal activation remain excluded.

## Latest HR-Only Continuation - 19 October 2026

See [the HR scope and acceptance record](hr-srs-remediation/HR-Scope-Continuation-20261019.md)
for the owner's clarified boundary and current implementation evidence. HR owns
approved inputs/handoff; downstream Accounts posting, settlement and payment are
not prerequisites to implementing the remaining independent HR SRS workflows.
This continuation covers scoped team/self leave calendar, recovery reservation
integrity, outstation policy boundaries, vehicle policy publication, private HR
read permissions and report context. It does not claim actual recovery work,
extra benefit awards, all vehicle adjustments or authenticated SQL acceptance.

## 19 October 2026 Continuation

Latest executed status: [Continuation-20261019.md](hr-srs-remediation/Continuation-20261019.md).
Bounded financial correction/posting separation, schema-4 month membership and
deployment/home-break claims now have integrated source and standalone checks.
Finance's deployment lock validates the owning module's protected decision before
scheduling an allowance. Older "wholly open" descriptions below are historical.
No migration was applied; no current authenticated HTTP/SQL or production
acceptance is claimed. Legal, provider and remaining workflow gates are explicit
in the new report.

## 15 October 2026 Current Remediation Status

The controlling 19-task ledger is
[Plan-Progress-20261015.md](hr-srs-remediation/Plan-Progress-20261015.md), with
[executed evidence](hr-srs-remediation/Verification.md). New local repairs include
strictly interest-free principal-only loans, separately attested exact loan
posting, paid-installment lock/export protection, authoritative ESS attendance and
leave views, 14 report projections, bounded reminder outbox delivery, cancelled
leave recovery replay denial, and dated vehicle/fuel source checks. These are
implemented backend slices with standalone tests, not full Attendance acceptance.

Financial credit/recovery carry-forward, full benefit/recovery fulfilment,
outstation deployment continuity and remaining vehicle outcomes are still open.
Legal activation is not approved. Real face/device and external OTP delivery are
excluded. No React/React Native client acceptance or salary payment is claimed.

The current A02 packet proposes a NEW isolated LocalDB instance and a bounded
one-session authenticated test. It has not been executed; no current SQL/host/
private-key write is authorized merely by preparing it. Historical database facts
below describe their recorded runs only and are not approval to reuse those
databases or evidence that today's changed workflows passed authenticated SQL.

## Historical Context

Superseding local continuation, 13 October 2026: approved synthetic `Cosmix_Testing` is now at
migration 22, including additive Office Roster history, with a separately verified backup and
retained business records. Shift SQL/receipt/audit/encrypted evidence verification now passes;
Office HTTP acceptance remains incomplete after a bounded OTP failure. Face code checks are
offline-only and actual deployment/model/device prerequisites remain absent. These are not
full Attendance, financial carry-forward or legal activation acceptance. See [latest evidence](attendance-completion/Verification.md).

Latest executed laptop status, 13 October 2026: the user-authorized existing synthetic
`Cosmix_Testing` is at migration 21 after a verified backup. Actual web authentication/CSRF/
revocation and unassigned shift CRUD/retry/concurrent HTTP checks passed within their recorded
boundaries. A subsequent guard found current Office Roster model changes absent from the
snapshot; independent shift SQL/encrypted evidence verification stopped before connecting.
These results do not complete Attendance, Finance handoff, legal activation, office duty or
real biometrics. [Latest execution record](attendance-completion/Verification.md) supersedes
older database-state claims below. Clone/module integration remains paused.

Baseline: requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx.
The original supplied document is unchanged. Every folder below is prepared for implementation,
not a claim that the SRS feature is delivered. The feature rows and linked verification records below
distinguish implemented workflows from models and unresolved decisions. The last recorded original
development upgrade is migration 12; approved isolated `Cosmix_Testing` acceptance now has migrations 1–17.
OT and gated zones have implemented workflows with offline/anonymous verification; encrypted zone
storage has 43 synthetic real-file checks. Their full authenticated duty/device acceptance remains pending. See
[the current attendance checklist](attendance-completion/Implementation.md) and
[verification](attendance-completion/Verification.md) for exact code, acceptance and deployment boundaries.
Both clients use the same backend; no React/React Native UI is included.

13 October approved Attendance-only continuation: authoritative saved issue-free empty deduction
sources now support an exact no-charge Finance handoff through the existing attestation workflow.
This is a reviewable record, not posting/settlement/payment or legal activation. Actual standalone
service checks cover authorization, source validity, duplicates and replay; authenticated isolated
SQL acceptance remains pending. See [latest verification](attendance-completion/Verification.md).
Legal approval authority is still proposed; Admin configuration and SuperAdmin company settings
do not automatically grant legal deduction activation. Clone/module integration is paused.

Second local pass: bounded prior ordinary/late input reconciliation consumes protected manual
posted/settled evidence; month locking inspects exact saved financial revisions and retains schema-2
membership with replay/source-race guards. Selected Development face use now has an approved-use
retention gate and encrypted lifecycle journal. These do not complete legal activation, physical
biometric deletion, financial carry-forward, OT settlement or real authenticated SQL/provider/device
acceptance. See [current second-pass evidence](attendance-completion/Continuation-Second-Pass-20261012.md).

Huma laptop continuation supersedes machine-state assumptions above: the current local test
database was read-only inspected at migration 12. Migration 21 adds manual financial attestations
and record-only legal dossiers and remains unapplied. The selected CompreFace/Silent Face ONNX
adapter and bound capture APIs are PoC infrastructure, not accepted real-device attendance.
See [local gaps, ownership and execution](attendance-completion/Laptop-Continuation-20261012.md),
[financial attestations](attendance-completion/Payroll-Attestations-Implementation.md),
[legal evidence](attendance-completion/Legal-Review-Evidence-Implementation.md), and
[remaining SRS decisions](attendance-completion/SRS-Remaining-Decisions-20261012.md).

| Application folder | Owner | SRS reference |
|---|---|---|
| Authentication | Developer 1 | 4, AUTH-001 to AUTH-008 |
| HR/Organization | Developer 1 | SRS 5, 6.1, LEAVE-010; six master APIs and immutable holiday revisions implemented |
| HR/Employees | Developer 1 | SRS 5; HR-001 to HR-008; hiring approval and blocking lifecycle APIs implemented, later amendments/rehire pending |
| HR/EmployeeDocuments | Developer 1 | SRS 5, API-005, SEC-007 |
| HR/Designations | Developer 1 | SRS 6.1 |
| HR/Branches | Developer 1 | SRS 6.1 |
| HR/StaffCategories | Developer 1 | SRS 5 and 6.1 |
| HR/EmployeeAssignments | Developer 1 | HR-004, SHIFT-001, SRS 14 |
| HR/Compensation | Developer 1 | SRS 5, ROLE-004, HR-004; salary requests/decisions/effective history implemented; bank amendments and payroll remain separate |
| HR/CompanySettings | Local continuation authorized by user | Configurable company/Head Office references and explicit dated site jurisdictions; current SuperAdmin plus permission required for changes. No seeded address/province or inferred legal certification. See attendance-completion/Company-Settings.md |
| HR/Insurance | Developer 1 | SRS 15 insurance expiry/reminders; confirmed employee and vehicle coverage |
| HR/Policies | Local Attendance continuation authorized by user | SRS 8, 9, 10 and 19; explicit versioned form settings, direct HR publication, dated applicability/history and historical policy resolution implemented locally. Conflicts remain unavailable; no precedence, salary withholding or financial settlement is inferred. See attendance-completion/Direct-HR-Policies.md |
| HR/Holidays | Developer 1 | SRS 6.1; LEAVE-010 |
| HR/Leaves | Local Attendance continuation authorized by user | SRS 9; direct employee intake/Admin review, returned resubmission, separate cancellation, scanned private evidence, exact shift-bound day allocations and explicit HR settings implemented locally. Encashment, extra-leave recovery and payroll settlement remain separate. See attendance-completion/Policy-Leave-Implementation.md |
| HR/Allowances | Developer 1 | SRS 10.1; ALLOW-001 to ALLOW-004 |
| HR/VehicleFuel | Developer 1 | SRS 10.2; VEH-001 to VEH-009 |
| HR/AdvancesLoans | Developer 1 | SRS 11, 12 and 18 |
| HR/PayrollHandoff | Developer 1 with Developer 2 | SRS 12, 15, AC-007; MD-014 |
| HR/EmployeeSelfService | Developer 1 | SRS 11; MOB-007 and MOB-008 |
| HR/Reports | Developer 1 | SRS 15; REP-001 to REP-003 |
| Attendance/Sites | Developer 2 | SRS 6.1 Site master and versioned geofence APIs implemented; HR owns date-based site membership; capture uses historical boundaries. See Site-Geofences.md and current attendance verification for separate acceptance limits |
| Attendance/Shifts | Developer 2 | SRS 6.2; master APIs, immutable schedule versions and locked-period guards implemented; see Shifts-Rosters.md for permission provisioning and verification limits |
| Attendance/Rosters | Developer 2 | SRS 6.2 plus confirmed site-only/direct HR override: site assignment/history/replacement/ending, temporary amendments and pinned informational holidays implemented. Migration 13 and scoped authenticated acceptance ran only on the approved isolated database. My Shift uses historical site membership; individual writes retired/history retained. See Site-Rosters.md and group-rosters/Verification.md |
| Attendance/FaceVerification | Developer 2 | FACE-001–008; enrollment/history/revocation/challenge/finalization code exists. Approved provider/runtime, authority, retention and medical alternatives pending; adapter defaults unavailable. See Attendance-Capture.md |
| Attendance/Punches | Developer 2 | ATT-001–008; self Check In/Out, raw evidence, assessments, registered device and atomic proof consumption implemented. Scoped management punch/session audit preserves immutable revision history and safe device-time evidence. Real face success and authenticated SQL/mobile acceptance pending |
| Attendance/WorkSessions | Developer 2 | SRS 7, SITE-009, MD-006; factual pairing, own history and overnight context implemented; numeric travel/dedup/duration/continuous-duty rules unresolved |
| Attendance/DailySummaries | Developer 2 | ATT-009, SHIFT-003, SRS 8; factual day/calendar/history and immutable recalculation plus verified configurable daily time/classification implemented. Exact policy/leave/shift/punch/correction/OT provenance is bound to each result. Financial payroll readiness remains false; missing dependencies never become zero salary. See attendance-completion/Policy-Leave-Implementation.md |
| Attendance/PayrollInputs | Local continuation authorized by user | Permissioned monthly ordinary/late deduction previews and protected immutable replacement revisions; configurable schema 3, exact time/source provenance, larger-only overlap handling and historical jurisdiction context. Legal activation, prior-month reconciliation/carry-forward valuation and payroll posting remain separate. No automatic payment. See attendance-completion/Payroll-Inputs-Implementation.md |
| Attendance/Corrections | Developer 2 | ATT-008/010; own intake and source-proof workflows implemented. On 9 October the user confirmed one permission-controlled HR reviewer, including manual missing/failed Check-In duty. Supervisor/reviewer/atomic factual application is under verification. Locked months never reopen; adjustments go to the first later unlocked month, with financial policy still pending. See Attendance-Corrections.md |
| Attendance/Zones | Developer 2 | SRS 6.3; ZONE-001–010, clarified Operational/Red only. 18 APIs: scoped configuration/assignment, duty notice/windows, expiring live location, coarse events/review/alerts and gated cleanup. Real activation/SQL/files/device acceptance pending; see Attendance-Zones.md |
| Attendance/SiteReports | Developer 2 | MOB-009/013; eleven self/scoped intake/history/evidence/queue/HR decision and returned-response operations implemented. HR-only permission-controlled Accept/Return/Reject, no self-review, immutable protected employee responses and scanned attachments. Local migration 18 is prepared, not applied; real authenticated SQL acceptance pending |
| Attendance/Reminders | Developer 2 | MOB notifications/missing punch; explicit immutable shift-form delay, opt-in bounded missing-checkout sweep and durable self reminder. Checkout/correction/inactive account suppresses queued delivery. No total-duty threshold, automatic closure or salary effect is inferred |
| Attendance/Overtime | Developer 2 | SRS 8.3; OT-001–005; MD-007. 23 APIs: common outside-staff ON/OFF, employee OFF/inheritance, requests/history/HR decisions/bulk and protected confirmed base-salary/calendar-days/nine-hour formula. Real SQL/payroll settlement pending; see attendance-completion/Overtime.md |
| Attendance/MonthLocks | Developer 2 | ATT-010, SRS 7.3 and 12; existing lock/frozen-summary guards remain. User confirmed single-authority direct locking, no reopening, and factual carry-forward to the first later unlocked calendar month. These workflows are implemented with synthetic service/offline model and anonymous HTTP checks; real SQL and payroll-ready calculation integration remain unavailable. |
| Attendance/Reports | Developer 2 | REP-001–003; factual preview, bounded multi-employee daily register/dashboard and real private bounded CSV/XLSX/PDF export jobs implemented, with visible generation/filter/policy/lock metadata and explicit selected-employee coverage. Payroll-ready/policy outputs, broader operational registers and real SQL/load acceptance remain pending |
| Common/Approvals | Developer 1 | SRS 12 |
| Common/Audit | Developer 1 | AUTH-008; SRS 12.2 |
| Common/Files | Developer 1 | API-005; SEC-007 |
| Common/Notifications | Developer 2 | APR-005; own inbox/read and transactional self attendance/site-report, shift-change and report-ready producers implemented. Correction/carry-forward/OT decisions and scoped zone warnings stage notifications; SQL worker acceptance remains outstanding |
| Common/BackgroundJobs | Developer 2 | REP-001; durable notification/export outbox with leases and retries implemented, disabled by default. Real SQL lease/restart acceptance pending |
| Common/Idempotency | Shared; Developer 2 coordinates punches | API-003; ATT-003 |

## Mandatory boundaries

Salary-change continuation: see [Employee Compensation](Employee-Compensation.md). The accepted bounded flow
uses HR request -> different fixed Admin approval, salary-specific permissions, protected before/after evidence
and immutable dated versions. General configurable approval routing (APR-001), retroactive payroll changes and
other MD-001 amendment authority are not implied by this salary implementation.

Employee update continuation (8 October 2026): the PM allows direct basic profile edits, but requires
approval for CNIC/employee contact numbers and other sensitive changes. Employee-Updates.md implements
this split for SRS 5.1, HR-004/HR-007 and the MD-001 update boundary: existing identity, assignments,
bank/medical records and generic approvals are reused with an append-only protected before/after journal.
Department/designation/manager/office transfers remain reviewed and effective-dated. Authentication
phone recovery and Admin site reassignment stay separate. This does not implement every MD-001 lifecycle,
retroactive correction, arbitrary approval chain, payroll effect or remaining attendance workflow.

- Raw punches are append-only. Corrections and derived summaries are separate.
- Face verification is live, one-to-one, Check In only; no Check Out face requirement.
- Zone monitoring is duty-window limited; zone events never silently modify salary or punches.
- Attendance and operational reporting use the application, not external messaging or attendance hardware.
- Employee mobile is self-service; department management stays on the web.
- Employee identity is shared across modules; assignments and compensation are effective-dated.
- Supervisor scope is assigned team/site only. System Administrator is not automatically entitled to HR secrets.
- Policy/rate/provider decisions in SRS section 19 remain pending. No placeholder provider may return success.
- A generic file-import row and external-device-sync phrase remain in the supplied SRS. They do not authorize hardware integration; obtain PM clarification before implementing imports.

## API convention

Prefix the SRS route groups with /api/v1 (the SRS requires versioning). For example,
/api/hr/employees becomes /api/v1/hr/employees. The reference feature is
GET /api/v1/hr/departments and GET /api/v1/hr/departments/{id}.
Other HR/Attendance business routes must not be advertised as implemented merely because their models exist.

### Organization Masters Update - 7 October 2026

See [Organization Masters](Organization-Masters.md) for implemented departments, designations,
branches/offices, cities, staff categories, employment types and holiday calendar APIs. Master changes
require explicit web permissions; shared read permissions do not grant sensitive employee access.
Updates/status changes require rowversion and reason. Every successful mutation has atomic audit and
idempotency evidence; holidays are immutable versioned snapshots rather than in-place corrections.

PM clarification: Office Staff and Site Staff are separate categories. In the future onboarding form,
HR selects an office only for Office Staff. Site Staff submissions have no HR-selected site. Admin
creates sites and assigns/reassigns employees while retaining assignment history. Site master
list/detail/create/edit/status/history is implemented with fixed-role write control and delegable HR
read access. Employee assignments are delivered by the later HR approval slice; geofence/attendance workflows remain separate.
Office, designation, employment type and
authorization role are different concepts. The supplied SRS remains unchanged.

### Shared Authentication Update - 7 October 2026

Current shared authentication contracts are documented in
[Shared Authentication](Shared-Authentication.md) and [Access Management](Authentication-Access.md).
These document actual mobile OTP/session/device/access routes, web password + registered-number
SMS OTP, secure browser cookies/CSRF and the HR provisioning contract. Staff including HR/managers
use mobile self-service; fixed Admin/SuperAdmin roles are web-only. Admin sets the first web
password and assigns approved access; this never replaces SMS proof. The original SRS is preserved.
Password/phone recovery and the local first-admin setup command are implemented; actual owner
initialization, SMS selection and deployment acceptance remain pending. HR approval integration is delivered by the later Employee slice below.
The original development database is verified through the seventh recovery/owner migration.

## Before feature development

### Employee Hiring Intake Update - 7 October 2026

HR-001 and the draft/submission portion of HR-002/HR-003 now have real APIs, not only entity folders.
SRS 5.1 fields are captured through a confidential profile, separate financial/medical proposals and private
document routes. API-003 retries and API-005 file bounds/type checks/scanner gating are integrated.
HR submits an immutable protected snapshot to the fixed Admin review queue; submission itself does not activate employment,
create accounts, assign sites or enable attendance. Admin approval/return/reject APIs now record decisions, and approval
atomically materializes initial employment records plus a dormant account. Account role/scope/mobile/web setup remains
explicit through the existing shared access services; there is no invented role or automatic logged-in session.
Admin date-based site assignment/reassignment APIs preserve the previous period. Post-hire status and other lifecycle
APIs, expiry notifications, roster/face/attendance and deployment acceptance remain separate work.
See Employee-Hiring.md and Employee-Hiring-Verification.md. No client UI was built.

The SRS section 19 decisions cover onboarding/approval matrices, face provider/liveness thresholds,
geofences, zone alert/exception rules, cross-midnight examples, overtime rates, dinner amounts,
KPI calculation, waiting-time deductions, leave rules, early-reporting exceptions, face retention,
payroll boundary, general retention and production load. Assign management owners and dates.
Do not turn these uncertainties into defaults that affect attendance or pay.

## Part B Continuation - 13 October 2026

This section supersedes earlier missing office/capture-policy/retirement source
claims, not the original SRS or actual provider/SQL acceptance requirements.

| Task | Current Backend Trace | Acceptance Boundary |
| --- | --- | --- |
| B01 / OFF / ATT / SHIFT | Real Branch boundary history, exact office raw/duty/calendar/member pins, capture, correction, calculation/payroll jurisdiction consumers and missing-checkout reminders | Source checks executed; exact real-device/SQL cases remain separate |
| B02 / ATT / MOB / API | Independent approved capture validation; duplicate/break/travel/duration/device evidence; offline Unverified; exact final/retry authority timing | Explicit company groups/limits/mappings and real device/SQL acceptance required |
| B03 / FACE-008 / AC-003 | HR ordinary corrections retained; user-confirmed dedicated fixed ADMIN alternatives with configured evidence, retained permission-time authority and schema5 calculation; office/factual locked carry-forward and special beneficiary/replay denial | Source/controller/selected-repository checks passed; actual authenticated SQL/scanner/provider acceptance remains separate |
| B04 / FACE / NFR | Existing Development-only CompreFace/ONNX PoC and fail-closed face contracts | No model/deployment/consented device captures available; no production or genuine spoof acceptance |
| B05 / FACE retention | Durable committed-source retirement, retry/restart processing and disabled-by-default worker | No approved physical destruction/holds/backup/key authority; no claimed deletion |
| B06 / ZONE | Confirmed Operational/Red scope, independent Unverified offline written claims with expiring encrypted storage and own reconciliation | Configured deployment/notice/delay/retention/hold approval and real duty/device acceptance required |
| B07 / site reporting | Scoped queue discovery, protected review and permanent own submission-key reconciliation | Backend/service/query/contracts checked; genuine HTTP/scanner/outbox/client acceptance separate |
| B08 | CLOSED by explicit user decision: mobile punches and authorized corrections only | No attendance file import or machine integration |

See [Part B ledger](attendance-gap-b/Implementation-Ledger-20261013.md),
[office contract](attendance-gap-b/B01OfficeCapture-Contract.md),
[B03 handoff](attendance-gap-b/B03-Office-Corrections-Handoff-20261013.md), and
[B24 source handoff](integration/Part-B24-Source-Handoff-20261013.md).
No payroll payment release, legal approval, raw rewriting, reopened month or
client implementation is established by this trace.

## HR SRS Audit Repairs - 15 October 2026

This is a scoped security update, not superseding the remaining workflow or
production-acceptance limits above. The original SRS is unchanged.

| Trace | Current change | Remaining evidence |
| --- | --- | --- |
| ATT / MOB / API; audit F01 | Legacy employee-tracking capture retired with 410; tracking history delegates to the existing scoped Attendance audit service, with protected DTOs and no fabricated verification | Tracking clients must adopt the bounded query/page contract; fresh authenticated HTTP/SQL acceptance pending |
| REP-001 to REP-003 / access; audit F02 | All 14 HR report reads/exports require current web MFA/session, separate read/export and source permissions; mandatory server-resolved current/historical/source scope; protected read audit and revalidation | Report values/formats remain H1; SQL execution/concurrency, generated OpenAPI and client acceptance pending |

See [remediation status](hr-srs-remediation/Status.md),
[verification](hr-srs-remediation/Verification.md),
[tracking contract](hr-srs-remediation/Tracking-Boundary.md), and
[report access contract](hr-srs-remediation/Report-Boundary.md).
No real face/OTP provider testing or legal/payroll activation is implied.

## HR Attendance Continuation - 15 October 2026

This is a bounded continuation of the audit repairs, not complete Attendance or
HR acceptance. See the latest dated remediation verification section.

| Trace | Implemented continuation | Remaining boundary |
| --- | --- | --- |
| LEAVE / encashment; F03-F05, C1/C2 | Self identity, fixed Admin/versioned durable decisions, schema-3 opt-in policy valuation, exact retained salary/KPI/policy, signed reservation/release/consumption in the existing ledger | Fresh HTTP/SQL/concurrency/storage acceptance; financial posting/settlement and later adjustments are not implied |
| LEAVE / recovery; C1/C2/G1 | Configured bounded commitments attached to real approved leave/policy/decision evidence, owner-only reads and retry protection | Actual work fulfilment, earned benefits and salary recovery remain separate incomplete workflows |
| REP / H1 | Daily/monthly latest or locked-retained revision selection, exact session links, nullable missing quantities, known-only monthly totals, coverage and total-before-pagination | Other twelve report computations, populated export parity and live SQL/client acceptance remain |
| PAY / F2 | Stale calculation-to-handoff chain produces explicit readiness dependencies; OT fingerprints retain exact decision/session headers | Exact OT award membership consumption by Finance, chain supersession, settlement adjustments and legal activation remain blocked/unimplemented |
| NFR / I/J | Generated OpenAPI metadata, isolated worker registration boundaries and actual DI graph checks | These checks neither start an approved host nor establish authenticated database acceptance |

Client and UAT changes are in
[Attendance continuation contracts](hr-srs-remediation/Attendance-Continuation-Contracts.md).
The source SRS, historical migrations, normal authentication and separate clients
were not rewritten by this continuation.

### PAY / F2 - Later OT Retained-Finance Repair

Exact OT award membership and Finance consumption above are no longer unimplemented:
schema-3 private locks retain validated approved awards; Finance uses those members
and exact retained payroll revision IDs. Source/IO races, missing or substituted
members, unauthorized source reads and receipt replay remain guarded. Approval-month
selection no longer shifts September duty into October. Thirteen affected standalone
suites (978 checks) passed, with real service code and synthetic IO/dependencies.

Chain supersession, confirmed settlement adjustments, financial carry-forward,
formal legal activation and live authenticated SQL acceptance remain outstanding.
No full Attendance/client/provider acceptance or salary payment is asserted.
See [OT retained-Finance repair](hr-srs-remediation/Ot-Retained-Finance-20261015.md).

### PAY / ESS / E1-E2-D - Later Financial Boundary Repair

Handoff processing no longer posts loan deductions. Route request/agreement IDs
bind retry receipts; a foreign schedule and repeat reversal are rejected, and
loan decisions retain the actual assigned role. Manual PayrollDeduction remains
unavailable without exact confirmed posting integration. No historical ledger
records are deleted or automatically reversed.

Financial ESS now requires explicit self financial permission, current session
and audit persistence. Salary uses effective local dates; whole signed loan ledger
amounts are counted once, including negative reversals. Generated OpenAPI documents
these restrictions. Eight affected standalone suites / 569 checks passed against
the final 15 October Finance/ESS Release, with synthetic IO only.

E1's posting integration, D's authoritative leave/assignment/calendar reads, F3's
financial carry-forward, formal legal activation and live authenticated SQL remain
open. The complete [19-task progress matrix](hr-srs-remediation/Plan-Progress-20261015.md)
supersedes old repaired shorthand, not the historical evidence. Exact changes and
verification: [Finance/Loan/ESS](hr-srs-remediation/Finance-Loan-ESS-20261015.md).

### HR Benefits, Recovery and Vehicles - 19 October Continuation

This entry supersedes earlier statements that the following three HR workflows
only contain settings or commitments. It does not close Accounts, legal or live
SQL acceptance. No database migration is applied in this continuation.

| Requirement | Actual backend flow | Remaining boundary |
| --- | --- | --- |
| LEAVE011 extra/KPI benefit | Configured v4 policy, finalized KPI/source/dispute validation, independent permitted Admin grant, immutable positive leave credit/history, ordinary quota and policy-selected encashment integration, unused reversal | Consumed-credit adjustment needs explicit allocation/review design; company policy/grants and authenticated acceptance remain |
| LEAVE013 recovery fulfilment | Retained commitment plus verified work-source preview, exact interval claim, independent review, partial/full progress, OT/make-up exclusivity and immutable evidence | Missing policy decisions block claims; changed/cancelled sources retain allocations; no invented financial recovery or refund |
| VEH-003 / VEH-008 monthly/incident adjustments | Effective-dated configured monthly distance review/revisions, approved entitlement and maintenance linkage, independent incident amendment with retained history | Ambiguous same-day assignment precedence and open-ended legacy policy succession remain explicit limits; no automatic wage deduction/insurer settlement |

See [the implementation and executed-results record](hr-srs-remediation/HR-Benefits-Recovery-Vehicles-20261019.md)
and [client contracts](hr-srs-remediation/HR-Benefits-Recovery-Client-Contract.md).
Separate React/React Native clients are not implemented by these backend changes.

### HR Correction and Dated Vehicle Follow-up - 19 October 2026

This follow-up supersedes the consumed-credit and open-ended vehicle succession
limitations in the preceding row, only within the explicitly implemented scope.

| Trace | Added implementation | Acceptance boundary |
| --- | --- | --- |
| LEAVE011 / APR-003 | Exact Admin-attributed consumed benefit correction; unused withdrawal, retained review obligation, source-bound non-spendable offsets, released-history revocation | Recovery settlement is not invented; locked periods remain protected; see executed verification separately |
| VEH-002/003/006 | Reviewed explicit daily trip segments, cross-month consumption, configured policy partitions and immutable version succession | No guessed mileage split or same-day assignment precedence; legacy approved trips without daily evidence require a separate correction design |
| SEC / client evidence | Targeted web repairs replace fabricated site, shift, biometric and attendance facts/successes with existing authorized sources or unavailable states | These frontend changes are separate from backend contracts and do not constitute real provider/mobile/SQL acceptance |

Contracts: [benefit corrections](hr-srs-remediation/Benefit-Correction-Contract.md),
[dated vehicle attribution](hr-srs-remediation/Vehicle-Dated-Attribution-20261019.md).
Executed build/test evidence and the owner integration list are in the
[dated follow-up verification](hr-srs-remediation/HR-Followup-Verification-20261019.md).
The bounded [remaining HR audit](hr-srs-remediation/HR-Remaining-SRS-20261019.md)
records other client workflows and acceptance requirements. No database migration
is applied and no Accounts payment or legal approval is introduced.

### HR Exports, Legacy Trips and Site Facts - Later 19 October 2026

This entry supersedes the preceding legacy-trip implementation limitation only
within the explicit correction contract, not live acceptance.

| Trace | Implemented continuation | Acceptance boundary |
| --- | --- | --- |
| VEH-002/003/006 | Admin-reviewed retained legacy trip attribution correction; source fingerprints, immutable original trip/mileage, cross-month stale-consumer rejection and explicit equivalent-assignment policy | Unsupported consumed/ambiguous legacy cases remain explicit conflicts, not automatic payment repricing |
| HR reporting / audit | Existing outbox-backed full filtered CSV/XLSX/PDF jobs; bounded complete generation, encrypted output, permission-bound replay/status/download and expiry | Real authenticated SQL/worker/storage/concurrency UAT and readable localized PDF remain unverified |
| Site scheduling / attendance evidence | Historical site shift and effective or punch-pinned boundary selection, unique factual workers, latest session revision, exclusive assignment ends | Conflicting evidence remains unavailable; not real-time tracking or compliance certification |

See [final integrated results and changed-file groups](hr-srs-remediation/HR-Production-Continuation-20261019.md).
Build02 and offline regressions passed; original `Cosmix_Testing` was independently
confirmed ONLINE by read-only identity checks. No migration or database recovery
operation was performed. Connectivity alone is not authenticated workflow acceptance.

### HR Closure and Bounded Authentication - Latest 19 October 2026

This entry supersedes older blanket statements that no authenticated SQL checks
have run. It does not mark all HR business workflows accepted.

| Trace | Implemented and checked | Remaining acceptance boundary |
| --- | --- | --- |
| LEAVE011 / APR-003 | Explicit policy-enabled Admin waiver of a consumed-benefit correction, immutable proof, current usage/source fingerprints, no self-review and retry binding | Does not grant spendable leave or manufacture recovery/payment evidence; business SQL concurrency remains unexecuted |
| LEAVE013 | Recovery replay revalidates allocations and retained decisions; contradictory or missing evidence fails closed | Real work-source, approval, cancellation and concurrent SQL UAT still required |
| VEH-003 / VEH-008 | Opt-in same-context combined daily commute calculation, immutable breakdown, trip/custody/commute overlap rejection | No automatic default, guessed mixed-context allocation or repricing of historical approved claims |
| Outstation | Configured rates/day counting retained; resubmission binds and validates prior protected claim and return evidence | Legacy resubmissions without the required evidence remain explicit conflicts, not fabricated backfills |
| HR reporting | Export expiry and scheduled scope changes revalidated around IO; actual encrypted CSV/XLSX/PDF offline checks | Worker/authenticated SQL contention and full client UAT remain separate |
| Authentication / SEC | Actual HR and Admin login, local MFA, permissions, CSRF rejection, logout and revoked-cookie rejection: 24 HTTP requests, 16 checks PASS against existing Cosmix_Testing | Auth-only writes; local named-pipe transport exception approved for this test only; not production TLS or full HR acceptance |

No migrations or business-data writes were performed. All 148 protected snapshots
and seven retained C01 evidence files were unchanged, including a comparison after
the owned HTTPS host exited. Separate web/mobile clients, real SMS/face, legal
activation and production deployment are not certified by these results.

Exact builds, standalone results, live target identity and remaining limits:
[latest closure evidence](hr-srs-remediation/HR-Closure-20261019.md).
New API and dynamic settings:
[client contracts](hr-srs-remediation/HR-Closure-Client-Contracts.md).

The later build02 additionally supports the first fuel claim for a previously
unclaimed, approved corrected trip, using protected daily attribution and dated
rates. Old approved claims are not repriced; old pending claims and missing
retained evidence remain explicit conflicts. See the build02 results in the same
closure evidence, rather than treating route availability as live SQL acceptance.

### HR Continuation - 20 October 2026

| Trace | Current implementation | Acceptance boundary |
| --- | --- | --- |
| Leave / benefit review | Fixed canonical system-role code mismatch (`ADMIN`/`SUPERADMIN`); new evidence uses canonical codes and legacy evidence retains exact crossbindings | Explicit permission, fixed-role assignment, scope, MFA and no-self-review remain required; no role/database edits |
| VEH historical fuel | Independent source-bound rejection/closure of superseded pending schema-1 claims | No repricing, replacement claim, refund, payment or one-claim-per-trip reset |
| VEH maintenance | Existing next-service workflow can retain a proven first-consumption equivalence after approved correction | Exact membership and per-trip through-service-date quantities required; chains, changed quantities and monthly-adjustment reconciliation remain outside the bounded slice |
| Business SQL acceptance | C03 stopped at Admin queue assertion before department creation; 155 protected snapshots unchanged | C04 unexecuted: after approved existing-instance startup, intended `Cosmix_Testing` was not registered. No substitute/attach/reset/migration |

Latest executed results, precise remaining cases and current SQL stop boundary:
[20 October continuation](hr-srs-remediation/HR-Continuation-20261020.md).

### HR Vehicle Continuation - Later 20 October 2026

This supersedes the first-consumption-only restriction above, not the SQL stop.

| Trace | Implemented and offline verified | Remaining boundary |
| --- | --- | --- |
| VEH maintenance history | Subsequent equivalent services and ordinary pre-correction ancestors retain exact per-trip evidence, chronology, decisions and single consumption | Changed quantities, monthly-adjustment and threshold/policy-transition reconciliation are not guessed |
| VEH historical fuel integrity | Original schema-1/2 claim evidence validates on retry; altered amount/liters/currency/policy rejected | No repricing of original approved claims |
| VEH changed fuel amount | Dated-policy comparison, immutable separate HR proposal, independent permitted review, exact source/version/replay checks and protected history | Not Accounts posting, settlement, credit/debt creation or payment; no automatic financial activation |
| Verification | Build06 clean; 1,247/1,247 across ten final-output offline suites; MVC OpenAPI includes 529 paths | No live vehicle SQL/storage/concurrency acceptance; C04 still unexecuted |

Contracts and evidence:
[fuel reconciliation](hr-srs-remediation/Vehicle-Fuel-Reconciliation-20261020.md),
[maintenance chains](hr-srs-remediation/Vehicle-Maintenance-Chain-20261020.md),
[latest results](hr-srs-remediation/HR-Continuation-20261020.md).

### Actual C04 SQL Acceptance - Latest 20 October 2026

The existing database was located on the SSMS user's running pipe and verified
against its original family/owner/files and 25 migrations. After explicit rebind
approval, C04 passed **28/28 checks across 35 actual HTTP requests**: local MFA,
permission/CSRF boundaries, Admin leave-queue access, concurrent department
create/update, persisted history/audit/receipt consistency and logout/revocation.
One marked inactive department remains; 155 protected snapshots and 24 prior
files were unchanged after host exit. No migration or existing business-data
change. This supersedes C04-unexecuted statements above, not the remaining
benefit/recovery/vehicle/outstation/report acceptance boundaries.

[Exact C04 evidence and limits](hr-srs-remediation/HR-Business-C04-20261020.md).

### Six-Gap Fixes and Actual R1 Acceptance - Latest 20 October 2026

Benefit review now supports the user-approved genuine Admin plus explicit
All-scoped custom review grant, including expiry/revocation rechecks. Vehicle
single-ancestor maintenance verifies the full chain inventory. Outstation
approved-claim retries retain their original result. Report publication rejects
a retained permission that expires during final scope IO. Rates, eligibility and
counting policies remain configured; no live permissions/policies were changed.

Build11 is clean; affected standalone benefit/recovery/vehicle/outstation/report
suites passed. Actual R1 authenticated SQL/private-storage report acceptance
passed **21/21 checks in 20 HTTP requests**: synthetic employee-only CSV/XLSX/PDF,
one captured durable job, same-key retry, download, logout/revocation and preserved
prior data/files. One report-ready notification intent is pending by design;
global workers were disabled. No migrations or real face/OTP/legal activation.

This does not replace the still-unexecuted policy-backed benefit/recovery,
historical vehicle/outstation SQL scenarios or frontend UAT.
[Latest six-area status and file handoff](hr-srs-remediation/HR-Six-Gap-Continuation-20261020.md).
[R1 exact evidence and limits](hr-srs-remediation/HR-Reports-R1-20261020.md).
