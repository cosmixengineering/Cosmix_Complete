# Missing checkout and three-hour overtime verification — 30 October 2026

## Result and expected behavior

Current application logic excludes open sessions from factual worked duration. The policy snapshot producer returns `attendance_session_still_open` instead of finalized daily policy time. The configured OT duration evaluator produces no eligible intervals from an open session, and OT submission rejects it with `overtime_closed_session_required`.

A missing-checkout correction is pending until the authorized independent HR reviewer approves. Submission does not close the session. Approval appends a session interpretation with the requested/evidenced end time, preserves the original open session, and does not invent a raw checkout punch. Approval time/current time is not substituted for departure time.

For an explicitly configured nine-hour shift from 12:00 to 21:00 Pakistan time:

| Case | Result |
|---|---|
| Employee leaves at 21:00 without checkout | Open/incomplete; no eligible or approved OT from elapsed home hours |
| HR approves actual departure at 21:00 | Closed at actual departure; no extra eligible time |
| Employee works until midnight and completes valid checkout | Exactly 180 eligible extra minutes, retaining the original business date |
| Employee arrived one hour late and checks out at midnight | Nine actual duty hours must complete first; 120 eligible extra minutes |
| Eight-hour configured shift plus three hours of duty | 180 eligible minutes under the explicit matching policy; no hardcoded nine-hour fallback |

Eligible time and approved OT are distinct. OT request submission has null approved minutes and no awarded money. Independent HR approval can award 180 minutes. Daily policy output takes OT from retained **approved** awards; checkout alone does not automatically create an approved award or release payment. Policy, site eligibility, valid duty evidence, permissions and no overlapping credit remain prerequisites.

The backend cannot infer a person's actual departure from an omitted checkout. A correction requires truthful evidence reviewed by HR; these tests do not establish physical presence or continuous location monitoring.

## Executed verification

- `dotnet run --file docs/attendance-completion/VerifyOvertimeRules.cs -c Release`: **36 passed**. Added exact 21:00/midnight, forgotten-checkout, late arrival and configurable eight-hour tests against `ConfiguredOvertimeDurationPolicy` with synthetic resolved policy inputs.
- `dotnet run --file docs/attendance-corrections/VerifyCorrectionService.cs -c Release`: **200 passed**. Added pending/no-session-change, zero open-session duration, exact approved departure and preserved original session checks against actual correction services.
- `dotnet run --file docs/attendance-completion/VerifyOvertimeService.cs -c Release`: **160 passed**. Added open-session submission rejection with no request/receipt/notification and pending-to-independent-approval of 180 minutes. The service fixture uses synthetic IO and duration evidence; actual configured duration is separately exercised by the rule suite.
- `dotnet run --file docs/attendance-policies/VerifyDailyCalculation.cs -c Release`: **193 passed**, including the open-session rejection and approved OT separation checks.
- `dotnet run --file docs/attendance-completion/notifications/VerifyMissingCheckoutReminders.cs -c Release`: **44 passed**, including configured reminder timing, duplicate prevention and suppression after closure. Real notification delivery was not exercised.

Total: **633 standalone checks passed**, all five commands exited successfully. Scoped `git diff --check` also passed.

The correction suite initially stopped at `access_denied` because its old synthetic permissions omitted validity evidence required by the current production guard. Test grant constructors were updated to provide explicitly dated synthetic authority. The same stale fixture pattern was updated in the OT service suite. Production permission validation was not changed.

Only existing standalone verifier files and this documentation were changed for this task. No new test project, migration, database write, application policy change, real employee, provider invocation or production source change was required. These are freshly executed Release standalone domain/service checks with in-memory dependencies, **not live authenticated HTTP/SQL or mobile/device acceptance**.

## Source paths

- `src/Cosmix.Application/Attendance/DailySummaries/AttendanceFacts.cs`: excludes open-ended sessions from duration and selects latest revisions.
- `src/Cosmix.Application/Attendance/Calculation/AttendanceCalculationSnapshotProducer.cs`: blocks unfinished duty and retains only approved OT for totals.
- `src/Cosmix.Application/Attendance/Calculation/ConfiguredAttendancePolicyCalculator.cs`: exposes approved OT minutes.
- `src/Cosmix.Application/Attendance/Overtime/OvertimeFormula.cs`: closed-duty, scheduled-start and actual-duration rules.
- `src/Cosmix.Application/Attendance/Overtime/ConfiguredOvertimeDurationPolicy.cs`: effective configured standard-duty duration.
- `src/Cosmix.Application/Attendance/Overtime/OvertimeRules.cs`: valid closed session and independent award eligibility.
