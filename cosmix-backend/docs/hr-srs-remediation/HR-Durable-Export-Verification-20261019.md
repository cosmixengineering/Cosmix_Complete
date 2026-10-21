# Durable rich-HR export verification

Product source is frozen and ready for the parent's coordinated Release build.
This worker did not run a product build, SQL, migration, API host, Git operation or
external provider. See [contract and exact source list](HR-Durable-Export-Contract-20261019.md).

## Executed red/green checks

### P1/P2 follow-up against production Build01

Product sources were frozen after GREEN 362/362 in `VerifyHrDurableExports.cs`.
Final test-only additions pass 365/365 with unchanged product sources. The added
checks deny publication/status/download after revocation of newly generated
historical scopes, even while the original request scope remains authorized.
Final durable evidence and input hashes:
`D:/CosmixVerification/hr-backend-20261019/hr-export-scoped-4028c6a486b1411bb8e6d4d5afd4d0f2`.
Current DLL input: `D:/CosmixVerification/hr-backend-20261019/hr-production-build01/bin/Cosmix.Api/release`.
The runner additionally copies the parent's frozen DurableJobContracts.cs and
DurableJobProcessor.cs as read-only source inputs. No shared file was edited here.

- P2 RED: `hr-export-scoped-c76ea9de0f694ba9bfdcfc29f40d6a87`, completed export denied
  after employee-status change. P2-only GREEN 353: `hr-export-scoped-517abc7a6e2446d490bb440b57db08c4`.
- P1 RED: `hr-export-scoped-32f5d2617ea84f618faab24ddc6a3e09`, output private IO was
  inside the source/publication transaction.
- First P1 compile run stopped on two unused test-hook fields (CS0649), not product
  errors: `hr-export-scoped-3659e0a4d05c4ed9b9f2f14e72f1757c`. The hooks now exercise
  revocation after preparation and during notification staging.
- Combined GREEN 362: `hr-export-scoped-ebfea3eef26544969159ac06be8b99f1`, containing
  input hashes, compile/run logs and synthetic PDF. All directory names in this
  subsection are under `D:/CosmixVerification/hr-backend-20261019/`.

New checks cover retained status/download/replay after mutable status and population
changes; source pages inside the read-only snapshot without global lock; output IO
outside all source/publication transactions; no published rows/notification before
completion; one final auth lock; revocation after preparation and during notification
IO; stale lease rejection before publication and fresh preparation on restart; exact
claimed-message metadata mismatch rejection. Existing retry, failure, scope, format,
count and download checks still pass. No zero-warning claim: PdfPig CS1701 warnings
and existing CS9236 informational diagnostics remain in the standalone compiler.

Other final P1/P2 suites, all GREEN:

| Source | Result |
| --- | --- |
| VerifyHrDurableExportSurface.cs | PASS |
| VerifyHrReportBoundary.cs | 370/370 |
| VerifyHrReportOvertime.cs | 12/12 |
| VerifyHrReportXlsx.cs | 81/81 |
| VerifyHrReportQueries.cs | 250/250 |
| VerifyHrReportResults.cs | 204/204 |

Logs/hash manifest: `D:/CosmixVerification/hr-backend-20261019/hr-export-scoped-fb977a68e16d43308ddaff5b320e95bf`.
The eight added repository checks use actual HrReportRepository source over synthetic
data and offline SQL translation: suspended retained employee with 300 unrelated Active
employees, no mutable Status predicate, exact retained-ID SQL restriction in both
authorization and source-preparation modes, and current/historical resource denial.
The connection interceptor reported zero attempted SQL connections.

### Original implementation against build04

Frozen input: `D:/CosmixVerification/hr-backend-20261019/hr-corrections-build04/bin/Cosmix.Api/release`.

The initial `VerifyHrDurableExportSurface.cs` run through existing VerifyManaged
failed as expected: durable HR status/download endpoints were absent in build04.
Red output: `D:/CosmixVerification/part-c-parent-20261013/managed-ca0b69ab755a43689bf433b92df62c3b`.

The report-specific `VerifyHrDurableScoped.ps1` copies frozen DLLs and the owned
sources to a new D: directory, then compiles standalone executables without a test
project or product rebuild. An unchanged partial repository file is a read-only
compile input. Existing HR overtime projection calls an Application-internal
reader: only the copied Application DLL receives InternalsVisibleTo metadata for
these standalone assembly names. Its evidence implementation remains the frozen
IL. Original binaries are never modified; input paths/hashes are retained/checked.
This is source-overlay verification, not an integrated product build.

| Source | Result | Scope |
| --- | --- | --- |
| VerifyHrDurableExportSurface.cs | PASS | Status and authorized download route registration |
| VerifyHrDurableExports.cs | 347/347 | Actual new services, dispatcher and renderers with synthetic transactions/storage/repositories |
| VerifyHrReportBoundary.cs | 370/370 | Existing report permissions, scoped source and current-page export behavior |
| VerifyHrReportOvertime.cs | 12/12 | Existing retained overtime evidence integration |
| VerifyHrReportXlsx.cs | 81/81 | Existing ZIP/XML XLSX structure and text safety |
| VerifyHrReportQueries.cs | 242/242 | Actual repository queries over synthetic rows and offline SQL translation; connections blocked |
| VerifyHrReportResults.cs | 204/204 | Attendance projections and retained-source results; connections blocked |

Final durable/boundary/OT/XLSX logs:
`D:/CosmixVerification/hr-backend-20261019/hr-export-scoped-c0703f6b71d74c3cab89190a31561add`.
The combined run then stopped in the query suite because the initial harness used
SqlClient's reference stub. This was a runner asset-selection error, not a SQL call.
The runner was corrected to copy Windows runtime assets from the frozen deps file.
Final query/result rerun and hash manifest:
`D:/CosmixVerification/hr-backend-20261019/hr-export-scoped-1275fab1630447908a7d6de8e64a07a8`.
The prior durable-only all-green run/hash manifest is
`D:/CosmixVerification/hr-backend-20261019/hr-export-scoped-d2ab9a2d3f7a4a8d8ff6de5d2ea72df1`.
Route green/hash manifest is
`D:/CosmixVerification/hr-backend-20261019/hr-export-scoped-20340aa7c8c54ffb8412f816541c703a`.

Compiler output retains existing PdfPig net9/net10 reference-unification warnings
(CS1701) and two lambda-binding informational diagnostics (CS9236). No zero-warning
product-build claim is made by these standalone runs.

## Behavior exercised

- All 14 report types and CSV/XLSX/PDF jobs, including exact empty results.
- 1005-row full result over two pages despite requested page 87/page size 2.
- Same-key replay, changed payload conflict, no data query during request, status
  counts absent until completion, before-ready and expired downloads.
- Incomplete pages, changing counts, missing counts, excess rows and text rejected
  without a completed output, StoredFile row or notification intent.
- Actor ownership, web/MFA, each site-report required permission at request/build/
  status/download/replay, removed current scope and retained original site scope.
- Worker session revocation before generation and during output/manifest writes;
  revocation during download IO denies bytes after the read.
- Filter/receipt/audit storage failure rolls back producer state. Output/manifest
  failure retries through the actual dispatcher; exhaustion is visible as failed.
- Service reconstruction after retry and cancellation/expired-lease recovery.
  Synthetic transaction rollback restores Pending and publishes one successful
  file/notification, without persisting Running separately.
- File digest/length checks, missing private storage failure, exact content types,
  formula-safe CSV/XLSX and PDF parsing/pagination/final row retention.

The transaction collaborator models rollback; these tests do not prove SQL locking,
atomic commits, competing live requests or process-restart durability. The memory
store validates purpose bindings but is not a claim of physical encryption tests.
Production uses the existing centrally registered AES-GCM IPrivateFileStore; no new
storage implementation or fallback plaintext output was introduced.

## PDF inspection and limits

Actual generated sample: `hr-export-scoped-c0703f6b71d74c3cab89190a31561add/synthetic-hr-report.pdf`
under the D: verification root above. PdfPig parsed all 300 pages of the 1005-record
sample and checked every glyph's bounds. Poppler rendered the first and last pages;
visual inspection found no clipping/overlap and confirmed record 1005 on page 300.
Poppler logged missing optional Symbol/ArialUnicode display fonts; the PDF uses
built-in Courier and the inspected text rendered. No external fonts were installed.

Unicode is shown as JSON `\uXXXX` escapes. This preserves data but is not readable
Urdu, shaping, localization or production localized PDF support. Headers/type/timezone
are printable-ASCII validated; values/title/filter use default JSON escaping.

Limits: 256 candidate employees, 8192 scope-source rows, 1024 roster reads,
65536 retained scope alternatives, 366 inclusive filter days, 3660-day source span,
16384 output rows, 2000000 cell-text characters total, 32767 characters per cell,
8000000 output bytes, 2048 PDF pages. Existing private-store configuration may be
smaller. Limits fail the complete job; no truncation or fabricated counts.
Membership is frozen at request; source rows are selected at generation under
serializable isolation. New matching employees require another job.

Downloads expire after one hour. Expiry is not deletion. Failed attempts can leave
encrypted orphan objects because the reused private-store contract has no deletion
API. No retention policy or cleanup worker was invented.

## Parent handoff

No shared DI, schema, Domain, DbContext, migration, central registry or package edits
are required. P1 does require the parent's prepared-handler contract/processor/claim
extension, detailed in the integration contract. Suggested parent-only text change: NotificationTemplates.ReportReady
title from `Attendance report ready` to `Report ready`, preserving template identity.
Mill received the stable endpoint/body/state/download and page-export contracts.

Live runner selects only message type `hr.report-export.v1` / HrReportExportHandler
in one isolated scope using the existing DurableJobProcessor/AttendanceJobOptions.
Dispatch writes job/outbox/audit/StoredFile and encrypted objects plus notification
intent; source reads are read-only. Do not label a real worker run read-only.
Parent owns Release integration and authorized live acceptance; no such result is
claimed by this worker until the parent supplies its evidence.

P1 removes the global auth application lock from pagination/rendering/output IO.
Serializable preparation can still hold row/range locks; concurrent live auth/polling
latency is unverified. Overtime protected source evidence is loaded within the source
snapshot. Notification private IO is within completion but before the auth lock;
its SQL intent is rolled back on final revocation. No live read/write was attempted,
and no catalog attach/reset/database isolation change was made.

## Parent Integrated Verification

Final coordinated `hr-production-build02` compiled all application layers with
zero warnings/errors. Frozen Release checks passed: prepared dispatcher 22,
durable exports 365, report boundary 370, repository queries 250, results 204,
overtime 12, XLSX 81, generated MVC/OpenAPI 293, plus durable route surface.
Logs and input hashes:
`D:/CosmixVerification/part-c-parent-20261013/managed-52cf4b6dca4a4f2a9e83dc139c02b78d`.
The 78 notification, 44 missing-checkout and 58 document-expiry regression checks
also passed against build02. See the [parent ledger](HR-Production-Continuation-20261019.md).

The original database was confirmed ONLINE via read-only metadata, but no
authenticated export/worker/concurrent SQL test was executed. That remains an
explicit acceptance gate; static review and synthetic checks do not replace it.
