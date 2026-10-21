# HR Attendance Setup Completion Report

13 October 2026, Asia/Karachi. Local source delivery and verification record.

## Conclusion

The approved six-task scheduling package has source implementation in the existing
backend and React web projects. Shifts, site rosters, circular site boundaries and
office rosters now have actual API-backed frontend implementations, not prepared
workspace controls. Office schedules use the employee's dated approved Office/Branch
assignment; Site Staff retains the existing site source.

This does **not** complete all seven non-mobile HR gaps. Office physical attendance,
other operational web integrations, new policy/claim/finance workflows, full reporting
and biometric production acceptance remain separate work. The model and CompreFace
deployment are currently unavailable; the PM requested inactive adapter/code readiness.

All changes remain local and uncommitted. This package did not apply SQL migrations,
start/change a database, create accounts, alter SMS/scanner/provider configuration,
activate biometric providers, modify React Native, commit or push. Existing unrelated
source changes and running applications were preserved.

## Six Task Results

| Task | Implemented | Remaining acceptance |
| --- | --- | --- |
| 1 | Actual DTO schemas, shared authenticated transport, paired permissions/scopes, subject-isolated queries, cancellation and exact in-memory mutation retries | Live cookies/CSRF/role-revocation acceptance was not executed by this package |
| 2 | Shift search/page/detail, create, rename, activation/deactivation and immutable effective-dated versions, including overnight schedules | Genuine current-build API persistence/concurrency acceptance remains separate |
| 3 | Site assignment/history/replace/end; temporary preview/create/cancel/supersede, captured timeline/staff/version tokens, stale-state and uncertain-result handling | Live locks, staff races, restart receipts and scoped SQL transactions remain unverified here |
| 4 | Actual circular boundary configuration/versions/history, precision checks, explicit approval reference/attestation, stale version protection and local circle preview | No polygon/GPS/external-map workflow; live fixed-Admin denial/persistence acceptance remains separate |
| 5 | Append-only Office roster/ending entities, restrictive mappings, audited services/repository/APIs, dated category dispatch, shift publication impact and exact retained Office leave provenance | Prepared migration must be reviewed/applied to the approved target; Office capture/calculations are not enabled |
| 6 | Office web/history/assign/replace/end, actual routes/nav, scope-aware references, narrow layouts, context-change draft disposal and package review | Isolated browser results are not real SQL/auth/provider acceptance |

## Business And Security Boundaries

- Office uses the existing Branch master, not a second office identity or fake Site.
- Office roster management uses existing All-scoped roster permissions. No Branch scope,
  automatic grant or hardcoded role-name authorization was invented.
- Site-scoped roster grants only authorize their matching Site. Shift and reference
  catalogue permissions must qualify independently; one role cannot lend another its scope.
- Changes retain the original assignment and append an effective ending/new assignment,
  actor, timestamp, reason, protected audit and idempotency evidence. History is not rewritten.
- Shared publication includes affected offices and requires current All authority there;
  retained replay authority is rechecked. In-use Office shifts cannot be deactivated.
- Dated employee transfers and overnight duty boundaries are validated. Site JSON stays
  byte-compatible when optional Office provenance is absent.
- Office leave snapshots retain exact office, roster assignment, employment assignment,
  shift version and pinned calendar. Approval rejects changed or absent provenance.
- Private forms/queries are removed on account/session/grant/resource changes. Sensitive
  bodies, tokens and keys are not placed in browser storage. Uncertain writes retain the
  exact original URL, method, body and key; changed intent requires reload/review.
- The existing login, testing OTP and API transport remain in place. Server checks remain
  authoritative; frontend controls are presentation, not a security boundary.
- Office punch/session/geofence/correction/calculation support was not manufactured by
  removing existing Site guards. Office overtime remains ineligible.

## Review Findings Fixed

1. Same-office employment-assignment changes could skip overnight publication checks.
   Impact validation now includes the exact assignment identity, not only the same office.
2. Runtime Office resolution could accept malformed global duty-window overlap.
   Full interval and overnight-boundary validation now runs before schedule use.
3. Leave approval previously retained only the shift version, which could survive an
   Office A to Office B transfer. Private snapshots now bind the exact Office source.
4. A corrupt null Office leave-source entry threw an uncontrolled exception. It now
   returns the controlled provenance conflict before source inspection.
5. Full-history validation repeatedly scanned all immutable endings at each boundary.
   A per-call validated history indexes the effective endings once; all duplicate,
   relationship, interval and overnight checks remain. No global cache was introduced.

The scale regression measured 1,024 adjacent assignments at **17,785.23 ms before**,
then **100.30 ms after**; 2,048 assignments measured **340.54 ms after**. These are this
machine's in-memory measurements, not a database latency or throughput guarantee.

## Executed Verification

| Check | Actual result | Limit |
| --- | --- | --- |
| Parent permission/schema/Office contract checks | 9 + 20 + 4 passed | Actual pure frontend code, no network/SQL |
| Shift/site/geofence helper checks | 27 passed, worker-run | Pure helpers, not live workflows |
| Shift/site/geofence browser checks | 28 passed, worker-run | Isolated transport fixtures, not genuine API persistence |
| Additional Office browser/route/schema checks | 21 browser + 13 static/schema passed, worker-run | Network blocked, empty browser storage; no product mock routes |
| Office behavior/model/scope/provenance | 52 passed after final optimization | Synthetic repositories/private IO; zero SQL/provider calls |
| Three initial independent-review repros | RED 0/3 to GREEN 3/3, final rerun passed | Actual pure resolver/impact behavior, not SQL locking |
| Corrupt null retained source | RED exception to GREEN controlled 409 | Reflection probe of actual validator; no SQL |
| Bounded Office history scale | 1,024 and 2,048 intervals passed | Local diagnostic budget, not production SLO |
| Existing migration/model/SQL | 16 passed | Exact 21 to 22 range; zero SQL connection attempts |
| Frontend `npm run verify` | Passed lint, formatting, TypeScript and production build | Vite warns about the existing combined 818.42 kB main chunk |
| Backend Release solution | Passed, zero warnings/errors | Temporary output preserves the running API; build is not acceptance |
| Whitespace review | Both repositories passed `git diff --check` | Backend emitted existing line-ending notices, not whitespace errors |
| Existing frontend server | Static page and current Office source return 200 | No login, business API writes or provider calls |

The independent whole-package review covered Office/Shifts/Leaves, Attendance web,
routes/layout, the prepared Office migration and the face slice. Findings above were
reproduced and fixed. The final focused source review found no actionable regression
in the history optimization; Timeline output and retained source identities remain
unchanged. For internal calls with both an invalid date and malformed history, error
precedence can now differ; the API resolver validates the date first. The reviewer did
not independently execute the parent benchmarks or regressions.

### Temporary Check Locations

Parent checks:
`C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{1bcd2936-e1a3-4f8c-a19b-b377fe1eec93}/AC/Temp/CosmixHrGap20261013`

Files: `attendance-access.mjs`, `attendance-contracts.mjs`, `office-contracts.mjs`,
`VerifyOfficeRosterBehavior.cs`, `VerifyOfficeReview.cs`, `VerifyOfficeLeaveCorruption.cs`,
`VerifyOfficeHistoryScale.cs` and `VerifyOfficeMigration.cs`.

Worker web checks:
`C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{627b9e45-2adc-46cc-b11a-306125d99142}/AC/Temp/cosmix-operational-web-checks`

Files: `checks.cjs`, `browser-checks.mjs`, `office-browser-checks.mjs`,
`office-contract-route-checks.cjs`, `browser-harness.tsx`, `build-browser.mjs`.
These remain outside Git. They use synthetic fixtures only inside isolated harnesses;
the application contains actual endpoint integration, not fixture APIs.

## Face Proof Of Concept

The existing actual CompreFace adapter and ONNX C# liveness service are retained.
No Python microservice, cloud API, fabricated biometric success, guessed threshold,
downloaded model or production provider activation was added.

Five new regressions were reproduced and fixed: retention clock rollback, a retained
use boundary longer than current approved duration, release at exact expiry, HTTP
cancellation at final EOF, and a body completing after its deadline. Existing checks
passed: **66 lifecycle, 122 provider/semantic and 103 capture**, plus the five new
regressions. Provider handlers and non-face PNGs were synthetic; no actual ONNX model
inference, CompreFace accuracy or genuine camera/liveness test was executed.

Check-In still requires both server-side liveness and matching, with active enrollment,
device/session/challenge binding, expiry, replay checks and single-use proof protection.
Check-Out has no face step. Missing provider/model/authority remains fail-closed.

Still required: approved actual artifact/license/hash/preprocessing/output semantics,
company-controlled deployment and real device/spoof/outage testing. Automatic retirement
after committed replacement/revocation needs a durable disposition intent. Authorized
physical deletion and backup/key retention need a separately reviewed vault contract
and policy. A retirement/use-denial journal is **not proof of ciphertext deletion**.

See [the detailed face verification record](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/docs/attendance-capture/Face-PoC-Completion-20261013.md).

## Integrated API Contracts

Paths below are relative to the existing `/api/v1` API base.

- `GET/POST /hr/shifts`; `GET/PUT /hr/shifts/{id}`;
  `POST /hr/shifts/{id}/status` and `/versions`.
- `GET/POST /hr/sites/{siteId}/shift-assignments`;
  `POST .../{assignmentId}/replace` and `/end`.
- `POST /hr/sites/{siteId}/temporary-shift-changes` creates a change; history is
  returned by the site's roster history, not an invented standalone temporary list.
  GET preview reads under that root are `/preview`,
  `/{changeId}/cancel/preview`, `/{changeId}/supersede/preview`; writes are create,
  `/{changeId}/cancel` and `/{changeId}/supersede`.
- `GET /hr/sites/{siteId}/geofences`, `/versions`, `/versions/{versionId}`, `/history`;
  `POST .../versions`.
- `GET/POST /hr/branches/{branchId}/shift-assignments`;
  `POST .../{assignmentId}/replace` and `/end`.
- Permission-qualified real references: `GET /hr/branches`, `/hr/sites`,
  `/hr/holiday-calendars`, `/hr/shifts` and `/hr/shifts/{id}`.

No individual/team roster precedence, polygon editing, month reopening, new approval
chain, automatic financial posting or new management privilege was introduced.

## Prepared Migration

An equivalent existing migration was found and reviewed rather than creating a duplicate:

- Starting migration: `20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence`.
- Ending migration: `20261013101327_AddOfficeRosterHistory`.
- Source: `src/Cosmix.Infrastructure/Database/Migrations/20261013101327_AddOfficeRosterHistory.cs`
  and its Designer; current `CosmixDbContextModelSnapshot.cs` agrees with the model.
- Reviewed SQL: `docs/sql/20261013101327_AddOfficeRosterHistory.sql` exactly matches EF output.
- Up creates two attendance history tables, four restrictive foreign keys and four
  indexes, including branch/start and one-ending uniqueness. No business seed or
  existing-table/data removal is present.
- Down refuses retained history before removing empty tables in dependency order.
  This is not a populated-database recovery plan.

This package only inspected/generated-in-memory SQL evidence, with connection interception.
It did not apply the migration or inventory current database history. Separate prior
laptop SQL records are not a current target verification by this package. Before any
application confirm exact server/catalog/history, reviewed source, backup and deployment
configuration. The running API was not restarted; source delivery is not deployed acceptance.

## Files Delivered Or Coordinated

Frontend under `cosmix_frotned`:

- `src/modules/attendance/api/attendance-api.ts` and public `index.ts`.
- `schemas/common.ts`, `shifts.ts`, `site-rosters.ts`, `site-geofences.ts`, `office-rosters.ts`.
- `hooks/use-attendance-access.ts`, `use-attendance-action.ts`, `use-attendance-lifetime.ts`,
  `use-attendance-query.ts`; `utils/access.ts`, `messages.ts`, `shift-schedule.ts`,
  `roster-intent.ts`, `geofence-input.ts`.
- Pages: `ShiftsPage.tsx`, `SiteRostersPage.tsx`, `SiteGeofencesPage.tsx`, `OfficeRostersPage.tsx`.
- `components/SchedulingUi.tsx`, `References.tsx`; shifts `ShiftList.tsx`, `ShiftDetail.tsx`,
  `ShiftScheduleForm.tsx`, `ShiftMetadataForm.tsx`; rosters `SiteRosterHistory.tsx`,
  `SiteRosterForm.tsx`, `SiteTemporaryChangeForm.tsx`, `TemporaryAmendmentForm.tsx`,
  `OfficeRosterForm.tsx`; geofences `CirclePreview.tsx`, `SiteBoundaryForm.tsx`,
  `SiteBoundaryHistory.tsx`.
- Shared edits: `src/app/router/AppRoutes.tsx`, `src/layouts/AppLayout.tsx`,
  `src/hooks/use-dirty-form.ts`, `src/styles/attendance.css`, module README.

Backend under `cosmix-backend`:

- Domain `Attendance/Rosters/OfficeRosterAssignment.cs`, `OfficeRosterAssignmentEnd.cs`.
- Application `Attendance/Rosters/OfficeRosterContracts.cs`, `OfficeRosterSchedule.cs`,
  `OfficeRosterService.cs`, `OfficeRosterImpactValidator.cs`, `OfficeRosterResolver.cs`,
  `EmployeeRosterResolver.cs`; optional Office provenance in `RosterContracts.cs`,
  `SiteRosterContracts.cs`; shared `Shifts/SchedulingRegistration.cs`,
  `ShiftVersionPublication.cs`, `ShiftService.cs`.
- Application `HR/Leaves/OfficeLeaveScheduleSource.cs`, `LeaveProvenance.cs`, `LeaveService.cs`.
- Infrastructure `Repositories/Attendance/OfficeRosterRepository.cs`, `ShiftRepository.cs`,
  `SchedulingRegistration.cs`, `Database/Configurations/Attendance/OfficeRosterConfigurations.cs`,
  and `Database/CosmixDbContext.cs` registration.
- API `Controllers/Attendance/OfficeRostersController.cs` and My Shift documentation.
- Face changes: Application `RetentionControlledFaceTemplateStore.cs`; Infrastructure
  `EncryptedFaceStorageRetentionJournal.cs`, `CompreFaceClient.cs`.
- Existing equivalent Office migration/snapshot/SQL reviewed; other shared migrations preserved.

The larger dirty working trees include unrelated earlier work. This inventory is not
an assertion that every modified file in either repository was created by this package.

## Seven Original Gaps And Next Work

| Original gap excluding Mobile | Current position | Next implementation or acceptance |
| --- | --- | --- |
| Operational Attendance web | Scheduling subset implemented and offline-tested | Wire actual daily/register/calendar, corrections, OT, policies, ordinary leave, zones/site reports, notifications and appropriate reports |
| Real face acceptance | Actual adapter/services hardened; model/deployment absent, PM requested inactive code readiness | Committed retirement/disposal contract and approved lifecycle policy, then genuine model/provider/device acceptance |
| Office schedules and time | Dated Office roster and ordinary leave source protected | Office boundary/punch/session/correction/calculation provenance; transfer/overnight acceptance; no fake Site fallback |
| KPI and extended leave/benefits | Not implemented by this package | Approved typed KPI/waiting/studying/make-up/benefit/encashment/recovery rules and evidence lifecycles |
| Claims and financial services | Not implemented by this package | Operational meal/outstation/fuel/mileage/maintenance/custody/advance/loan/repayment flows; models alone are insufficient |
| Payroll-ready closure | Existing financial/readiness guards preserved | Exact OT consumption, financial carry-forward, applicable legal activation and partial/reversal/deduplication acceptance; retain existing no-charge handoff |
| All fourteen reports and post-hire lifecycles | Not completed by this package | Real producers plus filtered exports; asset/SIM/certification issue/return/renewal/expiry workflows |

The immediate next supported frontend package is Attendance operations against existing
contracts. Office physical attendance is the next backend dependency. New policy/financial
modules require the specific management decisions listed in
[the retained work order](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/docs/HR-Gap-Closure-2026-10-13.md); explicit SRS amounts remain authoritative,
but unspecified applicability, authority and lifecycle are not invented.

## Local Application

Existing frontend URL: **https://localhost:5173**. Current Office page source and the
static root were read-only verified at that server. Authentication remains mandatory.
Scheduling routes begin at `/hr/attendance/shifts`, `/hr/attendance/sites`,
`/hr/attendance/geofences`, `/hr/attendance/offices`.

From `C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned`, run
`npm.cmd run dev` when no frontend server is already running. Do not start a second
process on an occupied port. The existing launcher uses the approved local HTTPS
certificate and API-origin configuration. Live Office use additionally needs the
reviewed schema/current backend and existing authorized account; no testing bypass
or automatic provider/account setup was added.
