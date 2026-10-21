# SRS data model coverage

## Salary Change Addendum - 7 October 2026

HR-004 salary changes, ROLE-004 salary isolation and APR-002/003 now have request/decision/current-as-of/history
services. Existing CompensationRecord, ApprovalRequest and ApprovalDecision store approved versions, attributable
decisions and protected snapshots; no entity/migration/table change is needed. Earlier salaries remain append-only.
The latest salary ID and employee/request rowversions protect stale proposals and decisions; the existing atomic
operation protects authorization, concurrent requests and retry receipts. Same-day replacement, backdating and
already-approved future replacement are rejected rather than rewriting evidence. Historical resolved intervals
respect finite recorded ends and never resurrect superseded salaries. See Employee-Compensation.md.

This is not a full configurable approval matrix, payroll calculation, bank amendment or general profile-edit feature.
Earlier addenda below describe their delivery-stage boundaries, not the latest salary feature status.

## Employee Updates Addendum - 8 October 2026

SRS 5.1, HR-004/HR-007 and the bounded MD-001 amendment workflow now have post-hire APIs.
The PM clarified basic details change directly; CNIC/phone and sensitive changes require Admin approval.
`EmployeeChange` adds one append-only `hr.EmployeeChanges` journal with employee/section/revision,
effective date, actor/time/reason, optional approval ID and encrypted before/after references. Unique
employee/section/revision and approval indexes plus restrictive FKs preserve history without another
employee/account/approval system. Revision ordering does not depend on timestamp resolution.

Basic edits update Employee/address/emergency records with this journal. Identity changes retain login
bindings and approved hiring evidence. Transfers close the prior EmployeeAssignment and add a successor
without changing staff category/type/site. Bank and medical use their existing immutable version records;
bank approval does not imply external verification. Document selections and identity evidence remain
permissioned, scanner-gated and traceable. See Employee-Updates.md and Employee-Updates-Verification.md.
Existing applied migrations are unchanged. No UI, seeded accounts or real personnel records are introduced.

## Employee Lifecycle Addendum - 7 October 2026

HR-005, HR-006, AUTH-007 and APR-002 now have the PM-approved HR-request/fixed-Admin-decision workflow
for suspension/resignation/termination. Existing ApprovalRequest/ApprovalDecision persist immutable snapshots
and attributable decisions; EmployeeStatusChange gains an optional unique approval reference with Restrict FK.
Legacy status rows remain unchanged. Employee.Status and account/session revocation are reconciled from approved
due changes; authentication independently reads effective status history. Requests/decisions use rowversion,
current web authorization, separate lifecycle permissions, encrypted evidence and audited retry receipts.
No new table is created by the eleventh migration `20261007164427_LinkEmployeeStatusApprovals`.

AC-009 punch endpoint acceptance remains untested/unimplemented until the attendance module exists; future
punch APIs must enforce the same employment boundary. Reactivation/rehire, retirement, future approval
withdrawal, automatic payroll/asset settlement and wider MD-001 amendment policy are not implied by this slice.

## Employee Approval Addendum - 7 October 2026

HR-002/HR-003/APR-002 now have real Admin decision orchestration after the earlier draft/submission slice.
Approve revalidates the frozen form, current masters and documents, persists the immutable decision, then
materializes existing address/emergency/employment/certification/custody/compensation/bank/medical records
as applicable. Sensitive payloads remain protected and bank details are not automatically verified.
The decision, related rows, dormant shared UserAccount, audit and retry receipt share one SQL transaction.
Return permits correction and a new version; rejection does not create an account. Historical decisions are retained.
Existing mobile, role/scope and web-credential services provide the explicit post-approval setup path.

`20261007151945_CompleteEmployeeHiringApproval` changes only EmployeeAssignments.BranchId to nullable,
matching Site Staff without an office. Office-required/Site-no-office rules are checked by application services.
The original development database now has ten applied migrations and still 124 business tables plus EF history.
Existing migrations were preserved. Down refuses null-office rows instead of inventing an invalid office identifier.
Admin site assignment/history APIs are date-based; they reject overlapping/backdated/same-start replacements.
Status/offboarding, later compensation amendments, roster/face/attendance and expiry notifications are not covered.
See Employee-Hiring.md and Employee-Hiring-Verification.md for the current delivery and verification boundary.

## Organization Masters Addendum - 7 October 2026

SRS 5/6.1 master references now have backend list/detail/create/update/status/history APIs:
Department, Designation, Branch (physical office), City, StaffCategory and EmploymentType.
They reuse existing tables, immutable codes, restrictive employee references and SQL rowversions.
New selectable staff codes are OFFICE/SITE per PM clarification; historical values are not rewritten.
Employee onboarding and Admin-owned site assignment/reassignment were outside this Organization slice;
their later delivery is described in the Employee Approval addendum above.

The confirmed continuation adds Site master APIs using the existing `attendance.Sites` table and
City/Project/Supervisor references. New selections validate active references and current employee
eligibility. Fixed Admin/SuperAdmin owns writes/history; explicitly permitted HR can read site metadata.
Existing audit/idempotency tables capture changes. No Site schema migration or geofence policy was added.
The later Employee Approval migration above reconciles BranchId with Site Staff submissions that have
no HR-selected office/site. Organization master APIs do not create an assignment or artificial office.

`HolidayCalendar` now has Revision, IsActive and SupersedesCalendarId. Existing rows remain revision 1;
revisions create new calendar/holiday rows for the same code and interval and do not change old dates.
Restrictive self-FK, unique successor and code/start/revision indexes prevent duplicate identities.
The service rejects overlaps for separate intervals and branches from outdated revisions. LEAVE-010
calendar publication is implemented; calendar applicability, leave calculation and file upload are not.

One append-only `OrganizationChange` table stores bounded before/after response JSON and its unique
restrictive AuditEvent reference. Shared AuditEvent and IdempotencyReceipt tables are reused. Actor,
time, reason, mutation and retry receipt commit atomically. No employee/credential payload is captured.
The eighth migration `20261007122140_AddOrganizationMastersAndHolidayRevisions` is applied to the
original development database: 123 business tables plus EF history, no actual owner or employee data.
See [API guide](Organization-Masters.md) and [verification](Organization-Masters-Verification.md).
The dated earlier inventory below remains historical, not the current table count.

## Recovery and First-Owner Addendum - 7 October 2026

The PM approved Admin-reviewed lost-phone recovery and delegated safe first-SuperAdmin initialization.
These are shared authentication changes, not completed HR CRUD or a public sign-up workflow.
`LoginPhoneChange` records previous binding, new number, reviewing account/assignment/system-role,
reason, approval reference, bounded validity/version and single completion/supersession evidence.
`LoginPhoneBinding.LoginPhoneChangeId` links recovered credentials to that same-account decision;
the original HR approval snapshot is never rewritten. A completed challenge is a same-account
restrictive FK with unique use; phone/recovery rows use rowversion and immutable-evidence guards.
No extra password table was added: existing OTP rows distinguish WebPasswordReset/PhoneRecovery.

Owner initialization explicitly distinguishes `OnboardingProvenance.OwnerBootstrap` from ordinary
HR submissions/decisions. External executor/approver/approval references replace otherwise invented
user IDs for this single first-owner record only. SQL CHECKs retain real-user actor requirements
for all ordinary HR records; matching provenance is part of the submission/decision FK. A unique
filtered index permits one owner submission. Existing records default to ordinary HR provenance.
The local guarded empty-database transaction is the only application path for this initialization.

Migration `20261007090713_AddAccountRecoveryAndOwnerInitialization` is applied and verified in the
original development database. Current mapped schema has 122 tables; actual data remains empty.
Model coverage is not a claim of full workflow completion, actual owner setup or real SMS acceptance.

## Web Authentication Addendum - 7 October 2026

Latest PM approvals require email/password then registered-phone SMS for every web role;
staff mobile uses SMS only, while fixed Admin/SuperAdmin are web-only. Admin controls initial
staff web credentials. `Role.SystemRole` identifies protected unique roles; `UserSession.SmsOtpChallengeId`
references a same-account single-use WebLogin proof. Existing OTP storage supports purpose-bound
MobileLogin and WebLogin. Approved phone selection is independent of mobile entitlement, allowing
web-only accounts without enabling the employee app. Initial credential setup reuses existing
UserAccount hash/email/status fields and adds no table. Original SRS and historical model inventory
below are unchanged. No data is automatically promoted into system roles or marked phone-verified.

The existing development database was previously verified through `20261006144249_AddSharedMobileAuthentication`
(five migrations, 121 business tables). The additive migration
`20261007080926_AddWebSmsAuthenticationAndProtectedRoles` is prepared **but not applied**.
See [Shared Authentication](Shared-Authentication.md) for actual feature coverage and remaining gates.

## Shared Authentication Addendum - 6 October 2026

The original model inventory below describes its reviewed baseline. The new authentication
implementation adds four mapped entities: LoginPhoneBinding, MobileOtpChallenge, OtpDelivery
and AuthRateBucket. Explicit mobile phone selection is captured on Employee/EmployeeSubmission;
UserAccount stores separate mobile/web entitlements and verified-phone evidence. No old account
is automatically mobile-enabled or phone-verified. This is the CEO-approved mobile OTP change,
not a change to attendance face verification. See [Shared Authentication](Shared-Authentication.md)
for AUTH/ROLE/MOB coverage, exact APIs, current limitations and HR-owner integration.

The new migration is prepared separately; this task does not apply it or modify a database.
The remaining inventory and historical verification statements below retain their original dates/scope.

Baseline: `requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx`, all tables,
workflows and acceptance criteria, plus the project manager's confirmed employee
approval/access flow. This document describes **model coverage**, not completed APIs
or acceptance tests. Department and the initial HR/attendance entities are reused.

Entities are under `src/Cosmix.Domain/{Authentication,HR,Attendance,Common}`.
Fluent mappings and the single `CosmixDbContext` are under
`src/Cosmix.Infrastructure/Database`. The four-project Clean Architecture is unchanged.

There are **117 mapped entity classes**: 56 HR (including onboarding), 34 Attendance,
17 Authentication/scope resources and 10 Common. The initial expanded model added 95 entities
to the 17-entity foundation; the 6 October review adds `PolicyAssignmentEnd`,
`InsurancePolicy` and `InsuranceExpiryReminder`.
The corrective review of commit `30aada1` adds `AllowanceClaimSubmission` and
`ShiftPopulationAssignmentEnd`; membership termination uses fields on existing records.
Enums, input records and validation helpers are not included
in that count. In Visual Studio, open `Cosmix.Backend.sln`, then expand `Cosmix.Domain`
and the relevant feature folder; there is no separate universal `Models` project.

## Requirements to persisted data

| SRS reference | Entities / retained fields and relationships | Implementation boundary |
|---|---|---|
| ROLE-001–004; AUTH-001–008; MOB-006–008 | `UserAccount` links one Employee to the shared web/mobile identity. `Role`, `Permission`, `RolePermission`, `UserRoleAssignment`, `UserAccessScope` keep designation, permissions and employee/department/team/site/project scope separate. `Team`, `TeamMember`, `Project`, `ProjectMember` identify effective scope populations. | No role-name authorization, seeded administrator or login endpoint. Permission catalog, delegation checks and scoped queries must be implemented server-side. |
| AUTH-002–007; SEC-004–006 | `RegisteredDevice`, `UserSession`, `RefreshToken`, `PasswordResetChallenge`, `MfaCredential`, `MfaChallenge`: expiry, revocation, rotation/version, protected credential references/digests; account lockout/MFA state. | Models do not issue/verify tokens, hash passwords, deliver OTPs or enforce rate limiting. Use an approved password hasher and trusted provider implementations. |
| HR-001–003; AC-001; latest confirmed employee flow | `Employee` starts Draft. `EmployeeSubmission` stores employee/version/draft revision and immutable private snapshot object version/hash/key reference. `EmployeeOnboardingDecision` stores reviewer, role, decision, reason and time. `AccountSetupInvitation` is expiring/single-use with delivery state separate from approval and account activation. | HR submits; Admin reviews and assigns supported permissions/scope. Return/reject require reasons. Complete form validation, snapshot creation and one-transaction orchestration remain Application work. |
| SRS 5.1; HR-004–007 | Employee identity/contact/SIM/joining/probation fields; `EmployeeAddress`, `EmployeeEmergencyContact`, `EmployeeAssignment`, `EmployeeStatusChange`; Department, Designation, Branch, City, StaffCategory, EmploymentType references. Server sequence generates EmployeeNumber; normalized identity/email uniqueness. | Identity, phone, controlled values and age validation require approved validation rules. Suspension/termination history is stored; blocking sessions/punches requires services. Referenced history uses restrictive deletion. |
| SRS 5.1; ROLE-004 | `CompensationRecord`, `EmployeeBankDetails`, `PreviousEmployment`, `EmployeeCertification`, `EmployeeAssetCustody`, `EmployeeMedicalRecord`, `EmployeeSimAssignment`: effective history, wage/payment basis, private sensitive references, expiry and issue/return evidence. | Salary, bank, medical and disciplinary authorization must be separate. Encryption/key management and private payload schemas are application/storage responsibilities. |
| HR-007–008; API-005; SEC-007,011 | `EmployeeDocument` retains private file metadata, document type, digest, issue/expiry, ordered scan evidence, scan-conflict state and manual verification bound to the exact scan. General `StoredFile` retains extension/MIME/signature/scan/retention/deletion metadata. | Document availability requires all `IsAvailable` predicates plus authorization; status alone is insufficient. Existing unbound verification requires a new review. Actual scanning/download providers remain future work; this correction did not alter general `StoredFile`. |
| SRS 6.1; SHIFT-001–004 | `Site` has city/client/project, supervisor, active dates; `SiteGeofenceVersion` retains coordinates/radius/accuracy. `Shift`, `ShiftVersion`, `EmployeeShiftAssignment`, `ShiftPopulationAssignment`, `TemporaryShiftChange` and `TemporaryShiftChangeEmployee` retain schedule, breaks, weekdays, effective dates and approval evidence. | `City` is a controlled master; optional CityId on branch/site/address preserves incomplete drafts and existing textual address evidence. No geofence radius, roster resolution precedence or policy threshold is seeded. |
| SRS 8; OFF-001–011; SITE-001–011; SHIFT-003–004 | `Policy`, immutable `PolicyVersion`, `PolicyAssignment`: effective scope, approval, document reference and versioned rules JSON. `PolicyAssignmentEnd` preserves approved withdrawal evidence without rewriting the original assignment. `PolicyImpactAssessment`, `WaitingTimeEvidence`, `DisciplinaryCase` retain calculated impacts/evidence and approval links. | A rules JSON schema version is persisted; the typed rule schemas/calculation engine and effective-end resolution are future work. No automatic salary deduction, KPI penalty or termination is implemented. |
| FACE-001–008; ATT-004; HR-008; NFR-005 | `FaceEnrollment`, `FaceVerificationAttempt`, `FaceVerificationException`: employee/device/enrollment/request binding, liveness/match outcome, provider/version, expiry/single use, revocation/reason and approved alternative evidence. | Check In only. Failure/unavailability cannot produce a successful assessment. Approved alternatives use human-approved correction records, not a face-bypass flag. No biometric SDK, thresholds, raw face capture storage or provider success stub. |
| ATT-001–009,011; MOB-001–005; AC-002–005,009 | `RawPunchEvent` is append-only received evidence, including offline/device/server times, request digest/idempotency, location/accuracy and optional file. `PunchAssessment` stores versioned server interpretation. `WorkSession`, `DailyAttendanceSummary`, `AttendanceSummarySession`, `AttendanceCalculationLine` retain pairing, policy/calculation versions and source breakdown. | Raw receipt is not proof of accepted attendance. Effective employment, assignment, device, location, duplicate tolerance and calculated pay checks still require transactional use cases. |
| SRS 7.3; ATT-008,010; AC-003,007 | `AttendanceException`, `AttendanceCorrection`, `AttendancePeriodLock`, `AttendancePeriodLockEvent`, `AttendancePeriodSummary`: reason, evidence, approvals, exact summary revisions and lock/reopen history. | Corrections do not edit raw punches. Services must enforce lock membership, authorized recalculation and post-payroll adjustments. |
| ZONE-001–010; MOB-010–013; SEC-008/008A/012; AC-005A | `ZoneType`, `OperationalZone`, `ZoneVersion`, `ZoneAssignment`, `ZoneExceptionRequest`, `DutyMonitoringWindow`, `ZoneEvent`, `ZoneEventResponse` preserve exact boundary/assignment versions and private restriction reasons. | Zones are separate from punches/payroll. Polygon validity, assignment precedence, duty-window enforcement, notifications and mobile monitoring remain workflow/provider work. |
| SRS 7.1; MOB-009,013; AC-005B | `SiteReport` and `SiteReportReview` retain arrival/departure/delay/access/incident, site/task, original device evidence, server receipt, location, attachments and idempotency. | In-app only. No WhatsApp or attendance-machine integration; no invented success response. |
| OT-001–005; SRS 8.3 | `OvertimeRequest`: employee/session, start/end/duration, site/job, requester, approval, evidence and policy/rate rule reference. | Eligibility, bulk membership, approved amount and travel exclusions are calculated by future services from approved rules. |
| LEAVE-001–013; SRS 9.1 | `LeaveType`, `LeaveLedgerEntry`, `LeaveRequest`, `LeaveCancellationRequest`, `LeaveEncashmentRequest`, `LeaveRecoveryCommitment`, `HolidayCalendar`, `Holiday`: policy/fiscal period, entitlement/reservation/usage/reversal, evidence, contact/handover/reliever, approval and recovery commitments. | Leave balance is a ledger calculation, not a second mutable balance table. Eligibility, holidays, benefit/deduction calculations and cancellation authority require approved policies. |
| ALLOW-001–004; SRS 10.1; confirmed Returned correction flow | `AllowanceClaim` retains one employee/kind/eligibility-cycle identity. Immutable `AllowanceClaimSubmission` stores each submitted amount, policy/calculation/evidence, payment dates, snapshot reference, correction reason/actor and its approval. `AllowanceSettlementRecord` binds payment evidence to the exact submission. | Returned claims resubmit on the same claim; rejected/approved/settled claims cannot use that path. Cycle uniqueness remains; one handoff and one paid record per claim, rowversion and staged approval decisions prevent duplicate local settlement. Actual external payment idempotency/approval authority remain service responsibilities. |
| VEH-001–009; SRS 10.2 | `Vehicle`, `VehicleCustody`, `EmployeeCommuteDistance`, `VehicleTrip`, `FuelClaim`, `VehicleServiceRecord`, `VehicleIncident` and `VehicleMileageEntry`: route/commute adjustment, liters/km, approved policy, workshop/odometer, issue/return condition, insurance/third-party/liability references. | Map evidence, approved mileage accumulation, service thresholds, expense exclusions and payment calculations remain policy/provider work. |
| SRS 15 insurance expiry/reminders; user's 6 October clarification | `InsurancePolicy` belongs to exactly one Employee or Vehicle, with provider/policy reference, inclusive coverage start/expiry, private file and immutable predecessor history. `InsuranceExpiryReminder` retains policy, recipient, channel, UTC due time, cancellation and optional queued Notification reference. | Both employee and vehicle coverage are supported. Lead times and recipients are explicit inputs; no fixed schedule or provider is assumed. Services must authorize access, suppress obsolete-policy reminders, enforce cross-record matching and dispatch after commit. No delivery worker is implemented. |
| SRS 11,12,18; MOB-007 | `AdvanceLoanRequest`, `AdvanceLoanAgreement`, `RepaymentScheduleEntry`, `RepaymentLedgerEntry`: requested/approved principal, currency, agreed installments, paid/reversed recovery and approval history. | Balances are calculated from the agreement and ledger. No loan rate or deduction authority is invented. |
| SRS 12,15; AC-007; MD-014 | `PayrollHandoffBatch`, `PayrollHandoffLine`: employee, period, approved/frozen source amount, policy, source type/ID/version, exact immutable lock event and trace; `PayrollHandoffProcessingRecord` records Accounts processing metadata. | Polymorphic source references must be validated by use cases. No bank disbursement, finance ledger or duplicate Employee/Payroll identity. |
| APR-001–005; AUTH-008; AC-010 | `ApprovalRequest`, immutable `ApprovalDecision`, `ApprovalRouteRule`, `ApprovalDelegation`, immutable `AuditEvent`; stage, scope/amount/duration/risk, actors/times/reasons and protected snapshots/change references. | Requester cannot decide own request. References alone do not prove permission or completed approval. Actual audit capture, route selection and sensitive before/after serialization are future work. |
| API-003–005; REP-001–003; SRS 3,12,15 | `IdempotencyReceipt`, `Notification`, `OutboxMessage`, `ReportExportJob`, `StoredFile`: request fingerprints, private payloads, safe template/channel metadata, dispatch leases/retries, export requester/filter scope/period/lock and file expiry. | Jobs/notifications dispatch only after commit when implemented. Reports/calendars/self-service views are queries/DTOs; no table per screen/report. Recheck authorization at export generation/download. |
| API-001,002,006; SEC-001–003,005,006,009,010; NFR-001–006 | Existing API foundation and the above persistence metadata support future workflows. | HTTP contracts, TLS/secrets, scoped queries, mobile secure storage, backups/restore, load testing and accessibility are not delivered by a data-model task. |

## Time, integrity and transaction conventions

- Instants use UTC-normalized `DateTimeOffset`; SQL uses `datetimeoffset`. Business dates use
  `DateOnly`/SQL `date`, local schedule times use `TimeOnly`/SQL `time`. Attendance services must
  derive business dates using Pakistan timezone rules (`Asia/Karachi`); device time is evidence.
- Effective `From`/`To` intervals are start-inclusive/end-exclusive unless a field explicitly
  represents an inclusive business end date (for example requested leave or payroll period).
  Selecting the applicable version and preventing conflicting date intervals require services.
- Money uses decimal with explicit precision and currency. GPS, distances and quantities also
  have explicit precision. Foreign keys use restrictive deletion; mutable records use rowversion
  where updates can conflict. Immutable evidence uses the shared append-only SaveChanges guard.
- Profile child writes require a tracked Employee and update its draft revision/rowversion.
  Pending forms cannot be changed, including during the transaction that decides the submission.
  Save draft changes before submitting its immutable snapshot. Snapshot storage must preserve
  the entire form and document object versions, not just the current Employee scalar fields.
- Save the approved PendingAccess account before its separate Admin access-assignment action;
  then save the assigned role/scopes before issuing its invitation. Role scopes and repayment
  schedules are saved with their new assignment/agreement. Summary calculation/source rows and
  lock membership are saved with their new immutable parent revision. Prepare and save payroll
  handoff lines before the separate lock action; tracked batch revisions prevent concurrent
  line additions from racing that lock.
- EF guards protect tracked SaveChanges; they do not secure direct SQL or bulk update/delete APIs.
  Repositories and database privileges must preserve these boundaries. A foreign key or role ID
  is never sufficient evidence of authorization, approval or matching employee ownership.
- Temporary shift affected-employee rows must be saved with the new approved change. Later
  population changes require a new change record and approval evidence.
- `ShiftPopulationAssignmentEnd` stores a single approved exclusive withdrawal end, reason,
  actor and timestamp without rewriting a group assignment. Roster resolution must account
  for that end; no precedence or retrospective attendance recalculation is implemented.
- Team/project `EffectiveTo` remains the original schedule. Audited `EndOn` records a separate
  one-time termination, rejects extensions/backdating/completed periods and requires a trusted
  server business date. Access queries must combine the original and termination dates as
  `IsEffectiveOn` does. Tracked writes cannot rewrite/delete this history.
- `ReplaceInitialAccess` recovers an invited account after validated access correction, revokes
  its old invitation and returns to ReadyForInvitation. Save that account and its tracked,
  revoked original invitation together; old tokens cannot activate. Approval remains separate.
- Save each allowance submission with its claim revision and approval. A returned correction
  supports amount, policy, calculation/evidence and payment dates; employee, kind, eligibility
  identity/window, currency and deployment/session source identity stay fixed. Each protected
  snapshot reference must identify immutable storage and cannot be reused for this claim.
  Settlement requires the current approved submission; save claim state and immutable settlement
  evidence atomically. If approval changes in that transaction, its immutable decision is required.
- Scan timestamps come from trusted providers. Stale scans do not mutate evidence; equal-time
  disagreements must persist the conflict flag. New scans require exact-scan verification.
  On a rowversion conflict, reload and re-evaluate the result instead of blindly retrying it.
- Bank, SIM and commute records have explicit revision/predecessor chains. Same-employee FKs
  and unique non-null predecessor indexes prevent cross-employee predecessors and forks.
  Services must enforce revision sequencing, resource identity and effective-date selection.
  Independent roots are allowed; no one-bank/SIM-per-employee rule has been assumed.
- Repayment reversals require the original ledger entry. Agreement/currency lineage is constrained;
  full/partial reversal eligibility and amounts still need transaction-level validation.
- Insurance policies are append-only, with exactly one owner and at most one successor.
  Same-owner successor chronology and reminder/notification recipient-channel matching are
  domain validations, not cross-row SQL constraints. Services must preserve these boundaries.
  Reminder cancellation and queueing are mutually exclusive and rowversion-protected.

## Open decisions and deliberate exclusions

The latest instruction resolves initial onboarding as HR -> Admin review -> Admin access assignment
-> secure setup invitation. MD-001 still needs the approval rules for later sensitive changes.
Other section 19 decisions remain open: face provider/liveness/enrollment/retry/retention
(MD-002/004/013); geofence and zone rules/authority (MD-003/003A); other approval matrices
(MD-005); extended-duty examples (MD-006); OT/dinner/KPI/waiting/leave rules
(MD-007–011); special employee policy assignments (MD-012); final payroll responsibilities
(MD-014); retention and load targets (MD-015/016). These are persisted as configurable
policy/evidence references where possible; no unapproved values are seeded.

The SRS file-import row conflicts with the clarified mobile-only channel. An attendance import
schema is deliberately not introduced pending PM clarification. Authentication provider and
initial administrator bootstrap also need a concrete approved implementation before login APIs.

The user resolved both migration blockers on 6 October: insurance expiry/reminders cover
employees and vehicles, and no database work/data exists yet. The new insurance models cover
that persisted information; the report and reminder delivery remain future implementation.

The three shared migrations through `20261006074233_CompleteReviewedHrAttendanceDataModel`
remain unchanged. New corrective migration `20261006105213_CorrectReviewedModelLifecycles`
adds two tables and sixteen columns; its snapshot is generated by EF. Its preflight stops for
legacy submitted allowance claims/settlements requiring an evidence-backed conversion.
Existing document verification evidence is retained but is unavailable until reverified against
the current scan. No guessed approval, payment, termination or scan-binding history is backfilled.
See [Migration Review](Migration-Review.md) for findings, explicit SQL ranges and application
prerequisites, and [SQL notes](sql/README.md) for the full-chain versus incremental scripts.
No database has been created, connected to or changed. No business controllers, services,
repositories, providers, mock data, test projects, commit or push are part of this correction.
The user's additional request adds Development-only Swagger UI at `/swagger`; it uses the
existing OpenAPI generator and bearer metadata. Application web/mobile UI remains unimplemented.
AC-001–010 are mapped above, **not passed end-to-end acceptance tests**.

## Verification

Corrective review verification on 6 October 2026, baseline `30aada1`:

- Release build passed with zero warnings/errors; 117 entities and 268 restrictive FKs.
- Existing integrated model/guard checks: 965 passed; previous focused regressions: 8 + 61 + 25 + 88 passed.
- New checks: document 68, membership 87, invitation 61, tracked-write guards 107,
  allowance/roster 65, plus both original roster/allowance reproductions now pass.
- Swagger HTTP checks: 22 passed across Development and Production. No database-backed endpoint invoked.
- New migration scripts match EF output; EF reports no pending model changes. All six prior
  migration/Designer files retain their hashes. See [Migration Review](Migration-Review.md)
  for migration inspection, explicit SQL ranges and remaining limitations.

Earlier review verification (the model at `30aada1`):

- Release build passed with zero warnings/errors.
- Integrated model/guard checks: **946 passed**, **115 entities**, **262 restrictive
  foreign keys**. Focused review regressions: **8 + 61 + 25 + 88 passed**. Final runs had
  zero database connection attempts; no SQL was executed.
- All four earlier migration/Designer files retain their original hashes. The snapshot was
  generated by EF. **4,213 offline migration checks** passed, including script/output identity,
  model/snapshot agreement, FK types/collations, key/index widths and additive forward changes.
  Independent Down dependency analysis preserves the original 17-table/23-FK graph.
  EF reports no pending model changes. No migration or SQL script was applied.
- These checks verify model construction and specific safeguards, not completed
  authorization, workflows, SQL Server persistence or end-to-end acceptance.

Original implementation verification on 05 October 2026 (before the review fixes):

- `dotnet build Cosmix.Backend.sln --configuration Release --no-restore
  -p:EnableSourceControlManagerQueries=false --verbosity minimal`: passed with zero warnings
  and zero errors. The process flag only disables Git metadata queries in this restricted host.
- Temporary ignored file-based verification (no test project): **925 checks passed**.
  SQL Server EF model construction found all 112 entities with explicit Fluent mappings,
  251 restrictive foreign keys, no shadow properties, bounded strings, decimal precision
  and rowversion metadata. SQL schema text was generated in memory, not executed.
- Focused checks covered draft/return/resubmit/approval, required reasons/self-approval,
  invitation delivery failure/expiry/replacement/reuse, account activation, pending profile
  child freeze, append-only writes, immutable summary/scope membership, locked payroll lines,
  file scan guards, idempotency, outbox leases, HR request/ledger/custody rules and face/zone/
  session/period history. A connection interceptor forbade database access; zero connection
  attempts occurred. These are model checks, not a delivered integration/UAT test suite.
- Existing migration and snapshot file counts/hashes are unchanged from task start.
  `git diff --check` passed; the existing develop checkout and earlier unrelated work remain.
  Application/API source and project references were not changed by this task.

Not verified: execution of SQL constraints/indexes against SQL Server, real concurrent
transactions, providers, endpoint authorization, policy calculations or end-to-end acceptance
criteria. Run database integration and approved business-case tests when those layers are
implemented and database access/application is separately authorized.
## Employee Hiring Intake Update - 7 October 2026

SRS 5.1 and HR-001/HR-002/HR-003 intake now use `EmployeeHiringDraft` alongside the existing Employee identity,
EmployeeSubmission, EmployeeDocument, AuditEvent and IdempotencyReceipt models. Nullable foreign keys hold proposed
department/designation/category/type/office/manager/shift. Separate encrypted profile/financial/medical section
references support incomplete drafts and preserve old values in encrypted audit evidence. These are proposals,
not effective assignments, compensation, verified bank records or asset custody. Finalization belongs to the
upcoming Admin decision/activation workflow. EmployeeDocument.Privacy isolates medical file metadata and downloads.
Existing append-only and pending-submission aggregate guards remain enforced. No old migration was rewritten.
See Employee-Hiring.md for implemented APIs, field boundaries and unresolved activation/policy work.
