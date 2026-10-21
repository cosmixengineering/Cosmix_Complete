# Corrections

Owner: Developer 2.

Source: SRS 7.3; ATT-008 and ATT-010.

Implemented locally: own source preview, immutable written typed proposals, own history/detail,
pending-request cancellation and safe own exception notices. Historical roster/source versions,
protected submission-bound evidence, self scope, rowversion, audit and replay controls are used.

New proposals are Pending with `routing_not_configured` and `changesAttendance=false`.
No reviewer role, approval, attendance adjustment, pay calculation or lock reopening is inferred.
Those workflows remain incomplete pending confirmed authority and rules. No raw/session/summary
mutation or new schema is introduced.

See [feature/client documentation](../../../../docs/Attendance-Corrections.md) and
[verification limits](../../../../docs/attendance-corrections/Verification.md).
