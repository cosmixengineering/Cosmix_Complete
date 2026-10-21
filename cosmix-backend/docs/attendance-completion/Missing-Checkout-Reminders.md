# Shift-configured missing Check Out reminders

The user confirmed on 12 October 2026 that the shift creation/version form must choose how long
after scheduled end to remind an employee, for example 30, 60, or 120 minutes. The delay belongs
to the immutable shift schedule version. It is not a company-wide default or a total-duty limit.

## Client contract

The existing shift creation `initialSchedule` and version publication `schedule` accept:

```json
{
  "effectiveFrom": "2026-11-01",
  "startTime": "09:00:00",
  "endTime": "18:00:00",
  "endDayOffset": 0,
  "scheduledMinutes": 540,
  "paidBreakMinutes": 0,
  "unpaidBreakMinutes": 0,
  "applicableWeekdays": 127,
  "missingCheckoutReminderAfterMinutes": 60
}
```

The delay may be any positive integer number of minutes. Omitted/null means explicitly
unconfigured; existing versions are not assigned a guessed delay. Zero/negative is invalid.
The shift form should label this field “Missing Check Out reminder after shift end” and offer
the chosen durations. Existing shifts are changed by publishing a future immutable version
through the existing scoped, audited, idempotent and rowversion-protected API.

Shift detail and effective roster/My Shift return the pinned version's delay when configured.
Null is omitted from serialized existing requests/responses so old replay fingerprints and
protected roster snapshots retain their original shape. No separate reminder HTTP route or
permission grant is introduced.

## Durable behavior

- A bounded worker scans employees with a latest open duty whose original shift version has
  a delay. A 09:00–18:00 shift with 60 minutes becomes due at 19:00 Asia/Karachi. For 20:00–05:00
  with 60 minutes the deadline is 06:00 the following day, retaining the original business date.
- The session's pinned `ShiftVersionId` supplies the schedule. Later versions, temporary
  schedules and employee transfers cannot substitute a new deadline for that original duty.
- The opening punch must still have the latest accepted/accepted-with-exception assessment,
  and match the employee, site, version, business date and server start time. Manual duty,
  pending/failed evidence, locked periods and conflicting open duties do not produce reminders.
- Only the existing employee account with approved, currently active employment and enabled
  web/mobile access can receive a generic in-app missing-event notice. Current effective
  employment status and account lock state are checked before enqueue and again at delivery.
- One purpose-protected payload and outbox intent identify the session key, original revision,
  shift version, employee/account and exact deadline. Repeated sweeps reuse the durable identity.
  The producer's existence-only `READUNCOMMITTED` probe avoids waiting for a dispatcher-held
  outbox row while holding the authentication lock. It reads no payload, recipient or status.
  A dirty positive may skip a sweep until the next pass; a missed identity still fails the
  database's unique outbox identity and rolls back. It cannot authorize a notice or grant access.
- Delivery rechecks all eligibility and the exact source revision. Check Out, a correction,
  recipient replacement or other lost eligibility suppresses the queued notice with an audit.
  The existing lease-owned outbox transaction atomically saves notification and completion;
  aborted/expired dispatch cannot commit a duplicate notice.
- A reminder never closes a session, rejects overtime, subtracts pay, or declares an absence.
  There is no inferred excessive-continuous-duty threshold and no recurring alert escalation.

## Activation and schema

Both `MissingCheckoutReminders:Enabled` and `AttendanceJobs:Enabled` must be explicitly true.
They default to false. Operational polling defaults to 60 seconds and 25 candidate employees
per page; these are worker throughput settings, not business delay defaults. Poll range is
5–3600 seconds; batch range is 1–100. When disabled, the handler is unregistered so queued
reminders remain unclaimed instead of exhausting retry attempts. Eligibility is checked again
if delivery is later enabled.

The schema addition is nullable `attendance.ShiftVersions.MissingCheckoutReminderAfterMinutes`
with `NULL OR > 0` check. Existing rows stay null. Root integration owns the new incremental
migration and snapshot; applied migrations 1–17 are unchanged. No real worker activation or
database migration is implied by this document.

## Verification

On 12 October 2026, **43 synthetic service/domain checks passed** in
[`notifications/VerifyMissingCheckoutReminders.cs`](notifications/VerifyMissingCheckoutReminders.cs).
They exercise explicit and legacy delays, same-day/overnight boundaries, retry deduplication,
conditional delivery, stale corrections/recipients, disabled gates, conflicts and protected-purpose binding.
**23 SQL provider translation checks passed** in
[`notifications/VerifyReminderQueries.cs`](notifications/VerifyReminderQueries.cs) inspect the actual
private repository queries while an interceptor forbids every connection. There were zero
connection attempts. The query harness first reproduced an untranslatable post-projection
ordering; the candidate query now orders employee numbers before projecting the bounded result.
These are not real SQL/lease/concurrent-write acceptance.

Affected regressions also passed in the same continuation: **41** schedule/resolver checks,
**171** site-scheduling checks, and **55** geofence service/operation checks. The shared continuation
model harness passed **16** offline model/migration checks, including the protected downgrade
guards, and generated the local migration 17-to-18 SQL without executing it.

Outstanding acceptance: approved isolated SQL fixtures for accepted Check In, competing sweep
and dispatch, immutable temporary/transfer schedules, Check Out/correction racing delivery,
revocation and lease restart, plus a real encrypted payload and inbox read. No production
accounts, attendance records or database writes are used by these standalone checks.
