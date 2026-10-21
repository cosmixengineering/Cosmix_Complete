# Direct HR attendance policy publication

The user confirmed direct publication by an explicitly permitted HR/Admin operator on 12 October 2026.
There is no second reviewer and no automatic permission grant. The existing policy, policy version,
assignment and assignment-end entities are reused. A successful write, protected before/after audit
and idempotency receipt share the existing serializable authorization transaction.
The operation checks explicit RBAC permission, All scope and current web MFA; it does not require
a fixed role name or infer publication authority from an Admin title alone.

These APIs configure attendance rules. They do not write compensation, stop salary, manufacture zero
salary, initiate deductions or authorize payroll settlement. Invalid or incomplete configuration is a
configuration error; it is never an instruction to withhold an employee's salary.

## HTTP flow

Base: `/api/v1/hr/attendance-policies`. Management requires current web MFA; POST also follows the
existing cookie/CSRF boundary. Write requests require `Idempotency-Key` and an explicit reason.

| Method/path | Permission | Result |
|---|---|---|
| GET base, GET `/{id}` | `attendance.policies.configure`, All | Definitions and rowversion/revision |
| GET `/recommended-financial-settings` | configure, All | Reviewable schema-3 financial form suggestions; no publication |
| POST base | `attendance.policies.publish`, All | Create code/name definition |
| GET `/{id}/versions` | configure, All | Immutable published history |
| POST `/{id}/publication-preview` | configure, All | Field errors or canonical rules and preview fingerprint |
| POST `/{id}/versions` | publish, All | Direct immutable version publication |
| GET `/{id}/assignments` | configure, All | Original applicability and explicit ending evidence |
| POST `/{id}/assignments` | publish, All | Assign version to one explicit population |
| POST `/{id}/assignments/{assignmentId}/replace` | publish, All | Atomic ending plus replacement |
| POST `/{id}/assignments/{assignmentId}/end` | publish, All | Append an exclusive ending |
| GET `/effective/{employeeId}/{date}` | `attendance.policies.applicability.read`, historical employee scope | Dated rules and source fingerprint |

Definition/history lists accept `page` (default 1) and `pageSize` (default 50, maximum 100), returning
`items`, `page`, `pageSize`, `hasMore`. Definitions support `search` against code/name, sorted by code.
Version history is ordered by version number; assignment history by start then identity. Repository
caps are 512 definitions, 1,024 versions per definition and 4,096 assignments per definition; overflow
is an explicit conflict, never a silently truncated successful history.

Create a definition, fetch its `version` and `revision`, then preview the full proposed rules and
effective period. Publish with the same values plus `expectedPreviewFingerprint`. Publication rechecks
rules, dates, locks, rowversion and revision. Assign the returned version separately to an `employee`,
`department`, `staff_category` or `site` target. No publication silently changes who receives a policy.
All request members are explicit, including null exclusive ends. JSON is limited to 32 KiB and rejects
unknown/duplicate members, numeric rule enums, missing required members and noncanonical enum names.

## Rule and date interpretation

New publications use `rules.schemaVersion: 3` and stored schema `attendance.v3`. The complete form
requires arrival thresholds, paid-time choices, half-day seconds, holiday/off-day treatment and all
late-deduction inputs, plus the explicit `ordinaryPay` financial settings. `paidTime.partialLeaveHalfDayTreatment`
must explicitly be `half_day_total_includes_paid_leave`. Schema 3 also requires
`lateDeduction.policyChangeCountingStrategy: restart_on_late_rule_change` and
`lateDeduction.salaryEffectiveDateBasis: threshold_completion_date`. No value is silently inserted.
Schemas 1 and 2 remain readable with their exact historical canonical bytes; they cannot be submitted
as a new complete publication or newly assigned as a financially complete policy. Existing assignments
remain effective under their historical time rules until explicitly replaced or ended.

The user subsequently confirmed ordinary payroll-input rules: basic monthly salary divided by the
selected day divisor, recommended as actual days in the payroll month; a full unpaid day uses an
explicit fraction recommended as 1 and an unpaid half-day uses an explicit fraction recommended as
0.5. Both fractions accept 0 through 1, with half-day no greater than full-day. Hourly shortfall uses
daily rate divided by scheduled hours. Paid leave, weekly off and applicable paid holidays explicitly
use no deduction. Rounding and the day divisor remain explicit configurable choices. Recommended
values are available from the read-only suggestions endpoint, with `published: false` and
`requiresExplicitPublication: true`; they are not seeded, granted or substituted into incomplete rules.
The confirmed recommendation uses two-decimal half-up rounding.

Late deductions require explicit `enabled` and retain all reviewed settings when disabled.
`deductionKind` selects exactly one of `day_fraction` (positive through one day), `fixed_amount`
(positive `fixedAmountPkr` fitting existing decimal(18,2) money storage), or `hours` (positive
`deductionHoursPerThreshold` through 24). Unused amount fields must be null; the legacy
`deductionDaysPerThreshold` JSON member remains required and must be null for fixed/hourly modes.
`resetPeriod` supports weekly, calendar month, calendar year and July–June fiscal year. Weekly
requires explicit `weekStartsOn`; nonweekly rules must omit it or use null. No weekday is assumed.
Threshold, participation choices, salary divisor and rounding remain explicit even for disabled
rules, so re-enabling cannot silently activate an unreviewed configuration.

Late thresholds use the salary effective on the threshold-completion date. The counting strategy
restarts only when the canonical `lateDeduction` settings change. A new policy identity, changes to
ordinary-pay settings or salary changes alone do not reset accumulated late occurrences. Each day's
arrival classification still uses its own dated arrival settings. Policy publication authorizes none
of the employee-level financial application, payment release or legal treatment; those boundaries
remain with the separately authorized payroll-input workflow. The only supported explicit overlap
treatment is `larger_of_ordinary_or_late`; overlapping ordinary and late amounts are never added
together. Unverified legal authorization cannot make a late penalty payroll-active merely because
its policy is published. OT remains separate.

Thresholds are offsets in seconds from the effective roster start. `actual_elapsed_duty` includes
verified elapsed duty after the scheduled start, while `scheduled_window_overlap` limits it to the
scheduled window. Pre-start duty needs a separate supported exemption producer. Approved overtime
has its own existing rules and protected award evidence. The form does not supply missing KPI,
paired-worker waiting, cross-midnight reset, meal, payroll settlement or exception authority.

Periods start inclusively and end exclusively. New publication, assignment, replacement and ending
boundaries must be after the current Pakistan business date; today and historical periods cannot be
rewritten. No changed interval may overlap an existing locked attendance month. A replacement
appends an ending at its start and a new assignment together, retaining the original row unchanged.
Ending an already ended assignment conflicts. Unused future versions remain retained history.

The resolver uses the employee's membership on the requested date. Missing/ambiguous membership,
unsupported authority, unsupported schema or invalid rules yields an unavailable state with provenance.
Overlapping assignments to the same explicit target are rejected. Different populations do not have
guessed precedence: distinct canonical rules return `conflict`; equivalent rules retain every assignment
in the digest and select a deterministic version identity. At most 128 applicable candidates are read;
overflow conflicts. Historical target identity, effective bounds, authority, ending and rule content
contribute to the fingerprint.

## Persistence compatibility

`Policy.Revision` is a concurrency token alongside SQL rowversion. Version, assignment and ending
now retain explicit `PolicyAuthority`: existing rows default to `ApprovedWorkflow` (1), and direct
publication uses `DirectHr` (2). Approval FKs become nullable. Check constraints require an approval
for authority 1 and no fabricated approval for authority 2. Original policy versions, assignments and
endings remain append-only. No earlier migration is rewritten.

The earlier foundation has no implemented policy approval producer defining a request type and
subject contract. An arbitrary approved request cannot prove policy approval. Such legacy records
remain readable history but unavailable to the effective producer until their authority is supported
explicitly. Historical role-target assignments also remain readable; new role-target assignment and
role-based calculation require a separately specified producer.

## Verification boundary

`VerifyPolicyPublication.cs` exercises the real service, resolver and authorization/idempotency
boundary with synthetic repositories and in-memory protected-file storage, including preservation
of a prior effective policy and unchanged compensation after invalid proposals, exact replacement
boundaries, unsupported authority, history, locks, retries, stale tokens and denied access.
`VerifyPolicyQueries.cs` translates the actual EF historical, applicable and overlap query compositions
with a connection interceptor that rejects any database opening. These are standalone checks, not
test projects. They do not establish SQL transaction/constraint, authenticated deployed HTTP or
real-employee payroll acceptance. No database connection, account, provider or salary change is
authorized by this local implementation run.

Executed locally on 12 October 2026: Release solution build succeeded with zero warnings/errors;
41 service/resolver checks passed (including a rerun after history DTO changes); 14 guarded EF
translation/model checks passed with zero connection attempts. The final integrated Release/API
checks and incremental migration preparation are tracked in the main continuation record.
Those counts describe the preceding schema-2 implementation. The schema-3 continuation adds
`VerifyRulesV3.cs` for fixed historical wire-byte compatibility, required financial choices, strict
parsing and financial-setting constraints, and expands the real publication-service harness to verify
recommendations, denied scope and preservation of existing policy/compensation after incomplete
financial proposals. Its execution results are recorded after the coordinated build slot.

Schema-3 continuation on 12 October 2026: `VerifyRulesV3.cs` passed 108 checks;
`VerifyRules.cs` passed 90; `VerifyRulesV2.cs` passed 7; `VerifyPolicyPublication.cs` passed 55.
All Release standalone commands exited 0 using local project restore sources/cached packages.
The requested leave regression also passed 65 checks. These runs used synthetic collaborators and
no database or network writes; API/document checks remain part of the final integrated verification.
