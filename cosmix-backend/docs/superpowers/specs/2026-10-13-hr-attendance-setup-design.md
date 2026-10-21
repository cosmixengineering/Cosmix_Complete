# HR Gap Closure: Attendance Setup Design

Date: 13 October 2026, Asia/Karachi.

Status: written design approved by the PM in this conversation on 13 October 2026. The PM then requested the implementation plan. That plan still requires review and an execution-method selection before implementation. SQL writes and provider activation remain outside approval.

## 1. Outcome

Replace the first unconnected Attendance web workspaces with actual shift, site-roster and site-geofence administration. Add an office-wise schedule source so an Office Staff employee can obtain a schedule from the employee's approved, effective-dated office assignment.

This is the first sub-project of the seven non-mobile HR gaps. It is not the complete HR/SRS delivery. Office schedule lookup alone does not establish accepted office attendance, payable time or payroll readiness.

## 2. Confirmed Business Flow

- Office means the existing physical `Branch` master, not a Git branch or a new company entity.
- Authorized HR manages an effective-dated roster for each office.
- An Office Staff employee inherits the roster of the employee's approved office assignment on the business date.
- Site Staff continues to inherit the existing site's roster. Site creation and employee site assignment remain the existing Admin workflows.
- HR office-roster writes are direct, permission-controlled and audited; this is not a new approval chain.
- Employee office transfers remain HR request -> Admin review -> effective-dated approved assignment. Editing a roster must not transfer employees.
- Individual/team/department roster precedence must not be revived. Missing or conflicting source evidence remains unavailable.
- Office Staff remains ineligible for overtime under the already-confirmed OT rule.
- Raw punches, corrections and zone events stay separate. Locked months never reopen.

## 3. Current Source Evidence

| Source | Actual limitation or reusable behavior |
| --- | --- |
| `src/Cosmix.Application/Attendance/Rosters/SiteRosterResolver.cs` | Resolves only `EffectiveSiteAsync`; no site yields `site_not_assigned`. |
| `src/Cosmix.Domain/HR/EmployeeAssignments/EmployeeAssignment.cs` | Existing dated assignment has `BranchId`, `SiteId`, staff category and assignment identity. |
| `src/Cosmix.Domain/Attendance/Rosters/ShiftPopulationAssignment.cs` | Direct scheduling supports Site only; no Office population or branch field. |
| `src/Cosmix.Application/Attendance/Rosters/SiteRosterContracts.cs` | Timeline tokens, immutable endings, pinned calendars and exact temporary-change staff evidence already exist. |
| `src/Cosmix.Application/Attendance/Calculation/AttendanceCalculationSnapshotProducer.cs` | Requires `roster.SiteId`; also verifies session/punch/employment site provenance. A null-check removal would not implement office provenance. |
| `src/Cosmix.Application/Attendance/Punches/AttendanceCaptureService.cs` | Uses site geofence repository and site relationship evidence; office capture is a later coordinated slice. |
| `src/Cosmix.Application/Attendance/Shifts/SchedulingOperation.cs` | Current-session permission checks, authorization transaction, protected audit and exact retry receipts are used by site scheduling and can be reused. |
| `src/Cosmix.Domain/Authentication/UserAccessScope.cs` | No Office/Branch access scope currently exists. Do not pretend a Site scope grants office access. |
| Frontend `src/modules/hr/components/workspaces/PreparedWorkspace.tsx` | Always presents an unconnected service; drafts and disabled actions are not API integration. |

Backend baseline inspected: `develop`, HEAD `3e4e424`, plus existing uncommitted changes. Frontend inspected on `codex/frontend-foundation`, also with existing uncommitted changes. Do not reset either checkout.

## 4. Alternatives And Recommendation

1. **Recommended: preserve site scheduling and add a focused office source.** Reuse shared shifts, date/time helpers, authorization, history, holidays and transport. Keep office ownership explicit. This minimizes changes to working site history and makes Office/Site selection inspectable.
2. Extend every population assignment with an Office enum/branch discriminator. This saves some table definitions but touches existing constraints, validators, group-candidate interpretation and legacy history. It needs broader migration/regression review.
3. Treat an office as a site or fall back to an individual's shift. Rejected: this changes the approved Office/Site model and can manufacture location/access evidence.

The recommendation keeps the existing Clean Architecture and one DbContext. It does not add a generic scheduling framework, a second permissions system or a new payroll module.

## 5. Backend Schedule Source

### Persistence

Propose two focused append-only records using the existing assignment/ending pattern:

- `OfficeRosterAssignment`: ID, BranchId, ShiftId, EffectiveFrom, optional exclusive EffectiveTo, optional immutable HolidayCalendarId, recording actor and UTC timestamp.
- `OfficeRosterAssignmentEnd`: ID, office roster assignment reference, exclusive EffectiveTo, recording actor and UTC timestamp. Reason and before/after evidence use the existing protected audited operation.

The final mappings must use explicit FKs to existing masters, restricted deletion, bounded keys/indexes and at most one effective withdrawal per assignment. Reject overlapping effective office assignments inside the existing serializable authorized write transaction. A replacement appends an ending and successor atomically.

Reuse shift versions; do not copy each shift's hours into an office row. A holiday calendar pins an immutable revision rather than following the latest calendar silently. A historical office's later inactive master status must not erase historical membership or an already-pinned schedule.

### Services And Proposed API

Keep Domain entities, Application DTO/service/repository contracts, Infrastructure mappings/repository and API controller separate. Proposed routes, not existing endpoints:

- `GET /api/v1/hr/branches/{branchId}/shift-assignments`
- `POST /api/v1/hr/branches/{branchId}/shift-assignments`
- `POST /api/v1/hr/branches/{branchId}/shift-assignments/{assignmentId}/replace`
- `POST /api/v1/hr/branches/{branchId}/shift-assignments/{assignmentId}/end`

Follow the existing site contract's reason, idempotency key, expected timeline and expected shift rowversion. Use start-inclusive/end-exclusive business dates in Asia/Karachi. Reject past/locked mutations and stale state; do not silently retry a changed payload.

Initially require existing `attendance.rosters.read` / `attendance.rosters.manage` with All scope for office administration. Recheck the current MFA web session server-side. This gives no account new grants. Office-specific delegated RBAC is excluded until its scope and target model are approved; Site/Team grants must not be borrowed for office-wide management.

Office temporary opening-time amendments are excluded from this first contract. Existing temporary site amendments remain intact. Do not add an unsupported office temporary form or assume one is authorized by the site rule.

### Resolution And Compatibility

Resolve the employee's single approved assignment and staff category on the requested date first:

- `SITE`: call the existing site resolver unchanged.
- `OFFICE`: require the approved BranchId and resolve that office's roster and shift version.
- Missing assignment, invalid category/location combination, overlap or missing office roster: return an explicit unavailable/conflict result, never an invented default shift.

Keep one shared schedule resolver entry point for existing consumers. Add typed office provenance: office identity, office roster assignment identity, employee assignment identity and exact shift/calendar revision. A caller's employee ID or current UI office is not historical evidence.

Preserve existing site DTO output, status meaning, hashes and retained receipt interpretation. New optional office fields must not change canonical site fingerprints through serialized null fields. Introduce/version new office evidence deliberately; never reinterpret historical site evidence as office evidence. Do not manually rewrite model snapshots or existing migrations.

Audit the existing My Shift and ordinary leave preview/approval consumers. Enable office use only where exact schedule and employment provenance is supported. Any remaining site-only consumer must continue to return an explicit unavailability result rather than accepting incomplete evidence.

## 6. Web Integration

Use the existing React project, session provider, cookie/CSRF transport, TanStack Query, Zod, shared controls, dirty-form protection and session-lifetime cancellation.

Place the active scheduling feature in `src/modules/attendance/{api,schemas,types,hooks,components,pages}` and compose it through that module's public export into the existing HR frame. Keep site/office masters in HR. Do not deep-import HR internals, duplicate authentication or create a second panel tree.

Deliver real shift list/detail/create/rename/status/future-version screens using the existing `/api/v1/hr/shifts` contracts. Deliver real site roster history/assignment/replacement/ending and preview-bound temporary create/cancel/supersede flows using the existing site contracts. Deliver actual site boundary as-of reads, version list/detail, future circular versions and permitted audit history. There is no geofence end/edit/polygon API; none may be invented. Operational-zone polygons are a different resource. No external basemap is required; keep the existing circle coordinate preview honest.

Add the office roster screen only after its actual backend contracts are implemented and reviewed. Label `Branch` as Office in UI; do not invent a deployed endpoint while the above routes remain proposals.

Permission presentation must match each endpoint:

- Global shift masters require All-scoped shift permissions.
- Site roster actions use the applicable Site/All grants; the current HR All-only helper must not suppress legitimate scoped access or borrow unrelated grants. Publishing a shared shift also rechecks roster-management authority for every affected site, including retained retry evidence.
- Geofence configuration/version reads require All-scope `hr.sites.read`. Creation and administration history require fixed Admin/SuperAdmin with All-scope `hr.sites.manage` on the same assignment; HR must not receive those writes merely through roster permissions. A management attestation/reference is required by the actual creation contract, not a fabricated approval-engine decision.
- Office management uses the proposed All-scoped roster access; office reference reads retain the existing organization permissions.
- Each grant's permission and scope remain paired to the same assignment. Frontend checks never replace server authorization.

Current global site and shift reference lists have their own All-scope permissions. A Site-scoped roster grant does not grant those lists. Permit supported known-site navigation where possible; otherwise show a reference-access limitation instead of fetching an unauthorized catalogue or inventing a scoped lookup endpoint. Do not require unrelated `hr.employees.read` simply to open shift setup.

Actual schedule forms use weekday bitmasks, explicit overnight offsets and numeric paid/unpaid breaks. They do not send the prepared form's unsupported policy ID, named-break collection or committed-hours fields. Site temporary forms choose an immutable replacement shift version and consume server-resolved affected staff, not a manually entered employee list. Geofence writes retain the expected site rowversion, latest boundary version, future date, coordinate precision, accuracy and management attestation.

Do not fetch employee, financial, medical or private evidence merely to display shift setup. Clear private queries and transient drafts on account/session/workspace change. Do not store drafts in browser persistence, log requests, calculate wages or render server exceptions.

Mutation forms retain server concurrency tokens. On an uncertain result, an explicit retry uses the exact key/body; 409 requires reload/review. Preview-bound temporary changes retain the matching timeline/staff tokens. Every form needs loading, denied, empty, stale, unavailable and success states without optimistic fake persistence.

Remove obsolete employee/team/department assignment controls from the active scheduling view. Keep historical records available only through supported history APIs; do not delete them. Office/Site are separate selectors, not extra employee shift overrides.

## 7. Acceptance And Completion Definition

Before claiming this sub-project complete, execute and record:

1. Site scheduling regression: the same dated employee/site/shift/calendar evidence and serialized site outputs still resolve identically.
2. Office employee resolves the correct office roster and exact assignment on both sides of an approved office transfer.
3. Unassigned/conflicting office or roster is unavailable; no individual/site fallback is used.
4. Replacement/ending retains original records; stale tokens, overlap, locked dates and earlier history edits are rejected.
5. Future shift version publication validates affected office schedules as well as existing site schedules; inactive references reject new assignments without deleting history.
6. Authorized Office All access succeeds; Site-only and unrelated management grants cannot administer offices. Anonymous/expired/non-MFA sessions fail.
7. My Shift and supported leave previews retain exact office assignment/shift/calendar provenance; unsupported site-only consumers fail closed.
8. Office employees remain OT-ineligible; no new payroll result or accepted attendance session follows merely from a roster lookup.
9. Actual web reads/writes/reload, exact retries, dirty navigation and session changes pass with approved synthetic fixtures.
10. Desktop and narrow-width forms, history tables, keyboard/focus and boundary preview remain usable; no secret/private data is persisted or logged.
11. Release build, focused offline model/service checks and `npm run verify` pass. Builds are not SQL, provider or UAT acceptance.
12. Incremental migration, if required, is reviewed separately with explicit range/SQL operations. No SQL application without the existing fresh-isolation review and approval.

No permanent test project/artifact folder, account creation, real provider call, production data, Git commit or push belongs to this stage. Temporary verification artifacts stay outside the repository.

## 8. Boundaries That Remain After This Slice

Office accepted attendance also requires office geofence configuration, punch/session/correction source binding, date/transfer/overnight checks and calculation provenance. Those are not solved by making SiteId optional or by putting office staff into a fabricated site.

Real device face/liveness/replay acceptance stays deferred by the earlier PM decision unless explicitly resumed. Check-In remains fail-closed; Check-Out has no face requirement. React Native remains excluded.

KPI, waiting, studying/make-up, extra-day/cash benefits, encashment and long-leave recovery need reviewed typed definitions/lifecycles and approved applicable values. Claims/vehicles/loans and broader reports are separate sub-projects. Missing policy or legal authority is not an exemption or zero charge. Month readiness gates must not be removed to force a successful lock.

## 9. Review Handoff

Review the technical recommendation, initial All-scope office administration, first-slice exclusions and acceptance criteria. After written-design approval, produce a file-level implementation plan and select continuous local execution. This design does not commit the project to all seven modules in one undivided patch.
