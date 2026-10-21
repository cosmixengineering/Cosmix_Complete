# Shifts domain

Source: SRS 6.2; SHIFT-001 to SHIFT-004.

`Shift` identifies the schedule; immutable `ShiftVersion` records local start/end times,
explicit end-day offset, planned minutes, paid/unpaid break minutes and applicable weekday
flags. All schedules use Asia/Karachi business time. The explicit offset distinguishes
same-day and cross-midnight schedules. Duration must be positive and at most 24 hours;
configured minutes and breaks must fit the schedule. No working-week, overtime, grace,
deduction or payroll formula is inferred.

Select the latest version whose `EffectiveFrom` is not after the attendance date.
Versions cannot share a shift/start date and cannot be edited/deleted through tracked saves.
Historical punches retain their selected version. Version approval, retirement, assignment
overlap handling, work sessions and calculations remain future service work.
