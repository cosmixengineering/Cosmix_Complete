# Punches domain

Source: SRS 7; ATT-001 to ATT-011.

`RawPunchEvent` is immutable received app evidence. Stored evidence is not proof of accepted
attendance: rejected attempts can be recorded and nullable coordinates allow location-denied
attempts to remain auditable. The application must supply the authenticated employee ID and
server receipt timestamp. Device timestamps and offline flags are untrusted evidence.
The registered-device and verification-attempt references are opaque until those features
are designed; no face provider or enrollment success is implied. Check Out has no face
requirement. More than two punches per employee/day are valid.

The employee/idempotency-key unique index and normalized SHA-256 request fingerprint support
future retry handling; the service must compare fingerprints, return the prior result on an
identical retry and reject conflicting reuse. Canonical request hashing, replay prevention,
device ownership, enrollment, current employment/assignment checks, verification results,
corrections, review states and summary calculations are still required. Raw events expose no
update/delete methods and tracked changes/deletes are rejected by the context. Bulk SQL or
direct database access must not bypass that boundary; runtime database permissions and any
database-level immutable storage protection are deployment work.

The site/geofence composite FK prevents cross-site references. The employee/assignment
composite FK prevents assignment references belonging to another employee. A nullable
verification reference deliberately permits recording a failed Check In attempt; an
accepted Check In must still fail closed through the future verification service.
