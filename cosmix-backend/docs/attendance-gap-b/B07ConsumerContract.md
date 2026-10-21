# B07 bounded site-report discovery and submission recovery

Scope: Part B07 of `HR_Backend_Completion_Plan_A_to_G_2026-10-13.docx`,
B07.3's missing queue discovery and own permanent-key submission recovery. Existing intake, Mine/detail/evidence,
Accept/Return/Reject, protected response/resubmission and immutable history
were inspected and reused. This is an additive report-module API, not a
claim that all B07 backend deployment or web/mobile clients are accepted.

## New discovery contract

`GET /api/v1/site-reports/queue/sites?page=1&pageSize=25`

Requires the existing `attendance.site-reports.read`, a current web MFA
session and current Site or All grants for that exact permission. Scope is
resolved inside `AttendanceOperation`'s existing authorization transaction.
An unrelated permission's All scope, a supervisor name or master-site
supervisor field cannot grant report visibility or decision authority.
Employee/Department/Team/Project/Self scopes alone cannot discover whole
site queues. No additional permission or shared registration is needed:
`IAccountAccessReader` is already registered by `AddAccessManagement`.

Page is 1-1000000, pageSize is 1-100; defaults are 1/25. Invalid direct
service requests return 400 `invalid_site_report_queue_page`; MVC validation
can return its normal validation ProblemDetails first. Items and totalCount
are scoped in SQL before paging, ordered by Code then site ID. TotalCount
counts sites, not reports. Sites with no reports and inactive sites are
included so reviewers can reach historical queues. Out-of-range valid pages
return empty items with the scoped total. Revocation is checked on every call.

```json
{"items":[{"siteId":"11111111-1111-1111-1111-111111111111","code":"SITE-01","name":"Historical site","isActive":false}],"totalCount":1,"page":1,"pageSize":25}
```

Only SiteId/Code/Name/IsActive are returned. Discovery is audited and no-store;
it has no client scope selector. Clients use a returned siteId with the
existing queue/detail/evidence routes, which independently reauthorize it.
Current Read All grants discover all master sites. Missing or incompatible
grants return 403; invalid current sessions return 401. Discovery does not
provision access, create supervisors or confer `attendance.site-reports.decide`.

## Own submission reconciliation

`GET /api/v1/site-reports/mine/reconcile`

Supply `Submission-Key: <original submission Idempotency-Key>` as a header.
It is an identifier, not a password, bearer token or face proof. No credentials
or proofs belong in this header or URL. No request body, employee, account,
site, capture, device or changed payload is accepted as recovery authority.
The route remains within the existing own-read API and does not replace
`queue/sites`.

Requires current `attendance.site-reports.self` and the current mobile/web
own-history authorization transaction. The server derives employee AND
account from the current authenticated session and queries both along with
the original permanent submission key. A current replacement device or web
session may read the retained original device provenance. Recovery never
authorizes an intake retry on that device or with changed content.

The key is validated by the same exact syntax as existing submission writes:
`\A[A-Za-z0-9_.:-]{8,100}\z`. No trimming, case folding or prefix matching.
The existing `SiteReports.IdempotencyKey` model mapping uses
`Latin1_General_100_BIN2` and its unique `(EmployeeId, IdempotencyKey)` index;
the lookup adds original `UserAccountId` without transforming the indexed
column. No schema/index/permission/registration change is required. Actual
deployed column/index acceptance remains part of the owner's SQL window.

Success is 200 with `{resultKind:"current_retained_report",detail:{...}}`.
`detail` is the existing safe detail contract, including current review and
purpose-bound written-response histories. This is CURRENT retained workflow
state, not the frozen original 201 receipt, and not a byte-for-byte validation
of a client's pending body. Each history is capped at 256 entries, with 257
fetched only to detect overflow; overflow returns 409 rather than truncation.
Corrupt or unavailable protected response history fails closed. Private
storage keys/references, original submission key, file paths/envelopes and
attachment bytes are absent. Attachments use the existing independently
authorized own/site evidence endpoints.

| Result | Client meaning |
| --- | --- |
| 200 `current_retained_report` | Link the retained server ID to that exact pending original key, mark the original as received, and stop intake retry. The report can now be Returned/Accepted/Rejected or pending HR review. |
| 400 `invalid_idempotency_key` | Direct service key syntax/length failure. MVC can instead return normal validation ProblemDetails for a missing/malformed header. Preserve the pending original for inspection. |
| 401 / 403 | Current session / permission denied. Reauthenticate or resolve access; this is not a missing report. |
| 404 `site_report_not_found` | No report visible under this exact employee/account/key, including wrong case or another submitter. Keep the original unresolved; this is not fresh-key retry authority. |
| 409 history/evidence failure | Preserve the pending original and request investigation; no partial recovery, automatic response or resubmission. |

Lookup is independent of date paging and retry-receipt retention. It neither
reads nor recreates the submission's retry receipt. Repeating the GET reads
current state again. It uses `AttendanceOperation.ReadAsync`: only the normal
own-read audit is staged/saved; no report/review/response/receipt/notification
or private-file write, scanner call, punch or settlement occurs. This slice
executed no SQL at all. It supplies a backend recovery contract, not an
implemented mobile queue, device transfer or network acceptance result.

## Existing consumer contracts

| Flow | Route | Shape or boundary |
| --- | --- | --- |
| Intake | POST `/api/v1/site-reports` | Mobile bearer and registered device; Self permission; mandatory Idempotency-Key; 201 detail and own-detail Location |
| Own history | GET `/api/v1/site-reports/mine` | Self, mobile or web; received-date `from/to/page/pageSize` |
| Own submission recovery | GET `/api/v1/site-reports/mine/reconcile` | Self, current employee AND original account; exact original key in `Submission-Key` header; current retained detail, no retry receipt replay |
| Site queue | GET `/api/v1/site-reports/queue?siteId=...` | Read, web MFA, exact Site/All scope; same date/page contract |
| Own detail/evidence | GET `/api/v1/site-reports/mine/{id}` and `/evidence` | Own report only; detail retains review and response histories |
| Site detail/evidence | GET `/api/v1/site-reports/sites/{siteId}/{id}` and `/evidence` | Read, web MFA, exact granted site; mismatched report returns 404 |
| HR review | POST `/api/v1/site-reports/sites/{siteId}/{id}/decisions` | Separate Decide grant, web MFA/CSRF, no self review; mandatory key and both expected sequences |
| Returned response | POST `/api/v1/site-reports/mine/{id}/responses` | Original submitter; mandatory key, both expected sequences, latest returnedReviewId and writtenResponse |
| Response attachment | GET own/site detail route + `/responses/{responseId}/evidence` | Own or granted-site binding and current access rechecked |

All report lists use `{items,totalCount,page,pageSize}`, not cursors. From/To
are inclusive Asia/Karachi server-received dates, at most 366 days. Report
ordering is ReceivedAtUtc descending, then ID; discovery uses site Code/ID.
An offline capture date does not choose the Mine/queue received-date window.

Submission requires non-empty siteId, kind and nonblank notes of at most
4000 characters. Kinds are numeric: Arrival=1, Departure=2, Delay=3,
AccessIssue=4, Incident=5. TaskReference is optional (at most 200 characters),
claimed task context; no authoritative assigned-task validation exists.
DeviceCapturedAtUtc is optional online, mandatory offline, cannot be future,
and offline capture cannot precede device registration. Online assignment
uses server time; offline uses the claimed capture date only to select an
unambiguous historical site assignment. Capture and location stay unverified.

Latitude/Longitude/AccuracyMeters are optional as one complete group, not
universally required. Latitude is -90..90 and longitude -180..180 with at most
7 fractional digits; accuracy is 0..99999999.99 with at most 2. Identity,
device, server timestamp, reviewer and private storage references cannot be
supplied in request JSON. Unknown submission/decision/response fields fail.
Optional attachment is `{fileName,contentType,content}` with base64 content,
PNG/JPEG/PDF only, at most 10 MiB. Type/signature, scanner and private-storage
checks are required; failed scanner availability returns 503, never success.

Details include Report, assignment/device IDs, notes, location, workflow
status, ordered reviews and responses. Reviews expose their immutable ID;
responses retain returnedReviewId, sequence, submittedAtUtc, writtenResponse
and attachment presence. Neither DTO discloses a private storage reference.
Histories fail closed above 256 entries rather than silently truncating.

## Sync, retry and resubmission

1. Before an offline/uncertain send, the client retains the original key,
   site, body, attachment bytes and capture timestamp durably. Pending Sync
   is a client state; the backend has not received or accepted that record.
2. Retry a lost response with identical content and the same key/session
   device context. Current authorization is always checked. A 201/server ID
   means Received / Pending Review, with explicitly unverified evidence.
3. Changed content under the same key conflicts with `idempotency_key_reused`.
   Receipt expiry returns `idempotency_key_expired`; a missing receipt with
   the original employee/key already persisted returns `site_report_already_received`.
   Do not silently generate a new key after uncertainty. Use own reconciliation
   with the original exact-case key after either failure. A 200 identifies the
   already received original; 404 remains unresolved and grants no permission
   for a fresh-key submission. Client/device/network acceptance is still pending.
4. A Returned detail has `employee_response_required`. Fetch fresh detail,
   retain both sequences and the latest returned review ID, and POST an
   immutable written response with its own stable key. The original capture,
   site, notes and review remain unchanged; the report becomes pending HR
   review again. The response cannot replace capture time or location.
5. Accepted=1 and Rejected=3 are terminal. Returned=2 permits one answer to
   the latest Return. Escalated=4 is unsupported. Stale/competing sequences
   conflict; responses and decisions use exact retained-result retries. This
   reconciliation route looks up only the ORIGINAL submission key; response
   and decision keys are not interchangeable with it.
   Acceptance is operational review, never an accepted punch or pay result.

Existing notifications stage durable in-app intents only for the owner;
delivery depends on the separately configured worker. No supervisor or HR
broadcast recipient list is inferred. Report operations do not alter punches,
work sessions or payroll. Review/response web writes use existing cookies,
browser context and CSRF; cookies must remain browser managed.

`B07Contracts.http` supplies requests for an existing authorized backend.
Its values are placeholders, not runnable fixtures or configured accounts.
Select a request deliberately; it includes real mutation examples. Nothing
in this slice executes that pack, starts a host or changes SQL/schema.

## Verification and outstanding work

Run `pwsh -File docs/attendance-gap-b/B07Verify.ps1` from the backend checkout.
It builds Release and runs the existing standalone .NET 10 report service
and repository checks plus the new compiled contract check. Every invocation
has its own artifacts path under TEMP. No test project is created. The
repository harness blocks database connections; the contract check inspects
compiled controller/DTO metadata without a host or HTTP request.

Observed red: the added service gate failed with
`FAIL: B07 readable site queues can be discovered through the existing report service`
before the method existed (exit 1). During implementation, repository SQL
translation also failed at ordering a projected record; moving ordering and
paging onto Site before projection fixed that demonstrated query defect.
The earlier queue-only combined `B07Verify.ps1` run on 13 October 2026 exited 0: Release solution
build with zero warnings/errors, 147 synthetic service/rule assertions, 24 SQL
translation assertions with zero database connections/commands, and 8 compiled
queue contract assertions. Logs are under
`%TEMP%/cosmix-b07-771bd1268e4347eca2e080def16a28ad/`:
`release-build.log`, `service.log`, `queries.log`, `contract.log`.
An initial runner build failed with NU1005 because disabling lock files
conflicted with existing lock files. The runner now uses locked restore for
the solution build; no shared lock-file edit was needed. Every build/run
used an explicit TEMP artifacts path, including the red and intermediate runs.

Remaining B07 gaps: actual generated OpenAPI/hosted authentication and denial
for discovery; genuine scoped mobile/web persistence, two-site supervisor
isolation, concurrency/restart replay and private scanner/storage/outbox
acceptance; consumer agreement on optional task/location evidence; durable
offline queue/device transfer and use of the new expired-receipt recovery contract; web/mobile UI
implementation and device/network testing. Existing response schema deployment
stays with Part A's approved SQL window. B07.T1/T2 end-to-end acceptance and
client completion are not established by these synthetic checks. No B schema,
DbContext, DI, permissions, Auth/HR, Capture or Git mutation was performed.

## Exact changed files

| File | Change |
| --- | --- |
| `src/Cosmix.Application/Attendance/SiteReports/SiteReportContracts.cs` | Queue page/label DTOs and module repository method |
| `src/Cosmix.Application/Attendance/SiteReports/SiteReportRules.cs` | Direct queue page validation |
| `src/Cosmix.Application/Attendance/SiteReports/SiteReportService.cs` | Current grant-scoped discovery using the existing access reader/operation |
| `src/Cosmix.Infrastructure/Repositories/Attendance/SiteReportRepository.cs` | Scoped count and bounded safe site projection |
| `src/Cosmix.Api/Controllers/Attendance/SiteReportsController.cs` | Documented queue discovery GET |
| `docs/attendance-completion/VerifySiteReports.cs` | Red gate, discovery behavior checks and existing adapter extension |
| `docs/attendance-completion/VerifySiteReportQueries.cs` | Scope/projection/pagination SQL translation checks |
| `docs/attendance-gap-b/B07VerifyQueueContract.cs` | New compiled route/DTO contract check |
| `docs/attendance-gap-b/B07Verify.ps1` | New isolated Release/standalone check runner |
| `docs/attendance-gap-b/B07Contracts.http` | New consumer request examples for an authorized existing backend |
| `docs/attendance-gap-b/B07ConsumerContract.md` | This contract, evidence, gaps and handoff |

`SiteReportReviewController.cs`, `VerifySiteReportsHttp.ps1`, module permissions
and registration files were inspected but not changed. All preexisting shared
changes were preserved. The new service constructor uses the existing
`IAccountAccessReader` registration; the solution compiled at that checkpoint
without edits to Part A's DI or permissions catalog. The recovery continuation
below records the later shared-source checks separately.

## Recovery continuation and handoff

13 October 2026 continuation: the recovery implementation above supersedes
the ledger's earlier B07 statement that permanent-key receipt recovery was
missing. The shared ledger remains with its coordination owner. Existing
queue discovery, permissions and registration were reused. No shared Domain,
DbContext, DI, Auth/HR, migration, Git, SQL, host or provider change was made.

Observed red before production edits: the existing service runner failed with
`FAIL: B07 own immutable submission can be reconciled by its permanent key after receipt loss`;
the repository runner failed with
`FAIL: B07 reconciliation has an employee/account/key-scoped repository query`;
the compiled controller check failed with
`FAIL: B07 authenticated own submission reconciliation endpoint exists`.
All three processes returned nonzero. The collecting shell deliberately
continued through all three failures. Red logs are in
`%TEMP%/cosmix-b07-reconcile-red-5687a68e9a0a40f49ed74065b83fc398/`.

The first recovery service green run passed 187 synthetic service/rule checks,
with its own artifacts under
`%TEMP%/cosmix-b07-reconcile-green-91a44e00c157454fb3f1a41f1ebafba8/`.
Further checks cover site-queue Return/answer/terminal status, repeated Returns,
frozen original receipt versus current accepted history, expired/missing receipt,
replacement device and changed-payload denial, exact-case/length/syntax,
employee/account isolation, revoked current access, bounded histories, protected
answer binding and no new report/receipt/file/notification staging.

The first combined Release attempt stopped before standalone checks on 12
CS9113 unread-constructor-parameter errors in the concurrent owner's
`FaceRetirementService.cs` and `FaceRetirementProcessor.cs`. Its log is
`%TEMP%/cosmix-b07-5212dd71c8df41b4a1992d2b2ff557a9/release-build.log`.
Those files were not modified by this slice. The final combined rerun below
supersedes these transient shared-source failures.

A subsequent normal focused service rerun stopped at those same diagnostics:
`%TEMP%/cosmix-b07-reconcile-focused-aef6f072990b458c95955dbefdf8e72e/service.log`.
To verify report behavior while concurrent work continued, an invocation-only
`-p:WarningsNotAsErrors=CS9113` was used for the existing runners, without
changing repository build configuration. That run passed 196 service/rule
and 32 SQL translation checks, but its controller check stopped before execution
on the parent's concurrent CS8794 pattern diagnostic in
`CaptureValidationPolicyRules.cs(54,76)`. Scoped-run logs are in
`%TEMP%/cosmix-b07-reconcile-scoped-1c2a799b2fc2467682feee5153fb1328/`.
Neither transient diagnostic was patched by the B07 owner; these partial runs
are not strict Release or hosted acceptance claims.

Final normal `pwsh -NoProfile -File docs/attendance-gap-b/B07Verify.ps1`
rerun on 13 October 2026 exited 0 after the concurrent source became coherent.
No diagnostic downgrade/override was used in that final run:

- Release solution build: zero warnings, zero errors; locked restore.
- `VerifySiteReports.cs`: 196 synthetic service/rule assertions passed.
- `VerifySiteReportQueries.cs`: 32 SQL translation/model assertions passed;
  zero database connections or commands.
- `B07VerifyQueueContract.cs`: 14 compiled queue/reconciliation controller/DTO
  assertions passed; no host, HTTP request or real authentication.

Final artifacts:
`%TEMP%/cosmix-b07-887eb1975c1e41178081c60b8d3d8800/` contains
`release-build.log`, `service.log`, `queries.log`, `contract.log`, with separate
solution/service/queries/contract build directories. Every build/run used an
explicit owned TEMP artifacts path. There is no new permanent test project.
The normal B07 runner remains unchanged and replays all four final checks.

Exact files changed by this recovery continuation:

| File | Change |
| --- | --- |
| `src/Cosmix.Application/Attendance/SiteReports/SiteReportContracts.cs` | Distinct current-detail reconciliation DTO and original employee/account/key repository lookup |
| `src/Cosmix.Application/Attendance/SiteReports/SiteReportRules.cs` | Bounded exact submission-key validation matching the existing write rule |
| `src/Cosmix.Application/Attendance/SiteReports/SiteReportService.cs` | Authorized own-read reconciliation, owner/key/bounds checks, safe current protected detail |
| `src/Cosmix.Infrastructure/Repositories/Attendance/SiteReportRepository.cs` | Employee/account/key equality lookup and reused bounded ordered detail-history loader |
| `src/Cosmix.Api/Controllers/Attendance/SiteReportsController.cs` | Authenticated no-store GET with a validated original-key header and documented current-state semantics |
| `docs/attendance-completion/VerifySiteReports.cs` | Red gate, recovery/retry/Return/queue behavior and faithful current-list history adapter |
| `docs/attendance-completion/VerifySiteReportQueries.cs` | Red gate, SQL scope/key/index/collation/bounded-history checks with connections forbidden |
| `docs/attendance-gap-b/B07VerifyQueueContract.cs` | Red gate and compiled GET/header/DTO/privacy checks |
| `docs/attendance-gap-b/B07Contracts.http` | Original-key mobile/web reconciliation requests and bounded recovery instructions |
| `docs/attendance-gap-b/B07ConsumerContract.md` | Recovery contract, evidence, acceptance limits and this exact handoff |

`SiteReportReviewController.cs` and review service/evidence behavior were
inspected and reused. Return/retry/site-queue checks exercise their current
behavior; no demonstrated additional production defect required a change.
The synthetic list adapter now includes current reviews/responses like the
real SQL repository, so queue workflow status is actually exercised.

Acceptance still requiring the appropriate owner/window:

- SQL: verify the deployed unique employee/key index and BIN2 key collation,
  response schema and original account binding; genuine authenticated
  expired/purged receipt recovery after restart, competing writes and read-audit
  persistence; two-owner/two-site isolation and current revocation/device denial;
  scanner/private-storage history and tamper failures. No such SQL was executed.
- HTTP/OpenAPI: actual registered runtime route, original-key header validation,
  bearer/cookie authorization, denial, no-store/privacy and traffic-limit behavior.
  Compiled controller metadata does not establish these hosted results.
- Mobile/web: durable original key/body/bytes/capture retention, uncertain-send
  recovery on device/network changes, 200 linkage versus unresolved 404, no
  automatic fresh-key or changed-payload retry, current Return/answer sequences
  and real UI/device testing. The request pack contains placeholders and was not
  executed; no client implementation is included.
- Notifications: genuine durable intent/outbox commit, owner-only delivery,
  restart/retry/concurrency and configured worker acceptance. Synthetic intent
  staging checks do not prove delivery. Reconciliation itself stages no intent.
