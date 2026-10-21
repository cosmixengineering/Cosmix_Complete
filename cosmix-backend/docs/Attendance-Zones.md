# Attendance Zones and duty-only live location

This local slice implements the user's two confirmed types: `OPERATIONAL` allowed area and `RED` prohibited area. Legacy Amber/other type rows remain stored but are not accepted by the new monitoring workflow. Zones never alter raw punches, worked time, pay or access exceptions.

## Authority and explicit activation

The five custom-role permissions are `attendance.zones.read`, `.manage`, `.review`, `.location.read`, and `.self`. Management uses current web MFA/CSRF; mobile sample writes require the current verified registered device. No role is automatically created or granted. Admin/SuperAdmin access administration follows the existing independently protected access module.

Zone creation, immutable boundary versions, configuration versions, population assignment/replacement/ending use explicit site scope, a reason, retry receipt and atomic audit. Configuration/version writes require the latest version number; assignment replacement/ending is one-way and serialized. The latest started configuration supersedes earlier settings; when it expires, older settings do not reactivate. A fresh configured window/notice is required. Site identity is included in creation/configuration retry fingerprints. New employee override requests are rejected because override/Red exception authority is not confirmed. Conflicting applicable versions remain unavailable. Different population memberships that select the same immutable version do not create a conflict.

Monitoring does not turn on when the application starts. It requires all of:

- `AttendanceZones:Enabled=true` after explicit management approval;
- `AttendanceZones:RetentionEnabled=true` and a recent successful complete cleanup sweep;
- `ZoneLocationStorage:Enabled=true` with separately provisioned private location storage;
- a current site configuration with explicit accuracy, warning distance, minimum interval, incident retention days, maximum monitoring duration, latest-location visibility seconds and visible notice;
- current approved employee/site eligibility, an accepted CheckIn backed by consumed passing liveness/match evidence, an open latest work session and no later accepted Checkout;
- current self permission, a matching verified mobile device and acknowledgement of the exact current notice.

There are no default business values. Bounds in validation are technical resource limits, not selected management policy. Missing Checkout does not allow indefinite monitoring: maximum duration is measured from the accepted CheckIn, not from a later notice. Transfer, assignment end, configuration change, permission/session revocation, Checkout and expiry invalidate new samples. Manual corrected duty is not fabricated as an accepted face-verified CheckIn.

## Configuration and applicability

Circle boundaries and simple GeoJSON exterior polygons are supported. The bounded polygon implementation permits 3–128 unique vertices plus the repeated closing point, no holes/self-intersections, no antimeridian/polar shapes, and maximum one-degree extent. Unsupported geometry is rejected explicitly. Accuracy crossing a boundary is uncertain, never confidently safe. Warning distance and maximum accepted accuracy come from configuration. The two-centimetre calculation tolerance is numerical only.

Population assignments support site, employee, team and role. Current effective-dated HR/team/role membership is resolved by the server, including terminations and revocations; assignments are restricted to their zone's site. New `isEmployeeOverride=true` is rejected. Existing override evidence is retained but not silently used. Assignments pin an immutable boundary version. Replacement ends the old matching population assignment and adds the replacement in one transaction; historical rows are never edited.

## Location lifecycle and privacy

A sample requires explicit latitude, longitude, accuracy, UTC device timestamp, source, independently random `requestNonce` and expected checkpoint version. The nonce is separate from any public ID and must never be reused as a public event identity. The ordinary permanent receipt/audit receives only the SHA-256 fingerprint of the complete request including this nonce. Neither nonce nor coordinates appear in permanent operation evidence, responses, outbox or logs.

Every accepted interval updates one optimistic checkpoint with the latest encrypted location reference and visibility deadline. Safe samples expire at that short visibility deadline. Boundary warning/transition events retain their location only until the explicitly configured incident retention deadline. Normal event/history/review responses are coarse and contain no GPS or private storage references. Live readers require the separate location permission plus original/current scopes and current accepted duty; location is denied after Checkout even if an incident object's retention period has not elapsed. There is no public historical route/location export.

The dedicated store reuses existing authenticated encryption and key files; it must not overlap ordinary private or biometric storage. Its private root contains preprovisioned `objects` and `expiry-index` children. It never creates or rotates deployment keys. A durable metadata-only allocation index is flushed before ciphertext allocation, so a rolled-back SQL transaction cannot strand an unindexed sensitive object. It contains no GPS or nonce. New indexes use expiry-hour buckets and bounded streaming pages instead of sorting the whole archive for every page. Legacy flat indexes remain readable. Allocation, reads and deletion share an in-process gate and an OS-held local filesystem lease, so cleanup cannot erase an in-flight allocation. Expiry is rechecked after obtaining the lease and after writing; an expired write keeps its cleanup index and cannot report a live success. This local filesystem adapter does not claim distributed/network-share acceptance.

The gated product `ZoneRetentionWorker` processes bounded expiry pages and database incident receipts. Deletion validates exact event/employee/window/purpose/expiry and authenticated content, deletes only canonical owned files, and records an append-only incident tombstone/audit separately. Missing owned objects are retry-safe. Incomplete/tampered data remains an explicit cleanup failure; monitoring becomes unavailable rather than claiming deletion succeeded. A stalled cleanup heartbeat also prevents collection. Product worker registration does not create a Codex automation.

## Warnings and review

The response returns the configured employee instruction. New warning/transition evidence is append-only; repeating the same position and warning state does not create repeated permanent incidents. A new approach warning is retained and notified even when Inside/Outside does not change; clearing and approaching again creates a new warning. Warning notifications stage in the same transaction for the employee and current active web accounts with explicit site/All `zones.review` authority. Role permission and scope must belong to the same effective assignment. Fan-out is bounded; notification text is generic and contains no employee name/location. Opening detail always rechecks current authority.

Employees can acknowledge or dispute their own coarse events. An authorized site reviewer appends a factual `Reviewed` response with expected last response identity, reason, audit and retry protection. This is not a disciplinary decision, permission to enter Red, or a payroll correction. Those authorities remain unconfirmed.

## API and client sequence

There are 18 operations: 13 under `/api/v1/attendance/sites/{siteId}/zones`, and five under `/api/v1/me/attendance/zones`. Site routes cover list/detail/create/version history, configuration history/write, assignments/history/end, event queue/review and current window/location reads. Self routes cover My Zones, notice/start, sample, event history and acknowledgement/dispute. OpenAPI documents exact request contracts and failure states.

1. Management creates a zone/boundary, records explicit configuration, and assigns the pinned version to a supported population. Configuration alone does not enable runtime collection.
2. After accepted CheckIn, the mobile client reads My Zones and visibly displays the returned notice. It hashes the exact UTF-8 notice, then submits assignment/configuration/session IDs with `noticeDisplayed=true` and an idempotency key.
3. Keep the returned checkpoint version. Send a fresh random nonce and live UTC capture at the approved interval; replace the expected version with the successful response version. Retry the exact request/key after uncertain delivery; changing coordinates under the same key conflicts.
4. Stop collection when My Zones is unavailable, a window/configuration changes, Checkout completes, or the maximum window ends. Never queue an off-duty historical location stream for later submission.
5. Authorized reviewers discover current windows for a site employee and request only that window's latest location. Coarse event history uses an encrypted actor/filter-bound cursor and bounded candidate scan, with no leaked total count.

## Deployment and verification limits

Migrations 16 and 17 contain the new model tables, nullable legacy location/provenance columns and separate coarse LastWarning checkpoint. They are applied only to approved isolated `Cosmix_Testing`; prior applied migrations were not rewritten. Encrypted storage passed 43 synthetic real-file checks. Actual zone SQL workflows, mobile session/device flow, concurrent SQL writers, process-crash recovery and management-approved runtime configuration are still required before activation. Exact evidence is recorded in [Attendance-Zones-Verification.md](Attendance-Zones-Verification.md).
