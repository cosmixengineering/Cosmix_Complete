# Shifts and Individual Rosters

**Current continuation:** [Site Rosters](Site-Rosters.md) records the user's later site-only/direct-change decision. My Shift and effective reads now use historical site scheduling; the three individual POST routes return 409 and preserve old history. The direct-site schema was applied only to the approved isolated acceptance database. The individual implementation description below is retained as the earlier baseline, not the active write contract.

Shift creation and immutable version publication now accept optional
`missingCheckoutReminderAfterMinutes`; configured values also appear in shift detail and effective
roster/My Shift. The [reminder contract](attendance-completion/Missing-Checkout-Reminders.md) gives
30/60/120-minute examples, historical pinning, activation requirements and verification limits.
Legacy versions remain unconfigured. This addition requires local migration 18, which is prepared
but not applied; it does not automatically enable delivery or close duty.

Implemented in the existing local Desktop checkout, based on local `develop` commit `70adaf7`. Retained source from local checkpoint `82b6567` was reviewed, reused and corrected. No branch switch, Git merge, fetch, push, PR or policy-feature integration was performed. Existing geofences and their registrations are preserved.

This slice implements shift masters, immutable effective-dated schedules, individual employee assignment/replacement/ending/history and self-scoped schedule lookup. It reuses existing Shift, ShiftVersion, EmployeeShiftAssignment and HR employee/site records. It does not create an employee identity, alter HR transfers or depend on policy calculation.

## HTTP contract

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/hr/shifts` | Paged/filterable shift list |
| GET | `/api/v1/hr/shifts/{id}` | Metadata and immutable version history |
| POST | `/api/v1/hr/shifts` | Create shift with initial schedule |
| PUT | `/api/v1/hr/shifts/{id}` | Rename shift; immutable code |
| POST | `/api/v1/hr/shifts/{id}/status` | Activate/deactivate |
| POST | `/api/v1/hr/shifts/{id}/versions` | Append schedule version |
| GET | `/api/v1/hr/employees/{employeeId}/shift-assignments?from=2026-10-01&to=2026-11-01` | Paged individual assignment history |
| POST | `/api/v1/hr/employees/{employeeId}/shift-assignments` | Create dated individual assignment |
| POST | `/api/v1/hr/employees/{employeeId}/shift-assignments/{assignmentId}/replace` | End old interval and create successor atomically |
| POST | `/api/v1/hr/employees/{employeeId}/shift-assignments/{assignmentId}/end` | Shorten interval without deleting history |
| GET | `/api/v1/hr/employees/{employeeId}/roster?businessDate=2026-10-12` | Scoped schedule resolution |
| GET | `/api/v1/me/shift?businessDate=2026-10-12` | Signed-in employee's own schedule |

All endpoints require authentication and use no-store responses. Admin routes require current web MFA. Cookie writes also require the existing X-CSRF-TOKEN; all business writes require Idempotency-Key and reason. Mobile My Shift uses the existing Bearer token. My Shift accepts a business date, never an employee selector.

## Permissions and runtime prerequisites

| Permission | Scope |
|---|---|
| `attendance.shifts.read` | All; shared shift reference data |
| `attendance.shifts.manage` | All; shift metadata/schedule publication |
| `attendance.rosters.read` | Employee and dated department/team/site/project or All scope |
| `attendance.rosters.manage` | Same server-derived resource scopes for assignment writes |
| `attendance.rosters.self` | Own employee only; web or mobile |

Definitions are registered through feature-specific SchedulingPermissions. No existing account grants, fixed-role permissions or owner bootstrap lists were changed. On 9 October 2026 the user confirmed that fixed Admin/SuperAdmin configure roles and module access through the web UI. Existing access APIs now use that explicit current fixed-role authority to select catalog permissions without requiring a pre-existing personal grant of each module permission. Ordinary employees cannot originate grants. Only SuperAdmin may change protected administrator accounts. Every recipient still needs an explicit role assignment and scope; no account receives scheduling access automatically.

PrivateFiles configuration must provide the existing protected directory and separate key file. Scheduling writes store encrypted result/audit objects through IPrivateFileStore. Configuration failures remain fail-closed. SQL rollback cannot remove an already-created private object; orphan retention/cleanup follows the existing storage responsibility.

The [provisioning design](access-provisioning/Provisioning-Design.md) describes the confirmed web role/catalog/assignment workflow. The earlier separate local-command proposal is superseded. [Access verification](access-provisioning/Verification.md) distinguishes synthetic checks from the separately authorized SQL/private-storage acceptance work. Registration and component checks do not establish successful real login or deployment.

## Dates, schedules and boundaries

- Business dates and local times use Asia/Karachi. Monday 22:00–Tuesday 06:00 belongs to Monday. Weekdays apply to the start date.
- Assignment intervals are `[EffectiveFrom, EffectiveTo)`; null end is open. Adjacent dates are permitted only when actual duty windows do not overlap.
- EndDayOffset is explicitly 0 (same day) or 1 (next day). Duration must be positive and at most 24 hours. Scheduled minutes and the sum of paid/unpaid break minutes must fit the window; no wage formula is inferred.
- Weekday flags: Monday 1, Tuesday 2, Wednesday 4, Thursday 8, Friday 16, Saturday 32, Sunday 64. Values must be nonzero combinations of these flags. No workweek default is invented.
- An initial schedule for a newly created, unassigned shift may start today. Later schedule versions and assignment creation/replacement/ending must be strictly future-dated. Schedule versions also follow the latest existing version's date.
- Older schedule rows never change. Their exclusive end is derived from the successor. Appending a schedule advances the parent shift rowversion.
- Deactivation is rejected for current/future use, including previous-day overnight carryover. Historical schedules remain readable.
- A locked period blocks affected changes, including removed future suffixes and occupied overnight dates. No retrospective recalculation bypass is supplied.

## Eligibility and scope

Writes validate currently approved active employment, joining date, approved lifecycle history, continuous HR assignment coverage and site/office relationships over the requested interval. SITE employees require the explicit matching site; OFFICE employees require their existing branch. Future transfers and suspension are checked. HR assignments remain owned by HR.

Resolver reads recheck the site relationship on both occupied dates after an overnight duty, including transfers recorded after the roster was created. A null-site roster cannot silently adopt a new site. An exact-midnight ending occupies none of the next date; rest days require only the start-date relationship.

Scoped effective-roster reads first authorize the requested business date, resolve inside the same transaction, then authorize all dates actually occupied before auditing/returning the result. A later date leaving a department/team/site grant therefore fails closed. History checks cover full returned intervals. End/replace check from the original assignment's immutable start through the future timeline, because their response contains that original history; the same range remains stable on retry.

The internal contract is `IIndividualRosterResolver.ResolveAsync(employeeId, businessDate, ct)`. It returns assignment/ShiftVersion IDs, version number, scheduled/rest_day/unassigned state, configured minutes/breaks and absolute timestamps. Call it inside the caller's consistent transaction, with that caller's own authentication, eligibility and attendance authority. It resolves a schedule, not permission to punch; historical reads can outlive active employment.

## Concurrency, audit and retries

SchedulingOperation revalidates current sessions and paired permission scopes inside the existing serializable authorization transaction. SQL changes, audit metadata and idempotency receipts commit together. Reads are audited; retries recheck current permission and resource scope before reading their protected result. No nested transaction is introduced for the post-resolution scope check.

Supply the latest Base64 rowversion for metadata/status/version writes, ExpectedShiftVersion for new assignments, both shift and assignment versions for replacement, and ExpectedVersion for ending. Unique shift codes, immutable version keys, future-date controls, actual duty overlaps and current expected versions prevent conflicting changes. Receipt keys are scoped to actor and operation; identical request retries return the original result, altered payload/key reuse and expired receipts return 409. The inherited 24-hour retry window is not a retention policy.

## Client examples

Synthetic examples only: choose management-approved times, workweek and durations. Replace date and version placeholders with API values.

```json
{
  "code": "DAY",
  "name": "Day shift",
  "initialSchedule": {
    "effectiveFrom": "{{todayOrFutureDate}}",
    "startTime": "09:00:00",
    "endTime": "17:00:00",
    "endDayOffset": 0,
    "scheduledMinutes": 480,
    "paidBreakMinutes": 0,
    "unpaidBreakMinutes": 0,
    "applicableWeekdays": 31
  },
  "reason": "Approved initial schedule"
}
```

POST the above to `/api/v1/hr/shifts` with Idempotency-Key. Read the returned shift ID/Version. Then POST an individual assignment:

```json
{
  "shiftId": "{{shiftId}}",
  "effectiveFrom": "{{futureDate}}",
  "effectiveTo": null,
  "siteId": "{{matchingAssignedSiteId}}",
  "expectedShiftVersion": "{{shiftVersion}}",
  "reason": "Approved individual roster"
}
```

Use null SiteId only when consistent with the employee's HR records. Set a bounded end when site/employment availability is bounded. For an overnight schedule use, for example, start 22:00, end 06:00, EndDayOffset 1; do not interpret 06:00 as same-day.

Web clients send authenticated HTTPS cookies, and unsafe requests send the CSRF token from `/api/v1/auth/web/csrf`. On a 409 stale-version response, reload current data before preparing a new intended mutation; do not change a payload while reusing the same retry key. On timeout, retry the identical request/key to retrieve its recorded result.

## Remaining SRS work

Group/team/site/category/department assignment precedence, approved temporary site-wide changes/evidence, holiday applicability and approved retrospective/locked-period recalculation are not implemented. Applicable group/temporary records produce an explicit conflict instead of a fabricated individual result. Shift group models are not completed workflows. My Shift's full SRS holiday/temporary-change presentation therefore remains a subsequent slice.

Policy configuration/calculations, salary/late/OT rules, face enrollment/verification, punches, work sessions, attendance summaries/corrections and zones remain separate modules. This feature does not modify the other developer's policy code. See [current verification](Shifts-Rosters-Verification.md) for executed checks versus remaining SQL/authenticated acceptance.
