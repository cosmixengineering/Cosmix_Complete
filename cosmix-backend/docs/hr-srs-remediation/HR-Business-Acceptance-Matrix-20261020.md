# HR business acceptance matrix - 20 October 2026

## Latest mobile-pilot continuation

See [fresh build09 results and remaining gates](HR-Mobile-Pilot-Readiness-20261020.md).
Historical vehicle successor replay is fixed; **1,749/1,749** affected standalone
checks passed. These do not replace the remaining authenticated benefit/recovery
and historical vehicle business tests.

The older fixed-role-only benefit provisioning blocker below is superseded:
an actual current fixed Admin can also hold the explicit All-scope benefit
permission through an approved custom role. No automatic grant is implied.
P1 evidence remains the authority for the bounded synthetic assignments.
Face was requested for a real-person pilot in this continuation, but neither
real-device acceptance nor production activation is established.

## Latest executed O1 update

This dated update supersedes the earlier source-audit statements below about
absent deployment grants and an empty deployment/claim population. P1/P1Retry01
provisioning and explicit Admin-plus-delegated-permission compatibility were
separately approved and verified; see
[P1 verification](HR-Permission-P1-Verification-20261020.md).

O1Continue01 reused nine exact retained synthetic sources with no reseeding or
employee change. **38 HTTP checks passed across 45 requests**, followed by
successful SELECT-only persisted-state reconciliation: one deployment, one
PKR 200 test claim, one submission, two independent Admin approvals, four
receipts, 32 audit events and two revoked local-MFA sessions. The original
runner's final audit-count timeout remains recorded; reconciliation did not
rerun the business flow. See [O1 execution and evidence](HR-Outstation-O1-20261020.md).

Only the single-policy/no-home-break lifecycle is covered. Benefit/recovery
work fulfilment, historical vehicle cases and additional outstation branches
remain distinct acceptance work. No migration, payment or real face/OTP/legal
activation occurred. The tables below retain their historical source-audit
context and are not a claim that every listed scenario has now passed.

## Parent live follow-up

After this source audit, the parent queried only permission codes and effective
synthetic-account grants on the verified C04 target. The actual catalog has no
`attendance.deployments.*` or `attendance.vehicle.policy.manage` entries, and
neither approved synthetic HR/Admin account has those grants. Thus the
unconfirmed-grant notes below are now resolved as **absent**, not a naming-only
search gap. No permissions or assignments were changed.

V1's asset/custody grants remain confirmed and avoid rate-policy publication.
The user approved the separate bounded V1 SQL batch: one new marked vehicle,
one custody issue/return, at most 100 HTTP requests, only the existing synthetic
HR/Admin identities, localhost:7481, and the new Huma20261020V1 private root.
The dedicated guarded host and runner executed once: 27 checks passed across
36 requests, then the final audit assertion stopped on an incorrect
caller-header correlation assumption. Separate post-stop read-only checks
verified exact business persistence and encrypted receipt/audit evidence.
Logout was not reached; V1 is not an overall pass. See
[the actual V1 record](HR-Vehicle-V1-20261020.md). The batch did not authorize
permission provisioning, policies, trips, fuel or migrations.

## Evidence and boundary

This is a source audit and bounded handoff, not an executed acceptance run.
Only this document was created. No SQL connection, host, credential read, build,
runner execution, code/configuration/migration edit or Git operation was performed.
Read: repository AGENTS, README, Architecture, Work-Ownership, SRS-Module-Map and
[latest HR continuation](HR-Continuation-20261020.md).

**Latest parent/user evidence supersedes the continuation's unavailable-target
entry:** SSMS located `Software-PC1\LOCALDB#9F3001F7`, PID 25768, with the same
database family, owner and files and 25 migrations. The approved C04 rebind and
preflight passed. C04 completed **28/28 checks, 35 requests**, with **155 protected
snapshots and 24 retained files unchanged**, host exited and no migration applied.
These are parent-reported results, not independently rechecked by this document's
author. C04 establishes its authentication/queue/department boundary only.

Parent-reported read-only inventory:

- Both synthetic HR and Admin have All scope for
  `attendance.vehicle.manage`, `attendance.vehicle.commute.manage`,
  `attendance.vehicle.custody.manage`, `attendance.vehicle.fuel.manage`,
  `attendance.vehicle.incident.manage`, `attendance.vehicle.maintenance.manage`,
  `attendance.vehicle.self` and `attendance.vehicle.trips.manage`.
- Both have `hr.reports.read`, `hr.reports.export`, `hr.reports.audit.read` and
  `hr.reports.disciplinary.read`.
- The live permission catalog lacks **`attendance.leaves.benefits.review`**.
  Its reported leave entries are `attendance.leaves.configure`,
  `attendance.leaves.review` and `attendance.leaves.self`. Catalog existence
  alone does not establish every account's effective assignment/scope.
- Reported relevant leave, recovery, deployment and vehicle business tables
  contain zero rows; `LeaveTypes` contains five. Thus no award, consumed benefit,
  recovery commitment, deployment or vehicle transaction fixture is established.
- No outstation-named permissions were reported. The actual source names are
  `attendance.deployments.read/request/decide`; their exact catalog/grant state
  must be checked by the parent, not inferred from a search for "outstation".

The parent owns SQL target handling, C04 results and the final build. No proposed
next batch below is authorized for execution by this document. Parent explicitly
requires the exact fixture manifest and provisioning review before new writes.

## Minimal next batch: one vehicle and custody lifecycle

Recommend a proposed **V1** batch, not benefit/recovery provisioning or a report
worker run. Existing confirmed grants cover V1. The inspected
[VehicleFuelService](../../src/Cosmix.Application/HR/VehicleFuel/VehicleFuelService.cs)
create/issue/return methods do not require a vehicle rate policy, approved trip,
KPI, biometric source or financial settlement. This accepts only asset/custody
persistence, replay and conflicts; it does not accept fuel, maintenance or
outstation outcomes.

### Exact fixture manifest to review before execution

| Item | Proposed bound / evidence required |
| --- | --- |
| Actors | Existing synthetic Admin as operator and existing synthetic HR as custody beneficiary/self reader. Bind their actual account and employee IDs in the private manifest. No new account, role, scope or permission. Current web MFA/CSRF and effective grants must still pass at execution. |
| Vehicle | Exactly one new marked vehicle: `registrationNumber=TEST-HR-V1-20261020`, `vehicleType=Motorcycle`, `makeModel=TEST ONLY custody acceptance`, `assetReference=TEST-HR-V1-20261020`. Require registration absent before any mutation; never reuse an earlier attempt silently. |
| Custody | Exactly one row linked to that returned vehicle ID and the existing HR employee ID. Freeze explicit ISO UTC `issuedAt` and `returnedAt`, with return later than issue and neither a guessed historical employment interval nor future event. Suggested synthetic odometers: 1000 and 1000, so this batch claims no travel. Confirm no active/overlapping custody immediately before issue. |
| Condition references | Freeze two clearly marked synthetic issue/return reference strings, each at most 500 characters. The inspected custody service stores these supplied references; it does not upload or verify a condition document. Do not claim attachment encryption/inspection from this test. Any real private condition-file fixture would need a separately enumerated file write. |
| Keys and scope | Three successful write keys, one each for `vehicle.create`, `vehicle.custody.issue`, `vehicle.custody.return`; additional conflict-probe keys explicitly listed. Bind all routes to the single vehicle/custody and existing employee IDs. Never broaden the C04 runner's department allowlist during execution. |
| Run bound | Proposed ceiling: two sessions, 100 total HTTP requests including bounded local OTP polling, at most 18 business requests, one create/issue/return lifecycle. No host restart, delivery worker, file damage or role mutation in V1. A new reviewed packet/profile is still required. |
| Storage | Separate owner-approved batch evidence directory and retained key references, preserving C01-C04. Enumerate audit/private-result writes as well as business rows. No cleanup or deletion of retained history. |

### Exact API sequence and write envelope

All paths are literal route patterns; replace IDs only from reviewed bindings or
successful responses. Browser writes require CSRF and `Idempotency-Key`.
Contracts:
[controller](../../src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs),
[DTOs](../../src/Cosmix.Application/HR/VehicleFuel/VehicleFuelContracts.cs).

| Step | Endpoint / permission | Expected evidence |
| --- | --- | --- |
| Pre-read | `GET /api/v1/hr/vehicles`; `GET /api/v1/hr/vehicles/custody/history?employeeId={employeeId}`. Respectively `attendance.vehicle.manage` and `.custody.manage`. | Registration absent and no conflicting custody, plus parent-owned SQL baseline. These are audited reads, not necessarily zero-write requests. |
| Create | `POST /api/v1/hr/vehicles`, `attendance.vehicle.manage`. | Missing-CSRF probe denied; two concurrent identical same-key requests resolve to the same vehicle; changed payload/same key conflicts. One vehicle row, not two. |
| Issue | `POST /api/v1/hr/vehicles/custody/issue`, `attendance.vehicle.custody.manage`; body `VehicleId, EmployeeId, IssuedAt, IssueOdometerKm, IssueConditionReference`. | One custody row; exact retry returns original result. A new-key competing issue of the same vehicle/employee conflicts without another custody. |
| Own read | `GET /api/v1/me/vehicles/custody`, `attendance.vehicle.self`, session-derived employee. | HR sees its fixture. Admin's self read must not disclose HR's custody. The Admin management read is allowed and is not a negative permission test. |
| Return | `POST /api/v1/hr/vehicles/custody/{id}/return`, `attendance.vehicle.custody.manage`; body `ReturnedAt, ReturnOdometerKm, ReturnConditionReference`. | One retained return; exact retry succeeds without another effect; new-key repeat conflicts with `custody_already_returned`. |
| Final read | `GET /api/v1/hr/vehicles/custody/history?vehicleId={vehicleId}&employeeId={employeeId}` and HR own read. | Same custody identity, issue preserved, exact return values, inactive custody. Vehicle remains a retained active asset; no delete/deactivate route is added to this batch. |

Allow only `hr.Vehicles` (one insert), `hr.VehicleCustodies` (one insert, one
return update), operation-linked `audit.AuditEvents`,
`workflow.IdempotencyReceipts`, protected audit/result objects and the enumerated
authentication/session/rate/local-OTP effects. Source shows no custody approval
or notification staging. Require three completed business receipts tied to the
three successful operations and inspect actual operation/audit links, rather
than assuming all reads/retries produce exactly three audit rows. Failed probes
must leave no committed extra business effect or completed business receipt.
All other business tables, historical rows and C01-C04 files must retain their
baseline hashes. Repeat preservation checks after the owned host exits.
These are future parent-owned SQL assertions; none ran here.

## Remaining workflow matrix

Notation: `L=/api/v1/leaves`, `V=/api/v1/hr/vehicles`,
`S=/api/v1/me/vehicles`, `D=/api/v1/hr/employees/{employeeId}/deployments`,
`R=/api/v1/hr/reports`. Each cell specifies the exact suffix and HTTP verb.
All mutation rows additionally need atomic audit/receipt/private-evidence
verification and real session/CSRF checks. Negative/concurrency cases apply only
to freshly owned fixtures, never by damaging historical evidence.

| Workflow | Exact endpoints and authority | Minimum synthetic source / fixture | Business writes and next bounded assertion |
| --- | --- | --- | --- |
| B1 Benefit award | `POST L/admin/employees/{employeeId}/benefits`; `GET` same path with `yearFrom`; `GET L/mine/benefits?yearFrom=...`. Management requires `attendance.leaves.benefits.review` with All scope **on the fixed ADMIN assignment itself**, current web MFA, beneficiary different from reviewer. Self read uses `attendance.leaves.self`. | One eligible employee, existing leave type, applicable published `leave-settings-v4` with explicit benefit minutes/threshold/service/target-year/encashment settings; completed KPI year, exact finalization ID, current matching KPI sources and resolved disputes. Current target leave year and unlocked award date. No such complete fixture established; missing permission blocks live award. | Append `hr.LeaveLedgerEntries` Entitlement and protected award fact; conditional decision-notification outbox intent if active recipient exists. Test one award, exact replay, changed payload and second-key same employee/KPI-year duplicate. No KPI fabrication or past-period rewrite. |
| B2 Benefit use | `POST L/mine`; `POST L/admin/{id}/decisions`; `GET L/mine/balance?leaveTypeId=...&policyVersionId=...&asOf=...`; optional `POST L/mine/preview`. Self permission `attendance.leaves.self`; independent fixed ADMIN/SUPERADMIN review with `attendance.leaves.review` All scope. | B1 award plus dated employment/roster/holiday/policy evidence and exact whole-minute leave interval. Select a policy-enabled paid type requiring no attachment for the first fixture; otherwise real scanner/private attachment prerequisites expand scope. Select a quota case where the award changes eligibility, without assuming a per-award spending order. | `hr.LeaveRequests`, `hr.LeaveRequestDays`, generic approval/decision, utilization ledger on approval. Verify one exact approval and balance before/after; pending/rejected leave does not prove consumption. Cancellation is a separate extension using `POST L/mine/{id}/cancellations` and `POST L/admin/{id}/cancellations/{cancellationId}/decisions`, with retained Release entries. |
| B3 Consumed correction | `GET L/admin/employees/{employeeId}/benefits/{id}/correction-impact`; `POST .../{id}/correct`. Same B1 fixed-ADMIN benefit authority. | Retained award fingerprint from benefit view; fresh usage fingerprint/ledger IDs from impact; explicit consumed allocations, compatible units and no unresolved reservation. Award-to-current correction interval unlocked. | One append-only Adjustment of **minus full award minutes**, original award retained, protected correction evidence, conditional notification intent. Derived consumed-only protection is not a new spendable ledger award. Test one partial-consumption correction, exact replay, stale fingerprint rejection and preserved utilization. |
| B4 Waiver / unused revocation | `POST L/admin/employees/{employeeId}/benefits/{id}/correction-resolution`; separate unused branch `POST .../{id}/revoke`. Same B1 authority. | Waiver: B3 plus effective published policy explicitly enabling `ConsumedCorrectionResolution.AllowAdminWaiver` and authority reference; exact correction/usage fingerprints and resolution version from impact, `Decision=Waive`. Revocation: different unused award, or verified full releases, exact award fingerprint and unlocked/unexpired period. | Waiver appends protected resolution audit fact and receipt, optionally notification; **no new ledger entitlement**, recovered work or money. Revocation appends immutable negative Adjustment. Do not combine both branches on one award or label waiver as recovery fulfilment. |
| F1 Recovery fulfilment | `POST L/{leaveId}/recovery-commitments` creates the parent promise; `GET L/mine/{leaveId}/recovery-commitments/{commitmentId}/work-source?policyVersionId=...`; `POST` same base `/work-claims`; `POST L/admin/recovery-work/{employeeId}/{id}/decisions`; `GET L/mine/{leaveId}/recovery-commitments/{commitmentId}/progress`. Self uses `attendance.leaves.self`; review uses fixed ADMIN/SUPERADMIN `attendance.leaves.review` All scope, independent of requester and beneficiary. No recovery-named permission is required. | Approved leave with exact immutable decision/commitment proof; separately selected, fully configured v4 fulfilment policy effective on commitment date and published before work. One verified closed canonical work interval, latest revision, whole minutes, matching employment/site/branch/shift, previous/current/next-day rosters, unlocked date, no OT/makeup allocation overlap. At least one eligible active web reviewer recipient is mandatory. | Parent stage inserts `hr.LeaveRecoveryCommitments`; work stage uses `workflow.ApprovalRequests/ApprovalDecisions`, protected audit facts and outbox intents. No separate recovery-work table or mutation of original promise. First test one claim with exact commitment minutes and independent approval; verify pending != fulfilled and replay does not double allocate. Cancellation/source-change/concurrent OT cases require separately bounded fixtures. |
| V2 Trip and first fuel claim | `POST S/trips`; `POST V/trips/{id}/approve`; `POST S/fuel-claims`; `POST V/fuel-claims/{id}/approve`; `GET V/trips` and `/fuel-claims`. Self `attendance.vehicle.self`; reviewers respectively `.trips.manage`, `.fuel.manage`, current web MFA and applicable configured reviewer authority. | V1-style vehicle/custody covering trip plus actual synthetic dated employment/commute/map/odometer evidence and approved policy/rates. Beneficiary/requester cannot review own claim. Start one single-day, single-policy trip; cross-day attribution is a separate case. Existing grants do not establish published policy/provenance. | `hr.VehicleTrips`, `hr.VehicleMileageEntries`, `hr.FuelClaims`, approvals/decisions and immutable calculation evidence. One approved mileage source and first fuel claim, replay and one-claim-per-trip protection. No reimbursement/posting. Zero current vehicle source rows makes this unsuitable as the very first tiny batch. |
| V3 Historical fuel / maintenance | `GET V/fuel-claims/{id}/historical-closure-context`; `POST .../historical-closure`; or `GET .../reconciliation-context`, `POST .../reconciliations`, `POST .../reconciliations/{requestId}/review`, `GET .../reconciliations`. Fuel authority `.fuel.manage` All and independent policy-authorized reviewer. Maintenance: `POST S/maintenance`, `POST V/maintenance/{id}/approve`, `.self` / `.maintenance.manage`. | Historical fuel needs a retained schema-1 pending or approved claim, original protected values and approved trip attribution correction. Reconciliation reviewer differs from beneficiary, original claimant and proposer. Maintenance needs exact service-date mileage membership, threshold/workshop policy, predecessor chain and per-trip through-service-date quantities. No legacy fixture exists in the reported empty tables. | Closure rejects the pending approval with new decision/evidence, preserving old claim amounts and one-claim slot. Reconciliation uses separate generic approval/evidence, not a rewritten FuelClaim. Maintenance inserts `hr.VehicleServiceRecords` and approval evidence. First future test must choose **one** branch, not synthesize all history. Changed maintenance quantities and monthly/policy-transition reconciliation remain unsupported boundaries. |
| O1 Outstation | `POST D`; `POST D/{id}/decision`; `GET D/{id}/calculation`; `POST D/{id}/claims`; `POST D/{id}/claims/{claimId}/decision`; `GET D/{id}/claims/{claimId}/evidence`. Exact permissions `attendance.deployments.request`, `.read`, `.decide`; scoped web MFA, fixed ADMIN/SUPERADMIN independent decisions. Home-break extension: `POST D/{id}/home-breaks` and `POST D/{id}/home-breaks/{breakId}/decision`. | One retained employment assignment covering completed deployment, destination/category/UTC dates/written evidence, published dated allowance policy and assignments, explicit currency/rates/day-count/remainder rules, protected approval and meal-overlap checks. Start single policy, no home break. Cross-policy and returned resubmission require their own retained evidence. Deployment grants are unconfirmed. | `hr.OutstationDeployments`, approvals/decisions, then `hr.AllowanceClaims` and `hr.AllowanceClaimSubmissions`; deployment rowversion/finalized-claim link. Optional `hr.DeploymentHomeBreakRequests` only in a separate extension. Verify retained calculation and independent approval, `NotScheduled` financial state; no settlement/posting table writes. |
| R1 Report preview/page | `GET R/{reportType}`; `POST R/{reportType}/export-page`. Read requires `hr.reports.read`; page export adds `hr.reports.export`; both require source permission and current web MFA. For `fuel-and-mileage`, source permission is confirmed `attendance.vehicle.fuel.manage`; for `leave`, `attendance.leaves.review` still needs account/scope confirmation. | A selected employee/vehicle and explicit bounded date/filter with known expected source rows. Current empty vehicle rows support only an explicitly labelled empty-data smoke test. V1 custody alone does not supply fuel/mileage values. `types` is just a catalog, not data acceptance. | Protected filter/selection evidence plus `audit.AuditEvents`; no durable export job or business mutation. Source permission must cover current and historical scopes independently. This can precede R2, but does not accept workers or numeric report values. |
| R2 Durable report worker | `POST R/{reportType}/export`; `GET R/exports/{id}`; `GET R/exports/{id}/download`. Same read/export/source grants, owner binding and current web MFA. Worker is `HrReportExportHandler` through durable outbox processing, not a public worker endpoint. | One nonempty, exact-source report/format first (CSV); reviewed worker-capable profile, persisted owner/session, encrypted store and retained key, no unrelated eligible outbox work. `AttendanceJobs.Enabled` must be true. Later XLSX/PDF/restart/lease tests are separate batches. | `reports.ReportExportJobs`, `jobs.OutboxMessages`, `files.StoredFiles`, receipt/audit/private scope/output/manifest and report-ready notification intent; `communication.Notifications` only if notification delivery is explicitly included. Test same-key request, one completed output, exact count/hash, owner download; later two-worker lease/restart and scope-expiry failures. C04 cannot run this worker. |

## Concrete blockers and schema dependencies

| Blocker | Evidence and required owner action before a positive live case |
| --- | --- |
| **Benefit fixed-role provisioning integration** | Live `attendance.leaves.benefits.review` is absent. [LeaveBenefitService.RequireAdmin](../../src/Cosmix.Application/HR/Leaves/LeaveBenefitService.cs) requires that exact permission, All scope, current system-role assignment and role code `ADMIN` on the **same grant**. A separate custom role, `leaves.review`, or SUPERADMIN alone cannot satisfy it. [AccessManagementService.GrantRolePermissionsAsync](../../src/Cosmix.Application/Authentication/Access/AccessManagementService.cs) rejects system roles with `system_role_is_immutable`; assigned custom roles are also frozen. Therefore **do not recommend normal grant-via-API or direct SQL role edits**. An owner-reviewed deployment/provisioning mechanism for adding the new permission and its fixed-ADMIN association, preserving immutable-system-role constraints and audit/version authority, is a prerequisite. No mechanism is designed, implemented or approved here. B1-B4 remain blocked. |
| Empty retained business fixtures | Five LeaveTypes are not a published benefit/recovery policy, finalized KPI, consumed award, verified work, custody/trip or approved deployment. Approve an exact fixture manifest separately. Do not transplant in-memory verifier rows as if they carried valid persisted private evidence. |
| Recovery work proof | [WorkTreatmentRepository.SessionsAsync](../../src/Cosmix.Infrastructure/Repositories/Attendance/WorkTreatmentRepository.cs) derives Verified from accepted raw/assessment/face evidence or a verified approved correction with protected decision. It does not trust a caller's `Verified=true`. Prefer a separately reviewed supported correction-based synthetic fixture when real face is excluded; no raw punch rewriting, fake face success or new host is implied. |
| Vehicle policy and historical fixtures | V1 needs no rates. V2/V3 do. Confirm exact policy/version/grants before proposing publication; `attendance.vehicle.policy.manage` was not among the supplied confirmed grants. Current empty tables cannot prove legacy monetary integrity or service-chain acceptance. |
| Outstation permission name and deployment schema | Verify the actual `attendance.deployments.*` names, not "outstation". Migration `20261015144424_AddFinancialAdjustmentsAndOutstationDeployments` creates `hr.OutstationDeployments` and `hr.DeploymentHomeBreakRequests`, with approval/employment/claim foreign keys. Parent reports 25 migrations and an empty deployment table; this is a missing fixture/grant verification boundary, **not a missing database claim**. |
| Worker profile | [HrBusinessTestingGuard](../../src/Cosmix.Api/Development/HrBusinessTestingGuard.cs) rejects enabled AttendanceJobs; [HrBusinessTestingHost](../../src/Cosmix.Api/Development/HrBusinessTestingHost.cs) restricts hosted services. Export request itself returns `503 hr_report_export_worker_not_enabled` when disabled. A separately reviewed bounded worker packet/composition is needed; do not toggle C04 settings or let a global worker consume unrelated jobs. |
| Evidence availability | Every prepared fixture must include resolvable protected references, purposes, key references and matching retained hashes/actor/source IDs. A valid row with unreadable evidence is not acceptance-ready. SQL/private IO/restart/rollback guarantees remain untested for the matrix workflows. |

Concrete physical dependencies, for parent verification without guessing new tables:

| Area | Required existing schema; source migration evidence |
| --- | --- |
| Benefits / recovery | `hr.LeaveLedgerEntries`, `LeaveRecoveryCommitments`, `LeaveRequests`, `LeaveTypes`, `Policies`, `PolicyVersions`, assignments; `workflow.ApprovalRequests/ApprovalDecisions/IdempotencyReceipts`; `audit.AuditEvents`. Migration `20261006074233_CompleteReviewedHrAttendanceDataModel` includes ledger/commitment tables. `20261012125614_AddDirectPoliciesAndAdminLeave` supplies direct policy authority, leave policy linkage and `hr.LeaveRequestDays`. Ledger uniqueness `(EmployeeId,IdempotencyKey)` and reversal foreign keys matter to duplicate/release tests. Benefit/waiver/recovery-work evidence reuses these structures; absence of a dedicated Benefits or RecoveryWork table is **not** a demonstrated schema defect. |
| Verified recovery sources | `attendance.WorkSessions`, `RawPunchEvents`, `PunchAssessments`, `FaceVerificationAttempts`, corrections/approvals, roster/shift history, locks and OT sources. In particular `20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims` adds WorkSessions `BranchId`, `EmployeeAssignmentId`, `HolidayCalendarId`, `OfficeGeofenceVersionId`, `OfficeRosterAssignmentId`; the current repository projects these even when a fixture is site-based. Table existence on an older baseline is insufficient without current columns. |
| Vehicles | `hr.Vehicles`, `VehicleCustodies`, `VehicleTrips`, `VehicleMileageEntries`, `VehicleServiceRecords`, `VehicleIncidents`, `FuelClaims`, `EmployeeCommuteDistances`, plus policies and generic approvals. Relevant creation definitions are retained in the initial reviewed model migrations, including `20261006074233_CompleteReviewedHrAttendanceDataModel`. V1 touches only the first two business tables. |
| Outstation | The migration above plus `hr.AllowanceClaims` and `AllowanceClaimSubmissions`; the submission table originates in `20261006105213_CorrectReviewedModelLifecycles`. Rowversions and retained submission chain must exist for returned-claim replay. |
| Reports | `reports.ReportExportJobs` including `SourceLockReference`, output/expiry/rowversion; `jobs.OutboxMessages` including lease token/expiry, attempts and unique `(MessageType,DeduplicationKey)`; `files.StoredFiles`, generic receipts/audit. Report/outbox definitions, including lease columns, already appear in `20261006074233_CompleteReviewedHrAttendanceDataModel`. Current EF configurations retain the lease/completion constraints. No new report-worker migration was established as missing by this audit. |

**No specific missing SQL table or column was independently demonstrated here.**
Source migration availability is not physical schema verification. Parent's
25-migration/table inventory is the live evidence available. The concrete known
gap is benefit permission provisioning; other requirements above distinguish
unverified grants, absent fixtures and restricted runtime composition from schema.

## Reusable runners and acceptance limits

| Existing runner / source | Reuse and limit |
| --- | --- |
| [Invoke-HrBusinessC04.ps1](Invoke-HrBusinessC04.ps1) | Parent reports PASS as above. Reuse its design for bounded login/CSRF, parallel requests, exact packet binding, independent SQL audit/receipt checks, protected-table/file hashes and post-host preservation. Its allowlist and successful-write assertions are department-specific. It is **not** a V1/B1/F1/O1/R2 runner; do not extend the running/retained packet. |
| [Invoke-HrBusinessC03.ps1](Invoke-HrBusinessC03.ps1) | Historical failed business run: 21 requests, stopped at Admin queue assertion before department creation. Source pattern only, not passing business evidence. |
| [Invoke-HrAuthOnlyC02.ps1](Invoke-HrAuthOnlyC02.ps1) | Reusable authentication-only pattern; cannot authorize or verify a business fixture. |
| [Invoke-HrAcceptanceC01.ps1](Invoke-HrAcceptanceC01.ps1) | Older pinned target and department mutation envelope. Not a safe generic resume or replacement for current C04/V1. |
| [VerifyManaged.ps1](../attendance-gap-c/VerifyManaged.ps1) | Standalone suites against frozen assemblies; useful for `VerifyLeaveBenefitWorkflow`, `VerifyLeaveBenefitKpi`, `VerifyLeaveBenefitUsage`, `VerifyRecoveryWorkClaims`, `VerifyRecoveryFulfilment`, `VerifyVehicleRepository`, `VerifyVehicleDatedAttribution`, `VerifyVehicleFuelReconciliation`, `VerifyOutstationAllowances`, `VerifyHrDurableExports` (all `.cs` under this directory). Compilation/synthetic collaborators/query translation are not actual SQL. None were run for this document. |
| [VerifyG3Scoped.ps1](VerifyG3Scoped.ps1), [VerifyHrDurableScoped.ps1](VerifyHrDurableScoped.ps1) | Standalone source-overlay tools with generated output/copies; not live acceptance runners or evidence of the final parent build. No execution is needed to use this matrix. |
| [Encrypted export verification](HR-Encrypted-Export-Acceptance-20261019.md) | Recorded 401 passing checks use production encrypted file IO and an independent reader process, but synthetic repositories/transactions/auth state. Not SQL worker restart or concurrency acceptance. |

The reviewed HR PowerShell runners contain no dedicated live benefit,
recovery-work, vehicle/fuel, deployment or HR report-export sequence. Their offline
runner checks establish runner structure only. The latest continuation's build06
1,247/1,247 result is offline and does not transfer to these live workflows or to
the parent's currently building output.

Implementation anchors for the matrix:

- Benefits: [controller](../../src/Cosmix.Api/Controllers/HR/LeaveBenefitsController.cs),
  [KPI reader](../../src/Cosmix.Application/HR/Leaves/LeaveBenefitKpiReader.cs),
  [correction](../../src/Cosmix.Application/HR/Leaves/LeaveBenefitCorrectionService.cs),
  [resolution](../../src/Cosmix.Application/HR/Leaves/LeaveBenefitResolutionService.cs),
  [repository](../../src/Cosmix.Infrastructure/Repositories/HR/LeaveBenefitRepository.cs).
- Use/recovery: [leave controller](../../src/Cosmix.Api/Controllers/HR/LeavesController.cs),
  [work controller](../../src/Cosmix.Api/Controllers/HR/LeaveRecoveryWorkController.cs),
  [source validation](../../src/Cosmix.Application/HR/Leaves/LeaveRecoveryWorkSources.cs),
  [repository](../../src/Cosmix.Infrastructure/Repositories/HR/LeaveRecoveryWorkRepository.cs),
  [work contract](../HR-Recovery-Work-20261019.md).
- Vehicle/outstation: [vehicle controller](../../src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs),
  [fuel reconciliation](../../src/Cosmix.Application/HR/VehicleFuel/VehicleFuelReconciliation.cs),
  [deployment controller](../../src/Cosmix.Api/Controllers/HR/DeploymentsController.cs),
  [deployment service](../../src/Cosmix.Application/HR/Allowances/DeploymentService.cs),
  [claim service](../../src/Cosmix.Application/HR/Allowances/DeploymentClaims.cs).
- Reports: [controller](../../src/Cosmix.Api/Controllers/HR/HrReportsController.cs),
  [executable source-permission matrix](../../src/Cosmix.Application/HR/Reports/HrReportAuthorization.cs),
  [export/worker implementation](../../src/Cosmix.Application/HR/Reports/HrReportExportService.cs).

## Handoff decision

Review only V1's exact private manifest and bounded runner/profile next. Its
confirmed grants avoid the benefit fixed-role provisioning blocker and its empty
source requirement fits the reported database. Stop if account/employee binding,
custody overlap, target/schema, grants, storage or preservation differs; do not
auto-provision or repair. A V1 pass would authorize a claim of tested
asset/custody behavior only. Benefits, actual recovery work, fuel/maintenance,
outstation and report workers retain the separate prerequisites above.
