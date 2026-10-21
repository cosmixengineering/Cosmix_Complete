# Cosmix Backend

Shared authentication backend and HR, attendance and employee mobile foundation for Cosmix Engineering Pakistan.
Solution name: **Cosmix.Backend**. Stack: ASP.NET Core .NET 10, C#, EF Core Code First, SQL Server.

**Open Cosmix.Backend.sln in Visual Studio and set Cosmix.Api as the startup project.**
Read [Developer Setup](docs/Developer-Setup.md) before the first run.

## Application projects

| Project | Developer responsibility |
|---|---|
| src/Cosmix.Domain | HR and Attendance entities and pure business rules |
| src/Cosmix.Application | Feature services, DTOs, validation and repository interfaces |
| src/Cosmix.Infrastructure | SQL Server, EF mappings/migrations, repositories and provider adapters |
| src/Cosmix.Api | Controllers, HTTP/authentication wiring and startup |

The solution contains only these four application projects.
There is one deployable backend. React web and React Native mobile are separate clients.

## What works now

- The second local Attendance pass adds receipt-backed ordinary/late cross-month input reconciliation,
  transaction-local month financial dependency inspection, schema-2 exact revision membership and
  replay/source-race guards. Selected Development PoC storage now uses an approved-use gate and
  encrypted lifecycle journal. Full legal activation, financial carry-forward/OT settlement,
  physical biometric deletion and genuine authenticated SQL/provider/device acceptance remain
  incomplete. See [second-pass handoff](docs/attendance-completion/Continuation-Second-Pass-20261012.md).

- Face deployment continuation (20 October): CompreFace is deployed on the private Ubuntu server;
  reviewed ONNX models and the C# adapter have real inference/connectivity checks. The configured
  provider and protected capture uploads can run in every environment. The
  `Cosmix.Api.FaceVerification` launch profile uses private local settings without the isolated
  testing profile. Initial thresholds and encrypted storage are provisioned privately; genuine
  biometric/device acceptance and threshold calibration remain outstanding.
  Manual Accounts/Finance handoff/posting attestations and versioned legal-review evidence recording
  have separate permissioned APIs; neither releases salary, certifies law or makes payroll ready.
  Migration 21 is prepared, not applied. The actual local `Cosmix_Testing` baseline is **12**,
  not the source laptop's 17. See [this laptop's continuation](docs/attendance-completion/Laptop-Continuation-20261012.md).

- Configurable schema-3 normal-salary and late-deduction rules now prepare restricted, immutable
  monthly payroll-input revisions, with larger-only overlap handling and two-decimal deduction precision.
  SuperAdmin company/Head Office settings and dated site jurisdictions reuse existing masters.
  These are calculation inputs for review, not legal approval, payroll posting or payment release.
  See [payroll inputs](docs/attendance-completion/Payroll-Inputs-Implementation.md) and
  [company settings](docs/attendance-completion/Company-Settings.md).
- Direct HR attendance-policy publication and dated applicability, direct Admin leave review/cancellation,
  and verified daily time/classification now have local implementations. Published rules are immutable;
  leave retains exact shift-bound intervals, private evidence and balance history. No salary is stopped
  or compensation changed by these flows. Financial payroll readiness, face-provider integration and
  real authenticated acceptance remain separate. See the [policy/leave implementation](docs/attendance-completion/Policy-Leave-Implementation.md).
- Confirmed HR site-report review now supports Accept/Return/Reject and immutable employee
  responses. Shift forms also retain an optional missing-checkout reminder delay, with gated
  durable delivery. Attendance-policy settings have a strict validation API; the later publication
  continuation is recorded above. Local migration 18 is
  prepared, **not applied**. See the [workflow continuation](docs/attendance-completion/Approved-Workflow-Continuation.md).
- Further Attendance completion (12 October): adds scoped exception list/detail and own safe detail,
  management punch/session audit, a bounded factual dashboard and multi-employee daily register. Month preview now reports missing
  assignment as a readiness issue, and mobile action guidance respects locks/schedules. See the
  [continuation record](docs/attendance-completion/Continuation-20261012.md),
  [current verification](docs/attendance-completion/Verification.md), and
  [local review guide](docs/attendance-completion/Attendance-UAT-Guide.md).
- Initial Attendance continuation checkpoint (12 October): 107 additional registered operations cover face/capture
  contracts, enrollment outcome history, own punches/work sessions, factual daily/calendar revisions,
  self/supervisor corrections and HR review including documented manual duty,
  exception notices, OT requests/HR review/common and employee eligibility/confirmed formula, direct month locks, factual carry-forward, gated Operational/Red zones, site reports, in-app notifications and private factual CSV/XLSX/PDF jobs. Provider-dependent face success and policy-dependent
  pay results remain explicitly unavailable. This is local code with standalone and anonymous HTTP
  verification, **not completed real-employee attendance acceptance**. The approved isolated
  `Cosmix_Testing` database now has migrations 1–17; delivery jobs default to disabled.
  See the [pre-push record](docs/Pre-Push-Readiness.md), [requirement checklist](docs/attendance-completion/Implementation.md)
  and [executed checks and remaining work](docs/attendance-completion/Verification.md).

- Local site scheduling follows the user's site-only, direct HR change rule. Ten site routes,
  site-based My Shift, pinned informational holidays, temporary cancellation/supersession and protected evidence are implemented.
  The prior acceptance applied migrations 1–13 to the approved isolated `Cosmix_Testing` database; it did not
  upgrade production or the original development database. Real MFA/CSRF access, site scheduling,
  independent SQL/private evidence, storage failure checks and main restart replay passed.
  Transfer-boundary replay with fresh login, competing metadata writes, SuperAdmin-only run-grant
  revocation and denial of old scoped receipts also passed. See the verification record for remaining boundaries.
  Individual write routes now return 409; history is preserved. See [Site Rosters](docs/Site-Rosters.md).

- Local shift metadata, immutable schedules and retained individual history remain available.
  Effective roster and My Shift follow site scheduling; assigned-site version publication validates staff transfers and impacted scopes.
  Fixed Admin/SuperAdmin explicitly configure role permissions through the web APIs; only SuperAdmin changes protected administrator accounts.
  See [Shifts and Rosters](docs/Shifts-Rosters.md)
  and [access verification](docs/access-provisioning/Verification.md) before claiming usable deployment.
- Local Site Geofence configuration: six registered administration/self APIs and reusable boundary
  evaluation. Existing site/self permissions apply. Capture now uses the retained historical geofence;
  its new authenticated SQL acceptance remains pending. See [Site Geofences](docs/Site-Geofences.md).

- Post-hire basic edits save directly with history. Identity/contact, department/office/manager, bank,
  medical and document-selection changes use HR request -> different fixed Admin approval, protected
  before/after evidence and dated versions where applicable. Login phone/roles/site assignment retain
  their separate controlled flows. See [Employee Updates](docs/Employee-Updates.md).
- Post-hire salary-change requests, fixed Admin decisions, cancellation, dated salary reads and immutable
  compensation history use existing tables with separate salary permissions. No payroll calculation or bank
  amendment is included. See [Employee Compensation](docs/Employee-Compensation.md).
- Employee hiring draft/profile APIs, separately permissioned financial/medical proposals, private encrypted
  document upload/scan/verification/download, submission validation and immutable HR submissions to the Admin queue.
  Fixed Admin decisions now approve, return or reject the frozen submission. Approval materializes employment
  records and one dormant account atomically; explicit mobile/access/web-credential setup reuses shared authentication.
  See [Employee Hiring](docs/Employee-Hiring.md) for the complete API sequence and verification limits.
- Clean Architecture project references and module-specific repository pattern.
- Organization Masters APIs for departments, designations, physical offices/branches, cities,
  Office/Site staff categories and employment types: list/detail/create/update/status/history.
- Separate Site Master APIs: fixed Admin/SuperAdmin creates/edits/status/history; HR may receive
  read-only site access. Employee site assignment/reassignment and geofence configuration are
  documented in their later feature slices; this master alone does not accept attendance punches.
- Notification-backed holiday calendars with immutable replacement revisions, audit history,
  concurrency checks and retry-safe writes. See [Organization Masters](docs/Organization-Masters.md).
- JWT access-token validation, explicit reference permission and deny-by-default authorization.
- Request validation, ProblemDetails, correlation IDs, explicit CORS and health checks.
- Development Swagger UI at `/swagger`, OpenAPI, local launch profile, package lock files and build workflow.
- SRS-aligned feature folders, entity models and EF Core mappings.
  See [Initial Models](docs/Initial-Models.md) for the historical first model handover.
- Expanded SRS data models and EF mappings for accounts/access, employee approval,
  HR, attendance and shared records. See [Data Model Coverage](docs/Data-Model-Coverage.md).
  Reviewed migrations and SQL scripts are retained. Insurance expiry/reminder models cover employees and vehicles.
- Model review findings, verification and database application prerequisites are documented in
  [Migration Review](docs/Migration-Review.md).
- Corrective model fixes cover ordered document scans, scheduled membership termination,
  invited-account access recovery, group-shift withdrawal and returned allowance resubmission.
  Corrective changes use incremental migrations; earlier migration history remains unchanged.
- Shared mobile phone/SMS OTP implementation, passwordless activation, rotated sessions,
  current-state token validation, device/session management, dynamic access services and a configurable SMS gateway adapter.
  The feature is disabled until deployment prerequisites are met. Read
  [Shared Authentication](docs/Shared-Authentication.md) and its verification record before enabling it.
- Web email/password followed by registered-number SMS OTP, protected browser cookies/CSRF,
  fixed web-only Admin/SuperAdmin roles and Admin-controlled initial staff web credentials.
  Web/fixed-role and recovery migrations are applied to the original development database.
- Password reset/change, administrator-reviewed phone recovery and an owner-controlled local
  first-SuperAdmin setup command; no public setup route or default credentials.

**This is not the completed HR module.** Mobile login/token issuance is implemented but configuration-gated;
approved SMS delivery, operating settings and running first-admin setup with real owner evidence are still pending.
The PM has explicitly deferred actual owner creation. Remaining employee lifecycle extensions, face-provider
integration, general attendance/payroll policy integration and complete production acceptance still need work.
Corrections, OT, direct month locking and gated zones have implemented workflows; their remaining
acceptance boundaries are documented in the current attendance verification record. Employee document
storage is implemented; real ClamAV deployment/detection acceptance remains configuration work.
Authentication and Organization Masters integrate audit persistence; later modules must do the same.
There are no seeded users or test-login APIs.
No mock business integration is enabled at runtime.

The tenth migration, `20261007151945_CompleteEmployeeHiringApproval`, allows a Site Staff employment assignment
without a fabricated office. It was applied to the same original development database after a verified backup;
no new business tables or real accounts were created. Admin date-based site assignment/history APIs are included.
Same-start-day replacement and replacement of an already scheduled assignment return explicit conflicts rather
than rewriting history. HR-request/Admin-approved suspension, resignation and termination now reuse the existing
approval and status-history tables, with effective-time access denial and background session revocation.
The eleventh migration, `20261007164427_LinkEmployeeStatusApprovals`, adds only their approval reference.
Face/capture/notification code was added by the later Attendance continuation above;
reactivation/rehire and the remaining approval/policy workflows are still separate work.

The twelfth migration, `20261008074438_AddEmployeeChangeJournal`, adds only the protected employee-change
history table. It was applied on 8 October to the same original development database after a verified backup:
126 tables including EF history, zero business rows/accounts, no pending migrations/model differences.
Employee update verification and client contracts are documented separately; this is not production acceptance.

On **7 October 2026**, verified backups preceded upgrades of the original SSMS development database through
`20261007090713_AddAccountRecoveryAndOwnerInitialization`: seven migrations, 122 business tables.
Rollback-only SQL checks exercised first-owner creation, phone recovery, password reset and web
SMS session persistence; all 122 business tables were empty afterward. No real account was created.
See [verification](docs/Shared-Authentication-Verification.md) for current evidence and limitations,
and [next module plan](docs/superpowers/plans/2026-10-06-next-backend-delivery.md) for delivery order.

Organization Masters subsequently added the eighth migration,
`20261007122140_AddOrganizationMastersAndHolidayRevisions`, to that same verified development database
after a new verified backup. It adds one history table and holiday revision metadata, not duplicate
employee/master tables. See [organization verification](docs/Organization-Masters-Verification.md)
for actual SQL/HTTP checks and remaining deployment prerequisites.

## Developer reading order

1. [Developer Setup](docs/Developer-Setup.md)
2. [Work Ownership](docs/Work-Ownership.md)
3. [Architecture](docs/Architecture.md)
4. [SRS Module Map](docs/SRS-Module-Map.md)
5. [Employee Fields](docs/Employee-Fields.md)
6. [Original SRS v1.2](docs/requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx)

## Branch workflow

- main: reviewed release baseline.
- develop: shared integration branch.
- feature/task-name: individual work, created from develop.

Both developers start from develop, open a pull request into develop and review one another.
Do not force-push shared branches. Keep secrets and real employee information out of Git.
Branch protections require separate GitHub repository-owner configuration.
