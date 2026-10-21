# Attendance workflow continuation

User confirmations, 12 October 2026:

- Only HR reviews site reports. Explicit HR review permission is assigned through the existing
  custom-role system; a fixed role name or implicit supervisor access is not introduced.
- Missing-checkout reminder delay is entered on the shift form (examples: 30, 60, 120 minutes
  after the scheduled end). The immutable shift version retains that setting. No automatic
  checkout, deduction, OT rejection or unconfirmed total-duty threshold follows from a reminder.
- Attendance-related policy implementation may now be completed in this existing local checkout.
  This supersedes the earlier prohibition on implementing the absent other-developer policy
  feature, but does not authorize Git integration or invent missing financial rules.

Implementation plan:

1. Preserve received site reports; append HR decisions and protected employee resubmissions.
   Enforce current historical scope, no self-review, expected review/response sequences,
   atomic audit/retry protection and safe in-app notifications.
2. Persist optional reminder delay on shift versions. Existing versions remain unconfigured.
   Add a bounded opt-in producer and durable delivery that rechecks an open latest duty.
3. Implement typed effective-dated attendance policy configuration and applicable calculations
   using only confirmed rules. Publication authority, required numeric form settings and leave
   decision authority have been asked separately; dependent rules await the user's answers.
4. Coordinate one new local migration after mappings stabilize. Existing migrations 1–17 are
   already applied to the isolated baseline and remain untouched. Do not apply the new migration.
5. Run standalone behavior/model/query checks, affected regressions, Release build and registered
   anonymous HTTP verification. Record actual evidence, and distinguish authenticated SQL and
   provider-dependent acceptance that has not been executed.

The face provider remains deferred. No database writes, real account changes, external provider
calls, scheduled follow-ups or source-control mutations are part of this continuation.

Evidence ledger:

- Response mapping check reproduced the missing persistent entity before adding its configuration.
- HR report review/resubmission: 119 service/rule and 14 real query-translation checks passed;
  94 registered OpenAPI/anonymous HTTP checks covered all eleven site-report operations.
- Missing-checkout reminders: 43 service/rule and 23 query-translation checks passed; the
  translation check first reproduced and then verified the candidate-ordering fix. Source review
  also corrected the producer/dispatcher lock-order risk in the narrow deduplication probe.
- Affected regressions: 41 schedule/resolver, 171 site scheduling and 55 geofence service checks
  passed. These counts describe standalone checks, not genuine SQL or mobile attendance.
- Policy settings: 90 Domain checks, 12 service/authorization checks and 12 real MVC resource-filter
  checks passed. The filter's test failed before implementation and then passed, proving duplicate
  JSON rejection before ordinary binding. Publication/calculation remains unimplemented pending
  the specific decisions above; no settings file is an approved company policy.
- Migration 18 and its incremental 17-to-18 SQL are prepared locally, not applied. Sixteen offline
  model/script checks passed, including snapshot equality and evidence-preserving downgrade guards.
  The 34 source/designer files for applied migrations 1–17 retain their pre-continuation hashes.
- Integrated Release build: zero warnings/errors. Main anonymous HTTP/OpenAPI/traffic sweep:
  726 checks across 119 Attendance and 58 existing employee operations. The first sandbox host
  was unreachable and logged Data Protection access denial; the same bounded localhost-only
  script passed under reviewed execution. Workers/providers/authentication stayed disabled and
  SQL pointed to an unreachable target; the owned host was stopped afterward.
- Policy OpenAPI: 114 exporter/transformer and 80 actual generated-document checks passed after
  correcting the reproduced nullable-enum `oneOf` ambiguity. See
  [schema verification](../attendance-policies/Schema-Verification.md) for the exporter and final
  generated-document evidence, including nullable enum composition correction.

No SQL connection, database write, real account permission change or provider call was used for
this continuation. Migration application, authenticated report/reminder acceptance and real
SQL/private-storage concurrency/restart checks remain outstanding. Face provider selection remains
explicitly deferred; the backend must still fail closed for real face-verified Check In.
