# Backend architecture

Cosmix.Backend is one deployable ASP.NET Core API with four projects.
HR and Attendance are feature folders, not separate services or databases.
Both React web and React Native mobile call the same versioned API.

## Project responsibilities

| Project | Put here | Keep out |
|---|---|---|
| Cosmix.Domain | Entities, value objects, pure approved business rules | EF Core, HTTP, vendor SDKs |
| Cosmix.Application | Feature DTOs, validation, services and repository/provider interfaces | DbContext, controllers, external implementations |
| Cosmix.Infrastructure | EF Core SQL Server, mappings, migrations, repositories and provider adapters | HTTP workflow decisions |
| Cosmix.Api | Controllers, HTTP/authentication adapters, startup, middleware and configuration | SQL queries and payroll rules |

Application references Domain. Infrastructure references Application (and uses its transitive
Domain reference). API references Application and Infrastructure to wire dependencies.
Only API is deployed as an executable. The solution contains only the four application projects.

## Follow the working reference

1. Api/Controllers/HR/DepartmentsController.cs selects the Department master and route.
2. OrganizationMasterControllerBase binds the six masters' identical HTTP contracts.
3. Application/HR/Organization/OrganizationMasterService.cs validates the use case.
4. OrganizationOperation.cs rechecks current web MFA/All-scope permission and owns audited retries.
5. IOrganizationRepository and Infrastructure/Repositories/HR/OrganizationRepository.cs perform
   bounded queries and writes using the existing Domain entities and Fluent mappings.

This is a module-specific six-master implementation, not a universal CRUD framework. It supports
create/update/deactivate/reactivate/history, with immutable codes and rowversion checks. No physical
delete route exists. Branch means a physical office; sites and employee assignments stay separate.
HolidayCalendarService publishes immutable revisions; it does not calculate leave or attendance.
SiteMasterService/ISiteRepository under Attendance/Sites reuse the audited operation for the separate
Site master at `/api/v1/hr/sites`. Its writes/history additionally require an active fixed Admin or
SuperAdmin role on the same assignment as the All-scope permission. Reads can be delegated to HR.
No employee assignment, geofence or automatic site access is created by Site metadata changes.
The older DepartmentService/repository remain for compatibility but are no longer the HTTP path.
See [Organization Masters](Organization-Masters.md) for request/response contracts and limits.

## Persistence pattern

Attendance month finalization additionally inspects exact saved monthly financial sources inside
the existing authorization transaction. Its financial reader never nests `AttendanceOperation`.
New protected schema-2 manifests retain exact payroll-input revision identities and dependency
hashes; legacy schema-1 locks remain unchanged. Reinspection after private evidence IO rejects
source races. Missing legal activation, no-charge handoff, OT settlement and financial carry-forward
are explicit dependencies, not zero amounts or payment authority. See the second-pass Attendance record.

Use module-specific repositories, not a universal CRUD interface. An attendance repository
must not expose Update/Delete for raw punches. Keep queries scoped before pagination/exports.
Return materialized results, not IQueryable, to Application.

IUnitOfWork.SaveChangesAsync is implemented by the scoped CosmixDbContext. A write use case
stages changes and saves once. EF Core makes one SaveChanges transaction atomic; if an operation
needs multiple saves or an outbox, design/test that transaction explicitly. External notifications
must not be sent before the business transaction is committed.

Organization operations reuse the existing serializable authentication transaction and lock to
serialize authorization against access changes. Business row, audit snapshot and idempotency receipt
commit together even though EF saves first to obtain SQL rowversion. This favors correctness for
low-volume administration; it is not a throughput design for future bulk attendance processing.

Code First migrations live only in Infrastructure/Database/Migrations. There is one context
and one model snapshot initially. Add mappings by feature; coordinate snapshot ownership.
No full HR database is guessed from folder names.

## HTTP and security foundation

Versioning is explicit in the URL: /api/v1. Swagger UI at `/swagger` and generated OpenAPI
at `/openapi/v1.json` are available in Development only. Swagger uses the existing generator
and route-specific cookie/CSRF or mobile bearer metadata; documentation assets do not grant access.
Unannotated endpoints require an authenticated user by default; public health/OpenAPI endpoints
are explicitly anonymous. Reference Department reads also require hr.organization.read.

Shared mobile OTP, web password plus SMS OTP, protected browser cookies/CSRF, recovery,
session revocation/current-state validation and paired permission/scope evaluation are implemented.
They are configuration-gated, not production acceptance. See [Shared Authentication](Shared-Authentication.md)
for current contracts, selected-provider requirements and deferred actual first-owner setup.
Add resource-specific scoped queries before employee/site-assignment/financial endpoints are introduced.
The Site master list is intentionally global reference data behind explicit All-scope read permission.
Never accept employee identity or a permission decision solely from a client request.

Errors use ProblemDetails with a server correlation ID. Unexpected exception details are not sent
to clients. No request body or credential logging is installed. CORS allows only configured
origins; localhost React ports are allowed only by Development settings.
Health endpoints expose status, not connection strings, keys or detailed dependency errors.

## Keep the design simple

No MediatR, CQRS framework, AutoMapper, generic repository base class, microservices or message
broker is introduced. The later approved self-hosted face PoC adds Infrastructure-only ONNX Runtime
and OpenCV dependencies; legal-evidence PDF parsing also stays in Infrastructure. Application owns
provider/capture contracts, not vendor implementations. Add abstractions only when a real feature needs them.
Use service interfaces and clear methods; place feature code alongside its DTOs and contracts.

## Scope not yet implemented

Employee hiring intake is now implemented under Application/HR/Employees: module-specific services/repository,
current web authorization, protected section DTOs, audited draft writes and immutable Admin-queue submissions.
One EmployeeHiringDraft stores proposed references separately from effective assignments/salary/custody. Private
local AES-GCM file storage and a fail-closed ClamAV adapter live in Infrastructure/Files. Read Employee-Hiring.md
for routes and limits. EmployeeApprovalService revalidates the frozen snapshot, records the Admin decision,
materializes related records and creates a dormant identity in one outer transaction. It saves the decision before
profile children to respect the pending-form guard; either both stages commit with audit/receipt or both roll back.
Explicit mobile provisioning and existing access/web credentials complete the login handoff without nested transactions.
EmployeeSiteAssignmentService retains effective-dated site history. EmployeeLifecycleService handles HR-requested,
fixed-Admin-approved blocking status changes using existing generic approvals and immutable employee history.
EmployeeLifecycleProcessor reconciles due changes in bounded per-employee transactions. Authentication checks
effective history independently, so worker delay does not extend access. No reactivation or automatic payroll changes.

EmployeeCompensationService under Application/HR/Compensation reuses EmployeeOperation and the existing
generic approvals for the PM-approved HR-request/fixed-Admin salary-change flow. CompensationRecord stays
append-only; effective/as-of reads resolve each version's end from its successor without updating old rows.
Private proposal/decision snapshots retain before/after values. Salary-specific permissions do not grant bank
or medical access. No new context, table, migration, worker or payroll engine is introduced by this slice.

The folder READMEs/SRS map are locations for future work, not placeholders that return success.
EmployeeUpdateService/EmployeeUpdateSections extend approved employees without rewriting hiring snapshots.
Basic edits are direct; typed sensitive/transfer requests reuse generic ApprovalRequest/ApprovalDecision,
EmployeeOperation and existing assignment/bank/medical records. One append-only EmployeeChange journal stores
private before/after references with per-employee/section revisions. Current-section APIs are distinct from
historical hiring reads. No new context, framework, identity system or profile scheduler is introduced.
See [Employee Updates](Employee-Updates.md) for permissions, dates, document evidence and exclusions.
Expanded model classes and persistence mappings now exist (see Data-Model-Coverage.md).
Employee update migration/application evidence is recorded in Employee-Updates-Verification.md.
Rehire/category conversion, attendance workflows, other business approvals, reports, face checks,
location monitoring and payroll calculations remain. Real malware-scanner acceptance is still pending.
Authentication already has audit capture, durable OTP delivery and rate limiting; future business
modules must integrate their own relevant controls. Production keys, SMS and operational acceptance
remain deployment work. This must not be deployed as the completed HR system. Current authentication
implementation/verification boundaries are maintained in Shared-Authentication.md and
Shared-Authentication-Verification.md.
