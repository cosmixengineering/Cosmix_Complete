# Operational site reports

Original intake was implemented locally on 9 October 2026. On 12 October the user
confirmed permission-controlled HR Accept/Return/Reject, no self review, and an
employee response/resubmission flow. The completed local workflow uses immutable
`SiteReport`, `SiteReportReview` and the new protected `SiteReportResponse` history.
The response table requires the root-owned incremental migration before SQL use;
this feature implementation did not write any database. No fixed HR role or automatic grant exists.

| Method and route | Permission and scope |
| --- | --- |
| `POST /api/v1/site-reports` | `attendance.site-reports.self`; current mobile session and its verified registered device |
| `GET /api/v1/site-reports/mine` | Same permission; own employee only, mobile or web |
| `GET /api/v1/site-reports/mine/{id}` | Same permission; own detail/review history |
| `GET /api/v1/site-reports/mine/{id}/evidence` | Same permission; audited own private attachment |
| `GET /api/v1/site-reports/queue?siteId=...` | `attendance.site-reports.read`; web MFA and exact Site or All scope |
| `GET /api/v1/site-reports/sites/{siteId}/{id}` | Same scoped web permission |
| `GET /api/v1/site-reports/sites/{siteId}/{id}/evidence` | Same scoped web permission; audited private attachment |
| `POST /api/v1/site-reports/sites/{siteId}/{id}/decisions` | `attendance.site-reports.decide`; HR custom-role permission, web MFA/CSRF, exact Site/All scope |
| `POST /api/v1/site-reports/mine/{id}/responses` | `attendance.site-reports.self`; original employee/account, protected written answer and optional scanned attachment |
| `GET /api/v1/site-reports/mine/{id}/responses/{responseId}/evidence` | Same self permission; original report/response binding |
| `GET /api/v1/site-reports/sites/{siteId}/{id}/responses/{responseId}/evidence` | `attendance.site-reports.read`; scoped web MFA |

Access is explicitly provisioned through existing custom-role APIs. No
permission is seeded or automatically granted. `AttendanceOperation` rechecks
current sessions and grants inside the existing authorization transaction.
Employee/device comes from the session; requests cannot supply employee,
account, device, server timestamp, private storage reference or reviewer.

Received reports are immutable. Kinds are Arrival (1), Departure (2), Delay
(3), AccessIssue (4), Incident (5). The server pins the unambiguous historical
HR site-assignment record. Online context uses server time in Asia/Karachi.
Offline reports require the original device timestamp and may refer to a
previous site through history; claimed time/location remain **unverified**.
Future captures, captures before device registration and ambiguous assignments
are rejected. Inactive sites retain historical offline evidence but cannot
receive current online reports. TaskReference is claimed context, not proof of
an independently assigned task; no task authority system is fabricated.

201 means **received**, not attendance/pay/review accepted. Before a review,
`submissionStatus` is `received_pending_review`; `evidenceStatus` is
`received_unverified_evidence` or `unverified_offline_evidence`;
`reviewWorkflowStatus` is `hr_review_pending`. Existing review trails
remain readable. Reports never modify punches, work sessions or payroll,
including locked periods.

Fresh writes persist business data, protected audit and receipt atomically.
They also stage a durable in-app confirmation only for the submitting account;
the immutable report ID is its deduplication identity. No supervisor recipient
is invented. The outbox worker remains explicitly configuration-gated; staging
does not mean a user notification has already been delivered.
Exact retries recheck current access before returning the saved result; changed
payload/device context conflicts. Permanent employee/key uniqueness prevents
duplicate submission after receipt expiry/removal. Reports have no edit/delete
operation. Review and response writes require both expected review and response
sequences from fresh detail; missing/stale/competing versions cannot overwrite history.

## HR review and employee response

The access module assigns `attendance.site-reports.decide` to the approved HR custom
role. A role's name is not trusted as authority. Read permission alone cannot decide;
the requester account and beneficiary employee cannot decide their own report even
if they also hold the decision permission. One authorized HR reviewer decides.

`Accepted=1` and `Rejected=3` complete the review. `Returned=2` requires an employee
answer before another HR decision. `Escalated=4` remains unsupported because no
escalation chain was approved. Returned reports are not accepted merely by opening
them or reusing a decision endpoint.

The original submitter sends `returnedReviewId`, `expectedReviewSequence`,
`expectedResponseSequence` and `writtenResponse`. The answer is encrypted with
report/employee/return/response binding; optional PNG/JPEG/PDF follows the existing
scanner and private-storage requirements. New capture time, coordinates and site
identity cannot be supplied in this response. The immutable original report stays
unchanged, and the report returns to `received_pending_review`/`hr_review_pending`.
Another HR Return can receive another immutable response. Accepted/Rejected cannot
be reopened through this flow.

Details include the entire bounded review/response trail. A queue uses only latest
review/response metadata to derive state efficiently. `reviewSequence` and
`responseSequence` are concurrency tokens, not editable versions. Unique review
and response sequences plus one response per Returned review protect competing writes
inside the shared authorization transaction. The history is capped at 256 reviews
and 256 responses; it never silently truncates a detailed history.

Review/response, protected before/after audit, retry receipt and an in-app owner
notification are staged atomically. A response confirmation goes only to its author;
no unapproved HR broadcast recipient list is invented. A retry with the same key and
payload returns its original result after current access checks; changing body or
route identity conflicts. Expired/removed receipts cannot cause another decision or
answer with the old expected sequences. Reading accepted operational evidence does
not validate its GPS, face, claimed timestamp, punches or salary.

Lists filter employee/site in SQL before pagination. From/To are inclusive
**server-received** dates in Asia/Karachi, at most 366 days; PageSize is 1–100.
Detail/download queries constrain both report ID and authorized owner/site.

## Evidence and operating dependencies

Optional inline base64 PNG/JPEG/PDF is limited to 10 MiB. Names, extensions,
media types and signatures are checked, then the existing content scanner must
return Clean. Rejected content returns 400; scanner unavailability returns 503
without successful report creation. No fake scanner success exists at runtime.

Raw bytes and a small metadata envelope are separately encrypted through the
private store with employee/key-bound purposes. The envelope preserves scanner
version/time and the protected file reference. Client DTOs never expose storage
references. Downloads reauthorize, audit, verify integrity and force attachment
with no-store/nosniff. Reports without attachments do not need a scanner, but
protected audit/receipt storage remains required.

For this low-volume slice the scanner's configured bounded timeout runs during
the fresh write transaction. Production latency/load acceptance is outstanding.
Objects written before a later SQL failure can remain encrypted orphans, as in
existing HR evidence; approved retention/reconciliation must handle them.

## Client examples

```http
POST /api/v1/site-reports
Authorization: Bearer <mobile-access-token>
Idempotency-Key: report-device-event-0001
Content-Type: application/json

{
  "siteId": "<assigned-site-uuid>",
  "kind": 1,
  "notes": "Arrived at the assigned site entrance.",
  "taskReference": "task-014",
  "latitude": 24.8607,
  "longitude": 67.0011,
  "accuracyMeters": 8.50,
  "isOffline": false
}
```

Offline clients retain the same key, original content and DeviceCapturedAtUtc
while showing Pending Sync. On a server ID, show Received / Pending Review,
not verified attendance. Retry a lost response with identical content; do not
hide key conflicts by silently choosing another key.

```http
GET /api/v1/site-reports/mine?from=2026-10-01&to=2026-10-31&page=1&pageSize=25
Authorization: Bearer <mobile-access-token>
```

Web reviewers use the existing HTTPS browser cookies/MFA and CSRF headers.
Read permission does not imply decision authority.

```http
POST /api/v1/site-reports/sites/{siteId}/{reportId}/decisions
Idempotency-Key: report-return-0001
Content-Type: application/json

{"expectedReviewSequence":0,"expectedResponseSequence":0,"outcome":2,"reason":"Please explain the access delay."}

POST /api/v1/site-reports/mine/{reportId}/responses
Idempotency-Key: report-answer-0001
Content-Type: application/json

{"expectedReviewSequence":1,"expectedResponseSequence":0,"returnedReviewId":"<returned-review-id>","writtenResponse":"The gate was blocked; the site contact was informed."}
```

Fetch detail again before the next HR decision. It includes `responses` and both
current sequences. Use outcome 1 or 3 to finish, with a reason and fresh tokens.

## Actual verification

- Latest continuation: `VerifySiteReports.cs` freshly passed **119** synthetic
  service/rule assertions on 12 October. These include HR Return → employee answer
  → HR Accept, Rejected terminal state, returned-review ID exposure, immutable
  history, self-review and wrong-site denial, web MFA, stale sequences, retry
  deduplication, changed-key payload conflict, scanner fail-closed behavior,
  response attachment scope, protected-envelope binding and staged rollback.
  `VerifySiteReportQueries.cs` freshly passed **14** exact repository SQL-translation
  checks, including latest response metadata and bounded report/employee response
  history. The query harness blocks every database connection/command.
- Historical intake checkpoint: `VerifySiteReports.cs`: **75** synthetic service/rule assertions using the
  real shared operation/evaluator with in-memory adapters: identity/device,
  MFA/site scope, session failure, revoked replay access, duplicate protection,
  offline history/ambiguity, strict validation, scanner failure, private
  attachment roundtrip, self-notification staging/retry and staged transaction rollback. Genuine login, SQL,
  encryption and scanner adapters are not exercised by this harness.
- `VerifySiteReportQueries.cs`: **11** exact repository SQL-translation checks:
  owner/site/date filters, stable pagination, bounded assignment ambiguity and
  latest review. Zero SQL connections/commands.
- `VerifySiteReportsHttp.ps1`: **34** real registered OpenAPI/anonymous HTTP
  assertions across all seven routes: 401, correct client schemes, strict
  identity-free schema, retry header and explicit statuses. Transient local
  host, disabled authentication/providers/workers, unreachable DB target, no
  account grants/fixtures. Only the owned host was stopped.
- Release solution build at slice checkpoint: **0 warnings, 0 errors**.

The first service gate failed before implementation. Behavioral tests later
caught missing Application length/page guards: constructor-parameter annotations
alone do not validate direct service calls. The added guards passed. Sandbox
loopback/DataProtection restrictions blocked the initial HTTP attempt; approved
unsandboxed localhost verification then passed without expanding SQL scope.

The 75/11/34 counts are the historical intake checkpoint. The updated HTTP harness freshly
passed **94** checks across all eleven routes and strict decision/response contracts after the
integrated Release build, using an owned localhost host with disabled providers/workers and an
unreachable database target. It verifies registered contracts and anonymous denial, not genuine
authenticated HR review or SQL acceptance.
Outstanding: applying the response migration to the separately approved isolated
setup, genuine mobile/web authenticated SQL acceptance, competing writes/restart
replay, real scanner/report-storage acceptance and outbox delivery. No live HR
notification recipient list or delivered notification is implied.
