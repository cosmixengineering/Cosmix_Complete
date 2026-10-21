# Policy and leave client examples

These are request-shape examples, not approved company settings or database fixtures. Replace
identities, dates, tokens and policy values with the explicitly selected current values. No example
is executed automatically. Web writes need current MFA cookies, browser context, `X-CSRF-TOKEN`
and a unique `Idempotency-Key`; reuse that key only for an identical retry. Mobile self requests
use the existing bearer session. Never send an employee identity in self leave intake.

## Direct HR attendance policy

1. `POST /api/v1/hr/attendance-policies`:

```json
{"code":"SITE-TIME-EXAMPLE","name":"Selected site time rules","reason":"Record approved configuration"}
```

2. Take `id`, `version` and `revision` from the response. Send
   `POST /api/v1/hr/attendance-policies/{id}/publication-preview` with:

```json
{
  "expectedVersion":"<current base64 rowversion>",
  "expectedRevision":0,
  "effectiveFrom":"2027-01-01",
  "effectiveTo":null,
  "rules":{
    "schemaVersion":3,
    "arrival":{"lateAfterShiftStartSeconds":900,"halfDayAfterShiftStartSeconds":3600,"hourlyAfterShiftStartSeconds":9000},
    "paidTime":{"basis":"scheduled_window_overlap","breakTreatment":"count_all_breaks","earlyExitTreatment":"actual_worked_time","halfDayCreditSeconds":16200,"offDayTreatment":"no_credit","holidayTreatment":"scheduled_day_credit","partialLeaveHalfDayTreatment":"half_day_total_includes_paid_leave"},
    "lateDeduction":{"occurrenceThreshold":3,"resetPeriod":"calendar_month","countHalfDayArrival":false,"countHourlyArrival":false,"salaryBasis":"base_salary","salaryDivisor":"actual_calendar_month_days","fixedSalaryDivisorDays":null,"deductionDaysPerThreshold":1,"rounding":"two_decimal_places_half_up","policyChangeCountingStrategy":"restart_on_late_rule_change","salaryEffectiveDateBasis":"threshold_completion_date","enabled":true,"deductionKind":"day_fraction","overlapTreatment":"larger_of_ordinary_or_late"},
    "ordinaryPay":{"salaryBasis":"base_salary","salaryDivisor":"actual_calendar_month_days","fixedSalaryDivisorDays":null,"fullUnpaidDayDeductionDays":1,"unpaidHalfDayDeductionDays":0.5,"hourlyShortfallCalculation":"daily_rate_over_scheduled_hours","paidLeaveTreatment":"no_deduction","weeklyOffTreatment":"no_deduction","applicablePaidHolidayTreatment":"no_deduction","rounding":"two_decimal_places_half_up"}
  }
}
```

3. If `canPublish` is true and the operator confirms the values, POST the same body to
   `/{id}/versions`, adding `expectedPreviewFingerprint` from the preview and `reason`.
   Do not change any rule/date between preview and publish. Refresh after a stale-write conflict.
4. Publish returns `policy` with a new version/revision token and `published.id`. Assign it with
   `POST /{id}/assignments`:

```json
{
  "expectedVersion":"<new policy rowversion>","expectedRevision":1,
  "policyVersionId":"<published version UUID>","target":"site","targetId":"<selected site UUID>",
  "effectiveFrom":"2027-01-01","effectiveTo":null,"reason":"Apply selected policy to this site"
}
```

Refresh tokens after every mutation. Effective dates must be future Pakistan business dates and
must preserve locks. A version without an assignment does not affect anyone. Conflicting applicable
populations have no guessed priority. `GET /effective/{employeeId}/{date}` explains availability.
These operations never update compensation or initiate salary payment/deduction.

New publications use schema 3. Historical schema 1/2 policies retain their original meaning and
bytes. Use `GET /api/v1/hr/attendance-policies/recommended-financial-settings` to populate only
the confirmed normal-salary form recommendations; explicitly choose the late threshold, grace,
deduction kind/amount, enablement and counting period. The example threshold of 3 is arbitrary,
not a fixed company rule. Weekly counting also requires `weekStartsOn`; fixed-amount and hours
deductions require their corresponding amount instead of `deductionDaysPerThreshold`. Publishing
calculation settings does not establish legal authorization or activate a payroll deduction.
New schema-3 ordinary and late deduction settings accept only two-decimal rounding modes.
Historical policy rounding and the separate whole-rupee OT rule are preserved.

## Restricted payroll-input preview and retention

The operator needs separately assigned `attendance.payroll-inputs.read` and
`attendance.payroll-inputs.calculate` permissions within every current and retained historical
scope. Read `GET /api/v1/attendance/employees/{employeeId}/payroll-inputs/2027/1/preview`.
Amounts are present only when calculation sources are complete. `payrollReady` and
`paymentInstruction` remain false; check `legalStatus`, source issues and jurisdiction context.
If `canSave` is true, POST to the same month path plus `/revisions`:

```json
{"expectedPreviewFingerprint":"<exact 64-character preview fingerprint>","expectedRevision":0,"reason":"Retain reviewed calculation inputs for payroll review"}
```

Use the revision from the preview, not a hardcoded zero after the first save. `Idempotency-Key`
and browser CSRF headers are required. A saved revision is the complete replacement monthly
input set, never another additive charge. Same-source resaves reuse the existing revision.
Read `/revisions` for bounded history and `/revisions/{id}` for the original protected calculation.
The latter does not silently recalculate under new salary, location or policy settings. Protected
storage references never appear in client responses. Locked months reject a new revision.

## Employee leave and direct Admin review

Use `GET /api/v1/leaves/mine/types` and the employee's actual shift before submission.
`POST /api/v1/leaves/mine` accepts exact intervals; this example is an eight-hour site shift:

```json
{
  "leaveTypeId":"<configured leave type UUID>",
  "days":[{"date":"2027-01-04","startsAtUtc":"2027-01-04T04:00:00Z","endsAtUtc":"2027-01-04T12:00:00Z"}],
  "reason":"Planned personal leave",
  "contactDuringLeave":null,"relieverEmployeeId":null,"handoverNotes":null,
  "attachments":[],"previousRequestId":null
}
```

Evidence requirements depend on the chosen type. Required evidence must be scanned successfully;
empty attachments do not bypass it. Returned applications are resubmitted as a new request with
`previousRequestId` pointing to the returned original.

Admin reads `GET /api/v1/leaves/admin/queue?from=2027-01-01&to=2027-01-31&pendingOnly=true`.
The assigned fixed Admin permission and self-review denial are enforced by the server. Approve via
`POST /api/v1/leaves/admin/{id}/decisions`:

```json
{
  "decision":1,"expectedApprovalVersion":"<current approval rowversion>",
  "reason":"Reviewed leave and coverage","treatment":1,
  "policyVersionId":"<explicit applicable published leave settings UUID>"
}
```

Decision `1` approves, `2` returns, `3` rejects. Concrete approval needs its applicable leave policy
and treatment. Return/reject use treatment `0` and `policyVersionId:null`. No separate HR approval
is required. The published leave policy must explicitly specify category, holiday revision and day
length; missing configuration does not authorize a salary hold.

For cancellation, the employee posts `expectedApprovalVersion` and `reason` to
`/api/v1/leaves/mine/{id}/cancellations`. Admin then posts `decision`, that cancellation's current
`expectedApprovalVersion`, and `reason` to `/api/v1/leaves/admin/{id}/cancellations/{cancellationId}/decisions`.
Cancellation retains the original request and appends a balance reversal; locked periods stay locked.

Daily attendance responses expose `inputProvenance` and `payrollReady:false`. Time/classification
is usable only when `policy.status` is `calculated`. An unavailable result is not zero salary or
permission to treat an incomplete source as settled payroll.
