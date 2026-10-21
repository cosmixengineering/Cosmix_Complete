# Scoped raw punch and session revision audit

SRS section 15's raw-punch audit needs management visibility separate from the existing employee
`/me` history. Two read-only routes now expose bounded factual evidence:

| Route | Result |
|---|---|
| `GET /api/v1/attendance/employees/{employeeId}/punches` | Received raw punches and their current assessment identity, revision, timestamp and outcome |
| `GET /api/v1/attendance/employees/{employeeId}/sessions` | All immutable session revisions, recorded timestamp, correction identity and `isLatestRevision` label |

Both require the explicit sensitive management permission `attendance.punches.read` and a current
web MFA session. Existing self history, correction review or reports access does not grant this
permission. The catalog and DI registrations make the routes available; no account, bootstrap
owner or role is automatically granted access. Existing access delegation remains authoritative.

## Safe evidence and meaning

Punch reads retain Accepted, Rejected, Unverified and offline evidence. Missing assessment is
explicitly Unverified with `ASSESSMENT_NOT_AVAILABLE`; this is not attendance success. The raw
record's server receipt and recorded business date are authoritative. A legacy record without
a business date uses its received Asia/Karachi date and is labelled `legacy_received_date`.
`claimedDeviceCapturedAtUtc` is untrusted employee-device evidence for review and never replaces
server receipt, business date or scope selection. GPS coordinates/accuracy, device credentials,
biometric proofs, risk codes and private-storage references are absent from the API DTO.

Session history deliberately includes superseded open, closed and corrected revisions. Use
`sessionKey` and `revision` to follow a duty's interpretation history, and `isLatestRevision`
to identify its current revision. **Do not sum audit revisions as separate worked duties.**
An open revision has no completed duration. Closed elapsed minutes are factual, not payable.
The existing factual dashboard remains latest-revision-only. A manual correction may have no
raw punches and retains its explicit correction ID; no face or successful punch is fabricated.

Reading either route adds an audit event through the existing authorization transaction. It never
approves attendance, changes raw evidence, closes duty, reopens a month, or calculates pay.

## Retained office and capture-validation trace

The 13 October bounded audit change copies `branchId`, `officeRosterAssignmentId` and
`officeGeofenceVersionId` directly from the retained raw punch, using the capture DTO's optional
fields. Missing office facts remain absent; no current Head Office, roster or boundary is substituted.
Historical site rows retain their existing JSON shape when those fields and validation are absent.

An existing final capture-validation record is exposed as `validation`: evidence ID, capture-policy
version/revision/fingerprint, codes and related raw IDs. Its employee/raw/action/offline/device/request,
receipt and final assessment time/outcome/reason must agree with the exact retained raw and original
assessment. Invalid chronology, policy metadata or structured trace values deny the row. A later
assessment cannot borrow the original capture's validation. Missing legacy validation remains null;
it is never converted into approval. Protected references, device identity and private observation
codes are not exposed.

Evidence is read only after historical row authorization. Caller cancellation is checked after the
await; current session and grants are checked again before adding rows and returning the page.
Known corrupt final evidence is omitted through the existing source-unavailable path. Storage outages
propagate instead of producing a successful empty report. Existing source scopes, scan limits,
keyset ordering and cursor bindings remain in force.

`AttendanceAuditService` additionally requires `ICaptureValidationEvidenceRepository`,
`ISchedulingJournalRepository` and `TimeProvider`. Existing scoped/singleton registrations supply
them; this slice changed no shared registration. Standalone fixtures that directly construct the
service must supply those dependencies explicitly.

The feature-local TEMP copy of `VerifyAuditReads.cs` reproduced both missing mappings before the
source change, then passed **85 service/authorization checks and 12 SQL translation checks**.
Regression source and assembly-only runner:

- `C:/Users/Huma.Khan/AppData/Local/Temp/Cosmix-Readonly-Face-Audit-a844a7463aca4655a417e17da2a83fbf/AuditTraceRegression.cs`
- `C:/Users/Huma.Khan/AppData/Local/Temp/Cosmix-Readonly-Face-Audit-a844a7463aca4655a417e17da2a83fbf/Checks.csproj`

The Application Release compilation passed. The combined solution attempt stopped in concurrent
`FoundationPolicyRepository.cs` work with CS1729 (`HrFoundationPolicyCandidate` constructor mismatch).
The runner used that freshly compiled Application/Domain and existing Infrastructure/runtime
assemblies. This is bounded offline service/query verification, not combined deployment acceptance.
No SQL connection, HTTP, private file, provider, mobile or schema operation was performed.

## Scope, bounds and continuation

Use `from` inclusive and `to` exclusive, maximum 366 days, and `pageSize` 1–100 (default 25).
Before selecting candidates, the service requires authority for the complete historical
employee/date range. For a narrow site grant, request the period the employee was assigned there.
An unavailable historical range returns `attendance_audit_source_unavailable`; it does not become
an empty successful report.

Every returned row also rechecks its original retained site and source dates. A session's business
date/site and each referenced Check-In/Check-Out's date/site must be authorized together, including
an overnight transfer beyond the requested business date. Retroactive assignment changes cannot
substitute authority over the original raw site. A source with no retained site requires explicit
employee/All authority. Missing, foreign, swapped or malformed linked source evidence is omitted
fail-closed, as are rows whose original source scope is no longer authorized. Unexpected server
failures propagate.

Each call selects at most 201 candidates and examines at most 200 before returning up to the
requested page size. There is no total count. Punches order by immutable received timestamp/ID;
session revisions order by immutable recorded timestamp/ID. The existing protected cursor binds
account, route purpose, employee and exact filters/page size. It grants no permission; every page
reauthorizes the current session and scopes. An empty page may have `nextCursor` when all scanned
rows were omitted. Continue until it is null. These live reads are not frozen export snapshots.

```http
GET /api/v1/attendance/employees/{employeeId}/punches?from=2026-10-09&to=2026-10-10&pageSize=25
GET /api/v1/attendance/employees/{employeeId}/punches?from=2026-10-09&to=2026-10-10&pageSize=25&cursor={nextCursor}
GET /api/v1/attendance/employees/{employeeId}/sessions?from=2026-10-09&to=2026-10-10&pageSize=25
```

## Verification and remaining acceptance

On 12 October 2026, `docs/attendance-corrections/VerifyAuditReads.cs` passed **46 service/authorization
checks and 12 SQL translation checks**. It runs the actual service and authorization boundary
with synthetic in-memory collaborators, then translates the actual EF SQL Server queries using a
connection-blocking interceptor. It verifies mixed raw outcomes, claimed/device versus server time,
self-versus-management permission, MFA/revocation, original and transferred sites, source mismatch,
all session revisions without aggregate totals, malformed history, bounded continuation and input
validation. SQL translation checks inspect employee/date/source ownership, latest assessment,
revision labels and keyset limits. The final executed counts are in the continuation ledger.

No fixture, database connection, migration, schema change, private file or real account is needed
for this verifier. It does not prove authenticated HTTP/SQL execution, competing revocation,
production query plans or load performance. Those checks need the separately approved isolated
acceptance setup. The root anonymous HTTP/Release pass is recorded separately.
