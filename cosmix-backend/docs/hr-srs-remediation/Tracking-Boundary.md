# B1 Tracking Boundary

Scope: plan task B1 / audit F01 only, existing checkout, 15 October 2026.
Source HEAD: `686d5027e79d1dfa0c2c7df2aadd7d3975e79253` plus concurrent local changes.
This document is B1 evidence, not an integrated acceptance or deployment claim.

## Contract

`GET /api/v1/hr/employees/{id}/tracking` now delegates to the existing
`AttendanceAuditService.PunchesAsync`. It requires `attendance.punches.read`,
current web MFA/session and server-resolved historical employee/date scopes,
including each retained original source site. Role/session state is rechecked
before disclosure. All, Employee, Self, Site, Department, Team and Project grants
retain the owning service's rules; a self-history permission alone does not grant
management access. Mobile self history remains `/api/v1/me/attendance/punches`.

The query is `From` inclusive, `To` exclusive (required, at most 366 days),
`PageSize` 1-100 (default 25), optional `Cursor`. Example:
`?From=2026-10-01&To=2026-10-16&PageSize=25`.
The response is `AttendancePunchAuditPage` with `items` and `nextCursor`.
Follow the cursor with identical filters, including after an empty page.
The existing audit service orders evidence by receipt time/ID ascending and
bounds each scan to 200 candidates. A successful read records the existing
scoped read audit; it does not create a punch or idempotency receipt.

This intentionally changes the unsafe legacy location response. Clients must
replace `checkInHistory`/`latestCheckIn`/`assignedLocation` handling with the
protected audit DTO and bounded query. No profile details, GPS coordinates,
distance/radius guesses, device credentials or private face/storage references
are returned. Missing assessment is `Unverified` / `ASSESSMENT_NOT_AVAILABLE`,
never `Verified`. Existing recorded outcomes are retained.

`POST /api/v1/hr/employees/{id}/tracking/record-checkin` is deprecated and
returns `410 Gone` / `ProblemDetails` with `code=tracking_capture_retired` when
the action is reached. Existing authentication and HTTP guards still apply;
anonymous/malformed/oversized requests can be rejected before the action.
The action consumes no body and performs no employee lookup, provider call,
capture, correction submission, business write, success audit or receipt write.
Unknown employee IDs and retries are equally unavailable. Its API metadata
advertises 410, with no attendance-success response.

The problem response links to these existing operations without forwarding:

- `checkIn`: `/api/v1/me/attendance/check-in`, registered mobile capture with
  existing self/device/face checks, fail-closed when unavailable.
- `checkOut`: `/api/v1/me/attendance/check-out`, existing mobile/device/sequence
  rules, no face requirement.
- `selfCorrections`: `/api/v1/me/attendance/corrections`, self proposal with
  source preview/fingerprint, written evidence and existing review rules.
- `employeeCorrections`: `/api/v1/attendance/employees/{id}/correction-requests`,
  `attendance.corrections.submit`, current web MFA/CSRF, scoped employee/date
  source preview, idempotency key and evidence. Submission is not approval or
  attendance. Ordinary cases use the authorized HR reviewer; explicit special
  alternatives retain dedicated Admin authority and configured evidence.

## Ownership And Integration

Only these B1 files are changed:

- `src/Cosmix.Api/Controllers/HR/EmployeeTrackingController.cs`
- `docs/hr-srs-remediation/VerifyTrackingBoundary.cs`
- `docs/hr-srs-remediation/Tracking-Boundary.md`

No DI hook is required: `AttendanceAuditService` and its collaborators already
have shared registrations. No shared permissions, DI, authentication, DbContext,
migration, HR Reports, payroll or vehicle files are edited by B1. Concurrent
changes remain owned by their authors. Parent owns Status and Verification.

Ruling: reuse the existing protected Attendance audit DTO rather than recreate
the legacy GPS/profile aggregation. This satisfies B1's scoped-history boundary
without a new location permission or repository. Integration cost: tracking
clients must adopt the documented query/response contract; no frontend was edited.

## Verification

Standalone runner: `docs/attendance-gap-c/VerifyManaged.ps1`. It copies frozen
release inputs to a unique managed directory and retains `inputs.json`, compiler
response/source, compile log and execution log. It does not start the API or SQL.

RED, before modifying the controller:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory 'D:\CosmixVerification\hr-srs-audit-20261015\release\bin\Cosmix.Api\release' -Sources 'docs/hr-srs-remediation/VerifyTrackingBoundary.cs'
```

Result: expected exit 1, 66/70 checks passed. Four failures: no scoped bounded
GET contract, direct database dependency, POST lacked deprecated 410 metadata,
POST still bound capture data/persistence. Service-only checks ran against the
real preexisting Attendance service; unsafe legacy controller actions were not
executed against a database. Evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-50baed5a5cee40d7b7afc6326111a95a`.
Original controller SHA256:
`196313D963BB744B0F8130AC45A48B866340ADF8BFF49149A9219EAEDFB488AE`.

GREEN, same regression source after the controller change:

```powershell
dotnet build Cosmix.Backend.sln -c Release --artifacts-path 'D:\CosmixVerification\hr-remediation-b1-20261015' -p:RestoreLockedMode=true -v:minimal -flp:logfile=D:\CosmixVerification\hr-remediation-b1-20261015-build.log
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory 'D:\CosmixVerification\hr-remediation-b1-20261015\bin\Cosmix.Api\release' -Sources @('docs/hr-srs-remediation/VerifyTrackingBoundary.cs','docs/attendance-corrections/VerifyAuditReads.cs','docs/attendance-completion/VerifyAttendanceScopes.cs','docs/attendance-corrections/VerifyCorrectionService.cs','docs/attendance-capture/VerifyCaptureService.cs')
```

Build: exit 0, zero warnings/errors. All product outputs/intermediates are under
the isolated B1 artifacts path above. Build log:
`D:\CosmixVerification\hr-remediation-b1-20261015-build.log`.

Runner: exit 0, five suites / 554 checks passed:

| Suite | Passing checks | Evidence type |
| --- | ---: | --- |
| VerifyTrackingBoundary | 129 | Real controller + service, metadata, synthetic IO |
| VerifyAuditReads | 59 | 47 service/authorization + 12 SQL translation, zero connections |
| VerifyAttendanceScopes | 23 | Historical scope evaluator |
| VerifyCorrectionService | 196 | Real intake/review with synthetic persistence/private IO |
| VerifyCaptureService | 147 | Real capture logic with synthetic proof/IO, no provider acceptance |

GREEN evidence, including all copied assembly hashes in `inputs.json`:
`D:\CosmixVerification\part-c-parent-20261013\managed-b54fa58753294373b800eb0728254659`.
The runner retains its own preexisting output root; no shared runner was edited.

SHA256 of the tested B1 source and primary assemblies:

| File | SHA256 |
| --- | --- |
| EmployeeTrackingController.cs | `2469A082AD4ADD73E21C4965895776913D003E872562EC8E51AC81CC28443935` |
| VerifyTrackingBoundary.cs (same RED/GREEN source) | `73107FE7D044C996C60427E09A2229D7D9E1EEE03ABCC0589215050C6D6E2F70` |
| Cosmix.Api.dll | `12B7026303B1F3F3220947305CDEE0C156C768034D4AA0A41CB155DD45B37237` |
| Cosmix.Application.dll | `831A58A9D214EA70CF8DF2E9478EB9E968343FF49E9693733137F1B1C2318BF8` |

The regression uses real controller/service/authorization operations with
synthetic repository, access, cursor and journal collaborators. It checks allowed
and denied employee/site/department/team scopes, All/Self/Project access, mobile,
MFA, revoked role/session, mid-read revocation, retained source scope, foreign
rows, protected/unassessed DTOs, cursor binding and bounds. Forbidden IO fails
immediately. Successful GET has a read audit; denied reads and retired POST have
no success audit/receipt/business write. API route/response/deprecation metadata
and alternative route targets are inspected in compiled assemblies.

Limits: these are service/controller and metadata regressions, not authenticated
HTTP, SQL persistence, CSRF/session middleware or generated OpenAPI acceptance.
No DB/provider calls, real face/OTP, client changes, branches, worktrees, commits,
fetch or push are part of B1. Real provider acceptance remains excluded.

Final review: author self-review of the scoped controller diff, regression and
B1 contract found no remaining issue within this slice; no independent reviewer
was dispatched. `git diff --check` for the controller passed. Only the five
listed suites were run, not the entire repository's standalone suite inventory.
B1 implementation is ready for parent integration review; authenticated HTTP/SQL
acceptance and tracking client migration remain outstanding. No DI hook is needed.
