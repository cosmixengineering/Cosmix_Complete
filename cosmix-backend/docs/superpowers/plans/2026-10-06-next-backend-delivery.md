# HR and Attendance Backend Delivery Plan

Updated: 7 October 2026. This replaces the earlier proposed roadmap in this file.
Historical verification remains in Shared-Authentication-Verification.md.

> **For agentic workers:** Use superpowers:executing-plans only after the PM authorizes
> a specific implementation slice. Track its steps with checkboxes. This is a delivery
> roadmap, not authorization to implement every module, invent policy or begin frontend work.

**Goal:** Deliver the remaining HR, Attendance and Employee Mobile requirements in
dependency order, reusing the implemented shared authentication backend.

**Architecture:** Keep the existing four-project Clean Architecture, one SQL Server
database, module-specific repositories and services, and one shared employee/account
identity. Reuse existing domain models and EF mappings.

**Tech Stack:** .NET 10, C#, EF Core Code First, SQL Server. React and React Native
clients are separate work owned by the PM.

**Spec:** ../../requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx, with the
latest PM-confirmed login, role and onboarding rules in ../../Shared-Authentication.md.
The supplied Downloads copy and repository SRS have the same SHA-256 hash.

## 1. Current Authentication Position

This is shared project authentication, not an HR-only login system.

| Capability | Backend position |
|---|---|
| Employee mobile login | Registered number plus SMS OTP, no password |
| Web login for all authorized staff | Email/password, then registered-number SMS OTP |
| Admin and SuperAdmin | Fixed protected web-only roles; not recreated as custom roles |
| HR, manager and other roles | Created from supported permissions; web access administered separately |
| Admin-set first web password | Implemented for an approved account with valid access |
| Forgotten first password before any login | Audited reset by another authorized Admin; existing sign-ins use SMS recovery |
| Sessions and devices | Rotation, replay detection, revocation and server-side eligibility checks |
| Revoked installation recovery | Admin identity review, new unverified device record, then fresh SMS OTP; history retained |
| Password recovery | SMS proof, change/reset, old-session invalidation |
| Phone recovery | Audited review by another authorized administrator, then new-number OTP |
| First SuperAdmin | Private one-time local setup command; no public signup/default password |
| Database | Existing Cosmix_Development, seven migrations, 122 business tables |
| Source control | PM authorized sharing the current backend baseline on develop on 7 October 2026; use the verified remote commit for handoff |

The PM explicitly confirmed on 7 October: keep the backend ready; actual CEO/director
account details will be supplied later. Do not create a fabricated owner or demo account.

Backend implementation/verification and production activation are separate sign-offs.
Read the current verification report for tests actually executed and remaining limits.

### Whole SRS Status

Rechecked against the supplied SRS, current controllers/services and the existing verification
record on 7 October. No tests or migrations were rerun for this planning-only update.
Tables and domain rules are implementation foundations, not working business modules.

| SRS area | Implemented now | Still to deliver |
|---|---|---|
| Sections 3 and 14, architecture/data | Four projects, shared identity, expanded models/mappings, applied development migrations | Module services/DTOs/controllers; approved incremental schema changes only when needed |
| Sections 2 and 4, authentication/access | Shared login, recovery, protected roles, current permission/scope evaluation and auth audits | Live SMS/settings/owner setup; each future module's permissions and actual scoped queries |
| Section 6.1, organization | Department list/detail endpoints | Master-data create/update/deactivate, remaining masters and reporting assignments |
| Section 5, employees | Entities, draft/submission/decision rules and callable auth provisioning contracts | Complete field validation, private documents, snapshot orchestration, HR/Admin APIs, lifecycle and history |
| Sections 6-7, attendance/zones | Models, evidence/history constraints and feature folders | Sites, shifts, rosters, face provider, punches, summaries, corrections, zones and site-report services/APIs |
| Sections 8-10, business rules | Effective-dated policy, leave/claim/vehicle data structures | Approved calculations, balances, eligibility, workflows and financial impacts |
| Section 11, employee self-service | Login/current-account/device endpoints only | Attendance, profile, leave, financial and operational self-service APIs; actual mobile screens/behavior |
| Sections 12 and 15, shared workflows/reports | Auth audit persistence and queued new-device notifications | General approvals, private file handling, notification inbox, jobs, business audits, real exports and payroll handoff |
| Sections 16-17, acceptance | Recorded auth/build/schema and bounded service/HTTP/SQL checks | Complete business workflow, client, face, performance, recovery/load, backup restore and UAT acceptance |

SRS section 18 status: Phase 1 is **partly complete**, because master-data writes,
general file storage and production environment/security work remain. Phases 2-6 have
model foundations but are not delivered modules. No SRS AC-001 through AC-010 end-to-end
completion is claimed, including AC-005A/005B. React/React Native repositories were not
reviewed in this update; frontend completion is not inferred from backend progress.

## 2. Activation Items Reserved for the Company

These do not justify rebuilding authentication or blocking independent HR development:

1. Select the SMS vendor, sender identity and approved gateway/adapter contract.
2. Provide provider credentials through local secrets or the deployment secret manager.
3. Approve OTP/session/password/rate-limit operating values and environment endpoints.
4. Provide approved CEO/director identity and authorization through private local input;
   enter the password privately in the first-owner setup, never in chat or Git.
5. Run real SMS acceptance on authorized test numbers: login, reset, recovery, expiry,
   rejection, timeout and resend. Provider acceptance is not proof of handset delivery.
6. Before production, validate deployment TLS, private storage/keys, backups/restore,
   concurrency/load and actual browser/mobile client behavior.

Until then, login remains configuration-gated. A compatible GenericHttp gateway is
supported; a vendor with a different protocol needs its own reviewed adapter.
No fake-success sender or fixed OTP may be enabled in the application.

## 3. Next Module: Organization Master Data

**Start here next.** Employee creation depends on these references.
**Owner:** Backend Developer 1.
**SRS:** sections 5 and 6.1; API-001 to API-004; ROLE-001/004.

Complete departments, designations, branches, cities and staff categories using the existing
HR feature folders. Expose the existing controlled employment types without duplicating them.
Developer 1 owns holiday calendars/notification references and reporting-manager assignments;
Developer 2 consumes these IDs in rosters and attendance. Preserve this single ownership.

- [ ] Review current fields and relationships before adding any model changes.
- [ ] Add protected create, detail, paginated list, update and deactivate operations.
- [ ] Validate required values and uniqueness; preserve referenced historical records.
- [ ] Add exact supported permissions for these implemented operations, separate from login.
- [ ] Enforce concurrency and audit; handle duplicate submissions safely.
- [ ] Verify success, invalid input, duplicates, stale version and unauthorized/mobile access.
- [ ] Review OpenAPI and build in Release.

**Done means:** the employee form can obtain real authorized dropdown data and masters
can be administered through working APIs. Folder names or read-only Department examples
are not completion.

## 4. Employee Master and Approval

**Owner:** Backend Developer 1.
**SRS:** HR-001 to HR-008, section 5.1, APR-001 to APR-003, AC-001/009/010.
**Dependencies:** master data, private file handling and current shared authentication.

Implement one complete vertical flow:

**HR saves Draft -> validates required fields/documents -> submits -> Admin reviews ->
Approve / Return / Reject -> approved account/access setup -> normal login.**

- [ ] Use the complete section 5.1 field list and docs/Employee-Fields.md. Include identity,
  contact/address/emergency, employment, assignments and separately protected compensation,
  bank, medical, documents/certifications and history where applicable.
- [ ] Keep required, optional and conditional fields distinct. Drafts may be incomplete;
  submission/activation may not bypass required validation.
- [ ] Preserve the employee number sequence, uniqueness and one employee identity.
- [ ] Implement private document upload/metadata/download, validation and malware-scan
  gating in the employee-document slice. Do not fabricate scan success.
- [ ] Preserve submission snapshots and decision history; enforce separation of requester
  and approver. The initial HR-to-Admin chain is confirmed; other approval matrices are not.
- [ ] Deliver the first actual approval queue and approve/return/reject use cases with employee
  onboarding, reusing approval/audit records. Do not postpone approvals until after leave/payroll.
- [ ] Wire approved identity to IEmployeeMobileProvisioner.ProvisionAsync after the HR
  approval transaction commits. Make retries durable/observable and idempotent; a failed
  provisioning attempt must not leave HR showing successful login activation.
- [ ] Coordinate access assignment and Admin-set initial web credentials through the
  existing authentication contracts. Approval alone does not prove a phone or create a session.
- [ ] Record effective-dated employment, manager, site, shift and compensation changes.
  Suspension/termination must be recognized by authentication and later attendance.
- [ ] Keep Developer 1 responsible for employee employment/reporting/site-assignment history;
  Developer 2 owns shift/roster assignment operations. Publish these contracts before either writes.
- [ ] Verify an authorized ordinary employee and HR/manager self-service account cannot
  read another employee's restricted fields or administer management APIs from mobile.

Face enrollment is an Attendance dependency, not a condition for ordinary OTP login.
Mobile Check In remains unavailable until the actual face/enrollment requirements are met.

**Done means:** an approved employee can be provisioned without a second account system;
history and retry behavior are verified. Live SMS completion still uses the selected provider.

## 5. Parallel Track for Backend Developer 2

Developer 2 does not need to wait for every HR endpoint before starting independent
reference services. Shared identifiers, permissions and migration ownership must be agreed first.

| Order | Complete module | Required dependency / evidence |
|---|---|---|
| A | Sites and geofence versions | SRS 6.1, ATT-005/006; configurable approved site values |
| B | Shifts and rosters, consuming Developer 1's holiday calendars | SRS 6.2, SHIFT-001 to SHIFT-004; effective dates and cross-midnight support |
| C | Shift assignments and consumption of employee/site assignments | Stable approved employee references; Developer 1 owns employment/site-assignment history |
| D | Face enrollment and Check-In proof | FACE-001 to FACE-008; approved provider, liveness/threshold/retention decisions |
| E | Check In/Out, raw punches, work sessions and daily summaries | A-D, authenticated employee, idempotency, server timestamps and immutable raw evidence |
| F | Corrections, overtime and month locking | Approved attendance and calculation/approval policies |
| G | Operational zones and site reports | SRS 6.3/7.1/11; separate from punches, restricted reasons protected |

**Important:** Face verification is Check In only. Zones never silently alter punches/pay.
Do not implement attendance hardware or an external messaging attendance workflow.
Offline attendance/zone evidence must retain Pending/Unverified semantics until validated.
Start the provider/privacy decision and approved-face proof-of-concept alongside independent
master-data work. Do not leave provider selection until the day Check In is expected to ship.
Policy configuration/version resolution must precede payable-summary calculations: Developer 1
owns configuration/approval APIs, Developer 2 owns attendance rule evaluation. Raw evidence and
worked-time processing must not pretend that unresolved payable/KPI/OT formulas are finished.

## 6. Remaining HR and Attendance Delivery

| Order after dependencies | Module | Main owner | SRS reference |
|---|---|---|---|
| 1 | Leave, balances, evidence and approvals | Developer 1 | Section 9, LEAVE-001 to LEAVE-013 |
| 2 | Effective policy calculations and confirmed overtime | Developer 2 with HR policy input | Section 8, OT-001 to OT-005 |
| 3 | Meal allowance and claim lifecycle | Developer 1, using verified work sessions | Section 10.1, ALLOW-001 to ALLOW-004 |
| 4 | Vehicle custody, fuel, maintenance and insurance | Developer 1 | Section 10.2, section 15 |
| 5 | Advances, loans, salary/deductions self-service | Developer 1 | Sections 11, 12 and 14 |
| 6 | Locked payroll handoff | Both; one contract owner | Section 12, MD-014, AC-007 |
| 7 | Remaining reports/exports and notification APIs | Respective module owner | Sections 12/15, REP-001 to REP-003 |
| 8 | Full workflow acceptance and deployment checks | Both plus PM/HR | Sections 16/17 |

Audit, authorization, idempotency and required notifications belong inside each delivered
module; they are not postponed to the final row. In-app new-device notifications are already
queued by authentication, but notification inbox/delivery APIs belong to the notifications slice.
Existing SRS models do not mean these business services are already implemented.

### Business Acceptance Checkpoints

- [ ] First milestone: masters -> complete HR draft -> private documents -> Admin decision ->
  correct account/access provisioned, with no login for rejected/pending employees (AC-001).
- [ ] Attendance milestone: approved face enrollment -> Check In -> Check Out without face ->
  own/authorized attendance views; duplicate/poor-location/provider-failure paths verified
  (AC-002/005). Termination at the effective time blocks future punches (AC-009).
- [ ] Review milestone: missing/out-of-zone evidence -> authorized correction -> revised
  summary, preserving raw events; zones and site reports remain separate in-app workflows
  (AC-003/005A/005B).
- [ ] Calculation milestone: HR-approved office/site/overnight/leave/meal examples produce
  agreed results with the correct dated policy; no guessed financial consequences (AC-004).
- [ ] Handoff milestone: employee sees only own information; Accounts receives locked approved
  sources without punch-edit permission; real exports enforce field access and retain audits
  (AC-006/007/008/010).

## 7. Management Decisions to Resolve Before Affected Work

- Initial HR-to-Admin onboarding and current login/recovery rules are confirmed. Other
  sensitive-change/leave/overtime/correction approval matrices remain MD-001/005/011 work.
- Face provider, liveness, retries, accessibility exceptions and retention: MD-002/004/013.
- Site geofence, accuracy and operational zone rules: MD-003/003A.
- Cross-midnight worked examples, overtime formula, dinner amount and KPI/waiting rules:
  MD-006 through MD-010. Do not silently use mockup defaults.
- Effective early-reporting exceptions: MD-012.
- Payroll ownership, retention and production volume/availability: MD-014 through MD-016.
- Clarify the SRS file-import/external-device-sync remnants before implementing imports;
  they do not authorize attendance-machine integration.

Continue independent confirmed work while these decisions are pending. Do not implement
guessed financial effects, provider success or authorization matrices.

## 8. Working Rules for Both Developers

- Keep complete vertical modules under their existing Domain/Application/Infrastructure/API folders.
- Agree request/response contracts before independent implementation. Use the same employee ID.
- Reuse the existing authentication services; no separate HR/manager/mobile login database.
- Permission codes are introduced with real implemented operations. Fixed system-role names
  are not a bypass; review any new fixed-role grants explicitly.
- Coordinate shared DbContext/configuration/permission files. One migration coordinator
  prepares reviewed incremental migrations; applied/shared migration history is immutable.
- Do not recreate all models/tables. Inspect actual schema and add only approved missing changes.
- No repo test projects without PM request. Use focused offline/integration checks and preserve
  verification evidence without committing binaries, databases, real identities or credentials.
- Each module must pass validation, 401/403 boundaries, scoped access, retry/concurrency,
  audit/history checks and a Release build before it is called complete.
- The PM authorized committing/pushing the current backend baseline to develop on 7 October.
  This does not authorize frontend changes, production deployment, purchases or future pushes.

## 9. Separate Web and Mobile Delivery

This section plans later frontend work; it does not authorize frontend implementation now.
The PM owns React/React Native work. Confirm screens/fields/states and request/response
contracts per module, then integrate only that module's reviewed APIs when authorized.

| Milestone | React web | React Native employee app |
|---|---|---|
| Authentication | Email/password -> SMS OTP, permission-driven navigation, session/recovery screens | Phone -> SMS OTP, secure token/device storage, session and recovery states; no HR administration |
| Employee onboarding | Masters, employee form/list/detail, document states, Admin approval queue, role/web-access setup | Own approved profile, emergency/document summary and permitted change requests |
| Attendance | Sites, shifts/rosters, face enrollment, daily attendance and exceptions | Home/shift/site, camera/liveness Check In, location, face-free Check Out, own calendar and pending/retry states |
| Field controls | Zones/assignments, authorized incident and site-report review | Applicable zones, warnings/acknowledgements/exception requests, arrival/departure/delay/incident reporting |
| HR self-service | Leave, corrections, OT, claims and advance/loan review | Balances, requests, evidence, status/history, salary/deductions/allowances and repayment/payslip views where approved |
| Closure | Month lock, payroll handoff, scoped dashboards/reports/exports and audit review | Monthly approved summary, safe notifications and read state |

Permissions denied, loading, empty, validation, expired-session, unavailable-provider and
offline states are part of each delivered screen, not a final cosmetic task. Mobile offline
punch/zone/site-report support has explicit SRS Should priority and needs scoped acceptance;
it never displays server acceptance before synchronization. Check-In proof requirements remain.
Actual Android/iOS hardware, camera/location permission, secure storage, duty-window monitoring,
accessibility and browser cookie/CSRF acceptance are separate from backend-only tests.

## 10. PM Actions and First Developer Handoff

1. The PM approved sharing the current authentication baseline on develop on 7 October.
   Confirm the reported remote commit before handoff, then pull develop and create separate
   feature branches. Configure each workstation's private settings; local secrets are not shared.
2. Give Developer 1 the complete Organization Masters module first. Give Developer 2 Sites and
   Shifts first, agreeing shared city/employee IDs, holiday ownership and contract names together.
3. Assign one migration coordinator (Developer 2 under the earlier team arrangement). Both
   developers propose required model changes; the coordinator reviews/generates the incremental
   migration and snapshot. Do not regenerate all tables or let both independently rewrite history.
4. Confirm the complete employee form and submit-time required fields with HR, including how
   the first reporting-manager/root employee is represented. SRS lists a reporting manager as
   required; do not invent a circular first-manager chain or exception.
5. Assign owners/due dates for SMS, face/liveness, private storage/malware scanning, site/location,
   approval matrices and the unresolved calculations in section 19. Confirm approved examples.
6. Review one working module demo at a time against its acceptance checklist. Require the API
   contract, actual verification evidence and known blockers, not just screenshots/table counts.
7. Plan UI review and later client integration separately. Before go-live, complete real-provider,
   end-to-end, scoped access, load, environment separation, encryption/backup restore and UAT checks.

Until scope, decision/provider dates and task estimates are agreed, this is an ordered delivery
plan, not a promise of a whole-system completion date. AI assistance does not remove those gates.

## Immediate Handoff

Authentication implementation and the recorded bounded verification are complete for the current
approved backend slice; the deployment/acceptance gates above remain. Do not restart models or login.
The next implementation request should be **Organization Master Data APIs**,
followed by **Employee Draft, Submission and Admin Approval**.
Developer 2 can take **Sites and Shifts** in parallel with the agreed shared contracts.
This document plans those next modules; it does not claim they were implemented in this task.
