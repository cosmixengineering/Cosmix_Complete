# SRS model completion and employee onboarding

The project manager explicitly requested the complete SRS model foundation and confirmed
the six-step employee approval/access flow on 05 October 2026. This authorizes implementation
on the current develop checkout, preserving the earlier local changes and four-project solution.

Latest instruction supersedes the original migration plan: implement entities/enums/value
objects, EF configurations and DbContext registration only. Do not generate/apply migrations,
change databases, add services/repositories/controllers or providers, create test projects,
commit or push. Earlier uncommitted migrations belong to the prior task and must be preserved.

## Binding employee flow

HR creates an incomplete draft without login access; submits an immutable version for Admin
review; Admin approves, returns with a reason, or rejects with a reason. Returned records can
be corrected and resubmitted as a new version. HR cannot edit pending submitted content.
Admin assigns an existing role or a role composed of authorized permissions plus resource
scope. Approval, employment status, account provisioning and invitation delivery are separate
states. Only completed approval and access assignment permit a single-use, expiring account
setup invitation. No plaintext/default password or raw invitation/refresh/reset token is stored.
Employee login will use current server permissions/scopes; model definitions alone do not
implement endpoint authorization or a login service.

## Model coverage and ownership

- Authentication/onboarding: accounts, roles, permissions, role membership, resource scopes,
  devices, sessions, credential setup/recovery, onboarding versions/decisions and invitations.
- HR: complete employee-related records, compensation, bank/medical private references,
  certifications, previous employment, asset custody; effective policies, holidays, leave,
  allowances, advances/loans, vehicle/fuel/maintenance and payroll handoff records.
- Attendance: face enrollment/attempts, punch assessments, work sessions, versioned summaries,
  exceptions/corrections, zones/assignments/events, site reports, overtime and period locks.
- Shared: approval routing/decisions/delegation, protected audit metadata, notifications,
  durable job/outbox metadata, idempotency receipts and export jobs.

## Cross-feature contracts

- Existing Employee.Id and all entity IDs are Guid; account actor IDs are opaque strings
  in legacy records. New UserAccount.Id is Guid, represented as a subject string at HTTP boundaries.
- Cosmix.Domain.HR.Policies.PolicyVersion and Cosmix.Domain.Common.Approvals.ApprovalRequest
  have Guid IDs. Optional FKs to these must not be used as proof of authorization.
- Authentication agent owns Domain/Authentication, Domain/HR/Onboarding, Employee workflow
  methods and their configurations. HR agent owns other new Domain/HR model folders/configs.
- Attendance agent owns new Attendance models/configs. Main owns shared models, DbContext,
  IAppendOnlyEntity, migration/snapshot preservation checks, documentation and whole-model verification.
- Shared Cosmix.Domain.Common.IAppendOnlyEntity marks immutable evidence/versions; main
  will enforce it in every SaveChanges overload. No generic repository is introduced.
- All persisted models need EF mapping, explicit storage lengths/precision, restrictive FKs,
  appropriate uniqueness/checks and concurrency for mutable workflow records.
- Scope grants bind permissions via role assignment; no client booleans, free-form SQL filters
  or implicit administrator entitlement to sensitive data. Public APIs are not added here.
- Use UTC instants, business DateOnly dates, decimal money, explicit currencies, and retained
  policy/assignment versions. No guessed calculation constants, approval thresholds or providers.
- Sensitive payloads use private/encrypted storage references and metadata; encryption providers,
  approved retention values, jobs, invitation delivery and enforcement services remain future work.

## Execution and verification

- [x] Analyze SRS and map each module to concrete model coverage and explicit service gaps.
- [x] Implement employee submission/access/account models and guarded domain transitions.
- [x] Implement remaining HR, Attendance and shared models with mappings.
- [x] Integrate one context without generating or changing migrations/snapshot.
- [x] Release build; EF model construction and mapping checks; temporary behavior checks
  outside the delivered solution, as AGENTS forbids recreating test projects.
- [x] Independent review, corrections and a precise model inventory plus remaining-work report.

No deployment, remote push, business database update, real employee data or completed-product
claim is authorized by this model request. The SRS management-decision items remain unresolved
except the onboarding route explicitly supplied by the project manager.
