# Site rosters, temporary changes and holiday context

Status: confirmed decisions and approved isolated acceptance, updated 2026-10-09. The user separately approved preparing the migration/snapshot and then fresh isolated SQL/private-storage acceptance. `20261009064639_AddDirectSiteSchedulingAndTemporaryAmendments` represents the complete direct-site and amendment schema and was applied with all 13 migrations only to the newly created local `Cosmix_Testing` database. Synthetic explicit permission grants, authenticated workflows, persisted evidence, restart/revocation replay and constraint checks are recorded in [Verification.md](Verification.md). No development or production database was modified. No DbContext or Git operation is included.

## Confirmed product decisions

- **Site-only scheduling:** the site's shift applies to staff assigned to that site on the applicable date. No individual/team/department/staff-category priority is to be invented. Other model-supported populations are outside this confirmed workflow.
- **Direct authorized changes:** the user placed site shifts in the HR module, with module permissions delegated through Admin/SuperAdmin. An actor with the appropriate module permission changes normal and temporary site shifts directly. There is **no request, review or approval workflow** for these changes. This instruction supersedes the SRS approval wording and the earlier proposed approval-chain design.
- **Informational holidays:** the user delegated holiday presentation to implementation judgment. Holiday context does not cancel, shorten or override the site's duty schedule. No arbitrary calendar is selected merely because a Federal calendar exists.
- Preserve effective dates, immutable schedule versions, historical site membership, written change evidence, affected employees, server-side scope, current authentication, web MFA for administration, audit, concurrency, idempotency and locked periods. Direct authority does not waive those controls.
- Module authorization is not an automatic grant. No startup grant, grant-all Admin rule, fabricated approval request or real account permission write is part of this implementation.

SRS SHIFT-001 provides the effective-dated assignment baseline; SHIFT-002 provides date/evidence/affected-employee requirements; SHIFT-003 binds the applicable shift/policy on the attendance date; SHIFT-004 protects locked history. The latest user instruction overrides the separate approval step. Operational-zone precedence in SRS 6.3 does not establish shift rules.

## Existing storage and exact required schema change

The legacy schema requires `ApprovalRequestId` for the three records below. New direct changes cannot honestly supply an approval reference. Generating a dummy request, self-approving one or retaining a required foreign key with an invented ID would violate the user's instruction. The new site assignment also pins an optional exact holiday revision.

| Existing table | Required column change for migration owner | Preserve |
|---|---|---|
| `attendance.ShiftPopulationAssignments` | `ApprovalRequestId`: required `uniqueidentifier` to nullable `uniqueidentifier` | Existing approval IDs/FK, append-only rows, matching population constraint, dates and indexes |
| `attendance.ShiftPopulationAssignmentEnds` | `ApprovalRequestId`: required `uniqueidentifier` to nullable `uniqueidentifier` | Existing approval IDs/FK, unique one-time parent relation, reason/actor/time |
| `attendance.TemporaryShiftChanges` | `ApprovalRequestId`: required `uniqueidentifier` to nullable `uniqueidentifier` | Existing approval IDs/FK, original/replacement version evidence, actor/time/written evidence |
| `attendance.ShiftPopulationAssignments` | Add nullable `HolidayCalendarId` (`uniqueidentifier`), nonunique `IX_ShiftPopulationAssignments_HolidayCalendarId` and Restrict FK `FK_ShiftPopulationAssignments_HolidayCalendars_HolidayCalendarId` to `hr.HolidayCalendars.Id` | Exact immutable selected revision; null means no selected calendar; no automatic default |

The migration includes these six operations plus the [amendment table and its two indexes](Temporary-Amendments-Schema.md): nine Up operations total. The current model and snapshot match exactly. Only the fresh isolated testing database has been migrated in this task; any other target cannot serve these queries or direct writes until its separately authorized application. Existing approved history stays intact; no approval or calendar backfill is fabricated. The guarded Down migration refuses rollback if direct records, pinned calendars or amendment evidence exist; it never manufactures approval IDs.

Temporary storage retains legacy property names `ApprovedBy`/`ApprovedAtUtc`. A direct change must document those as the recording actor/time for the new path and must not report that a separate review occurred. Immutable audit evidence records the direct authorized operation. Legacy approval records retain their original meaning.

`TemporaryShiftChangeEmployee` remains a unique change/employee manifest saved with its new parent in the same `SaveChanges` call. Existing DbContext guards reject later manifest additions. No new employee identity or edit to HR transfers is involved.

## Concurrency, membership and history

Site-population parents have no rowversion. An expected deterministic timeline token revalidated inside the existing serializable operation, plus the unique end index, protects stale requests without adding a token column. Date-effective `EmployeeAssignment` records determine who belongs to the site. Overnight duty must validate the occupied next day too. Current master deactivation must not erase historical membership.

An assignment can be ended only once. Repeatedly changing its recorded end is unsupported; replacement appends the old end and a new assignment atomically. Multiple overlapping site assignments are a conflict, not a reason to choose a latest inserted record. Existing non-site records are not silently rewritten into site assignments.

`AccessScopeKind.Site` can represent the confirmed site boundary. Staff-category administration is outside this slice. Reuse explicit module permissions and delegated access rather than inferring authority from a role name alone. The approved isolated run exercised concurrent identical SQL-backed writes, current site scopes and denied frozen-result replay after revocation; this does not establish behavior on a development or production deployment.

## Remaining persistence limits

### Exact holiday revision selection

The nullable `ShiftPopulationAssignment.HolidayCalendarId` pins a calendar revision to the immutable effective-dated site assignment. This is the supported mapping and is installed in the isolated testing database; a separate category/team calendar-selection hierarchy is not introduced. There is still no calendar publication timestamp. `EffectiveFrom` describes a calendar period; `Revision` orders corrections to that period. Neither proves when a revision was known. Automatically choosing the highest revision for a historical date changes the historical answer.

The pure `RosterHolidayContext.Resolve` helper accepts an **explicit exact revision** loaded from that selected assignment by a trusted caller. It preserves that ID/code/revision, does not follow successors and returns only safe holiday IDs/names. Missing selection is `not_configured`; outside and inactive revisions are explicit states. Every result is informational and does not cancel duty.

The direct configuration path retains site, exact `HolidayCalendarId`/revision, effective interval and recording/audit evidence. Queries reconstructing what was known at a particular instant additionally need trusted publication/selection timing or an immutable resolution snapshot. Historical lookup uses the assignment's pinned revision; a later calendar correction does not automatically alter the old assignment. No additional calendar mapping table is created here.

### Temporary amendments and mixed originals

`TemporaryShiftChangeAmendment` now supplies explicit append-only cancellation/supersession links with recording actor, time, reason and protected evidence. A successor and its exact new manifest are saved with the amendment atomically. Cancelled/superseded originals remain history; no latest-by-time rule is used. At most one active change is allowed per site/date. See [amendment contracts and schema](Temporary-Amendments-Schema.md).

One temporary row has exactly one original and one replacement `ShiftVersionId`. A mixed-original-version affected set cannot be represented truthfully by one row. Split only through a defined atomic operation retaining each original/version/manifest, or reject the conflict. Do not discard employee-specific original-version evidence.

### Existing implementation safeguards

Site resolution, immutable version selection, active amendment chains, conflict handling and occupied next-day site checks remain required. Shift-version publication now validates affected site intervals and staff transfers, requires roster authority at every affected site, and protects active temporary original-version pins. Unsupported legacy/non-site populations still fail closed. Model classes alone are not completed APIs.

## Verification evidence and limits

Historical 8 October evidence: `VerifyApprovalModel.cs` originally executed **107 offline checks**, then **131** after the direct-write model changes, with zero database connection attempts. Those runs verified the then-pending six-operation gap. The 9 October continuation instead checks model/snapshot equality, the prepared migration's exact nine Up operations and safe rollback metadata; current executed counts are in [Verification.md](Verification.md). Generic approval-domain checks preserve legacy behavior; they do not introduce a new site approval workflow.

`VerifyHolidayContext.cs` used a failing absent-helper run followed by **47 passing checks**, exit 0. Checks cover explicit/no selection, exact corrected versus original revisions, inactive/outside boundaries, maximum date, malformed revision/holiday evidence, duplicate dates, cross-revision ownership and serialization privacy. No database, private files, authentication or permission changes were involved.

```powershell
$env:TEMP = Join-Path $PWD 'artifacts/site-geofences-temp'
$env:TMP = $env:TEMP
dotnet run --file docs/group-rosters/VerifyHolidayContext.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false
```

The offline model harness translates representative SQL Server query shapes without opening a connection. Neither offline harness establishes deployed SQL behavior, encrypted-disk replay, live authentication or usable HTTP workflows. The separately executed isolated results, including actual pinned-calendar transfer boundaries, belong to the current [feature verification record](Verification.md).
