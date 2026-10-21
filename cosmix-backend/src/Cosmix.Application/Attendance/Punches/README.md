# Punches

Owner: Developer 2.

Source: SRS 7; ATT-001 to ATT-011.

Append-only raw IN/OUT evidence, server UTC receipt time, device evidence, idempotency/replay checks, enrollment/assignment/geofence validation and review states. More than one punch per employee/date is valid. App-only production channel; no attendance hardware or external messaging.

Status: local registered mobile evidence capture, assessment, factual session pairing and self state/history
are implemented. Accepted Check In still requires the unconfigured approved face provider and dedicated
template store; offline/review evidence never produces fake success. No financial policy is inferred.

See `docs/Attendance-Capture.md` and `docs/attendance-capture/Verification.md` for routes, client examples,
schema requirements, executed offline checks and the remaining provider/SQL/management acceptance.
