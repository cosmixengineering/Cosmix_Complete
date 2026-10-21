# Shifts

Owner: Developer 2.

Source: SRS 6.2; SHIFT-001 to SHIFT-004.

Same-day/cross-midnight shift definitions, breaks and effective rule references. Do not rewrite locked history when a shift changes.

ShiftService now implements master create/list/detail/rename/status and immutable effective-dated schedule versions. SchedulingOperation owns scoped live authorization, audit and idempotency. See docs/Shifts-Rosters.md for permissions, dates, required runtime provisioning and actual verification limits. Group/temporary workflows and attendance/pay calculations remain separate. Keep EF Core and HTTP types out of this project.
