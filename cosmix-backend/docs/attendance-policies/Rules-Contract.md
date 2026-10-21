# Attendance policy form and strict rules contract

Status: local rules contract, updated 12 October 2026. The user confirmed direct permission-controlled
HR publication and explicit form configuration. The [publication workflow](../attendance-completion/Direct-HR-Policies.md)
uses this contract; validation alone still does not publish or select company settings. The registered
daily calculator consumes verified policy, schedule, duty and leave inputs for time/classification.
It does not stop salary or post deductions; financial readiness remains separate. The confirmed OT
formula and eligibility rules remain unchanged.

`Domain/HR/Policies/AttendancePolicyRules.cs` contains immutable input records, named options,
structured validation and a strict JSON codec. No new package, entity, DbContext member, database
table, test project or schema migration is introduced by this contract.

## Public API

The registered HR form endpoint is `POST /api/v1/hr/attendance-policies/validate` with
`{"rules": ...}`. It requires explicit `attendance.policies.configure` with All scope and current
web MFA/CSRF. Successful access is audited through the existing operation. It returns
`isValid`, `published` (always false), `canonicalJson`, `configurationSha256` and field `errors`.
Invalid input returns no usable canonical rules/hash. No policy, salary or assignment is saved.

Its endpoint-local resource filter rejects duplicate fields (including escaped duplicates), case
aliases, unknown fields, numeric enums and quoted numbers before typed MVC binding. Bodies are
bounded to 32 KiB even without Content-Length; invalid JSON is 400 and oversized input is 413,
without echoing the request. Existing authorization, CSRF and storage controls remain unchanged.

```csharp
AttendancePolicyRulesValidation AttendancePolicyRulesCodec.Validate(AttendancePolicyRules? rules);
AttendancePolicyRulesValidation AttendancePolicyRulesCodec.ValidateAndCanonicalize(string? json);
string AttendancePolicyRulesCodec.Canonicalize(AttendancePolicyRules rules);
```

The first two methods return `Rules`, `CanonicalJson`, `Errors` and computed `IsValid`. An error
has `Path`, `Code` and safe `Message`. Invalid input always returns null rules and canonical JSON;
there is no usable partial policy. `Canonicalize` throws `ArgumentException` for invalid typed input.
None of these methods persists a record, grants permission or declares a policy approved.

Every section and value is explicit. Nullable input fields distinguish a missing decision from
zero, `false` or the first enum member. `fixedSalaryDivisorDays` is the one conditionally nullable
value: it must be null for actual calendar days and an explicit positive integer for fixed days.
JSON must still contain that member. New publication requires rules schema version `3`. Schemas `1`
and `2` remain readable without rewriting their canonical JSON. Version `2` additionally requires
`paidTime.partialLeaveHalfDayTreatment`; version `1` must omit that choice. Version `3` adds required
`ordinaryPay` settings and explicit late-rule counting strategy/salary-date basis. Its new members are
omitted when null in retained schema-1/2 serialization, preserving historical canonical bytes.

## Form fields

| Section | Fields and meaning |
|---|---|
| `arrival` | `lateAfterShiftStartSeconds`, `halfDayAfterShiftStartSeconds`, `hourlyAfterShiftStartSeconds`: explicit whole seconds after the resolved shift start, strictly increasing. Each accepts 0 through 86,400. The contract defines named thresholds only; it does not execute classification or choose company values. |
| `paidTime` | `basis`, `breakTreatment`, `earlyExitTreatment`, `halfDayCreditSeconds`, `offDayTreatment`, `holidayTreatment`: explicit ordinary attendance treatment. Half-day credit is 1 through 86,400 seconds; it is never assumed to equal half the shift. Application-level publishing must additionally validate compatibility with applicable schedules and supported calculation semantics. |
| `lateDeduction` | `occurrenceThreshold` (1–366), `resetPeriod`, `countHalfDayArrival`, `countHourlyArrival`, `salaryBasis`, `salaryDivisor`, `fixedSalaryDivisorDays`, `rounding`; schema 3 also requires `enabled`, `deductionKind`, `overlapTreatment`, `policyChangeCountingStrategy` and `salaryEffectiveDateBasis`. The selected deduction kind requires its own explicit amount. No count/reset, salary basis, divisor, participation or rounding choice is implied. |
| `ordinaryPay` (schema 3) | `salaryBasis` (confirmed basic monthly salary), `salaryDivisor`, `fixedSalaryDivisorDays`, `fullUnpaidDayDeductionDays`, `unpaidHalfDayDeductionDays` (each 0–1, half no greater than full), `hourlyShortfallCalculation`, `paidLeaveTreatment`, `weeklyOffTreatment`, `applicablePaidHolidayTreatment`, `rounding`. Every member is explicit. |

The bounds are the supported schema envelope, not management-selected amounts. There is no hidden
disabled mode: an unsupported policy design must receive a new explicit contract rather than use
zero, missing data or an unknown enum to bypass validation. A structurally valid rules document
does not establish that every choice is supported by a future calculation engine; that producer
must check capability and provenance before reporting a calculated result.

Options are exact, case-sensitive, snake_case strings:

| Field | Explicit possible choices |
|---|---|
| `paidTime.basis` | `actual_elapsed_duty`, `scheduled_window_overlap` |
| `paidTime.breakTreatment` | `count_all_breaks`, `deduct_scheduled_unpaid_break` |
| `paidTime.earlyExitTreatment` | `actual_worked_time`, `half_day_credit` |
| `paidTime.partialLeaveHalfDayTreatment` | `half_day_total_includes_paid_leave` (explicit in version 2) |
| `paidTime.offDayTreatment`, `holidayTreatment` | `actual_worked_time`, `no_credit`, `scheduled_day_credit` |
| `lateDeduction.resetPeriod` | `calendar_month`, `calendar_year`, `fiscal_july_june`, `weekly` (schema 3 only) |
| `lateDeduction.salaryBasis` | `base_salary`, `gross_salary` |
| `lateDeduction.salaryDivisor` | `actual_calendar_month_days`, `fixed_days` |
| `lateDeduction.rounding` | `whole_currency_unit_half_up`, `two_decimal_places_half_up`, `whole_currency_unit_half_even`, `two_decimal_places_half_even` |

These are candidate form choices, not automatically published defaults. The schema is ordinary Attendance policy,
not OT: selecting an ordinary break or salary choice must never rewrite the approved OT formula.
Calendar-year and July–June reset options require the eventual calculator to load complete,
authorized history across the full configured period; a partial query cannot count as complete.
Holiday treatment must consume the roster's retained calendar revision; it does not itself change
the roster or cancel a scheduled duty.

Schema 3's new exact options are:

| Field | Explicit choices |
|---|---|
| `ordinaryPay.salaryBasis` | `base_salary` only; the reused enum's `gross_salary` is rejected for ordinary inputs |
| `ordinaryPay.salaryDivisor`, `rounding` | The existing divisor and rounding options above |
| `ordinaryPay.hourlyShortfallCalculation` | `daily_rate_over_scheduled_hours` |
| `ordinaryPay.paidLeaveTreatment`, `weeklyOffTreatment`, `applicablePaidHolidayTreatment` | `no_deduction` |
| `lateDeduction.policyChangeCountingStrategy` | `restart_on_late_rule_change` |
| `lateDeduction.salaryEffectiveDateBasis` | `threshold_completion_date` |
| `lateDeduction.deductionKind` | `day_fraction`, `fixed_amount`, `hours` |
| `lateDeduction.weekStartsOn` | `sunday`, `monday`, `tuesday`, `wednesday`, `thursday`, `friday`, `saturday`; required only for weekly counting |
| `lateDeduction.overlapTreatment` | `larger_of_ordinary_or_late` |

For new schema 3, both `ordinaryPay.rounding` and `lateDeduction.rounding` must be
`two_decimal_places_half_up` or `two_decimal_places_half_even`. Other choices return
`deduction_precision_required` at the corresponding rounding path, including retained settings
on a disabled late rule. The shared enum still lists all four modes for historical schema 1/2
contracts; those stored policies preserve their original behavior. The separate approved OT
calculation continues to use whole rupees.

The approved starting recommendation uses actual payroll-month days, full unpaid fraction 1 and half
unpaid fraction 0.5. `GET /api/v1/hr/attendance-policies/recommended-financial-settings` provides these
and the confirmed two-decimal half-up rounding as reviewable suggestions under the configure permission,
All scope and current web MFA. It returns `published: false` and `requiresExplicitPublication: true`,
creates no policy and does not complete missing arrival or late settings. HR must submit every
required choice through the ordinary preview/publication flow.

Late counting restarts when canonical `lateDeduction` settings change, not on an unrelated ordinary
setting, a policy ID change or a salary change. Each occurrence is classified under its own dated
arrival rules. Threshold pricing uses salary effective on completion of that threshold. These rules
produce authorized payroll inputs only; they do not approve payment release or legal authority, and
they never modify the employee's base salary record. Overtime has separate inputs and authority.

Late enablement is explicit and does not remove the other configuration requirements. Disabled
rules retain complete reviewed settings for controlled re-enablement. `day_fraction` requires
`deductionDaysPerThreshold` greater than zero through one (full and half-day values are deliberate
choices). `fixed_amount` requires positive `fixedAmountPkr` fitting decimal(18,2). `hours` requires
positive `deductionHoursPerThreshold` through 24, priced by the configured daily/scheduled-hour rate.
Exactly one amount applies: unrelated amount fields must be null or omitted, except the original
required JSON member `deductionDaysPerThreshold` must explicitly be null for fixed/hourly kinds.
Schemas 1 and 2 retain their previous positive-through-366 day-multiplier envelope; they gain no
new financial authority from the schema update. A weekly reset requires a reviewed `weekStartsOn`
and nonweekly rules must not carry one. Overlapping ordinary and late deductions use only the
larger applicable amount; published settings cannot bypass the separate legal-authorization gate.

## Synthetic form example

This example is for request-shape review only. Its numbers and choices are **not approved company
policy**, and the code does not seed or publish them. The permissioned endpoint may wrap this
object in its own request field; consult its controller/OpenAPI for that outer envelope.

```json
{
  "schemaVersion": 3,
  "arrival": {
    "lateAfterShiftStartSeconds": 900,
    "halfDayAfterShiftStartSeconds": 3600,
    "hourlyAfterShiftStartSeconds": 9000
  },
  "paidTime": {
    "basis": "scheduled_window_overlap",
    "breakTreatment": "count_all_breaks",
    "earlyExitTreatment": "actual_worked_time",
    "halfDayCreditSeconds": 16200,
    "offDayTreatment": "no_credit",
    "holidayTreatment": "scheduled_day_credit",
    "partialLeaveHalfDayTreatment": "half_day_total_includes_paid_leave"
  },
  "lateDeduction": {
    "occurrenceThreshold": 3,
    "resetPeriod": "calendar_month",
    "countHalfDayArrival": false,
    "countHourlyArrival": false,
    "salaryBasis": "base_salary",
    "salaryDivisor": "actual_calendar_month_days",
    "fixedSalaryDivisorDays": null,
    "deductionDaysPerThreshold": 1,
    "rounding": "two_decimal_places_half_up",
    "policyChangeCountingStrategy": "restart_on_late_rule_change",
    "salaryEffectiveDateBasis": "threshold_completion_date",
    "enabled": true,
    "deductionKind": "day_fraction",
    "overlapTreatment": "larger_of_ordinary_or_late"
  },
  "ordinaryPay": {
    "salaryBasis": "base_salary",
    "salaryDivisor": "actual_calendar_month_days",
    "fixedSalaryDivisorDays": null,
    "fullUnpaidDayDeductionDays": 1,
    "unpaidHalfDayDeductionDays": 0.5,
    "hourlyShortfallCalculation": "daily_rate_over_scheduled_hours",
    "paidLeaveTreatment": "no_deduction",
    "weeklyOffTreatment": "no_deduction",
    "applicablePaidHolidayTreatment": "no_deduction",
    "rounding": "two_decimal_places_half_up"
  }
}
```

## Strict input and stable storage

- The stored JSON path rejects malformed JSON, comments, extra documents, unknown members, missing
  members, duplicate decoded property names at any depth, alternative member casing, unknown schemas,
  numeric enums, alternative enum casing, numeric strings and fractional whole-second fields.
- A maximum of 32,000 characters and JSON depth 16 bound input to the existing `PolicyVersion`
  storage envelope. Parser exception text and supplied values are not returned as errors.
- Typed input records also require all JSON members, disallow unknown members and use strict
  numbers/enums. API endpoint-local raw validation is still required for duplicate detection:
  ordinary ASP.NET model binding otherwise discards earlier duplicate properties before a typed
  service receives the body. The service cannot recover discarded input from a DTO.
  Additive versioned fields are optional at deserialization for historical compatibility, then required
  by the schema-3 validator; their absence is never interpreted as a default value.
- Canonical JSON uses fixed record property order, exact enum names and compact camelCase output.
  Equivalent property ordering/whitespace and decimal trailing zeroes yield the same canonical form.
  The codec does not hash or sign input; the publishing workflow must bind its canonical digest,
  author/approval evidence, effective dates, scope and version before it can be a policy source.
- Validating these rules does not prove salary/leave access, calculation readiness, approvals or
  retrospective authority. No automatic grant or startup publication is introduced.

## Verification

Run the standalone Domain harness from the repository root; it creates no test project and uses
no SQL, network, account or private employee data:

```powershell
$env:TEMP = Join-Path $PWD 'artifacts/attendance-final-checks'
$env:TMP = $env:TEMP
dotnet run --file docs/attendance-policies/VerifyRules.cs --configuration Release --property:RestoreSources="$PWD/src/Cosmix.Domain" --property:NuGetAudit=false
```

The harness uses deliberately synthetic choices, not a default policy file or fixture for database
publication. It checks strict parsing, enum/required-field handling, range and threshold conflicts,
conditional divisor rules, typed validation and canonical equivalence. The separate integrated
continuation passed 12 service/authorization checks and 12 actual resource-filter checks after
reproducing duplicate-member acceptance before the filter. These use synthetic collaborators or
in-memory requests; publication, real authenticated SQL, history resolution and calculations remain
outside this validation contract. See [schema checks](Schema-Verification.md) for OpenAPI evidence.

Executed on 12 October 2026: **90 checks passed**, Release file-based run, exit 0. The initial
red-phase run failed because the unimplemented validator rejected the explicit supported input;
the completed implementation passed after the harness was aligned with the repository's
`PublishAot=false` file-run convention. No SQL writes, fixture creation, migrations, real-account
changes or Git actions were performed by this contract task. Full-solution Release and endpoint
checks belong to the integrated continuation record.

Schema-3 continuation: `VerifyRulesV3.cs` passed **108 checks**, including fixed schema-1/2 wire
bytes, absent/null compatibility, explicit enablement, conditional day/fixed/hour amounts, weekly
start choices, overlap enum, number bounds and canonical decimal normalization. The existing
`VerifyRules.cs` and `VerifyRulesV2.cs` also passed **90** and **7** checks after moving their
unsupported-schema fixture to version 99. `VerifyPolicyPublication.cs` passed **55** real-service
checks, including recommendation authorization and preservation of prior policy/compensation
after an incomplete financial proposal. Every command used Release and exited 0 without a database.
