# HR Benefits and Recovery Client Contract

## Scope

These contracts extend the existing HR leave ledger, approval records and
effective-dated policies. They do not release salary, pay encashment, attest
Accounts settlement, enable legal penalties or change biometric capture.
The companion implementation/verification record is
[HR-Benefits-Recovery-Vehicles-20261019.md](HR-Benefits-Recovery-Vehicles-20261019.md).
Vehicle routes and policy choices are documented separately in
[Vehicle-Adjustments-20261019.md](Vehicle-Adjustments-20261019.md).

Use the existing web session/MFA and CSRF handling for Admin writes. Self routes
bind employee identity from the authenticated session, never a submitted employee
identifier. Every write requires a valid `Idempotency-Key`. Retrying an identical
request uses the same key; changing its payload requires a new key. Refresh source
evidence and row versions after a conflict, rather than silently resubmitting.

## Extra / KPI Benefits

1. An authorized Admin publishes explicit `leave-settings-v4` configuration
   through the existing leave-policy workflow. `Benefits` defines eligible leave
   kind, service months, KPI threshold, award minutes, target-year offset, award
   window and encashment eligibility. No sample value is a company default.
2. KPI must already be finalized for the exact completed annual source period.
   Pending reviews, unresolved disputes or changed source evidence block a grant.
3. Admin reviews and grants through
   `POST /api/v1/leaves/admin/employees/{employeeId}/benefits` with
   `leaveTypeId`, `policyVersionId`, `kpiYearFrom`,
   `expectedKpiFinalizationId` and `reason`. The server chooses the amount from
   the approved policy. Clients cannot submit a score or awarded amount.
4. The reviewer must independently hold a genuine active fixed Admin assignment
   and `attendance.leaves.benefits.review` with explicit All scope. That permission
   may be delegated through a separate active custom-role assignment using the
   existing authorized role-assignment flow. Neither policy configuration nor a
   custom role alone grants Admin authority. Current membership, permission,
   expiry/revocation and MFA remain checked; self-awards are forbidden. This
   direct review does not add an unapproved second approval stage.
5. The response includes exact minutes and their retained `standardDayMinutes`,
   source/target periods, policy/KPI identities, `encashmentEligible`, status and
   `evidenceFingerprint`. Keep these units; do not divide by a hardcoded workday.
6. History is available at the same Admin path with `GET ?yearFrom=...`, and at
   `GET /api/v1/leaves/mine/benefits?yearFrom=...` for the employee.
7. Existing leave balance and eligibility calculations consume the verified
   credit. `additionalDays` is separate from factual `usedDays`; exact day values
   use numerator/denominator strings. Existing encashment considers only spendable
   credits explicitly eligible for cash and exact consumed-only offsets; it retains
   shared reservation/consumption guards.
8. `POST .../benefits/{id}/revoke` takes `expectedEvidenceFingerprint` and `reason`.
   It appends a reversal only in an unlocked, unexpired period with no outstanding
   utilization, reservation or encashment. Exact fully released history is valid;
   an unverified net-zero balance is not.
9. Consumed corrections use the reviewed
   [benefit correction contract](Benefit-Correction-Contract.md): impact fingerprint,
   exact Admin-attributed usage, unused removal, immutable consumed-review record
   and protected offsets without automatic salary/future-leave recovery. Completed
   unlocked years are supported; locked history is not reopened.

The target year uses the source KPI start **calendar year** plus configured
`TargetYearOffset`, together with the leave policy's year-start month/day. The
award window is measured from the KPI source period's exclusive end. Awards are
available from their actual recorded date. A changed policy or recalculated KPI
does not grant the same source year twice or silently reprice an existing award.

## Recovery Fulfilment

A recovery commitment is a promise, not completed work. Keep its existing create
and review flow. The additional `RecoveryFulfilment` v4 section explicitly selects
qualifying work, exact-minute basis, review authority, partial-fulfilment choice,
per-claim bounds, claim count, submission window and handling of changed parents
or attendance sources. Missing sections or Pending choices block work claims.

1. Obtain actual work with
   `GET /api/v1/leaves/mine/{leaveId}/recovery-commitments/{commitmentId}/work-source?policyVersionId=...`.
   The response retains the parent, exact policy, employment, roster and closed
   work sessions plus a fingerprint. It is not a fulfilment decision.
2. Submit through the same prefix plus `/work-claims` using `POST` with
   `policyVersionId`, `expectedSourceFingerprint`, `intervals` and `reason`.
   Each interval identifies its retained session, business date and UTC bounds.
   Use actual whole-minute intervals; do not manufacture a checkout or duration.
3. Read the pending claim at `GET /api/v1/leaves/mine/recovery-work/{id}`.
   Reviewers use `GET /api/v1/leaves/admin/recovery-work/queue` and
   `GET /api/v1/leaves/admin/recovery-work/{employeeId}/{id}`.
4. The independently authorized existing leave reviewer submits
   `POST /api/v1/leaves/admin/recovery-work/{employeeId}/{id}/decisions` with
   `expectedVersion`, `decision` and `reason`. Use the registered approval-state
   values. Review cannot be performed by the requester or beneficiary.
5. Approval rechecks source history, locks, configured limits and remaining
   obligation. Normal scheduled duty does not qualify under the implemented
   outside-duty option. The same physical interval cannot also earn OT or
   make-up credit, including an overlapping interval carrying another date.
6. Read the commitment prefix plus `/progress`. The response distinguishes
   `commitment_only`, `partially_fulfilled` and `fulfilled`, with committed,
   fulfilled and remaining minutes. Only approved work counts.

Changed/cancelled parents or corrected attendance require review. Previously
allocated work is retained and cannot silently become available for another
reward. No automatic wage refund, recovery amount or payment is inferred.

## Acceptance Checklist

- Exercise allowed and denied roles, missing MFA, CSRF, revoked sessions and
  expiry during evidence/receipt IO; denied operations must not commit effects.
- Test same-key replay, changed payload, stale versions, duplicate KPI source,
  disputed KPI, inactive policy, locked dates and storage failure.
- Consume earned leave then attempt excess leave/cash; verify the shared quota.
- Submit two disjoint recovery parts and verify remaining minutes; attempt an
  overlapping OT/make-up claim and excess recovery minutes.
- Verify review/decision notifications, retained history and absence of payment
  or automatic legal activation.
- Authenticated HTTP/SQL concurrency and persistence require a separately
  permitted test execution. Offline service tests do not certify those checks.

No migration is applied by these instructions. Separate React/React Native
clients have not been implemented by this backend continuation.
