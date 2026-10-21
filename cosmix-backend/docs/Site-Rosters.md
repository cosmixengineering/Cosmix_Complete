# Site scheduling — direct HR configuration

The user confirmed on 8 October 2026 that a shift belongs to a **site** and applies to all staff historically assigned to that site. Authorized module users change schedules directly; there is no request/review/approval chain. Admin/SuperAdmin controls permission delegation through the existing access infrastructure. These instructions supersede the earlier individual-over-group proposal and the SRS temporary-approval step. Holidays are informational and do not cancel duty.

The code is local. On 9 October 2026 the user separately approved migration preparation and fresh isolated acceptance: **all 13 migrations were applied only to the new local `Cosmix_Testing` database**. Real authenticated HTTP, persisted SQL/private evidence, restart replay, revoked-site denial and database constraints passed; [current verification](group-rosters/Verification.md) records the exact checkpoints. No development or production database was modified. Site history and resolution still return `503 site_roster_schema_not_ready` on a database without the required schema. See the [base schema](group-rosters/Decisions-and-Schema.md) and [append-only amendment schema](group-rosters/Temporary-Amendments-Schema.md). Application startup introduces no automatic migration, account grant, dummy approval or fixture. [Access provisioning](access-provisioning/Provisioning-Design.md) follows the confirmed web workflow: fixed Admin/SuperAdmin configure custom roles and select catalog permissions explicitly; only SuperAdmin manages protected administrator accounts.

## Routes and access

All routes are under `/api/v1/hr/sites/{siteId}` and require current authenticated web MFA, the paired permission/scope and no-store responses. Cookie writes retain CSRF protection. No role is automatically granted module access.

| Method | Suffix | Permission | Behavior |
|---|---|---|---|
| GET | `/shift-assignments` | `attendance.rosters.read` | Bounded immutable assignment/end/temporary history and timeline token |
| POST | `/shift-assignments` | `attendance.rosters.manage` | Direct future site assignment |
| POST | `/shift-assignments/{assignmentId}/replace` | `attendance.rosters.manage` | Append old ending and successor atomically |
| POST | `/shift-assignments/{assignmentId}/end` | `attendance.rosters.manage` | Append one controlled future ending |
| GET | `/temporary-shift-changes/preview?businessDate=...&replacementShiftVersionId=...` | `attendance.rosters.manage` | Revalidate dates, versions and site staff; return original/replacement IDs, affected IDs and expected tokens |
| POST | `/temporary-shift-changes` | `attendance.rosters.manage` | Direct dated exception with protected written evidence and an immutable staff manifest |
| GET | `/temporary-shift-changes/{changeId}/cancel/preview` | `attendance.rosters.manage` | Preview restoration, current staff and expected tokens |
| POST | `/temporary-shift-changes/{changeId}/cancel` | `attendance.rosters.manage` | Append cancellation, preserving the original and validating the restored base |
| GET | `/temporary-shift-changes/{changeId}/supersede/preview?replacementShiftVersionId=...` | `attendance.rosters.manage` | Preview replacement with current staff |
| POST | `/temporary-shift-changes/{changeId}/supersede` | `attendance.rosters.manage` | Append successor, refreshed immutable manifest and supersession link atomically |

Site configuration requires an explicit Site grant for that site or All scope. A department/team grant is not silently treated as authority over an entire site. The existing scoped employee roster read still checks all dates occupied by the returned duty. My Shift derives identity from the server session and exposes no affected-employee list or private evidence.

`GET /api/v1/hr/employees/{employeeId}/roster` and `GET /api/v1/me/shift` now resolve historical HR **site** membership. The legacy interface name `IIndividualRosterResolver` remains source-compatible, but runtime DI uses `SiteRosterResolver`. Individual assignment history remains readable. The three individual assignment POST routes remain registered to return `409 individual_scheduling_replaced_by_site_rosters`; their rows are not deleted, migrated or used as an override/fallback. Old individual services remain available for regression/history compatibility, not as the public scheduling workflow.

## Resolution and history

1. Resolve exactly one effective HR site assignment for the employee and business date. Missing site is explicitly `site_not_assigned`; ambiguous membership is a conflict.
2. Select exactly one effective site shift assignment using inclusive start/exclusive end and its append-only ending. Two matching assignments remain a conflict even if they reference the same shift.
3. Select the immutable shift version effective on that date. Follow explicit amendment links to the sole active temporary change, if present; reject malformed chains or competing active changes. It uses its exact replacement version and requires its original version to still match the base schedule. Cancelled/superseded rows remain history.
4. Validate occupied next-day site membership for overnight duties. Exact-midnight endings and rest days do not occupy the next date. Site assignment creation also validates the final duty's occupied end against site availability.
5. Return assignment/version IDs plus `explanation`: source (`site`/`site_temporary`), site assignment, historical membership, temporary-change and original-version IDs. No team/department/category priority or individual fallback exists.
6. Read the assignment's exact selected calendar revision. Return safe holiday IDs/names, calendar ID/code/revision and informational flags. `not_configured`, `outside_calendar_period` and `inactive_calendar` are explicit; later calendar revisions never replace the pinned ID automatically.

Site scheduling does not change HR employment/site records. A staff member assigned to the site later automatically inherits the applicable normal site schedule. A temporary change's affected set is frozen at write time: if later HR changes alter that set, resolution returns `temporary_change_staff_changed`, rather than silently widening the recorded exception. Resolution is not proof of attendance eligibility and does not calculate wages, leave, overtime or punches.

Legacy approval-linked population/temporary records are preserved but are not accepted as new direct records or silently relabeled. Applicable legacy records return `legacy_site_roster_requires_review`; historical acceptance/conversion needs an explicit migration/data review. New direct records have no approval request ID, and temporary DTOs call the legacy actor/time columns `recordedBy`/`recordedAt` truthfully.

## Writes, concurrency and evidence

Writes are future-dated, preserve past schedules and reject affected locked periods. This batch does not implement retrospective or same-day schedule rewriting. Shift-version publication validates affected site timelines, occupied date boundaries and historical staff transfers. It requires `attendance.shifts.manage` with All scope plus `attendance.rosters.manage` for every affected site. Active temporary original-version pins remain protected; explicitly cancel/reconcile them before incompatible publication. See [publication verification](shift-publication/Verification.md).

Obtain the timeline from the site's history read. Send it as `expectedTimeline`; the service recomputes it inside the existing serializable authorization transaction. It covers immutable assignment/end/change/amendment/version identities and selected calendar references. New assignments additionally require the shift's rowversion. Temporary writes revalidate the preview's independently hashed historical staff set. Stale tokens return 409. All six site write operations require an `Idempotency-Key` and a reason; identical retries return the original protected result only after current permission/scope checks. Changed payloads cannot reuse a key. Cross-module publication receipts additionally retain and recheck original affected-site permissions, even if later endings remove a site from the current impact query.

Temporary `writtenEvidence` is a short written explanation, not an arbitrary private-file path. The service writes the evidence, original/replacement identities and affected staff through the existing encrypted private-file store; the database holds its serialized protected reference. The parent and all manifest rows are added in the same SaveChanges call, preserving the existing append-only guard. SQL audit/idempotency metadata commits with the business records through `SchedulingOperation`. Existing private-object orphan retention applies if a database transaction later rolls back; no weaker test storage is used in production.

## Client examples

The identifiers/tokens below are placeholders. Use IDs returned by existing site, shift and calendar APIs; do not send an employee ID for site writes or My Shift.

```http
GET /api/v1/hr/sites/{siteId}/shift-assignments

POST /api/v1/hr/sites/{siteId}/shift-assignments
Idempotency-Key: site-roster-20261012-01
X-CSRF-TOKEN: {web-cookie-session-token}
Content-Type: application/json

{
  "shiftId": "{shiftId}",
  "effectiveFrom": "2026-10-12",
  "effectiveTo": null,
  "holidayCalendarId": "{exactCalendarRevisionId}",
  "expectedTimeline": "{64-character-timeline-from-history}",
  "expectedShiftVersion": "{12-character-shift-rowversion}",
  "reason": "Apply the site's winter schedule"
}
```

Use the same body for `/shift-assignments/{assignmentId}/replace`; its effective start must shorten the previous interval. Ending uses `{ "effectiveTo": "2026-11-01", "expectedTimeline": "{token}", "reason": "Site schedule ended" }`. An already-ended assignment cannot be ended again. To revise only the calendar binding, replace the site assignment with the desired exact revision and the same shift at a future boundary.

```http
GET /api/v1/hr/sites/{siteId}/temporary-shift-changes/preview?businessDate=2026-10-15&replacementShiftVersionId={versionId}

POST /api/v1/hr/sites/{siteId}/temporary-shift-changes
Idempotency-Key: site-opening-20261015-01
X-CSRF-TOKEN: {web-cookie-session-token}
Content-Type: application/json

{
  "businessDate": "2026-10-15",
  "replacementShiftVersionId": "{versionId}",
  "expectedTimeline": "{preview.timeline}",
  "expectedStaff": "{preview.staffToken}",
  "writtenEvidence": "Recorded site opening arrangement for this date",
  "reason": "One-day opening-time change"
}
```

To cancel, obtain `/temporary-shift-changes/{changeId}/cancel/preview` then POST to `/cancel` with `{ "expectedTimeline": "{preview.timeline}", "expectedStaff": "{preview.staffToken}", "writtenEvidence": "Reason for restoring normal timing", "reason": "Restore site schedule" }` and a fresh idempotency key. Supersession uses the corresponding `/supersede/preview` and `/supersede` routes, adding `replacementShiftVersionId` to the body. It can refresh stale staff evidence after a future HR transfer; it never rewrites the previous manifest.

There are no approval/rejection endpoints. At most one temporary change is active per site/date. History exposes `active`, `cancelled` and `superseded` statuses and explicit links. After cancellation, a new dated exception can be created if normal validation passes. The isolated run verified those workflows, SQL evidence, encrypted-disk restart replay, actual transfer boundaries and pinned holiday revisions. Generic populations, real mobile/carrier/provider behavior, faces, punches and pay calculations remain outside this verification. See [current verification](group-rosters/Verification.md).
