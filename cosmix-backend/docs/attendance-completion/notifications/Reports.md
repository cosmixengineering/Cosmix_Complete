# Factual attendance reports and private CSV, Excel and PDF export

This implements scoped factual report reads and real asynchronous CSV, XLSX and PDF output. It does not
classify absence, calculate pay, produce payroll approval, or include salary/bank/medical/GPS
columns. Missing accepted evidence stays `no_accepted_session_evidence`, not an absence decision.

| Route | Permission | Result |
|---|---|---|
| `GET /api/v1/attendance/reports/factual?employeeId={id}&from=2026-10-01&to=2026-10-09` | `attendance.reports.read` | One employee, at most 31 inclusive dates, factual summary rows |
| `GET /api/v1/attendance/reports/factual/batch?employeeIds={id1}&employeeIds={id2}&from=2026-10-01&to=2026-10-31` | `attendance.reports.read` | Exact selected employee/day register with generation/filter/lock metadata |
| `GET /api/v1/attendance/reports/dashboard?employeeIds={id1}&employeeIds={id2}&from=2026-10-01&to=2026-10-31` | `attendance.reports.read` | Totals and per-employee factual aggregates for that same selected register |
| `POST /api/v1/attendance/reports/exports` | `attendance.reports.export` | 202 queued job; not completion |
| `GET /api/v1/attendance/reports/exports/{id}` | `attendance.reports.export` | Requester's pending/running/completed/failed state |
| `GET /api/v1/attendance/reports/exports/{id}/download` | `attendance.reports.export` | Authorized private CSV/XLSX/PDF attachment, or explicit not-ready/expired failure |

All routes use current web MFA; unsafe requests use the existing CSRF middleware. No permission
is automatically granted. Every protected employee/date scope must be authorized; one equivalent
server-resolved employee, site, department, team or project membership path is sufficient within
that scope. Different employees/date slices cannot substitute for each other. Retained source
site evidence keeps its separate required scope. No mobile team management API is added.

## Selected employee register and dashboard

The two multi-employee reads require **1–25 explicit, unique employee IDs** and at most
**31 inclusive business dates**. Pass each ID as a repeated `employeeIds` query parameter.
No automatic employee discovery, whole-company/site totals, unbounded selection or pagination
is implied. `metadata.coverage` is `explicit_selected_employees_and_dates_only`; the response
retains the sorted employee IDs, inclusive date filters, UTC generation time, `Asia/Karachi`
business timezone and `all_locked`, `all_unlocked` or `mixed` lock state.

Both reads authorize every selected employee's historical membership and retained source scope
before loading attendance evidence. One denied employee/date fails the entire request. An
unavailable selected source also fails explicitly; neither route silently omits rows or returns
partial totals. The register includes exactly one row per selected employee/date, ordered by
employee ID then business date, and reuses the same factual and frozen-lock evidence loader as
the original single-employee preview and export worker. A missing accepted session is the explicit
`no_accepted_session_evidence` factual state, not an absence or zero-pay decision.

The dashboard exposes these quantities in `totals` and in each `employees[].totals`:

- `employeeDays`: selected employee/date cells. The three evidence-day counts
  (`closedSessionEvidenceDays`, `openSessionDays`, `noAcceptedSessionEvidenceDays`) partition them.
- `workedSeconds`: summed elapsed seconds of closed session evidence; open sessions add no
  continuously growing estimate. `sumDailyWorkedMinutes` sums the already floored daily minutes,
  so it can differ from flooring the total seconds once. Neither field is payable time.
- Session/closed-session/open-session counts, exception count and distinct days with exceptions.
- Locked/unlocked employee/day counts, current saved-summary coverage and
  `unsavedOrStaleSummaryDays`. Stale saved revisions are omitted by the shared loader; locked
  days retain their pinned evidence. These are coverage indicators, not approval or readiness.

No salary, payable, late, overtime, present/absent or percentage-of-company result is added.
Policy status remains `factual_only_no_payroll_calculation` and data kind remains
`factual_attendance_not_payroll`. An overnight session stays on its retained shift-start business
date. The original preview, export contracts and permissions remain unchanged. These reads work
with the export worker disabled and require no new permission, schema or background job.

Example client requests, after completing web MFA with a currently authorized report role:

```http
GET /api/v1/attendance/reports/factual/batch?employeeIds=00000000-0000-0000-0000-000000000001&employeeIds=00000000-0000-0000-0000-000000000002&from=2026-10-01&to=2026-10-31
GET /api/v1/attendance/reports/dashboard?employeeIds=00000000-0000-0000-0000-000000000001&employeeIds=00000000-0000-0000-0000-000000000002&from=2026-10-01&to=2026-10-31
```

The GUIDs above are examples, not provisioned accounts. Neither GET needs an idempotency key or
CSRF token; existing browser authentication and server-side scopes still apply, and each read
is audited through `AttendanceOperation`. Invalid/duplicate/oversized filters return
`attendance_report_range_limit`; evidence matrix disagreement returns
`attendance_report_rows_conflict`. Authentication, denied scope and unavailable-source failures
retain their existing explicit error contracts.

Request example (use real authorized employee IDs and a fresh request key):

```http
POST /api/v1/attendance/reports/exports
Idempotency-Key: factual-report-20261009-001
Content-Type: application/json
X-CSRF-TOKEN: <current browser request token>

{
  "employeeIds": ["00000000-0000-0000-0000-000000000001"],
  "from": "2026-10-01",
  "to": "2026-10-09",
  "format": 1,
  "reason": "Review factual attendance evidence"
}
```

Limits: unique explicit employee IDs, at most 25 employees and 31 dates (775 output rows),
at most 2 MB generated output. Choose format `1` for CSV, `2` for XLSX or `3` for PDF;
other values return `unsupported_attendance_report_format`. Each genuine format includes
generation time in UTC, report identity, inclusive date/employee filters, the requested policy
period, explicit factual-only/unavailable policy calculation status and lock state at generation.
Large/unbounded exports and payroll-ready calculations remain outside this bounded factual pipeline.

## Processing and authority

`ReportExportJob` and its outbox message stage in the request's existing authorization,
audit and idempotency transaction. The protected filter snapshot retains the requester,
dates, employee list and original resolved resource groups. Duplicate request keys retain
existing audited retry behavior. New snapshots retain grouped alternatives. Older snapshots'
flat resource lists remain ALL singleton requirements; they are never reinterpreted as ANY or
discarded when grouped evidence is present. The outbox is the durable lease/retry owner.

The worker reuses the exact factual/locked-evidence loader used by daily summaries, without
nested application transactions. Its original web session must still be current and its
current export grants must cover both the originally captured and freshly resolved scopes.
Log out, revocation or expiry before generation fails that job explicitly; the user can make
a new request through a valid session. The worker acquires the shared authentication lock
within its transaction. Transient lock/deadlock conflicts remain retryable.

Report Start, stored output metadata, protected output/source manifest, Complete, report-ready
notification intent, outbox completion and audit are saved atomically. Start is not committed
separately, so an interrupted worker leaves Pending and the outbox lease can recover without
adding another report-specific lease schema. When dispatch fails terminally before completion,
the status API reports Failed using retained outbox failure metadata; it does not pretend the
still-pending business row is a successful report.

The original account alone can query/download a job. A fresh session is allowed for download,
but original and current resource scopes must both remain authorized. The exported rows are
frozen at generation, with factual source fingerprints and saved summary IDs/revisions in a private
manifest. A saved revision is included only when its complete input fingerprint still matches
the reported facts, roster, policy and exceptions; a stale revision is omitted rather than used
to label newer live values. Locked results reuse their pinned summary evidence. The report is still factual,
even when individual days have policy or period-lock context.

## File handling

The real UTF-8 CSV uses escaped quoted cells and neutralizes spreadsheet formula prefixes,
including leading whitespace. It includes employee GUID, business date, factual status,
worked seconds/minutes, session/open-session counts, exception count, locked flag, optional
summary identity/revision, source fingerprint and `factual_attendance_not_payroll`.
The CSV starts with a two-column metadata preamble, a blank line, then the attendance header/data.

The XLSX is a real OOXML ZIP workbook with `Report metadata` and `Attendance` sheets, frozen
headers and an attendance auto-filter. IDs, dates, status, hashes and metadata are explicit inline
text; trusted seconds/minutes/counts/available revisions are numeric cells usable in sorting and
sums. No formulas, macros or external links are created. The writer uses framework ZIP/XML support.

The PDF is a real paginated A4 PDF with a metadata cover and repeated date/policy headers.
Employee IDs, saved summary IDs/revisions and 64-character source fingerprints are preserved in
full, with at most 11 rows per data page. Server-derived factual text uses standard PDF fonts;
unsupported control/non-ASCII text is rejected rather than silently changing source evidence.
No PDF actions, scripts, attachments or external resources are produced. No runtime package or
provider was introduced for either format.

The existing encrypted private store saves bytes. `StoredFile` saves metadata, and the
protected output manifest preserves the complete `PrivateFileReference`, including object
version and digest. `ReportExportJob.SourceLockReference` points to that protected manifest;
it does not assert that every reported period was locked. Download verifies file identity,
metadata, generator provenance, expiry and protected content. No public path/URL is returned.
New output manifests bind format and embedded generation metadata to the job and protected
filters. Existing CSV-v1 output manifests and the original CSV private-file purpose remain
readable with current authorization. The durable message type retains its historical
`attendance.report-csv.v1` name for persisted-job compatibility; the bound job's format selects
the renderer. New CSV output uses generator v2, XLSX/PDF use their own v1 generator identities.

These are controlled server-generated bytes, not user-uploaded files. No fake clean malware
scan is recorded in StoredFile. The dedicated report download validates generator provenance;
this does not relax the separate HR document scanner or upload download rules.

The download window is one hour after generation. It is an access window, not an approved
retention period or automatic deletion policy. Failed transactions may leave encrypted orphan
objects; retention/orphan cleanup needs the separately approved policy.

## Runtime and verification

On 12 October 2026, `VerifyDashboard.cs` passed **54 offline checks** against the real report,
summary and attendance authorization services with in-memory collaborators. It verifies the
exact selected employee/date matrix, maximum 775-row range, overnight business-date attribution,
factual aggregates, daily-minute rounding, explicit selected-only metadata, pure aggregation of
lock/saved-summary evidence, null/duplicate/oversized filters, unavailable source failures,
mixed allowed/denied selections with no attendance reads before authorization, historical
transfer scopes, revoked grants/session, web MFA and mobile denial. Malformed or partial row
matrices fail explicitly. Locked SQL evidence and encrypted-file loading remain the shared
summary loader's separate verification boundary; the lock aggregate check does not claim those
were exercised. No SQL, encrypted disk, host or authenticated HTTP was used by this harness.

```powershell
dotnet run --file docs/attendance-completion/notifications/VerifyDashboard.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Application" --property:NuGetAudit=false
```

`AttendanceJobs.Enabled` remains false by default. Export requests explicitly return 503
`attendance_export_worker_not_enabled` while disabled; factual previews remain available with
their permission. No schema change is required by reporting/notification metadata. This slice
has not enabled a worker or executed any new SQL fixtures or authenticated acceptance tests.

`VerifyReports.cs` passed **70 offline assertions** in the final coordinated run on 9 October 2026: CSV bytes/injection/row
bounds, range limits, original and changed scope, requester/mobile isolation, protected filter
binding, real factual summary reuse, worker session/grant denial, generated private manifests,
ready-notification recipient, expiry, absence of fabricated scanner results, and actual DI
registrations, stale saved-summary provenance, Team/Project alternative scope, unchanged legacy
ALL semantics, original/current transfer requirements, all-format request/generation/download,
tampered generation identity denial, CSV-v1 manifest compatibility and unsupported format rejection.

`VerifyReportFormats.cs` passed **20 assertions**, including non-Gregorian process-culture dates,
all 775 rows, format signatures, numeric/text workbook types, formula-looking literal strings,
metadata completeness and rejection of partial/altered provenance. Its maximum synthetic exports
were CSV **185,974 bytes**, XLSX **51,126 bytes** and PDF **586,770 bytes**, below the 2 MB ceiling.
`VerifyReportFormatParsers.py` passed **19 independent CSV/openpyxl/pypdf/pdfplumber checks**:
all workbook rows/types/provenance, strict PDF parsing, all 775 PDF records, and every glyph's
page bounds across **76 pages**. Poppler rendered pages **1, 2, 3, 4 and 76** at 120 DPI; all five
images were visually inspected for the maximum cover, complete/partial repeated tables and final
page. IDs/hashes remained full and readable with no clipped/overlapping content. `pdfinfo` confirmed
A4 PDF 1.4, 76 pages, no JavaScript or forms. The local Poppler environment logged unavailable
unused Symbol/ArialUnicode display fonts; the PDF's Helvetica/Courier content rendered correctly.

Collaborator persistence/private storage were in-memory doubles. This is not
real SQL authorization, rollback, worker concurrency, encrypted-disk or HTTP acceptance.

Synthetic format QA commands, after coordinating exclusive build access in this shared checkout:

```powershell
dotnet run --file docs/attendance-completion/notifications/VerifyReportFormats.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false -- artifacts/attendance-formats-qa
dotnet run --file docs/attendance-completion/notifications/VerifyReports.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false
# Use the configured bundled Python/Poppler executables; no package installation is necessary.
python docs/attendance-completion/notifications/VerifyReportFormatParsers.py artifacts/attendance-formats-qa
pdftoppm -f 1 -l 4 -r 120 -png artifacts/attendance-formats-qa/attendance-maximum.pdf artifacts/attendance-formats-qa/page
pdftoppm -f 76 -l 76 -r 120 -png artifacts/attendance-formats-qa/attendance-maximum.pdf artifacts/attendance-formats-qa/last-page
```

The output folder is ignored and contains synthetic employee GUIDs only; it is not the encrypted
private store or deployment output. Inspect the rendered cover, complete/partial data pages and
final page for readable full IDs/hashes and clean repeated headers. Parser geometry checks cover
every page and all 775 rows; those checks complement visual inspection.

The notification/queue suite separately exercises retry and crash-recovery contracts. Real
two-worker SQL generation, database rollback, private-store fault/restart recovery and current
MFA/CSRF/download authorization need an explicitly approved isolated extension of the earlier
testing scope. No new test project, database, migration or real account was created here.
