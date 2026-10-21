# Durable rich-HR exports: integration contract

Status: PRODUCT SOURCE FROZEN / PARENT MAY START COORDINATED RELEASE BUILD NOW.
Endpoint contract stable. P1/P2 standalone durable rerun passed 365 checks against
the frozen production-build01 owned-source overlay and parent-owned frozen prepared
contract/processor sources. Product source freeze is renewed after those fixes.
Only documentation/standalone test files may change during the parent build.
Parent owns shared files. No Domain, DbContext, migration or central DI edit is required:
the existing durable processor discovers `IDurableJobHandler` registrations. HR-owned
registration will add `hr.report-export.v1`. Existing AttendanceJobs enablement applies.
AttendanceOperation's write entry point deliberately rejects HR permissions, so HR
uses its existing authorization transaction with the shared journal/receipt contracts.
There is no new queue, worker process, storage module or report table.

Exact parent shared changes: NO DI/job registry/Domain/schema change required.
Euler/parent owns the coordinated preparation extension in
`Application/Common/BackgroundJobs/DurableJobContracts.cs`,
`Application/Common/BackgroundJobs/DurableJobProcessor.cs`, and the existing
`Infrastructure/BackgroundJobs/DurableJobRepository.cs`.
`IPreparedDurableJobHandler : IDurableJobHandler`
adds `Task PrepareAsync(DurableJobClaim claim, CancellationToken ct)`.
The four-argument claim gains init-only `DurableJobPreparation? Preparation` with
MessageType, EncryptedPayloadReference, CorrelationId, nullable DeduplicationKey.
SQL claim populates it; the processor prepares outside completion and compares
exact immutable metadata/handler/attempt against the locked completion message.
The HR handler requires this extension; missing preparation cannot silently fall
back to its old long-transaction path. Old handlers retain their existing behavior.
`Application/DependencyInjection.cs` already calls `AddHrReportsApplication()`;
`Infrastructure/DependencyInjection.cs` already calls `AddHrReportsInfrastructure()`.
`DurableJobProcessor` discovers their `IDurableJobHandler` registration automatically.
Suggested presentation-only parent change: in
`Application/Common/Notifications/NotificationTemplates.cs`, change the ReportReady
title from `Attendance report ready` to `Report ready`. Keep its existing template
key and body for compatibility. The HR handler reuses this existing notification.

App messaging rejects messages to the native parent. Frontend worker Mill received
the contract directly. This document and commentary are the parent coordination
handoff; do not start product build until source freeze is recorded.

Source freeze is now recorded above. Green sources so far:
`VerifyHrDurableExportSurface.cs`, `VerifyHrDurableExports.cs` (365 checks).
The report-specific runner is `VerifyHrDurableScoped.ps1`; it does not rebuild
products or modify frozen binaries. Existing OT evidence is Application-internal:
the runner adds InternalsVisibleTo metadata ONLY to its copied Application DLL
to compile the owned-source overlay against the unchanged internal evidence IL.
Original frozen input hashes are checked. Parent Release is the integrated check.

Production files frozen (relative to src):
- Cosmix.Application/HR/Reports/HrReportAuthorization.cs
- Cosmix.Application/HR/Reports/IHrReportRepository.cs
- Cosmix.Application/HR/Reports/HrReportContracts.cs
- Cosmix.Application/HR/Reports/HrReportService.cs
- Cosmix.Application/HR/Reports/HrReportOvertimeEvidence.cs
- Cosmix.Application/HR/Reports/HrReportRegistration.cs
- Cosmix.Application/HR/Reports/HrReportExportContracts.cs (new)
- Cosmix.Application/HR/Reports/HrReportExportService.cs (new)
- Cosmix.Application/HR/Reports/HrReportFullExport.cs (new)
- Cosmix.Application/HR/Reports/HrReportPdfExporter.cs (new)
- Cosmix.Infrastructure/Repositories/HR/HrReportScopeRepository.cs
- Cosmix.Infrastructure/Repositories/HR/HrReportInfrastructureRegistration.cs
- Cosmix.Infrastructure/Repositories/HR/HrReportExportRepository.cs (new)
- Cosmix.Api/Controllers/HR/HrReportsController.cs

Parent live-runner boundary: create an isolated service scope; resolve
`IDurableJobHandler` registrations and select only exact MessageType
`hr.report-export.v1` (`HrReportExportHandler`). Construct/use the existing
`DurableJobProcessor` with that one-element list and existing `AttendanceJobOptions`
(`Enabled=true`; normal defaults: MaximumAttempts=5, LeaseSeconds=120,
RetryBaseSeconds=5, RetryMaximumSeconds=300). Do not enable unrelated hosted workers.
Dispatch necessarily writes ReportExportJob/outbox/StoredFile/audit, encrypted
output/manifest and ready-notification intent; it is NOT a read-only action.
Business source queries are read-only. Read-only inspection may query the job,
outbox identity and retained file metadata, but cannot verify execution/restart.
New-handler synthetic claims must populate Preparation. Preparation creates no
business/audit/outbox rows; the existing completion transaction owns publication.
The handler retains one prepared result per instance, checks message/lease/attempt/
payload/correlation/key plus pending job owner/format/period/filter binding, and
consumes that result once. No lease renewal or stale-claim fallback was added.

## P1/P2 publication and retained authorization

P1: preparation reads the protected request before opening its own serializable
read-only source transaction. No global authentication application lock is acquired
during materialization. Snapshot completion clears tracked source entities before
rendering and output/manifest private writes. Overtime's existing protected source
evidence reads still occur as part of source materialization, not final publication.
The prepared StoredFile is newly constructed and untracked, solely reserving its ID
for the private manifest. No queried tracked entity crosses into publication.

Apply first stages the existing notification inside the uncommitted completion
transaction, BEFORE the global authentication lock, so its private IO is also outside
that lock. It then takes the lock, reloads/binds the pending report, rechecks original
and generated retained scopes, current session/grants and current/historical resources
for retained IDs, and stages the file/report state. There is no pagination, rendering,
report/manifest private IO or notification IO after this worker acquires that lock.
Any final denial rolls back the file/report/notification publication atomically.

Serializable source reads may still block other SQL writers through row/range locks.
This is not a claim of nonblocking SQL or a new SNAPSHOT/RCSI database configuration.
Concurrent login/refresh/poll/revoke acceptance awaits parent evidence. HTTP request/
download auditing retains its existing transaction behavior; this split fixes worker
generation. The one-hour window starts at publication, not preparation.

P2: new requests retain their original filter/candidate selection. Replay, status,
download and final publish authorize retained employee IDs independently of mutable
employee status/filter eligibility and unrelated matching populations. Source
preparation queries additional source scopes only for those retained IDs. Actual
resource, original/generated scope, permission or session revocation still denies
the whole export. Business filters still apply to generation's data rows, not to
eligibility to read an already generated snapshot.

## Frontend contract

- Existing current-page CSV/XLSX remains at `POST /api/v1/hr/reports/{reportType}/export-page`:
  same request body, HTTP 200 binary, no idempotency key. Uses the existing synchronous
  service and preserves separate total/returned metadata. The frontend worker has
  been informed to move its existing Blob request to this route.

- `POST /api/v1/hr/reports/{reportType}/export`: HTTP 202 JSON, `Idempotency-Key`
  required (8-100 letters/digits/underscore/dot/colon/hyphen), existing web CSRF.
  Body remains `{ "format": 1, "reason": "Approved review", "filter": {} }`.
  Format: 1 CSV, 2 XLSX, 3 PDF. Pagination is ignored; filters retain their meaning.
- `GET /api/v1/hr/reports/exports/{id}`: owner-only current-authority status.
- `GET /api/v1/hr/reports/exports/{id}/download`: authenticated binary response,
  completed only, one-hour expiry, no public or storage URL.
- Job response: `id`, `reportType`, `state`, `format`, `requestedAt`, `generatedAt`,
  `downloadExpiresAt`, `failureCode`, nullable `totalCount`, nullable `returnedCount`.
  States: `pending`, `running`, `completed`, `failed`. Counts become available only
  after successful generation. Download 409 when not ready; 410 when expired.
- Worker disabled: 503 `hr_report_export_worker_not_enabled`. Scope/source/row/output
  limits fail explicitly; never present a truncated file as a successful export.
- Current web MFA/session plus `hr.reports.read`, `hr.reports.export` and each
  report's existing source/field permissions are required at request/build/download.
  Removed original employee/source scope denies the retained job, not a smaller file.
- Same key/payload replays the job after authorization; changed payload returns 409.
  A failed job is retried automatically only within the existing dispatcher policy;
  requesting another job requires a new key.

## Exact limits and PDF presentation

The job freezes employee membership selected under request-time authorization.
The worker reads complete filtered source records for those employees at generation
time, inside one serializable transaction. Newly matching employees require a new
job. Removed original scope denies the job. SourceLockStatus states
`request_employee_scope;generation_source_records;not_payroll_certification`.
Counts describe that complete authorized snapshot, never the current preview page.

Existing limits remain: 256 candidate employees, 8192 scope-source rows, 1024 roster
reads, 65536 scope alternatives, 366 inclusive filter days, 3660-day source-footprint
span. Complete result maximum is 16384 rows; text maximum 2000000 characters total
and 32767 per cell. Generated output maximum is 8000000 bytes and PDF maximum is
2048 pages. Underlying configured private storage may impose a smaller byte bound.
Exceeding limits fails the whole export with a narrowing/error code; no partial file
or invented total is published. Private storage failure follows dispatcher retries.

PDF is a complete paginated record/field layout, not a wide table. Values, title and
filters use the default JSON serializer, which escapes non-ASCII and control text.
Headers/type/timezone and the final rendered text must be printable ASCII, otherwise
generation fails explicitly. This preserves Unicode data as visible `\uXXXX`
escapes; **it does not render human-readable Urdu and is not localized production
PDF support**. CSV and XLSX retain their existing Unicode output behavior.

The one-hour download window is not physical deletion or retention enforcement.
The existing private-store interface has no delete operation. Interrupted attempts
can leave encrypted unreferenced objects; no orphan cleanup/retention worker was
invented. Report output metadata never publishes a storage key or public URL.

## Implementation and verification boundary

Own Application/HR/Reports, Infrastructure/Repositories/HR/HrReport*.cs, HR report
controller and report-specific standalone checks/docs only. The original request
scope and generated source scope are privately retained and reauthorized. Output
and manifest use encrypted IPrivateFileStore; the outbox completion transaction
atomically commits ReportExportJob, StoredFile and notification intent.

Current frozen DLLs: D:/CosmixVerification/hr-backend-20261019/hr-production-build01/bin/Cosmix.Api/release.
Owned-source standalone overlays may compile against these DLLs without modifying
them. Parent coordinates the product build after source freeze. No SQL writes,
migrations, Git operations, API hosts or external providers are authorized.
