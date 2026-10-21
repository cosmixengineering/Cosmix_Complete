# Rosters domain

Source: SRS 6.2; SHIFT-001 and SHIFT-002.

`EmployeeShiftAssignment` links the shared HR employee to a shift and optional site.
The effective interval includes `EffectiveFrom` and excludes `EffectiveTo`; null end
means open-ended. A rowversion supports future concurrency-aware edits. Referenced
employees, shifts and sites use restrictive deletion.

Team rosters, temporary changes, approval evidence, transactional overlap checks and
authorization are not implemented. The table does not imply that a referenced draft or
inactive employee may record attendance. Selection of an approved applicable shift version
and preserving it on the punch remain application-service responsibilities.
