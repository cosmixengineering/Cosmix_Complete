# Employee lifecycle and directory archival

Part A04 extends the existing HR request -> independent fixed Admin/SuperAdmin decision flow.
Existing suspension, resignation and termination authority is unchanged. Retirement and suspension
release use that same flow only after management approves and configures the new transition policy.
This document supplements the original lifecycle section of [Employee Hiring](Employee-Hiring.md).

## Policy and effective history

`EmployeeLifecycle` configuration has `ApprovedVersion`, `EffectiveFrom`, optional `EffectiveTo`,
`AllowRetirement` and `AllowSuspensionRelease`. The two flags default to false. An enabled new transition
also requires a nonblank approved version and an effective window containing both the request/decision
time and the intended effective instant. There are no seeded approval values. Missing approval returns
503 `employee_lifecycle_policy_approval_required`. Management must approve these values before enabling
the corresponding workflow; a source build is not that approval.

Allowed new transitions are Active -> Retired, Suspended -> Retired, and Suspended -> Active.
Use the existing `POST /api/v1/hr/employees/{id}/lifecycle/requests` with `newStatus: 6` for retirement
or `newStatus: 2` for suspension release, and the existing request decision route. Required reason,
employee/request rowversions, idempotency key, independent reviewer, current web SMS MFA, All scope and
fixed Admin decision permission remain mandatory. A policy version change after request creation requires
a fresh request. Request detail and status history return the exact `lifecyclePolicyVersion`.

Null `effectiveAt` applies at approval; a scheduled instant must still be future when approved and strictly
follow the previous effective instant. History remains append-only. Due changes are applied in chronological
order using `Employee.LastAppliedStatusChangeId`, including when a worker misses both suspension and release.
Invalid/missing approval evidence, ambiguous effective instants and incompatible legacy cached history remain
conflicts for review. The marker is constrained to the same employee in SQL. Shared migration ownership
belongs to Part A07. The A23 legacy marker backfill requires exact approval/submission evidence and
fully reconciled account, channel, device, role, session, open OTP and face state. Residual or ambiguous
legacy state stays unmarked for explicit review; cached inactive status alone is insufficient. See the
[A23 upgrade runbook](integration/Part-A23-Upgrade-Runbook.md) before rollout or suspension release.

Authentication enforces nonactive effective employment independently of worker timing. A due Active release
cannot make unreconciled old access usable: live authentication/access checks also require its applied marker.
The periodic worker reconciles durable account/device/access/face state; its polling interval does not define
the employment effective instant.

At each applied transition, old sessions, open OTP challenges, devices and role assignments are revoked;
mobile and web channels are disabled. Revocation reasons retain the status-change ID, and original dated
records remain. Active face enrollment is logically revoked in the same lifecycle transaction, with ciphertext
and original enrollment evidence retained. Release also retires any enrollment left by an older implementation.
Physical biometric disposal/retention is Part B05 and remains a separate approved-policy dependency.

Suspension release prepares only an account suspended by `employee-lifecycle` to be eligible again, with both
channels disabled. Independent account suspension/disable remains in force. Current roles/channel approval,
new device/session/OTP evidence and fresh face enrollment are needed before mobile Check In; the workflow
never restores old grants, channels, sessions, devices or templates. Protected system-role targets require
the separate owner handover and cannot use these workflows.

Terminal Resigned/Terminated/Retired -> Active requests return 503
`employee_rehire_requires_approved_employment_episode_policy`. Rehire remains open until management approves
new employment episode identity, approval authority, joining/compensation/assignment boundaries and evidence.
The existing onboarding decision is never reused to pretend that a new employment episode was approved.

## Archive and unarchive

`POST /api/v1/hr/employees/{id}/lifecycle/archive` accepts:

```json
{
  "archived": true,
  "expectedVersion": "AAAAAAAAAAA=",
  "reason": "Approved directory archival after ended employment"
}
```

The version above is an example; send the current employee version. Use `archived: false` to unarchive.
The route requires an `Idempotency-Key`, lifecycle read and dedicated `hr.employees.lifecycle.archive`
permission on a current fixed Admin/SuperAdmin assignment. Self actions and protected owner targets are
denied, including receipt replay. Only approved Resigned, Terminated or Retired employees can change archive
state, with no pending request/future status change. A stale version, blank reason or unchanged state fails.

The response includes `archiveChangeId`. `GET /api/v1/hr/employees/{id}/lifecycle/archive/history` returns the
append-only `EmployeeArchiveChanges` journal with previous/new archive state, employment status, exact actor,
role, reason and timestamp. Lifecycle state and employee summaries expose `isArchived`; lifecycle state
also includes `archivedAt`. Unarchive changes only directory metadata, never employment/access eligibility.

`GET /api/v1/hr/employees` excludes archived employees by default. `includeArchived=true` includes both;
`archivedOnly=true` returns archived employees and takes precedence. These filters apply before counting and
pagination. Authorized direct profile/history reads remain available. No employee, compensation, assignment,
document, punch, financial evidence or audit row is physically deleted by archival.

## Verification limits

Temporary standalone checks cover domain transitions, policy gates, delayed worker cycles, access/evidence
revocation, independent blocks, owner protection, archive reason/version/authorization/replay and preserved
history. They use synthetic entities and test-only repository/clock/file adapters. Part A07 owns migration,
runtime EF model/snapshot checks and final Release integration evidence. No database migration, provider
deployment, account creation, real SMS/device/biometric test or production-policy enablement is implied.
