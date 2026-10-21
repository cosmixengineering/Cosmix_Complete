# Attendance implementation — updated 12 October 2026

The supplied complete-Attendance prompt is the authorized implementation scope. Work stays in
the existing dirty local checkout. No source-control mutations or database writes are authorized
by this document. Previous isolated acceptance covered access/scheduling/geofences, not these new fixtures.

## Preserved decisions

Site-only historical scheduling, direct permission-controlled HR scheduling changes, pinned
informational holidays, current shared employee identity and existing role management remain.
No individual/group precedence, automatic permission grants, inferred financial rules or face success.
The user subsequently confirmed a single authorized HR reviewer for attendance corrections.
Review permissions are assigned through the existing custom-role flow; a role name alone grants
no authority. The user also confirmed HR approval of manual missing/failed Check-In duty, retaining
failed raw/face evidence. Locked months **must never reopen**: approved factual adjustments belong
to the first available unlocked calendar month after the source month, with the target shown before
approval. Financial effects still require the approved policy/payroll integration. A provider
comparison was supplied from current official AWS/FaceTec/Innovatrics/Azure sources; the user
explicitly deferred provider selection while other backend work continues. None was enabled or called.
The user confirmed that one authorized person reviews and directly locks a month; no second
approval or fabricated approval request is required. Payroll readiness remains a prerequisite.

## Design and ownership

One scoped AttendanceOperation reuses current authentication, registered devices, scope evaluation,
the existing SQL authorization transaction, private evidence and idempotency journal. Modules own
their repositories, DTOs and services. No generic CRUD framework or new identity is introduced.
Raw evidence remains append-only; assessments, session revisions and calculated revisions remain
separate. Factual time must not require a fabricated policy ID. Provider calls cannot hold the SQL
authorization lock. Their finalization must revalidate current identity, device, enrollment and expiry.

Root coordinates shared registrations, permission catalog, context/snapshot/migrations and factual
summaries/reporting. Capture agent owns face/punch/session files. Site-report agent owns site reports.
Notification agent owns in-app inbox and durable outbox delivery. No agent applies migrations.

## Requirement checklist (current local result)

The latest [policy/leave continuation](Policy-Leave-Implementation.md) supersedes the older
policy/leave gaps and operation count below: direct HR publication, Admin leave workflows and
verified daily time interpretation have been added locally. Financial readiness, live face-provider
acceptance and authenticated SQL/private-storage acceptance remain incomplete. The table below
retains the previous checkpoint's executed checks; they are not a claim that every new workflow
has passed those earlier database runs.

One hundred and nineteen Attendance operations are registered and exercised anonymously. The original access/site
scheduling SQL acceptance is a separate baseline. New service checks use explicit in-memory
collaborators; **none establishes real SQL, live provider or completed Attendance acceptance**.

| Requirements | Implemented behavior | Remaining implementation / dependency | Endpoints and actual evidence |
|---|---|---|---|
| SHIFT-001/002; ATT-005/006 | Reused site-only roster/geofence history in capture; added atomic affected-employee shift notifications | Fresh capture SQL acceptance, production configuration/load | Existing site APIs/My Shift; 171 site, 135 shift, 41 publication, 32/55/29 geofence regressions |
| FACE-001–008; SEC-011/012; AC-002 | Scoped enrollment/replacement/revocation/history; device/punch-bound challenge, audited verification attempts and single-use proof; durable enrollment start/terminal outcomes and safe retry arbitration; protected, scoped enrollment-outcome pagination; separately keyed template-store contract | **Not complete:** approved SDK/runtime/thresholds, authority, real liveness integration, retention/deletion and medical alternatives. Default provider/store fail closed | Eight face operations under `/attendance/face` and `/hr/employees/{id}/face-enrollments`; [capture contracts](../Attendance-Capture.md), 30 domain/30 model/85 service, 10 readiness/19 enrollment-journal checks |
| ATT-001–008; API-003; AC-005/009 | Own Check In/Out, immutable raw evidence, explicit assessment outcomes, factual session revisions, historical pins, current registered device, atomic proof consumption and exact retries; scoped management audit and mobile guidance; immutable shift delay plus gated durable missing-checkout reminders | Real accepted Check In depends on approved face integration; quantitative duplicate/travel/duration/continuous-duty rules and offline revalidation remain unresolved. Real reminder delivery needs new isolated acceptance | Five self capture operations and two [audit reads](Audit-Reads.md); 15 rules/89 capture-service, 46 audit-service/12 query checks; [reminders](Missing-Checkout-Reminders.md): 43 service/rule + 23 query checks |
| ATT-009; SHIFT-003; SRS 8; AC-004 | Actual closed elapsed time; own/scoped daily/calendar/history; immutable unlocked recalculation with protected inputs; historical Team/Project alternatives and frozen lock evidence; internal append step reuses the caller's authorized transaction | Approved general HR policy calculator absent locally; leave/late/early/absence/payable fields stay unavailable. Confirmed OT calculation is separate | Seven summary operations; [summaries](Summaries.md), 11 facts/61 services/23 scopes/8 query translation checks |
| ATT-010; SRS 7.3; AC-003 | Own/supervisor source preview, immutable proposals/history/cancel; scoped single HR review/return/reject/approve; manual missing/failed Check-In duty, missing checkout and corrected factual session/summary revisions; atomic decision notices; original raw/face evidence retained | Real authenticated SQL/rollback acceptance pending. Locked-month factual carry-forward is implemented separately; reopening forbidden | Six self and eight web operations; [corrections](../Attendance-Corrections.md), 99 service/33 actual query translation/25 historical eligibility checks |
| OT-001–005 | Common outside-staff OT ON/OFF plus profile OFF/inheritance, immutable history, self/named/bulk requests, single HR decisions and protected confirmed base-salary/calendar-days/nine-hour financial formula | Authenticated SQL/private-storage acceptance; payroll settlement and locked-source financial adjustments; unsupported compensation/schedules explicit | 23 registered operations; 26 formula, 67 service, 46 query/model checks; [OT contracts](Overtime.md) |
| ATT-010; SRS 7.3/12; AC-007 | Direct single-authority month preview/finalize/frozen detail; first-unlocked-month factual carry-forward request/review/decision and immutable ledger; original month never reopens | Real authenticated SQL and approved payroll-ready calculation integration remain unavailable; pending/unsettled OT blocks finalization | 3 month-lock and 15 carry-forward operations; 34/41 month checks, 13/55/23/35 carry checks; [month locks](MonthLocks.md) |
| MOB-009/013; AC-005B | Real mobile submission/history/private evidence; scoped queue; HR Accept/Return/Reject, no self-review, immutable protected employee response/resubmission; scanner gating and retained offline Unverified status | Migration 18 is prepared but unapplied. Real authenticated SQL/scanner/concurrent-write acceptance remains | Eleven `/site-reports` operations; [site reports](Site-Reports.md): 119 service/rule, 14 query and 94 dedicated registered HTTP checks |
| SRS 8/9/19 policy dependencies | Explicit typed HR settings validation with safe field errors, canonical hash, strict JSON and permission-controlled audit; no guessed rules or publication | Publication authority, policy assignment/calculator, approved leave effects and payroll handoff are incomplete; validation always returns `published: false` | One `/hr/attendance-policies/validate` operation; [contract](../attendance-policies/Rules-Contract.md): 90 Domain, 12 service and 12 JSON-boundary checks |
| ZONE-001–010; SEC-008A; AC-005A | Operational/Red only; explicit immutable site configuration/boundaries/assignments, accepted-duty notice/windows, interval samples, short-lived current location, coarse events/reviewer notifications and gated retention | Management values and activation, real private-files/SQL/device acceptance; no Red exemptions or invented override precedence | 18 registered operations; 16 geometry, 52 service, 27 query/model/migration checks; [zones](../Attendance-Zones.md) |
| MOB-001/003/004/007/008 | Own attendance state/actions, punches, sessions, factual calendar/history, correction proposals and exception notices; existing My Shift preserved | Own OT/decision views are implemented; leave/HR/payroll integration remains absent. Separate React/React Native clients not built | Reuses routes above; server-side self identity/revocation and synthetic service coverage |
| APR-005; API-002/004; SRS 12 | Own inbox/read, safe event templates, transactional producers, persisted outbox leases/retries; exact registered-handler claim ownership; capture exceptions, site reports, site/temporary/version shifts and export-ready notifications | Worker opt-in plus real SQL lease/restart/rollback acceptance; decision/periodic alerts depend on missing workflows. No external SMS/email workflow added | Three `/me/notifications` operations; [delivery](notifications/README.md), 78 standalone checks |
| REP-001–003; AC-007/008 | Scoped factual preview, multi-employee daily register/dashboard, private asynchronous CSV/XLSX/PDF, visible generation/filter/policy/lock metadata, preserved source provenance, original/current scope reauthorization | Payroll-ready/policy reports, broader operational registers/load acceptance and real SQL/private-file/worker acceptance remain outstanding | Six `/attendance/reports` operations; [reports](notifications/Reports.md), 54 dashboard/batch checks, earlier 70 export-service/20 format/19 independent parser checks and five rendered PDF pages |

The latest continuation also adds three [exception read operations](Exceptions.md): scoped
management list/detail and own sanitized detail. The separate permission has no automatic grants.
54 service checks and 35 real query-translation checks passed without SQL execution. Missing
assignment now appears as an employee/date issue in month preview (40 service checks), preserving
the blocked lock; correction regression passed 99 checks. The [review guide](Attendance-UAT-Guide.md)
and proposed [policy integration contract](Policy-Integration-Contract.md) explain remaining dependencies.

## Execution and evidence

- [x] Read supplied implementation scope, repository instructions and current core contracts.
- [x] Identify mandatory-policy schema mismatch for factual attendance; do not seed fictitious policy.
- [x] Ask one initial batch covering provider/runtime, attendance decision authority and face/policy settings.
- [x] Implement and independently check shared authorization/replay, including old flat and new grouped receipts.
- [x] Implement independent capture, site-report, self correction intake and notification code; record gated behavior.
- [x] Implement factual summaries and real CSV/XLSX/PDF export workflows with private provenance.
- [x] Implement confirmed correction/month/carry-forward/OT/Operational-Red workflows; preserve unresolved dependencies explicitly.
- [x] Review/script incremental migrations 14–17; apply only to user-approved isolated `Cosmix_Testing` after verified backups. Actual batch-compilation defect reproduced and corrected before guarded continuation.
- [x] Execute 43 encrypted zone-storage checks with a disposable owner-only private fixture; preserve retained settings/keys.
- [x] Execute the approved narrow factual web slice: 39 continuation assertions and one summary, followed by 59 independent read-only SQL/private-evidence checks. Full HTTP run remains partial at missing historical staff assignments in month preview; no lock success is claimed.
- [x] Final Release rebuild (0 warnings/errors), meaningful standalone checks and existing affected regressions.
- [x] Latest registered HTTP/OpenAPI/traffic sweep: 726 passed across 119 Attendance and 58 existing employee operations; anonymous only. Dedicated site-report sweep: 94 passed. Full results and limits are in the current verification record.
- [x] HR site-report decisions/responses and immutable shift reminders implemented; migration 18 and incremental SQL prepared locally, not applied. Existing 34 migration/designer file hashes are unchanged.
- [x] Update contracts, client examples, requirement statuses and actual verification without claiming complete Attendance.

## Required decisions and deployment gate

Provider selection remains explicitly deferred, so real face-verified Check In remains unavailable.
The user authorized local Attendance policy work. Form validation exists; general policy
publication/calculation, leave effects and payroll integration remain incomplete and were not fetched or merged.
One HR reviewer, common outside-staff OT plus profile OFF, base salary / actual calendar days / nine,
no break deduction and whole-rupee final amounts are confirmed and implemented. Office staff remain
ineligible. HR site-report review and shift reminder configuration are now implemented. Publication
authority, remaining financial/leave settings and quantitative capture limits still need decisions.
Operational/Red monitoring requires management-approved explicit values and stays disabled. Real
full SQL/mobile/competing-writer and separate-process crash acceptance remains outstanding. The
approved [isolated acceptance plan](Isolated-Acceptance-Plan.md) covers schema and a narrow factual web slice;
its result does not establish live duty, OT approval or payroll acceptance.
No real account grants, biometric uploads or provider calls occurred. **Real employee Attendance is
not ready to enable.**
Global SQL authorization serialization is initially reused for correctness and compatibility with
access revocation. It is a throughput limitation requiring production load validation (MD-016), not
a claim of high-volume punch readiness. Private objects can outlive a rolled-back transaction;
retain evidence and never delete unowned files during cleanup.
