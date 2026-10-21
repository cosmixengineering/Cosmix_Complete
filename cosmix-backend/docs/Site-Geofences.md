# Site Geofences

Implemented locally against the existing Sites and employee-assignment modules. No shifts/rosters integration is required for these configuration APIs. References: SRS 6.1, ATT-005/006/007, sections 7/13/14 and MD-003. This delivers boundary configuration and reusable geometry; ATT-007 punch/review-event persistence remains part of the future punch workflow.

## Routes and authorization

| Method | Route | Access |
|---|---|---|
| GET | `/api/v1/hr/sites/{siteId}/geofences?asOfDate=2026-10-10` | `hr.sites.read`, All scope, current web MFA |
| GET | `/api/v1/hr/sites/{siteId}/geofences/versions?page=1&pageSize=20` | Same read authority |
| GET | `/api/v1/hr/sites/{siteId}/geofences/versions/{versionId}` | Same read authority; version must belong to the specified site |
| GET | `/api/v1/hr/sites/{siteId}/geofences/history` | `hr.sites.manage`, All scope on an active fixed Admin/SuperAdmin assignment, current web MFA |
| POST | `/api/v1/hr/sites/{siteId}/geofences/versions` | Same fixed administrator authority; Idempotency-Key and web CSRF required |
| GET | `/api/v1/me/site-attendance-configuration` | Current eligible mobile/web employee session and `auth.self.read` for own employee |

All responses use no-store. Existing `auth.self.read` now explicitly includes the employee's own assigned-site attendance configuration. No new permission, role grant or owner bootstrap change is introduced. No manager/team/site-scoped administration is inferred: the existing Site master requires All-scope authority.

The self endpoint accepts no employee ID, site ID or date. The server rechecks the session, employment and self permission, then reads today's assignment using start-inclusive/end-exclusive dates. Overlapping assignments fail with `409 employee_assignment_state_conflict`. Site end dates are inclusive. It returns only site ID/code/name and the applicable boundary, with one of these statuses:

- `not_assigned`: no effective assignment or that assignment has no site.
- `not_configured`: assigned active site has no effective boundary yet.
- `site_inactive` / `site_outside_active_dates`: no usable current site configuration; boundary is null.
- `configured`: configuration is available, not an accepted punch or proof of location.

Administrative as-of reads retain historical boundary access even after the Site master is deactivated. Site's current status/dates are not presented as historical status evidence.

## Create a version

First GET the configuration to obtain `siteVersion` and `latestVersionNumber` (zero if none). Submit explicit management-approved coordinates, radius and maximum GPS accuracy. Numeric examples below are synthetic integration examples, not business defaults or recommended limits.

```http
POST /api/v1/hr/sites/{{siteId}}/geofences/versions
Content-Type: application/json
Idempotency-Key: site-boundary-example-001
X-CSRF-TOKEN: {{csrfToken}}

{
  "effectiveFrom": "{{futureBusinessDate}}",
  "latitude": 24.8607000,
  "longitude": 67.0011000,
  "radiusMeters": 200.00,
  "maximumAccuracyMeters": 30.00,
  "expectedSiteVersion": "{{siteVersion}}",
  "expectedLatestVersionNumber": 0,
  "managementApproved": true,
  "approvalReference": "management-approved-boundary-reference",
  "reason": "Initial approved site boundary"
}
```

Use the actual latest version number from GET; the example's zero is only for a new configuration. A successful response is `201`, containing `boundary`, `approvalReference` and `managementApproved`, with a `Location` pointing to the exact immutable version. ManagementApproved/reference capture the authorized administrator's attestation of external approval. They do not validate an external document, invoke an electronic approval engine, or select MD-003 values on management's behalf. Avoid personal data/secrets in references and reasons; these are normal site administration audit records.

Validation:

- Latitude in [-90, 90], longitude in [-180, 180], up to seven decimal places.
- Radius/maximum accuracy positive, up to two decimal places and no greater than 99999999.99, the existing decimal storage limit. This upper bound is not approval for an operationally appropriate radius.
- No omitted numeric inputs, silent rounding or hard-coded 80 metre setting.
- Explicit true approval attestation, nonblank approval reference/reason of at most 500 characters, with no control characters.
- Required canonical eight-byte Base64 Site rowversion and latest boundary number prevent stale writes.

## Dates, history, concurrency and retries

`EffectiveFrom` must be strictly after today's configured server business date and strictly after the latest version's effective date, within the Site's configured lifetime. The intended deployment timezone is Pakistan, using existing `MobileAuthentication:BusinessTimeZoneId`; an invalid/missing timezone fails with 503. Even the first version starts no earlier than tomorrow. The endpoint deliberately cannot retrospectively change a date-only boundary or replace a scheduled version in place. Management-approved retrospective/cancellation workflows are outside this slice.

The active version is the greatest EffectiveFrom <= the requested date. Its successor establishes its exclusive end without updating the older row. Future versions do not become current early. Any intersecting locked global attendance period rejects creation. Scheduled dates are appended in increasing order; out-of-order inserts return 409 rather than altering an earlier plan.

The existing `OrganizationOperation` executes live fixed-role authorization, boundary creation, audit snapshot and retry receipt under one SQL transaction. It serializes with current Site/access/employee administration, avoiding stale authorization and version races. Two writes with the same expected latest number cannot both append. The boundary has unique database keys for (SiteId,VersionNumber) and (SiteId,EffectiveFrom), plus the existing append-only EF guard.

The same actor/operation/key and identical request return the stored original 201 body without creating another version/audit record. Changed payload/key reuse returns 409. Authorization is rechecked before replay, including after revocation. The inherited retry receipt window is 24 hours; this is not a record-retention policy. A retry can return its original version even after it becomes effective or the Site later changes. GET the configuration again for current state.

Errors use ProblemDetails. Representative codes: `invalid_geofence_values` / `management_approval_required` / `geofence_requires_future_date` (400); `access_denied` / `forbidden` (403); `site_not_found` / `geofence_version_not_found` (404); `organization_version_conflict`, `geofence_version_conflict`, `geofence_requires_later_effective_date`, `attendance_period_locked`, `idempotency_key_reused` (409).

## Client consumption and future punch integration

```javascript
// Web: send the existing authenticated HTTPS cookies.
const response = await fetch('/api/v1/me/site-attendance-configuration', {
  credentials: 'include'
});
if (!response.ok) throw await response.json();
const configuration = await response.json();
// Render configuration.status explicitly; only configured has an applicable boundary.
```

Mobile clients use the existing Bearer token. The reusable `SiteGeofenceEvaluator.Evaluate` takes a server-selected `SiteGeofenceVersion` and reported latitude/longitude/accuracy. It returns `inside`, `outside`, `poor_accuracy`, `invalid_location` or `not_configured`, distance when evaluated and the exact version ID. Radius and accuracy comparisons are inclusive. Accuracy never expands the circle. Haversine geometry uses a mean Earth radius of 6371008.8 metres and handles poles/antimeridian/antipodal points.

This calculation alone does not prove GPS authenticity, approve Check In/Out, create an ATT-007 event, check roster eligibility, verify faces or permit an offline punch. A future punch handler must resolve assignments and boundary on the server using authoritative timing, preserve the selected version ID with raw evidence and enforce the remaining SRS rules. Operational zones and continuous location tracking are separate.

## Files, schema and handoff

Feature files are under Application/Attendance/Sites, Domain/Attendance/Sites, Infrastructure/Repositories/Attendance and Api/Controllers/Attendance. Shared edits are two service registrations in Application DI, one repository registration in Infrastructure DI, a clarified existing self permission description and the 201 Location OpenAPI metadata. No Program, DbContext, schema, migration, package or role grant changes are required. The other developer should preserve these additive registrations when work is integrated later.

See [verification record](Site-Geofences-Verification.md) for executed checks and acceptance boundaries.
