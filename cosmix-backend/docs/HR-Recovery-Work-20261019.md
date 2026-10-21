# HR Recovery Work Handoff - 2026-10-19

Scope: original HR SRS LEAVE-013, actual qualifying work against an existing approved leave recovery commitment. This is not salary recovery, extra/KPI benefit awarding, Accounts posting, settlement, payment release, or legal certification. Product/test sources remain frozen. No SQL connection, migration application, host, real provider, Git operation, or product build was performed by this worker.

## Policy Contract

Parent owns `LeavePolicySettings.RecoveryFulfilment` in `leave-settings-v4` (`SettingsVersion = 4`), its integration validation, and recursive duplicate-key rejection. The property requires `Recovery`; v1-v3 remain strict and cannot enable fulfilment. All fields below are required; unknown fields are rejected. Numerical limits are validation bounds, not company defaults.

| Field | Supported explicit configuration |
| --- | --- |
| `QualifyingWork` | `OutsideScheduledDuty = 1` |
| `CreditBasis` | `ExactWorkedMinutes = 1` |
| `ReviewAuthority` | `FixedAdmin = 1` |
| `AllowPartialFulfilment` | Explicit Boolean |
| `MinimumClaimMinutes` | 1..1440 |
| `MaximumClaimMinutes` | MinimumClaimMinutes..1440 |
| `MaximumClaimsPerCommitment` | 1..1000 |
| `SubmissionWindowDays` | 0..366 |
| `ParentChangeHandling` | `BlockClaimsRetainAllocations = 1` |
| `SourceChangeHandling` | `RejectChangedSourcesRetainAllocations = 1` |

Every enum also has `Pending = 0`: it can be represented in settings but cannot authorize work. Execution returns 409 `leave_recovery_fulfilment_policy_pending`. Absent configuration returns `leave_recovery_fulfilment_not_configured`; invalid values fail validation. No company settings, rates, conversion formulas, authority, or amounts were seeded or inferred.

The explicitly selected published policy must have verified provenance, `DirectHr` authority, no approval-request reference, eligible staff category/kind/treatment, and an effective interval containing the commitment date (`EffectiveFrom` inclusive, `EffectiveTo` exclusive). Publication cannot postdate the claimed work. An existing v3 commitment can use a separately selected v4 policy without rewriting its original approval. First submission pins the fulfilment version, including returned/rejected claims; changing policy cannot bypass limits. Submission-window age uses the Asia/Karachi business date.

## Workflow And Evidence

- Validate the original approved leave, frozen approval/commitment proof, policy and reservation chain. Cancellation pending/approved, supersession or changed parent proof blocks use. The original promise is immutable and is not labelled fulfilled by submission.
- Require canonical verified, closed actual sessions with exact employee/assignment/site/branch/shift/revision identity. Claimed UTC boundaries must be whole minutes, inside the closed session, and no later than server time.
- Require valid previous/current/next-day rosters, each no longer than 24 hours. Claimed work must not overlap any scheduled duty. Missing rosters, open/unverified sessions, changed sources, or a locked commitment date fail closed.
- Submission creates a pending approval. Only independent approved exact minutes count. Partial fulfilment requires explicit policy opt-in; claims cannot exceed remaining committed minutes. Derived progress is `commitment_only`, `partially_fulfilled`, or `fulfilled`.
- Current fixed Admin/SuperAdmin, web MFA, and existing `attendance.leaves.review` All scope are required for review. Both requester account and beneficiary employee are barred from self-review. Self routes derive identity from the session and use `attendance.leaves.self`.
- Existing `ApprovalRequest`, append-only `ApprovalDecision`, typed `AuditEvent`, protected JSON and idempotency receipts provide persistence. No new entity, table or migration. Request type: `leave-recovery-work`; audit target: `LeaveRecoveryWork`; events: `attendance.leave.recovery.work.submitted` and `.decided`.
- Private purpose: `hr:leave-recovery:{employee:N}:{claim:N}:{submission|decision}:v1`. Proof binds source fingerprint/ranges, actor/account/session, idempotency key and versioned decision. Missing/corrupt evidence is an error, never an empty allocation.
- Writes run in the existing authorized serializable transaction with approval rowversion checks. Retained keys prevent duplicate claims after short-lived receipts disappear. Exact replay creates no duplicate allocation, approval or notification.
- Final write/replay callbacks revalidate private source evidence and finish with current session checks; review callbacks also revalidate exact Admin authority after callback-owned evidence IO. Parent owns the generic `AttendanceOperation` final authentication hooks. Queue/Progress use `authority.ScopeAsync` and post-evidence role/session checks.

## Shared Allocation

`ILeaveRecoveryCreditReader.ReadReservedAsync` exposes verified approved recovery ranges across all business dates. Recovery checks existing approved OT/makeup/work-treatment ranges; OT and work-treatment submission/approval paths also consume recovery ranges. Physical UTC overlap is rejected in either creation order. The work-treatment reader excludes only the exact retained own-recovery range when validating that recovery claim again.

Missing recovery-reader wiring fails with 409 `leave_recovery_allocation_reader_unavailable`; optional constructor syntax is not permission to treat null as empty. Source correction or parent cancellation does not release approved allocations. No reversal/reallocation workflow is implemented. Pending claims do not fulfil a commitment; approval rechecks availability.

## Notifications

`LeaveRecoveryWorkService` requires existing `INotificationOutbox` as its final constructor parameter. No new template, shared dependency, worker or migration is required.

Submission selects distinct current active, web-enabled Admin/SuperAdmin accounts with a current fixed-role assignment, granted/nonrevoked leave-review permission and All scope on that assignment. Requester and beneficiary accounts are excluded. No recipient returns 409 `leave_recovery_review_recipient_unavailable`; over 256 returns `leave_recovery_review_recipient_limit`. The recipient set is rechecked after staging; drift aborts the transaction.

Both submission and decision stage the existing safe `NotificationTemplates.Decision` (`attendance.decision`) without private text/references. Submission uses claim ID as event/resource ID and notifies reviewers; decision uses immutable decision ID as event ID, claim ID as resource ID, and original requester as recipient. Staging failure aborts the approval/allocation/audit/receipt transaction. Exact retries do not duplicate intents. This verifies staging only, not delivery; no provider or notification host was run.

## API

All routes below have prefix `/api/v1/leaves`, require authentication and disable response caching.

| Method | Route |
| --- | --- |
| GET | `mine/{leaveId}/recovery-commitments/{commitmentId}/work-source?policyVersionId=...` |
| POST | `mine/{leaveId}/recovery-commitments/{commitmentId}/work-claims` |
| GET | `mine/recovery-work/{id}` |
| GET | `mine/{leaveId}/recovery-commitments/{commitmentId}/progress` |
| GET | `admin/recovery-work/queue` |
| GET | `admin/recovery-work/{employeeId}/{id}` |
| POST | `admin/recovery-work/{employeeId}/{id}/decisions` |
| GET | `admin/{leaveId}/recovery-commitments/{commitmentId}/progress` |

Both POSTs require `Idempotency-Key` and a reason. Submit body: `PolicyVersionId`, `ExpectedSourceFingerprint`, `Intervals` (`SessionId`, `BusinessDate`, `StartUtc`, `EndUtc`), `Reason`. Decision body: `ExpectedVersion`, `Decision` (Approved/Returned/Rejected), `Reason`. Original commitment routes are unchanged.

Technical bounds: 32 KiB POST body; 64 claim intervals; 512 canonical sessions; 1000 retained audit facts per employee/event family; 200 pending queue rows; 256 notification recipients; reason at most 1000 characters without control characters. History/queue overflow fails rather than silently truncating. Existing idempotency-key validation is retained.

## Changed Files And Wiring

New actual-work product files:

- `src/Cosmix.Application/HR/Leaves/LeaveRecoveryFulfilmentSettings.cs`
- `src/Cosmix.Application/HR/Leaves/LeaveRecoveryWorkContracts.cs`
- `src/Cosmix.Application/HR/Leaves/LeaveRecoveryWorkParent.cs`
- `src/Cosmix.Application/HR/Leaves/LeaveRecoveryWorkSources.cs`
- `src/Cosmix.Application/HR/Leaves/LeaveRecoveryWorkEvidence.cs`
- `src/Cosmix.Application/HR/Leaves/LeaveRecoveryWorkService.cs`
- `src/Cosmix.Infrastructure/Repositories/HR/LeaveRecoveryWorkRepository.cs`
- `src/Cosmix.Api/Controllers/HR/LeaveRecoveryWorkController.cs`

Coordinated existing allocation integration:

- `src/Cosmix.Application/Attendance/WorkTreatments/WorkTreatmentCreditReader.cs`
- `src/Cosmix.Application/Attendance/WorkTreatments/WorkTreatmentService.cs`
- `src/Cosmix.Application/Attendance/Overtime/OvertimeService.cs`

New suites: `docs/hr-srs-remediation/VerifyRecoveryWorkPolicy.cs` and `VerifyRecoveryWorkClaims.cs`. Earlier reservation-integrity work is separate: `LeaveRecoveryReservations.cs`, `LeaveRecoveryEvidence.cs`, recovery portions of `LeaveEncashmentService.cs`, and `docs/hr-srs-remediation/VerifyRecoveryFulfilment.cs`; promises alone still do not count as work.

Explicitly coordinated synthetic-empty recovery-reader fixture updates: `docs/attendance-completion/VerifyOvertimeService.cs`, `docs/hr-srs-remediation/VerifyHrReportOvertime.cs`, `docs/attendance-policies/VerifyDailyCalculation.cs`, and `docs/attendance-gap-c/C03C06VerifyWorkflow.cs`. Positive/overlap assertions were retained. After the first frozen05 failure, parent approved one further test-only change in `VerifyOvertimeService.cs`: wrap direct month-lock reader calls in the fixture transaction, assert the unwrapped call is rejected, and check transaction state restoration. The strict recovery-reader guard and all product sources were unchanged by this worker.

Parent completed scoped registrations for `LeaveRecoveryWorkService`, `LeaveRecoveryWorkSources`, `LeaveRecoveryWorkEvidence`, `ILeaveRecoveryWorkRepository -> LeaveRecoveryWorkRepository`, and `ILeaveRecoveryCreditReader -> LeaveRecoveryCreditReader`. Existing outbox resolves. Parent owns shared policy v4/Benefits, final authority hooks, DI, permissions and builds. No remaining registration/schema request.

## Verification And Blockers

Parent-reported frozen04: RecoveryWorkPolicy 2/2, RecoveryWorkClaims 79/79, DI 54 and OpenAPI 256 passed. Parent confirmed build06 PASS with zero warnings/errors. Parent's final 24-suite build06 run subsequently passed, exit 0, including expanded RecoveryWorkPolicy 46/46, RecoveryWorkClaims 79/79, authority-time 54/54 and ESS 38/38. Evidence: `D:\CosmixVerification\part-c-parent-20261013\managed-fa0e8bddaeb341d2a1239e699e0d8d8c`.

Worker-run final frozen06 shared regression run: all nine suites passed, 708 checks, runner exit code 0. The same nine also passed on frozen05 after the approved fixture correction. No product/test sources changed for the frozen06 run:

| Suite | Actual result |
| --- | --- |
| `C03C06VerifyWorkflow.cs` | PASS 53 checks |
| `VerifyOvertimeService.cs` | PASS 156 checks |
| `VerifyHrReportOvertime.cs` | PASS 12/12 |
| `VerifyAttendanceScopes.cs` | PASS 23 checks |
| `VerifyCaptureAuthorityTime.cs` | PASS 14 checks |
| `VerifyCaptureService.cs` | PASS 147 checks |
| `VerifyCorrectionService.cs` | PASS 196 checks |
| `VerifyKpiWorkflow.cs` | PASS 22 checks |
| `VerifyMonthLocks.cs` | PASS 85 checks |

Initial failure retained for traceability: the direct `IMonthLockOvertimeReader.ReadAsync` fixture call lacked its surrounding transaction and failed with `Recovery allocations require the authorized transaction`. The approved fixture-only wrapper resolved it; a negative check retains that rejection. Source-only verification confirmed `MonthLockService` preview/detail/finalize enter `AttendanceOperation`, whose authorized callback runs through `IAccessManagementRepository.InTransactionAsync`; `AccessManagementRepository` opens a serializable transaction. No product correction was required for this failure.

Exact invocation (existing runner compiles only standalone suites, not product projects):

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:\CosmixVerification\hr-backend-20261019\hr-benefits-build06\bin\Cosmix.Api\release' `
  -Sources @(
    'docs/attendance-gap-c/C03C06VerifyWorkflow.cs',
    'docs/attendance-completion/VerifyOvertimeService.cs',
    'docs/hr-srs-remediation/VerifyHrReportOvertime.cs',
    'docs/attendance-completion/VerifyAttendanceScopes.cs',
    'docs/attendance-gap-b/VerifyCaptureAuthorityTime.cs',
    'docs/attendance-capture/VerifyCaptureService.cs',
    'docs/attendance-corrections/VerifyCorrectionService.cs',
    'docs/attendance-gap-c/VerifyKpiWorkflow.cs',
    'docs/attendance-completion/VerifyMonthLocks.cs'
  )
```

Final frozen06 output: `D:\CosmixVerification\part-c-parent-20261013\managed-5471b2f1158d4aa19c303a3795ba2d56`. Passing frozen05 output: `D:\CosmixVerification\part-c-parent-20261013\managed-5b2ba11be37c4782aa04b30294d67cac`. The earlier failed run remains at `D:\CosmixVerification\part-c-parent-20261013\managed-cb574215604d4f868822941c7a3dd21b`. The runner verified frozen input/source hashes. The unfamiliar suites were inspected before execution; authority-time inspection uses query translation with a connection-forbidding interceptor. No SQL/host/provider execution occurred.

Read-expiry follow-up: parent added shared `ReadAsync` final `RequireCurrentAsync` after the result callback and before audit, plus exact Admin `RoleAsync` after recovery detail evidence IO. These placements were checked in source; the requested nine integration suites now pass on frozen06 containing those changes. Targeted new read-expiry regressions remain part of the parent's independent verification. No unresolved fixture failure remains in this nine-suite batch. Actual company policy approval/publication remains required. Standalone tests do not certify SQL transaction/concurrency behavior, runtime repository query execution, authenticated HTTP, encrypted-storage deployment, provider delivery, or legal/payroll readiness.
