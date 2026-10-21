# Organization Masters Backend

Implemented on 7 October 2026 in the existing four-project backend. This is the first HR reference-data
slice, not employee onboarding, site assignment, attendance processing or a frontend implementation.

## Business Flow

1. An authorized web user maintains cities, physical offices, departments, designations, employment
   types and the two staff categories. Fixed Admin/SuperAdmin maintains the separate Site master.
   There are no seeded business records.
2. The next Employee module will let HR choose Office Staff or Site Staff.
3. Office Staff requires HR to select the relevant physical office. Site Staff has no HR-selected site.
4. HR submits the employee for Admin review. Admin owns initial/later site assignment.
   That future module must retain reassignment history rather than overwrite the previous site.
   Site creation/editing is implemented now; assigning an employee is explicitly deferred by the PM.

`Branch` is the existing database/API name for an office. It is not a site. Staff category, designation,
employment type and login role remain separate. The supplied SRS has not been silently rewritten.

## Permissions and Setup

All these APIs require a current **web session with MFA**, plus the matching permission paired with
an `All` scope on that same assignment. Mobile self-service cannot manage or query these endpoints.
These are global reference data permissions, not access to employee records or salary details.

| Operation | Permission |
|---|---|
| Read masters and their change history | `hr.organization.read` |
| Create/edit/activate/deactivate masters | `hr.organization.manage` |
| Read holiday calendars and their history | `hr.holidays.read` |
| Publish a calendar or replacement revision | `hr.holidays.manage` |
| Read site list/detail, including HR lookup | `hr.sites.read` |
| Create/edit/activate/deactivate sites or read site change history | `hr.sites.manage`, on a current fixed Admin/SuperAdmin assignment |

Use the existing access-management flow to give a custom HR role the required permissions. A role
name alone grants nothing. Read and manage are separate; a working management screen normally needs
both. The private first-owner setup explicitly includes these permissions for newly initialized fixed
roles. It does not silently elevate existing accounts/roles. An installation whose owner was already
initialized needs a separately reviewed fixed-role grant upgrade before those roles can use new rights.

No authentication settings were enabled, owner created or SMS provider selected in this task. Actual
browser login still requires approved owner details, configured authentication and working SMS. Do not
seed a fake admin or add a login bypass to test these endpoints.

For Site mutations/history, the fixed role and the `All`-scope manage permission must belong to the
**same effective role assignment**. An unrelated Admin assignment cannot supply missing scope for a
custom role. Inactive, expired, revoked, future or another account's assignments do not qualify.
The check runs again before replaying a previous response. HR can receive the read permission without
the ability to edit sites. Selecting a supervisor does not grant that employee any access role/scope.

## Master Endpoints

All routes below start with `/api/v1/hr/`:

| Route group | Meaning / extra input |
|---|---|
| `departments` | Department name/code |
| `designations` | Job designation, not authorization role |
| `cities` | City; optional `province` |
| `branches` | Physical office; required active `cityId` for a new office |
| `staff-categories` | New codes must be `OFFICE` or `SITE` |
| `employment-types` | Management-supplied employment types; no guessed defaults |

Each group has:

| Method and suffix | Result |
|---|---|
| `GET /` | Paginated list |
| `GET /{id}` | One record and its current `version` |
| `POST /` | Create; 201 with Location pointing to detail |
| `PUT /{id}` | Update permitted descriptive fields; code is immutable |
| `POST /{id}/status` | Deactivate or reactivate without deleting references |
| `GET /{id}/history` | Actor/time/reason and before/after response snapshots |

List queries: `page` (default 1), `pageSize` (default 20, max 100), `search` (max 100 chars),
`isActive`, `sortBy=code|name`, `descending`. History is newest-first, with page/pageSize;
the other list filters are not history filters. Responses use the existing `PagedResult` envelope.

Codes are 1-30 ASCII letters/digits/underscore/dot/hyphen, start with a letter/digit and normalize to
uppercase. Names are required, max 150 characters (cities: 100). Province is optional, max 100.
New office city must exist and be active. Editing an office may retain its existing inactive city,
but switching city or reactivating an office requires an active city. Existing office/city references
are never deleted by a status change. Employee onboarding must later revalidate active selections.

`cityId` is accepted only for offices, and `province` only for cities. Unsupported JSON properties,
such as `siteId` on an HR master or `code` on an edit, are rejected. Historical non-OFFICE/SITE staff
codes are not rewritten; they cannot be newly created or reactivated through this API.

### Example: Department

Every mutation needs an `Idempotency-Key` header: 8-100 characters from letters, digits, `_ . : -`.
Use a fresh key for a new action and the **same** key/body when retrying an interrupted action.
Browser-cookie mutations also need the existing `X-CSRF-TOKEN` flow.

`POST /api/v1/hr/departments`, header `Idempotency-Key: department-create-001`:

```json
{ "code": "HR", "name": "Human Resources" }
```

Read `id` and `version` from the response. Then `PUT /api/v1/hr/departments/{id}` with a new key:

```json
{
  "name": "Human Resources and Administration",
  "expectedVersion": "<version from latest response>",
  "reason": "Approved department name correction"
}
```

For `/status`, send `isActive`, `expectedVersion` and `reason`. Reason is required, max 500.
The version is SQL rowversion encoded as Base64, not a timestamp or an editable sequence number.
A stale version returns 409: reload the record and reconcile changes before submitting a new action.
There is no DELETE endpoint. Deactivation does not erase or automatically move existing employees.

## Site Master

Route group: `/api/v1/hr/sites`. It has the same six list/detail/create/update/status/history operations
shown above, but uses the Site-specific permissions and fixed-role restriction. Writes require
`Idempotency-Key`, browser CSRF and, for update/status, `expectedVersion` and `reason`.

| Create/edit field | Validation |
|---|---|
| `code` | Create only; immutable, uppercase, same 1-30 character code rules as masters |
| `name` | Required, max 150 |
| `address` | Required, max 500 |
| `cityId` | Required existing active City for a new selection |
| `clientName` | Optional, max 200 |
| `projectId` | Optional existing active Project for a new selection; no Project creation here |
| `startsOn` | Required business date |
| `endsOn` | Optional, cannot precede start; inclusive end under the existing Site model |
| `supervisorEmployeeId` | Optional approved, currently active employee whose joining date has arrived |

Supervisor validation uses the configured business timezone and current effective status history.
Future joining dates, effective suspension, ambiguous latest status history or invalid timezone cannot
qualify a new supervisor. No employee name, phone, identity document or salary is exposed in Site DTOs.
Supervisor/account lookups belong to the later scoped Employee module, not an unprotected directory.

An edit may retain an unchanged inactive reference to correct descriptive metadata. A newly selected
reference must be eligible, and reactivation checks all references again. Optional project/supervisor
references can be cleared with `null`. Old incomplete Site rows remain readable; editing/reactivating
them requires the mandatory current fields. Site code and ID remain stable.

`isActive` is an administrative flag; the date interval does not run a scheduler or decide attendance
eligibility. Editing dates/status does not change historical attendance or move employees. List
responses include current City name; immutable history/retry responses retain the saved snapshot.

This finishes Site **reference data**, not the whole SRS Sites feature. Coordinates, geofence versions,
radius/accuracy decisions, site employee assignments/reassignments and attendance eligibility remain
separate pending work. No office is assigned to Site Staff by these APIs. The existing assignment
schema's required `BranchId` must be reviewed in the Employee module for the approved office/site flow;
do not fill it with a fake office. Site APIs reuse existing tables and require no additional migration.

## Holiday Calendars

Route group: `/api/v1/hr/holiday-calendars`.

- `GET /`: list the latest revision of each code/date interval; `includeHistory=true` includes old revisions.
- `GET /{id}`: exact immutable revision and all its holidays.
- `POST /`: publish revision 1, returning 201 and its new ID/version.
- `POST /{id}/revisions`: publish a replacement of the latest revision, returning 201 and a **new ID**.
- `GET /{id}/history`: change evidence for that exact revision ID; follow `supersedesCalendarId` for its predecessor.

Create example (sample notification reference is illustrative, not an approved holiday policy):

```json
{
  "code": "OFFICE-2027",
  "name": "Approved office holidays 2027",
  "effectiveFrom": "2027-01-01",
  "effectiveTo": "2028-01-01",
  "governmentNotificationReference": "<approved private notification reference>",
  "holidays": [
    {
      "date": "2027-01-01",
      "name": "<approved holiday name>",
      "notificationReference": "<approved private notification reference>"
    }
  ]
}
```

Calendar codes: max 40; calendar/holiday names: max 200; notification references: max 500.
The `holidays` array must be supplied explicitly; `[]` intentionally publishes an empty list.
At most 400 holidays per request. Dates are business dates; the interval is **start-inclusive,
end-exclusive**. Null end means open-ended. Duplicate holiday dates, holidays outside the interval
and overlapping separate intervals for the same code are rejected.

Revision input contains `name`, `governmentNotificationReference`, `isActive`, the complete replacement
`holidays` list, `expectedVersion` and `reason`. Code/start/end stay fixed. Send `isActive:false` to
withdraw through a new inactive revision. Old calendar/holiday rows remain readable and unchanged.
An old revision that already has a successor cannot be revised again. This does not recalculate pay,
choose calendars for staff, or override existing attendance/leave decisions.

Notification references are private metadata, not uploaded files or public download URLs. General
file upload, scanning, protected download and calendar applicability are later workflows.

## Consistency and Errors

- Current authorization is checked inside the same transaction as the write and before every retry.
- A successful business mutation, AuditEvent, OrganizationChange snapshot and retry receipt commit together.
- An identical retry returns the original saved response, even if the record was edited afterward.
- Reusing a key with different input, reusing an expired key (24-hour retry window), duplicate code,
  outdated revision or stale version returns 409. Expiry does not delete history or recycle a key.
- Input errors return 400; anonymous requests 401; insufficient web permissions/mobile requests 403;
  missing IDs 404; temporary transaction contention 503. Unexpected exceptions expose no stack traces.
- Mutations have bounded request bodies (masters/sites: 8 KiB, calendars: 1 MiB) and audit snapshots.
  A serialized snapshot over 524,288 characters returns 400 `organization_snapshot_too_large`
  and rolls back the mutation. Unicode escaping can reach that limit before the item-count limit.
- SQL `rowversion` protects updates. Audit timestamps use UTC; holiday dates use `DateOnly`.
- Append-only guards reject EF updates/deletes of calendar revisions, holidays and change evidence.

## Manual Acceptance Order

1. Use the actual approved local database and authentication setup; sign in through web password + SMS.
2. Obtain browser context/CSRF using `GET /api/v1/auth/web/csrf`. Swagger uses browser-managed cookies,
   not a manually pasted mobile token. Follow [Shared Authentication](Shared-Authentication.md).
3. Create a city, office, department, designation, approved staff categories and employment types.
4. List/search/filter, open detail, edit using the returned version, deactivate and reactivate.
5. Repeat a create with the same key/body: it must return the original ID, not create another row.
6. Try a stale version, different body under the same key, missing city, unknown JSON field and mobile
   session. Confirm rejection without a partial write. Check history after every accepted action.
7. Publish a calendar, replace it, inspect both revisions and try revising its outdated predecessor.
8. As Admin/SuperAdmin, create a site with a real City ID, then edit/status/history using the latest version.
   Give HR `hr.sites.read` and confirm list/detail work while Site writes/history are denied. A custom
   role with `hr.sites.manage` alone must still be denied. Site create must not insert an employee assignment.
9. Continue with Employee onboarding/approval only after this reference-data slice is accepted.

Detailed executed checks and their limitations: [Organization Masters Verification](Organization-Masters-Verification.md).
