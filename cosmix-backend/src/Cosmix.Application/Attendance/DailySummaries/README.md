# DailySummaries

Owner: Developer 2.

Source: ATT-009, SHIFT-003, SRS 8.

Implemented: factual own/scoped daily views, calendars, revision history and unlocked immutable
recalculation with protected source inputs. Actual elapsed time is separate from payable time.
No sessions does not imply Absent; policy-unavailable results have null pay/late/OT values.

`IAttendancePolicyCalculator` is the explicit integration contract. The local checkout has no
implemented policy calculator; the default reports unavailable. Historical membership, original
session sites and frozen lock evidence are authorized before returning results. No raw event is edited.

See `docs/attendance-completion/Summaries.md` for routes, client examples and schema boundaries,
and `docs/attendance-completion/Verification.md` for executed checks versus pending SQL acceptance.
