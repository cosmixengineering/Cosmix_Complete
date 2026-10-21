# HR closure client contract additions

## Consumed benefit review resolution

Existing award, unused revocation and consumed-correction routes remain intact.
The implemented resolution is an explicit Admin waiver, not automatic recovery.
Existing recovery-work commitments cannot also settle this obligation: they
already belong to another leave obligation and must not be double-counted.

1. Publish an effective `leave-settings-v4` policy through existing authorized
   settings APIs. Its `Benefits.ConsumedCorrectionResolution` must explicitly
   contain `SchemaVersion: 1`, `AllowAdminWaiver: true`, and a nonempty
   `AuthorityReference`. No setting is enabled automatically by this change.
2. Read `GET /api/v1/leaves/admin/employees/{employeeId}/benefits/{id}/correction-impact`.
   Its `resolution` contains correction identity/fingerprint, current version,
   consumed/protected minutes and any retained waiver.
3. Submit `POST /api/v1/leaves/admin/employees/{employeeId}/benefits/{id}/correction-resolution`
   with CSRF and `Idempotency-Key`. Request fields are `policyVersionId`,
   `decision` (numeric enum value 1 = Waive), `expectedCorrectionFingerprint`,
   `expectedUsageFingerprint`, `expectedResolutionVersion` and `reason`.
4. Use the returned benefit view and reload the retained history. A resolved
   correction reports `waived` and the retained reviewer/policy/reason/minutes.

Use server fingerprints, never construct them in the client. Retain the exact
request/key for retries. Changed source/version or missing resolution evidence
is a conflict, not success. Current MFA, explicit Admin review permission and
no-self-review restrictions apply to both first requests and receipt replay.

A waiver does not grant spendable leave, alter earlier consumption, create a
recovery-work claim, change salary or record payment. Unsupported recovery
methods must not be displayed as completed actions.

## Vehicle daily commute configuration

The existing monthly policy's `AssignmentSelection` additionally supports the
explicit opt-in value `CombineDailyTravelOnce`. Existing policy modes retain
their behavior. This is not a newly installed company default.

For eligible same-day trips sharing the exact policy and commute baseline,
the monthly calculation combines official distance and applies the baseline
once. `DailyCommuteBreakdown` retains member mileage IDs, baseline, combined
business distance, prior eligible distance, combined eligible distance,
eligibility adjustment and nearer-distance deficit. Original mileage records
are unchanged. Normal preview, exact-fingerprint submission and independent
review are still required.

Different same-day policy/commute contexts remain an explicit conflict; no
unapproved split is guessed. Corrected legacy fuel and previously consumed
maintenance reconciliation are not silently repriced by this mode.

Vehicle submissions now reject duplicate time/odometer overlap, overlapping
commute periods, overlapping historical custody and custody returns that would
invalidate retained pending/approved trip or service evidence. Display the
conflict and refresh authoritative records; do not retry with a new key to
work around it.

## First fuel claim after approved trip correction

An approved corrected trip with no existing fuel claim may now use the existing
self fuel submission route. The server uses approved daily segments, each day's
retained policy/rate and currency, not a client-supplied amount. Protected fuel
evidence schema 3 binds the correction ID/fingerprint and complete dated
calculation. Submission, independent review, commit and receipt replay revalidate
that binding, including source changes during protected-file operations.

One claim per trip still applies. This does not reprice an existing approved fuel
claim. An old pending claim based on superseded mileage cannot be approved; the
limited historical closure below is now available. There is no replacement or
settlement workflow introduced here. Replay also requires the
retained claim and calculation evidence to remain readable, including historical
schema-1/2 claims. Do not delete those files because a receipt exists. Missing
evidence is an explicit failure, never a guessed value or payment authorization.

## Close superseded pending historical fuel evidence

- GET `/api/v1/hr/vehicles/fuel-claims/{id}/historical-closure-context` returns
  `resourceId`, `version`, `sourceFingerprint` and `correctionRequestIds`.
- POST `/api/v1/hr/vehicles/fuel-claims/{id}/historical-closure` requires
  `ExpectedVersion`, `ExpectedSourceFingerprint`, `EvidenceReference`, `Reason`
  and the normal `Idempotency-Key`/web CSRF headers.
- This independently reviewed action rejects a superseded pending schema-1
  claim. It retains the original calculation, amount, mileage, trip and receipt.
  The response retains the decision ID, rejected state and original source binding.
- Current fuel-review permission, MFA, no-self-review and exact source/version
  checks apply before replay, protected IO and commit. Approved claims cannot use
  this path. Changed evidence, authority expiry or a stale version is not success.
- Closing this review does not create a replacement claim, reconciliation credit,
  payment, settlement or a newly available one-claim-per-trip slot.

## Existing client boundaries

These are backend contract additions. They do not claim the separate web/mobile
forms have been extended or tested. Employee self-service remains separate
from HR management. Original source records, legal gates, no-payment scope and
locked-period protections remain unchanged.

## Consumed maintenance with unchanged historical quantities

The later 20 October continuation extends equivalence-bearing chains. Follow
[the chain contract and evidence](Vehicle-Maintenance-Chain-20261020.md) for the
latest supported histories and limits; the first-consumption-only restriction
below describes the earlier implementation.

Existing service submission and independent approval routes may now retain an
equivalence proof for the first already-consumed service after an approved trip
correction. The client supplies no equivalence boolean or corrected payable
amount. The server reconstructs original membership and compares each trip's
eligible kilometres through the consumed service date.

Only an intact original calculation/approval, unchanged required threshold and
cycle, no prior consumption chain and no applicable monthly adjustments qualify.
The next service remains Pending until its normal independent approval. Its
protected calculation retains the proof; original service amounts, calculations,
trip and mileage records remain unchanged.

Show source changes as a conflict requiring fresh review, not a successful
calculation with zero:

- `vehicle_maintenance_equivalence_sources_changed`: original membership or
  fingerprint cannot be reconstructed exactly.
- `vehicle_maintenance_equivalence_policy_required`: per-trip quantities or
  threshold/cycle changed; a separately approved reconciliation rule is needed.
- `vehicle_maintenance_attribution_reconciliation_required`: prior chains,
  already-equivalence-bearing consumption or other unsupported historical cases.
- `vehicle_service_eligibility_changed`: retained proposal/predecessor no longer
  matches authoritative evidence.

Third/subsequent services after this equivalence-bearing service are not covered
by the bounded continuation. No automatic recovery, reimbursement, payment or
historical rewrite is introduced. SQL/storage/client acceptance remains separate.

## HR fuel amount reconciliation

Approved legacy fuel claims can now have an independent, immutable HR comparison
against their approved dated attribution correction. Four new operations expose
context, proposal, independent review and history. Original claims stay intact;
neither sign of the difference creates a refund, debt or payment. Review is a
separate record and `SettlementState` is `NotRecorded`.

Exact routes, request examples, permissions, evidence and retry semantics:
[fuel reconciliation contract](Vehicle-Fuel-Reconciliation-20261020.md).

## Explicit benefit-review delegation - 20 October 2026

Benefit award/correction/resolution still requires a genuine active fixed Admin,
web MFA, current session, self-review denial and an explicit All-scoped
`attendance.leaves.benefits.review` grant. That grant may come from a separate
custom role on the same Admin account. A custom role named Admin is not enough;
neither SuperAdmin status nor the fixed Admin role automatically supplies the
benefit permission.

SuperAdmin uses the existing protected-target role-assignment workflow to grant
an explicitly configured role. Fixed roles remain immutable. No automatic grant
or live database provisioning was performed in this change. Clients must handle
denied authority without showing a successful award; role expiry/revocation
during final validation and retries remains a denial.

Outstation same-key claim retries now retain an existing approved claim where
the original requester, payload and protected source evidence still match.
They do not reopen approval or create a second payable claim.

The actual employee-master report contract passed bounded HTTPS/SQL acceptance:
CSV/XLSX page exports and one durable PDF. The durable status values remain
lowercase `pending`, `running`, `completed`, `failed`.
[R1 evidence](HR-Reports-R1-20261020.md).
