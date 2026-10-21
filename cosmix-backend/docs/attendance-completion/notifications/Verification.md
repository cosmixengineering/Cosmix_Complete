# Notifications/jobs verification — 9 October 2026

Executed locally without SQL, a running HTTP host, real identities, biometric/provider calls
or private-file mutation. These are standalone file-based harnesses, not test projects.

| Check | Actual result | Boundary |
|---|---|---|
| `VerifyNotifications.cs` | 78 assertions passed | Real notification/outbox services and Domain lease transitions, exact registered-type queue ownership, in-memory repositories/private files, real EF model and SQL query translation with the connection closed |
| `VerifyReports.cs` | 70 assertions passed | Real report/handler/download logic across CSV/XLSX/PDF, grouped Team/Project scope, original+current authority, legacy ALL scope/CSV-v1 compatibility, stale revision/generation provenance and DI descriptors; in-memory collaborators |
| `VerifyReportFormats.cs` | 20 assertions passed | All three genuine formats at the 775-row limit, embedded metadata, culture invariance, typed inert XLSX values and provenance validation; synthetic local files |
| `VerifyReportFormatParsers.py` | 19 checks passed | Independent csv/openpyxl/pypdf/pdfplumber parsing, all rows/provenance, numeric/text types, strict PDF structure and glyph bounds on all 76 pages |
| Poppler PDF render + visual inspection | 5 pages inspected: 1–4 and 76 | Maximum filter cover, full and partial repeated tables, final page; full readable IDs/fingerprints, no clipping/overlap. Output sizes CSV 185,974 B, XLSX 51,126 B, PDF 586,770 B |
| `docs/group-rosters/VerifySiteScheduling.cs` | 171 assertions; 0 scenario failures | Existing site/temporary workflow regression; notification probe verifies no duplicate event staging on replay |
| `docs/shift-publication/VerifyPublication.cs` | 41 assertions; 0 scenario failures | Existing assigned-version/site/transfer/authority behavior; synthetic notification sink |
| `docs/shifts-rosters/VerifyServices.cs` | 135 assertions passed | Existing service/operation regression; synthetic notification sink |

The new suite demonstrated and fixed a direct-service page bound defect caused by record
constructor attributes not being property validation attributes. Explicit Application bounds
now reject invalid page/size even outside MVC. Existing unread stale writes conflict; already
read retries preserve timestamp and audit without a new mutation. Revoked sessions and another
recipient cannot obtain or mark a notification read.

Producer verification covers transaction-required staging, unchanged-event deduplication,
changed-event conflict, unknown recipient, invalid template and safe display fallback. Worker
checks prove that unrelated pending and expired messages, case aliases and padded identifiers
remain untouched while registered jobs dispatch. They also cover bounded/validated handler-type
lists, a post-claim handler mismatch failing closed, simulated atomic completion failure and retry,
bounded storage failures, active/stale/expired leases and a new processor resuming a persisted
in-memory lease. The actual EF claim query translates the explicit allowed types, binary comparison
and trailing-space sentinel together with pending/expired eligibility and deterministic bounded
ordering; the SQL connection remains closed. Translation is not executed SQL verification.
Recipient selection tests use exact historical site overlap and verify one
shared version notice across transferred sites, no invented account and malformed resolver denial.

Actual integrated producers: capture exceptions/missing checkout (current actor),
site report submitted (current actor), completed factual report (requester), site assignment/
replacement/ending, temporary creation/cancellation/supersession, and assigned shift version
publication (server-resolved affected employees with existing accounts). Decision authority
dependent alerts, HR document expiry and policy-notice producers are not implemented by this slice.

The report formats use framework ZIP/XML and bounded PDF serialization, with no new package or
provider. New manifests bind selected format and embedded generation/filter/lock/policy metadata;
old CSV-v1 manifest/file-purpose compatibility remains verified. These are factual reports with
unavailable policy calculations, not payroll-ready output. Synthetic QA files are under the ignored
`artifacts/attendance-formats-qa` folder. Poppler logged unused Symbol/ArialUnicode environment font
warnings; the actually used Helvetica/Courier fonts rendered correctly.

Scheduling producers stage inside the existing outer authorization/audit/retry transaction.
Temporary amendments use the union of exact previous/current affected manifests. Normal site
changes and publication use bounded HR membership overlap for the impacted period. The notification
content stays generic and contains no timing, reason or private evidence. Employees without an
existing account receive no invented identity; the underlying schedule still applies.

The worker remains disabled by default. No new SQL acceptance was authorized or executed for
this slice. None of the checks proves real SQL application locks,
rowversion races, DB rollback, real private-storage tampering, authenticated HTTP, process restart
or live production delivery. Additional isolated fixtures and worker writes require the new
approval specified in the master task; earlier access/scheduling acceptance is not relabeled as
notification/report acceptance. Final Release/API registration results belong in the root delivery record.
