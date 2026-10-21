# HR Attendance Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver actual shift/site-roster/site-geofence web workflows and an approved office-wise roster source without changing site history or claiming complete office attendance/payroll.

**Architecture:** Existing four-project Clean Architecture backend, one EF context, module-specific repositories and immutable schedule evidence. Existing React authentication/transport/frame; active scheduling belongs in the Attendance module and is composed through its public exports.

**Tech Stack:** .NET 10/C#/SQL Server/EF Core Code First; existing React 19/TypeScript/Router/TanStack Query/Zod/Lucide/Vite stack. Do not introduce a new framework or dependency for this package.

**Spec:** [Approved Attendance setup design](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/docs/superpowers/specs/2026-10-13-hr-attendance-setup-design.md).

Status: the PM requested continuous implementation after this plan. All six tasks have
source implementation; final independent review and the dated completion report record
the verification boundary. Live Office SQL/API acceptance is not inferred from source
or synthetic checks. No migration was applied by this package.

| Task | Implemented scope | Verification boundary |
| --- | --- | --- |
| 1 | Actual contracts, shared transport, paired scopes, context isolation and exact retries | Nine access, twenty schema and four Office response assertions; no network/SQL |
| 2 | Shifts list/detail/name/status and immutable schedule publication | Focused helper and isolated browser checks; genuine API persistence acceptance remains separate |
| 3 | Site assignments, endings, replacement and temporary amendments | Captured intent, server tokens and history; isolated browser checks are not SQL transaction acceptance |
| 4 | Actual circular boundary versions and retained approval references | Precision/attestation/stale-input checks; no GPS, polygon or fabricated management authority |
| 5 | Office entities, restrictive mappings, audited APIs, dated resolver and shared publication/leave protection | Fifty-two offline Office assertions; three reproduced review regressions fixed; sixteen offline migration/script checks |
| 6 | Office web/routes/navigation plus final package review and handover | Build/browser results and remaining runtime acceptance are recorded in the dated report |

The parent execution checks live in
`C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{1bcd2936-e1a3-4f8c-a19b-b377fe1eec93}/AC/Temp/CosmixHrGap20261013`.
The initial planned check filenames below describe the acceptance cases; the executed
combined helpers/browser harnesses and their actual paths/counts are recorded in the
dated report. Checks exit nonzero on failed assertions. The C# checks use the existing
projects, reject SQL connections and do not initialize the application host.

## Global Constraints

- Work in the nested authoritative `cosmix-backend` and `cosmix_frotned` projects under `Cosmix_Erp_software`; preserve all existing changes. React Native and the donor project are excluded.
- No SQL connection, LocalDB creation/start, database writes, migrations application, seeding, real provider calls, scanner/SMS changes, private key/config generation, Git synchronization, commit or push.
- Office means the existing Branch master. Office employees inherit their dated approved office; site employees keep their dated approved site. No individual/team/department precedence or fake-site fallback.
- Office administration initially requires existing All-scoped roster permissions. No new grants, Branch access-scope kind or automatic fixed-role privilege.
- Direct roster changes preserve immutable history, audit and retry evidence. Employee office transfer approval remains separate. Raw punches and locked periods are not rewritten.
- Financial rules and pay calculation stay in the backend; this package does not activate payroll or legal deductions. Office OT remains ineligible.
- Check-In face remains fail-closed; Check-Out has no face requirement. Real face/device acceptance remains deferred, not passed.
- Temporary checks live outside Git in a task-specific TEMP directory. Do not create permanent test projects, mock production APIs or screenshot/artifact folders.
- After plan approval execute continuously through all six tasks; stop only for an actual destructive/security-sensitive operation or missing binding decision. Document blocked runtime acceptance rather than bypassing it.

## Review Focus

1. Site-scoped actors must not gain office management, global reference data or unrelated employee reads; Task 1 and Task 5 cover grant pairing/denial.
2. Changed staff or schedule state between preview/write/retry must reject stale intent, not silently save a new plan; Task 3 covers preview races and exact retry.
3. Office transfers and overnight shifts must not reinterpret a historical site/office session or fingerprint; Task 5 covers dated provenance and unchanged site evidence.
4. Cancelled requests, logout and permission reload must not leave private forms/caches active; Task 1 and Task 6 cover cancellation and context isolation.
5. A deployment lacking office tables or reference permissions must remain unavailable, not appear successfully configured; Task 5 and Task 6 cover missing-schema/reference behavior.

## Task 1: Attendance Transport, Contracts And Presentation Access

**Files (frontend-relative):** create `src/modules/attendance/api/attendance-api.ts`, `schemas/shifts.ts`, `schemas/site-rosters.ts`, `schemas/site-geofences.ts`, `types/contracts.ts`, `hooks/use-attendance-access.ts`, `hooks/use-attendance-query.ts`, `hooks/use-attendance-action.ts`, `hooks/use-attendance-lifetime.ts`, `utils/messages.ts`. Existing authentication/transport is consumed unchanged unless a demonstrated narrowly scoped defect requires review.

**Interfaces:**

- `attendanceGet<T extends z.ZodType>(path: string, schema: T, signal?: AbortSignal): Promise<z.infer<T>>` validates the existing shared `request` result.
- `attendanceWrite<T extends z.ZodType>(path: string, body: unknown, method: 'POST' | 'PUT', schema: T, key: string, signal: AbortSignal): Promise<z.infer<T>>` uses the existing secure transport.
- `hasAttendancePermission(user: SessionUser | null, code: string, siteId?: string): boolean` accepts a matching All grant or, only for supported site-roster operations, the matching Site grant. Unrelated scope kinds do not qualify.
- Query keys include session subject, exact resource and all query parameters. Inactive private cache retention is zero. Queries are disabled until permission/context is valid.
- The action hook retains exact URL/method/body/key for uncertain results; definitive 4xx requires correction/reload rather than automatic replay. A response-schema failure after a write remains uncertain.

- [ ] Capture current routes/contracts and create TEMP checks `attendance-access.mjs` and `attendance-contracts.mjs`. Cases: no grants denied; unrelated permission denied; Site A cannot manage Site B; All succeeds; permission/scope cannot be borrowed across role assignments; global shifts never accept Site scope; unknown response state is rejected.
- [ ] Run the checks before implementation. Expected: missing implementation/exports fail, not a provider or database failure. Retain the observed failure output.
- [ ] Implement the module contracts/hooks against actual C# DTOs. Preserve cookie/CSRF/origin/retry handling; do not deep-import HR internals or persist private payloads.
- [ ] Rerun focused checks and `npm run typecheck`. Expected: checks pass and TypeScript exits zero. Record no live API acceptance when SQL is unavailable.

## Task 2: Operational Shift Management

**Files:** create Attendance `pages/ShiftsPage.tsx`, `components/shifts/ShiftList.tsx`, `ShiftDetail.tsx`, `ShiftScheduleForm.tsx`, `ShiftMetadataForm.tsx`, `utils/shift-schedule.ts`; update public `index.ts`, existing app routes/frame and narrowly scoped styles.

**Interfaces:** consume Task 1 transport/schemas/access. Existing endpoints are `GET/POST /hr/shifts`, `GET/PUT /hr/shifts/{id}`, `POST .../status`, `POST .../versions` relative to the configured API base. Detail returns exact parent rowversion and immutable versions; new version publication also requires authority over every affected site.

- [ ] Add TEMP `shift-schedule.mjs` cases before code: Monday=1/Sunday=64; 22:00-06:00 with EndDayOffset=1 is valid; same times with offset=0 are invalid; configured/break minutes cannot exceed the duration; inactive/new-code restrictions and current-versus-future dates are enforced.
- [ ] Run checks and observe failure. Implement real list/filter/page/detail/create/name/status/version forms; Code is immutable, name/reason limits are 150/500, initial schedule may start today and appended versions must follow today/latest version.
- [ ] Do not send prepared-only policy IDs, named-break lists, committed hours or editable EffectiveTo. Render server history and local validation; retain overnight offsets and immutable versions exactly.
- [ ] Rerun checks and relevant type/lint checks. Runtime acceptance when separately permitted: valid create/read/reload, stale rowversion denial, inactive referenced shift rejection and lost affected-site authority on replay. Do not insert fixtures now.

## Task 3: Operational Site Rosters And Temporary Amendments

**Files:** create Attendance `pages/SiteRostersPage.tsx`, `components/rosters/SiteRosterHistory.tsx`, `SiteRosterForm.tsx`, `SiteTemporaryChangeForm.tsx`, `TemporaryAmendmentForm.tsx`; update public route/nav wiring without altering HR site-master ownership.

**Interfaces:** use exact SiteRosterContracts: expected timeline/staff hashes, shift parent rowversion, pinned calendar, exact replacement shift-version ID, written evidence and server-derived employee set. Supported actions: assignment, replacement, ending; temporary preview/create/cancel/supersede. No approval-request endpoint is introduced.

- [ ] Create/run TEMP `roster-intent.mjs`: a changed preview token invalidates submit; changed payload cannot reuse an uncertain key; ending is exclusive; replacement retains the old assignment; affected staff come only from preview. Expected first run fails against missing implementation.
- [ ] Build known-site permission-aware history/forms. Global reference lists require their own read grants; do not issue unauthorized list requests or invent a scoped reference API.
- [ ] Display active/cancelled/superseded and legacy flags separately. Keep current server tokens with the reviewed intent, protect dirty navigation and require fresh review after 409.
- [ ] Rerun intent/type checks. Later authorized API acceptance must prove overlap/lock denial, temporary staff-change race, cancel/restoration and supersession history, exact timeout retry and unrelated site denial.

## Task 4: Actual Circular Site Geofence Versioning

**Files:** create Attendance `pages/SiteGeofencesPage.tsx`, `components/geofences/SiteBoundaryForm.tsx`, `SiteBoundaryHistory.tsx`, `CirclePreview.tsx`, `utils/geofence-input.ts`; extend Task 1 schema/API contract only as actual source requires.

**Interfaces:** `GET /hr/sites/{siteId}/geofences?asOfDate`, `/versions`, `/versions/{versionId}`, `/history`; `POST .../versions`. Configuration/version reads require All-scope `hr.sites.read`; history/create require fixed Admin/SuperAdmin with All-scope `hr.sites.manage` on the same assignment. UI grants do not prove the fixed role; server denial remains authoritative.

- [ ] Create/run TEMP `geofence-input.mjs`: reject latitude outside -90..90, longitude outside -180..180, excess coordinate precision, non-positive/excess radius or accuracy, missing explicit attestation, missing reference and stale expected-version input. Observe failure before code.
- [ ] Implement real as-of/current/future-version views and circular coordinate preview. Retain ExpectedSiteVersion, ExpectedLatestVersionNumber, future EffectiveFrom, ManagementApproved, ApprovalReference and reason. Coordinates allow seven decimal places; radius/accuracy two.
- [ ] Do not expose polygon/end/edit actions, GPS permission or external maps: they are not supported by these contracts. Operational zones remain separate.
- [ ] Rerun focused checks and type/lint. Later API acceptance must distinguish not-configured from configured, confirm immutable history and deny ordinary HR management, wrong-site versions and stale publication.

## Task 5: Office Roster Backend And Historical Source

**Files (backend-relative):** create Domain `Attendance/Rosters/OfficeRosterAssignment.cs`, `OfficeRosterAssignmentEnd.cs`; Application `Attendance/Rosters/OfficeRosterContracts.cs`, `OfficeRosterSchedule.cs`, `OfficeRosterService.cs`, `OfficeRosterImpactValidator.cs`, `OfficeRosterResolver.cs`, `EmployeeRosterResolver.cs`; Infrastructure repository/configuration counterparts; API `Controllers/Attendance/OfficeRostersController.cs`.

**Coordinated existing files:** `IRosterRepository.cs`, `RosterContracts.cs`, `SiteRosterContracts.cs` (optional explanation extension only), `Shifts/SchedulingRegistration.cs`, `ShiftVersionPublication.cs`, `ShiftContracts.cs`, `ShiftService.cs`, `Infrastructure/Repositories/Attendance/RosterRepository.cs`, `ShiftRepository.cs`, scheduling Infrastructure registration, `Database/CosmixDbContext.cs`. Inspect actual paths before edits; never replace concurrent changes. Audit My Shift and `HR/Leaves` consumers before enabling office use.

**Interfaces:**

- `CreateOfficeRosterRequest(Guid ShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? HolidayCalendarId, string ExpectedTimeline, string ExpectedShiftVersion, string Reason)` and `EndOfficeRosterRequest(DateOnly EffectiveTo, string ExpectedTimeline, string Reason)` mirror the reviewed site convention.
- `OfficeRosterAssignmentResponse(Guid Id, Guid BranchId, Guid ShiftId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, Guid? HolidayCalendarId)`; history returns BranchId, Timeline and bounded assignment/ending evidence.
- `OfficeRosterState(Guid BranchId, IReadOnlyList<OfficeRosterAssignment> Assignments, IReadOnlyList<OfficeRosterAssignmentEnd> Ends, IReadOnlyList<ShiftVersion> Versions)` and `OfficeRosterSchedule.Resolve(Guid employeeId, DateOnly date, OfficeRosterState state, EmployeeAssignment membership): EffectiveRosterResponse` define the pure historical schedule boundary. Validate membership employee/branch/exclusive dates; holiday lookup pins the selected assignment's exact calendar separately.
- `OfficeRosterService.HistoryAsync`, `AssignAsync`, `ReplaceAsync`, `EndAsync` use BranchId, current SchedulingOperation, reason, idempotency key and existing AuthRequestContext/CancellationToken conventions.
- `EmployeeRosterResolver.ResolveAsync(Guid employeeId, DateOnly businessDate, CancellationToken ct): Task<EffectiveRosterResponse>` becomes the shared entry point. It dispatches by the single dated approved employment assignment: SITE to existing resolver, OFFICE to the new office resolver. It must not dispatch merely from nullable SiteId.
- Add typed office provenance only when applicable; preserve existing serialized site output and canonical fingerprints. Extend publication impact/authorization to offices without changing existing Site/All semantics or allowing Site-only office administration.

- [ ] Add TEMP `VerifyOfficeRoster.cs` using actual services and synthetic repositories/private IO, not a permanent test project. First failures: Office assignment at Office A cannot resolve; missing office is unavailable; Site-only grant cannot manage offices; same-date conflict/overlap rejected; replacement/ending retains original evidence; site output/fingerprint must remain byte-compatible.
- [ ] Run with .NET 10 file-based tooling and record the real failure. The harness must intercept/reject database connection attempts and external providers; no host/private-key/database initialization.
- [ ] Implement append-only entities and explicit restrictive mappings, timeline/service/repository/controller, dated category dispatch and registration. All-only office administration gives no new grants. Add schema readiness checks that fail closed if unapplied tables are absent.
- [ ] Cover approved Office A->B transfer boundary, inactive historical office, overnight transfer overlap, future-version impact, revoked replay authority and reference deletion restrictions. Preserve retired individual writes and existing site resolver behavior.
- [ ] Audit ordinary leave/My Shift provenance and enable only supported office consumers. Do not remove SiteId conditions in punch/session/calculation code to manufacture office support. Document remaining office capture/geofence/calculation scope explicitly.
- [ ] Run Release build and offline model/mapping/concurrency/retry checks. Expected: build exits zero; focused assertions pass; connection-attempt count is zero. Generate a new incremental migration only after model review, preserving all shared migration history; review Up/Down and SQL range without applying it. Detect equivalent concurrently prepared migration first.

## Task 6: Office Web, Integration Review And Handover

**Files:** create Attendance `schemas/office-rosters.ts`, `pages/OfficeRostersPage.tsx`, `components/rosters/OfficeRosterForm.tsx`; extend public exports/routes/nav. Update relevant module README and existing backend/frontend verification documentation; copy the dated progress report to Desktop.

**Interfaces:** office web consumes Task 5's actual implemented contracts, not the proposed design before delivery. Office reference data remains `/hr/branches` behind existing organization read authority. Labels say Office; payloads use BranchId. Share focused roster display/form units where the contracts genuinely match; do not create a universal CRUD/workflow builder.

- [ ] Create/run TEMP contract/navigation checks before office UI: no unrelated employee-read requirement; All-only office access; denied Site scope; missing office tables show unavailable; no API call while unauthorized; office drafts discarded after session/context change. Observe failure first.
- [ ] Implement actual office history/create/replace/end forms with server tokens and explicit loading/error/stale states. Preserve existing login/testing OTP behavior and core HR navigation.
- [ ] Run `npm run verify` from frontend and `dotnet build Cosmix.Backend.sln -c Release` from backend. Expected: zero failing lint/format/type/build checks; record actual output. No success claim from a command that did not run.
- [ ] Conduct fresh whole-diff review focused on contracts/scopes/hash compatibility/retries/privacy. Fix findings with reproducing checks; do not review only added filenames.
- [ ] When approved isolated runtime exists, verify genuine API-backed persistence/reload/denials, cookies/CSRF, concurrent writes, restart replay and desktop/narrow screenshots. Without that approval/environment, mark these BLOCKED and do not count synthetic transport checks as live integration.
- [ ] Report six task statuses, exact files/endpoints, performed checks, pending migration range, office-capture limitations and remaining policy/finance packages. Keep all code local and uncommitted.

## Dependency And Execution Handoff

Tasks 1-4 integrate existing contracts; Task 5 adds office contracts; Task 6 consumes Task 5. Parent owns shared routes, navigation, DI, context and migration snapshot. If parallel agents are chosen, use disjoint frontend feature writes and do not let multiple workers edit shared schema/composition.

Recommended execution: **Native/inline, continuous local execution**, with a fresh whole-package reviewer at the end. The tasks share security hooks, roster provenance and route contracts, so one owner reduces integration conflict. The PM may instead choose task-by-task subagent implementation/review.

The PM selected continuous implementation. This is the first package only, not an
approval to guess policy values or mark all seven gaps complete. The detailed checklists
above retain the original planned acceptance scope; source delivery and runtime
acceptance are distinguished in the execution table and dated report.
