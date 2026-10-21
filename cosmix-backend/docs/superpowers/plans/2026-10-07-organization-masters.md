# Organization Masters Implementation Plan

> Execute the confirmed Organization Masters slice locally, with focused tests outside the repository. No commit or push is authorized for this new slice.

**Goal:** Deliver usable, permission-controlled organization reference APIs before employee onboarding.

**Architecture:** Existing four .NET projects, one DbContext, module-specific organization repository/services. Reuse current web-session validation, permission grants, transaction boundaries and audit metadata. No generic cross-module CRUD framework.

**Tech Stack:** .NET 10, C#, EF Core and SQL Server.

**Spec:** SRS v1.2 sections 5, 6.1, 9 (LEAVE-010), 12, 13 and 16, plus PM decisions in this conversation on 7 October 2026.

## Confirmed Boundaries

- Office means a physical company office/branch; multiple offices and multiple sites are separate.
- Employee onboarding will offer Office Staff or Site Staff. HR selects an office only for Office Staff; HR cannot select a site for Site Staff.
- Admin creates sites and assigns/reassigns staff with history. Originally deferred together; the PM's explicit continuation below brings only Site master APIs into this slice. Employee assignment schema changes remain subsequent work.
- Organization administration uses explicitly assigned web permissions with current MFA. Ordinary HR master changes use the recommended permission-controlled direct-write flow; no unconfirmed approval matrix is fabricated.
- Employment type, designation and access roles remain distinct. New selectable staff category codes are OFFICE and SITE; existing historical records are not rewritten.
- Reuse existing master tables. Codes are immutable, updates carry the current SQL rowversion, and records deactivate/reactivate instead of physical deletion.
- No frontend, real identities, seeded accounts, login bypass, SMS provider enablement or test project inside this repository.

## Data and API Design

- Departments, designations, branches, cities, staff categories and employment types expose bounded list/detail/create/update/status operations under their existing `/api/v1/hr` names.
- Use explicit endpoint groups and typed request validation. Branches use an active CityId for new/changed city references; legacy optional city references remain readable.
- Masters are global reference data: both reads and writes require the respective All-scope permission. This does not grant employee-record access.
- Add `hr.organization.manage`, `hr.holidays.read`, `hr.holidays.manage` to the supported catalog. Extend only the explicitly enumerated first-owner grants; no silent mutation of existing assigned roles.
- Authenticated mutations and audit/idempotency persistence commit together. Retry keys are account+operation scoped, bind a canonical SHA-256 payload and replay the saved response only after current authorization passes. Expired keys are not recycled.
- Reuse IdempotencyReceipt. Add one append-only OrganizationChange row per successful mutation for safe, bounded before/after response snapshots and the existing AuditEvent reference.
- Holiday calendars contain effective-dated notification-backed holidays. Published snapshots are immutable. A correction/replacement creates a next revision for the same code and date interval, referencing its predecessor; changing/deactivating a revision never rewrites historical evidence or recalculates attendance.
- Different intervals of one calendar code cannot overlap. Replacement cannot branch from an outdated revision. Calendar dates/holiday dates are business dates, not UTC instants. Notification references remain private metadata, not public file URLs.
- General file upload/scanning, employee reporting lines, policy calculations, calendar-to-roster selection and approval workflows are separate later slices. No undocumented automatic applicability or recalculation is introduced.

## Tasks and Verification

### 1. Master lifecycle rules
- [x] TEMP failing checks for update/status/immutable code/invalid input and no partial mutation.
- [x] Add focused methods to the six existing Domain masters; no schema change for these methods.
- [x] Verify domain checks and existing behavior.

### 2. Holiday revisions
- [x] TEMP failing checks for first/replacement revision identity, immutable history, interval preservation and bounds.
- [x] Implement HolidayCalendar revision metadata and restrictive EF mapping; preserve existing rows as revision 1.
- [x] Verify mappings and append-only enforcement without modifying existing migrations.

### 3. Audited organization services and persistence
- [x] TEMP failing checks for feature availability and denied/invalid/retry/conflict behavior.
- [x] Add `Application/HR/Organization` DTOs, repository contract and service; corresponding Infrastructure repository and bounded audit entity/configuration.
- [x] Add HTTP controllers and DI; extend Department reads compatibly (including version/sorting).
- [x] Verify current web identity, All-scope permission, reauthorization before retry, duplicate codes, inactive cities, rowversion conflicts and atomic rollback.

### 4. Migration and end-to-end backend checks
- [x] Release build and EF model/snapshot comparison.
- [x] Generate/review one additive incremental migration and explicit SQL range; never rewrite shared migrations.
- [x] Inspect the actual configured development database before any integration writes; no system/production target.
- [x] Exercise real SQL uniqueness/concurrency/idempotency/audit where available using bounded synthetic fixtures and exact cleanup; report limitations honestly.
- [x] Actual HTTP/OpenAPI checks for all routes, validation, cookies/CSRF, 401/403, mobile denial and supported statuses.
- [x] Rerun focused authentication regression checks; independent final review.
- [x] Update setup/API/coverage/verification documentation with exact results and remaining dependencies.

## Review Focus

1. Valid session revoked between authorization and write: recheck current state inside the shared auth transaction.
2. Duplicate retry key after edits or permission revocation: compare original request hash and reauthorize before replay.
3. Case/space variants and conflicting city fields: normalize deterministically and reject unsupported inputs.
4. Two simultaneous updates/revisions: rowversion and unique successor checks, without overwriting history.
5. Holidays outside interval or overlapping intervals: reject ambiguous input; never silently choose payroll rules.

## Execution Record

- Baseline: develop commit 966bfbd; working tree clean. Fresh baseline Release build passed in preceding analysis (0 warnings/errors).
- Work branch: codex/organization-masters in the existing requested checkout. Preserve the user's Visual Studio setup and existing database.
- User approved starting implementation after reviewing the module boundaries and clarifying Office/Site ownership. No additional frontend or whole-HR scope is inferred.
- Organization slice implemented and independently reviewed; findings reproduced and fixed. Final evidence:
  150 master-domain checks, 37 holiday/model checks, 571 service checks, 789 HTTP assertions across
  41 operations, 40 SQL workflow checks, 8 schema checks, plus web/fixed-role/mobile regressions.
- Eighth migration applied only to the original development database after a checksum-verified backup.
  Exact synthetic cleanup confirmed original row counts across 124 tables. No owner/account was created.
- Read `../../Organization-Masters-Verification.md` for substitutions and unverified production/provider
  dependencies. Read `../../Organization-Masters.md` for current contracts and deferred employee/site work.
- Code remains local; no commit or push performed.

## Approved Site Continuation

PM selection: Organization settings + Admin Site create/edit APIs; employee assignment with onboarding.

- [x] Add six Site master endpoints using existing Site rows and shared audited transactions.
- [x] Require paired current fixed Admin/SuperAdmin and All-scope manage permission for writes/history;
  explicitly permitted HR can read. Do not grant access through a supervisor field.
- [x] Validate city/project/supervisor references, dates, rowversions and retry keys; no assignment/geofence changes.
- [x] Reproduce/fix future-join and effective-suspension supervisor eligibility with real SQL checks.
- [x] Verify Domain 36, feature contracts 5, operation gate 29, HTTP 277 and SQL 33 checks; rerun prior
  Organization and authentication regressions. Original database remains eight migrations with no model drift.
- [x] Independent narrow review and updated API/verification guides. No commit/push or frontend work.

Employee module follow-up: implement approved Office/Site onboarding and Admin assignment/reassignment
history; reconcile mandatory BranchId without creating a fake office for Site Staff. Do not treat Site
master completion as attendance eligibility, geofence configuration or full Employee-module delivery.
