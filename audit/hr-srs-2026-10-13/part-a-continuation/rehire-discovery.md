# Rehire discovery and safe implementation boundary

Prepared 2026-10-13 from a read-only review of the nested `cosmix-backend` checkout. No source, model, migration, database, provider or Git operation was performed. Part B owns the next EF migration window after frozen A23; this note does not allocate a migration number or authorize changes to B-owned files.

## Requirements and actual decisions

The extracted SRS requires an immutable system-generated employee identity (line 122), dated assignment/compensation/status evidence (137–147 and HR-004/005), archival rather than deletion (HR-006), and fresh authorized enrollment evidence (HR-008). It contains no explicit rehire chain or rules. MD-001 (line 525) still owns onboarding/change authority. MD-012 covers categories; MD-013/015 cover biometric/other retention; MD-014 covers financial responsibility.

`Employee-Lifecycle.md` and `EmployeeLifecyclePolicy.ValidateTransition` deliberately return `employee_rehire_requires_approved_employment_episode_policy` for Resigned/Terminated/Retired -> Active. The required next feature is a new approved employment episode, not reuse of the former hiring decision.

Management must supply the actual approved policy version, effective window and evidence, including:

- Whether Resigned, Terminated and Retired employees are rehire-eligible, including disciplinary/blacklist exceptions.
- Whether the approved mode is HR request -> different current fixed Admin/SuperAdmin with All scope, or another final chain. Implement the fixed independent Admin mode only when explicitly configured as approved; do not infer it from generic routing.
- New joining/effective instant, probation and same-day/backdated scheduling rules. Existing assignments and salaries are DateOnly while blocking status is UTC-instant based; partial-day and same-date returns need an explicit supported boundary.
- Required new starting salary/wage/payment terms, office/site/manager references, and document evidence. Decide whether current documents, certifications, bank and medical records may be explicitly selected again and whether fresh verification is required.
- Treatment of service tenure, leave entitlement/usage, OT eligibility, employee-specific policy assignments, team/project membership, assets/SIM and outstanding claims/debt/financial adjustments. Preserve all old records; do not invent a reset, carry-over, repayment cancellation or settlement.

Stable Employee.Id and EmployeeNumber are already required by the SRS and should be retained. The episode identifier is separate technical evidence, not a replacement employee number.

## Current implementation facts

- `Employee` holds lifetime identity, original JoinedOn/probation, original submitted version and `ApprovedOnboardingDecisionId`; `ApplyStatusChange` rejects terminal returns. Old submission and decisions are append-only.
- `UserAccount` is unique per employee (`AccountConfigurations.cs`, unique EmployeeId and WithOne). It stores the original onboarding decision plus current setup/activation/credential pointers. Authentication, provisioning and recovery require that original decision to equal the employee approval pointer.
- Terminal cleanup revokes current sessions, open MobileOtpChallenge, devices, roles and active face enrollment; channels are disabled. It does not revoke original phone bindings, setup invitations or unfinished phone recovery. Password/reset/MFA evidence and former activation metadata must also be inspected before a return.
- `EmployeeMobileProvisioningService` rejects fresh bindings if *any* binding history exists. It cannot serve a rehire by itself.
- Compensation assumes one employee-wide revision-one root, a predecessor chain, and salary-change request evidence. A rehire salary is not currently recognized as a new root/approval source. Old open salary may remain current indefinitely by date alone.
- Assignment queries often use EmployeeId and open/date windows alone. Offboarding does not close employment assignments; a new Active status would reactivate old office/site/category/manager evidence.
- Leave tenure and quota usage use lifetime JoinedOn and employee/year. Payroll and month coverage use original hiring evidence and one lifecycle stream. Old open duty sessions, employee-specific policies/OT/zone assignments and memberships also need an explicit return boundary.

## Recommended architecture

Keep the shared employee and one shared account. Add a small HR employment-episode aggregate and a separate rehire proposal/submission. Do not put the original employee into Draft/PendingApproval or overwrite original hiring pointers while a rehire is pending.

Suggested new domain types:

- `EmployeeEmploymentEpisode`: immutable EmployeeId, Id, sequence, predecessor episode, JoinedOn/probation, effective start instant, approved policy version/fingerprint, exact original-onboarding or rehire approval evidence. Exactly one origin kind; restrictive same-employee composite FKs; unique employee/sequence and predecessor successor. A separate append-only episode-end record binds terminal status-change/approval/time, preserving start evidence.
- `EmployeeRehireDraft`: employee and previous ended episode, rowversion/revision, private typed profile/financial/document references, selected new login phone/mobile request and joining/effective proposal. Incomplete drafts are allowed; no employment or account effect.
- `EmployeeRehireSubmission`: immutable per-request revision snapshot/hash/schema/object-version, current employee/source episode/status marker and required document versions, exact policy version/fingerprint and typed requested new employment terms.
- Use existing `ApprovalRequest` / `ApprovalDecision` for `EmployeeRehire`, not the existing `EmployeeStatusChange` request. Returned revisions create new submission/request evidence; cancellation/rejection never changes employment.

Introduce a module-specific employment reader with a pure resolver, not a universal repository framework. `ResolveAt(employeeId, instant/date)` returns the exact episode, join boundary, approval evidence, episode end and lifecycle evidence or an explicit ambiguity/readiness failure. A compatibility branch can read original employment only when there is no rehire episode evidence and exact original approval/status evidence is valid. Once episode evidence exists, never fall back to employee cached status or original approval to authorize a later episode.

Keep original Employee.JoinedOn and onboarding pointers as historical facts. Expose `firstJoinedOn`, `currentEmploymentEpisodeId`, `currentJoinedOn` and current probation separately. Current cached Employee.Status remains a projection of the due episode and its status stream. New `BeginApprovedEmploymentEpisode` is a separate domain operation; do not widen `ApplyStatusChange` to permit a generic terminal -> Active.

Assignment, compensation and status records must have same-employee episode ownership for new records. Dated read windows must be clipped to episode start/end, with gaps unavailable. Salary successors are confined to their episode; each episode starts a separately approved root. Historical initial rows must be associated only through exact reconciled evidence; ambiguous legacy records remain unavailable for rehire rather than guessed into an episode. Preserve old append-only values and ciphertext. Migration metadata association is separate from changing a business value.

## Concrete API/service contracts

Place `EmployeeRehireContracts.cs`, `EmployeeRehirePolicy.cs`, `EmployeeRehireService.cs`, `EmployeeRehireMaterializer.cs` under Application/HR/Employees; add `IEmployeeEmploymentRepository` plus `EmployeeEmploymentContextReader`. Add Infrastructure/Repositories/HR/EmployeeRehireRepository.cs, mappings, a write guard and a small HR controller. Reuse EmployeeOperation's current web SMS MFA, serializable authorization, protected audit and actor/action/key/payload-bound receipt transaction.

Suggested routes under `/api/v1/hr/employees/{id}/rehire`:

- draft read/write and separately permissioned financial/document selection;
- POST submissions: expected employee and draft versions, required reason and Idempotency-Key;
- GET requests/list/detail and immutable submission/decision history;
- POST requests/{requestId}/decisions: expected employee/request versions, Approve/Return/Reject and reason;
- POST requests/{requestId}/cancel: current request version and requester authority;
- GET employment-episodes and episode detail, showing approval/effective/access readiness distinctly.

Dedicated `hr.employees.rehire.read/request/review` permissions do not grant salary/bank/medical rights. Conditional section permissions must be rechecked before receipt replay as well as before execution. Reviewer must differ from requester and subject; exact current fixed system-role assignment must carry the review permission with All scope. Protected owner/system-role targets remain a separate handover.

Submission freezes the previous terminal change, source episode, current employee version, identity/reference/document revisions, exact policy, joining/probation/assignment/new salary and separately approved phone/mobile selection. Approval rechecks every source under the current transaction after private IO, including policy still effective at now and intended start, no conflicting future lifecycle/rehire change, current rowversions, exact required scanned/verified evidence and current master/manager/site eligibility. Source changes or policy changes require a fresh request. Fail closed on missing/multiple chains, policy, evidence, unreconciled legacy state or unsupported time boundary.

Approval stages the new immutable episode, new assignment and salary root, exact selected evidence, approved decision and account handoff together. Scheduled starts remain denied until an audited due activation commits its applied episode marker; worker delay never authorizes old access. Do not send external notifications before commit.

## Fresh access handoff

Retain the same UserAccount Id and its original onboarding association as historical evidence. Add current episode approval/setup association, and resolve eligibility from it in *all* auth paths. Before preparing a new episode, preserve former setup/activation metadata in an immutable protected handoff journal; do not archive password hashes or expose credentials in snapshots.

Revoke all old role assignments, sessions/refresh capability, devices, active phone binding, unconsumed setup invitations, unfinished phone recoveries, open OTP/MFA/reset proofs and logically active face enrollment. Record exact episode/reason IDs; retain original rows and biometric ciphertext. Invalidate SecurityVersion. Clear current initial role/scope/invitation and current credential/verification/activation fields through a dedicated guarded transition. Prepare PendingAccess with both channels off only when the old disable was the exact employee-lifecycle block for the ended episode. An independent suspension/disable/lock stays in force.

Fresh explicit Admin role/scope/channel approval, approved episode phone binding, new password if web is requested, real OTP verification, registered device and fresh authorized face enrollment are required. Never restore old grants, old verification date, old password, session/device/template or old enrollment exception. Rehire provisioning must distinguish current episode bindings from retained historical bindings. All access receipts and proof consumptions must bind the current episode and revalidate resource/episode authority on replay.

## Consumer changes required before active return

Paths below are existing consumers, not proposed features. Prefixes abbreviate `src/Cosmix.Application` (App) and `src/Cosmix.Infrastructure` (Infra).

| Area | Existing files needing episode boundary |
|---|---|
| Auth and role eligibility | Domain/Authentication/UserAccount.cs, Mobile/LoginPhoneBinding.cs, Mobile/LoginPhoneChange.cs, AccountSetupInvitation.cs; App/Authentication/{IAuthenticationRepository,MobileAuthenticationService,PhoneRecoveryService,EmployeeMobileProvisioningService,AccountSessionService,DeviceRecoveryService}.cs; App/Authentication/Web/{WebAuthenticationService,WebCredentialProvisioningService}.cs; App/Authentication/Access/AccessManagementService.cs; Infra/Authentication/{AuthenticationRepository,EmployeeMobileProvisioningRepository}.cs; Infra/Authentication/Access/{AccessManagementRepository,AccountAccessReader}.cs |
| Initial hiring and lifecycle | App/HR/Employees/{EmployeeApprovalService,EmployeeApprovalMaterializer,EmployeeApprovalPersistence,EmployeeLifecycleService,EmployeeLifecycleProcessor,EmployeeLifecycleContracts,EmployeeLifecyclePolicy}.cs; Infra/Repositories/HR/{EmployeeHiringRepository,EmployeeApprovalRepository,EmployeeLifecycleRepository}.cs; keep OwnerBootstrapService original provenance distinct |
| Assignment/salary/profile/records | App/HR/Compensation/{EmployeeCompensationService,EmployeeCompensationContracts}.cs; App/HR/Employees/{EmployeeSiteAssignmentService,EmployeeSiteAssignmentContracts,EmployeeUpdateService,EmployeeUpdateSections,EmployeeRecordService,EmployeeBankService,EmployeeDocumentService,EmployeeDocumentCompleteness}.cs; Infra/Repositories/HR/{EmployeeCompensationRepository,EmployeeSiteAssignmentRepository,EmployeeUpdateRepository,EmployeeBankRepository,EmployeeRecordRepository,EmployeeDocumentRepository}.cs |
| Attendance duty and historical eligibility | Infra/Repositories/Attendance/{AttendanceCaptureRepository,FaceRepository,RosterRepository,SiteRosterRepository,OfficeRosterRepository,ShiftRepository,SiteRepository,SiteGeofenceRepository,SiteReportRepository,AttendanceSummaryRepository,AttendanceCorrectionRepository,CorrectionHistoricalEligibility,CorrectionReviewRepository,AttendanceExceptionRepository,CarryForwardRepository,MissingCheckoutReminderRepository,ZoneRepository}.cs; App/Attendance/Punches/{AttendanceCaptureService,PunchCaptureRules}.cs; common attendance authorization and face/capture proof consumers |
| Policy, leave, OT and approval subjects | Infra/Repositories/HR/{AttendancePolicyRepository,LeaveRepository}.cs; App/HR/Leaves/{LeaveTreatmentRules,LeaveSelfReads,LeaveAttendanceContextReader,LeaveService}.cs; Infra/Repositories/Attendance/{OvertimeRepository,OvertimeEligibilityRepository,AttendanceLegalReviewRepository}.cs; App/Common/Approvals/ApprovalRoutingService.cs. Old employee-specific policy/OT/zone/team/project applicability must not silently cover the new episode |
| Financial evidence and period coverage | Infra/Repositories/Attendance/{AttendancePayrollInputRepository,MonthLockRepository,AttendancePayrollInputLedger,AttendancePayrollAttestationRepository}.cs; App/Attendance/MonthLocks/MonthLockCoverage.cs; App/Attendance/PayrollInputs/{AttendancePayrollInputService,AttendancePayrollInputProducer,AttendancePayrollReconciliationSourceReader,AttendanceMonthLockFinancialReader}.cs. Historical hiring/salary roots and each date's episode belong in immutable provenance; preserve existing locked/revision/attestation IDs |
| Shared persistence/client contracts | CosmixDbContext registration and tracked-write guards, Employee/Assignment/Compensation/Status/Account/phone/face mappings; DI, permission catalog, response/receipt result identifiers and controller contracts. Coordinate the EF snapshot/migration and B-owned readers after B's current window |

Old unresolved work sessions must remain historical facts and must never pair with a new episode's Check Out or open zone duty. An episode gap cannot become an attendance day because an old open assignment exists. History APIs can still span all episodes with explicit episode IDs and date provenance. Pending old amendments/claims must remain associated with their source episode; a later Active status must not make an old request applicable to the new episode.

## Honest minimum feature boundary

The smallest isolated deliverable is a policy-gated, protected, versioned rehire proposal and independent decision queue plus immutable episode evidence with `ApprovedAwaitingActivation`. It preserves terminal employment and denied access until the consumer and fresh-setup boundary is implemented. Call it rehire intake/approval preparation, not operational rehire completion.

A usable active rehire needs the episode-aware auth, salary/assignment, duty/historical readers and current-episode evidence above. Unknown tenure/balance/carry-over rules can remain explicit unavailable financial/leave dependencies, but cannot inherit or return zero silently. Even completed gated source is not actual management approval, SQL/provider/device acceptance, or production activation.

Use temporary standalone synthetic verification, not a new test project (AGENTS forbids recreation). Include two and three employment episodes, gap dates, initial-root versus rehire-root salary, open old assignment/session, original and rehire approval separation, all old proof replays, stale employee/request/draft versions, competing rehire/lifecycle decisions, policy changes/expired windows, independent account blocks, owner protection, failure rollback, delayed worker and history preservation. The later owner must run full Release build, runtime model/snapshot check and migration review; true SQL/auth/device/provider acceptance is separate.
