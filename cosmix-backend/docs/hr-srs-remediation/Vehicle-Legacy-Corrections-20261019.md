# Legacy Approved Trip Corrections

## Contract

An already-approved legacy trip with an intact schema-3 protected mileage
calculation and its original approval decision can receive one independently
approved dated-attribution correction. A pending trip must use the existing
attribution workflow. A trip with pending or approved daily attribution is not
eligible for this legacy correction path.

The original trip, approval, approval decision, mileage row and calculation file
are retained unchanged. A correction uses existing generic ApprovalRequest and
ApprovalDecision rows, protected evidence, audit and idempotency receipts. It
never creates another mileage row, rewrites a fuel claim, changes an approved
service record, deducts salary or records payment/settlement.

The context fingerprint binds the original trip, requester and approval context,
rowversion, exact mileage identity and protected calculation, and original
approval decision. The proposal also retains exact daily segments and the
then-current policy's attribution authority. Every date, interval, odometer,
map total, historical policy, commute revision, staff eligibility and custody is
validated by the existing explicit-segment calculation. No proportional split
or omitted-distance default is introduced.

Review requires the trip-management permission with current web MFA and All
scope, the retained proposal and segment roles, and the policy attribution
reviewer role currently configured on the review date. Employee, trip requester
and correction proposer cannot review their own correction. Current session,
timed grant, matching role and sources are checked around protected IO and
before commit. Protected decision replay also rechecks current authority and
source validity. Return/reject retains evidence and permits an explicitly based
replacement; an approved correction cannot be replaced in this bounded flow.

History is bounded to 1,000 requests per trip; segment count and elapsed duration
remain bounded to 366. Existing serializable authorization transactions own
business/decision/audit/receipt atomicity. Orphaned private files on a denied
transaction remain possible; successful receipts or decisions must not survive.

## Routes

All routes below are under `/api/v1/hr/vehicles`.

| Method | Route | Input |
| --- | --- | --- |
| GET | `/trips/{tripId}/attribution-correction-context` | Exact trip, original mileage fingerprint and latest request |
| POST | `/trips/{tripId}/attribution-corrections` | `SubmitVehicleTripAttributionCorrectionRequest` and `Idempotency-Key` |
| POST | `/trips/{tripId}/attribution-corrections/{requestId}/review` | Existing `ReviewVehicleAdjustmentRequest` and `Idempotency-Key` |
| GET | `/trips/{tripId}/attribution-corrections` | Bounded retained history |
| POST | `/{vehicleId}/monthly-assignment-context` | Existing monthly preview request; response has candidates and context fingerprints |

Correction submission requires `ExpectedTripVersion`, an explicitly nullable
`ExpectedLatestRequestId`, `ExpectedSourceFingerprint`, `Segments`,
`EvidenceReference` and `Reason`. It uses the existing strict
`VehicleTripSegmentRequest` for each daily segment. Review accepts Approved,
Returned or Rejected, exact `ExpectedVersion`, evidence and reason.

## Consumers

- Monthly preview/review/entitlement uses approved corrected segments, including
  next-month segments whose original mileage anchor is in the previous month.
  Older retained monthly evidence becomes `SourcesChanged` (or explicitly
  unavailable if sources cannot be read), with a null eligible quantity, never
  an invented zero. A new ordinary monthly request and independent approval
  explicitly reconcile the corrected sources.
- New fuel submission and pending fuel approval on corrected trips fail with
  `vehicle_fuel_attribution_reconciliation_required`. Existing approved claims
  and exact prior receipts remain historical evidence. This slice intentionally
  provides no fuel repricing, refund, additional payment or claim replacement.
- Pending maintenance compares its retained calculation to the corrected
  sources. A next cycle affected by an older consumed service fails with
  `vehicle_maintenance_attribution_reconciliation_required`. New services
  calculated after correction retain `AppliedTripCorrectionIds` in their private
  eligibility snapshot, so subsequent cycles can distinguish already-reviewed
  corrected evidence from unreconciled historical consumption. Old snapshots
  omit this nullable field and keep their original serialization.

## Same-Day Selection

Existing `SingleEligibleTripPerDay` policies keep their existing conflict for
multiple eligible trips. The additional explicit policy mode
`ExplicitEquivalentTripPerDay` allows a caller to select a retained candidate
when all same-day candidates share the exact policy, commute revision and
baseline, and every candidate has zero nearer-distance deficit. All approved
eligible mileage stays included; the selection changes no commute exclusion or
distance quantity.

The context endpoint returns `Days`, each with `BusinessDate`, `Candidates` and
`ContextFingerprint`. Preview and submission accept optional
`AssignmentSelections`: one item per ambiguous day with `BusinessDate`,
`MileageEntryId`, and `ExpectedContextFingerprint`. The calculation retains
these choices. Review, entitlement and maintenance revalidate them against the
complete eligible candidate set. Missing choice, foreign/duplicate choice,
added candidate or changed evidence fails closed. Existing calculations without
choices keep their prior serialization/fingerprint.

A nonzero nearer deficit or different commute/policy contexts still returns
`vehicle_monthly_commute_allocation_rule_required`. Concrete management decision:
for two same-day official journeys of 20 km against a 30 km usual commute, should
the daily obligation use one journey, combined daily work, or a retained split
of the commute baseline, and how should that obligation be assigned across
policy partitions? Choosing a trip ID alone does not answer this. No maximum,
minimum, first/last-trip or double-deficit rule is inferred.

## Verification Checkpoint

No product build was run by this worker. Parent owns the integrated build.
No database access, fixture database writes, migrations, hosts or Git writes
were performed. No Domain, DbContext, DI, permission or shared file was edited.

Standalone RED used the supplied frozen build:
`D:/CosmixVerification/hr-backend-20261019/hr-corrections-build04/bin/Cosmix.Api/release`.

Final expanded `VerifyVehicleDatedAttribution.cs`: **25/50 PASS**, exit 1.
All original 25 scenarios passed. All 25 additions failed for expected absent
correction/selection APIs or the absent correction request contract. Compilation
succeeded. Evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-3a0f57708c944c0cb3b6258c3f2fb744/VerifyVehicleDatedAttribution.log`.

Added scenarios cover preserved trip/mileage/decision identities, immutable
approved and pending fuel, new fuel rejection, changed monthly entitlement and
explicit reconciliation, cross-month lookup, consumed maintenance rejection,
pending maintenance invalidation, corrected maintenance cycles, independent
review, context/version/source changes, exact retries, return/replacement,
current configured reviewer authority on replay, missing required members,
pending/already-attributed ineligibility, result-IO source mutation and six
session/grant expiry cases across decision/result/audit storage. Equivalent
same-day selection is tested with complete candidate membership and a concrete
nonzero-deficit decision block.

**GREEN remains pending the parent-coordinated rebuilt Release output.**
Run against that frozen output without building inside this command:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory $ParentReleaseDirectory -Sources @(
  'docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs',
  'docs/hr-srs-remediation/VerifyVehiclePolicySuccession.cs',
  'docs/hr-srs-remediation/VerifyVehicleAdjustments.cs',
  'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs',
  'docs/hr-srs-remediation/VerifyVehicleRepository.cs',
  'docs/hr-srs-remediation/VerifyVehiclePolicyPublication.cs')
```

Real SQL concurrency/rollback, encrypted storage restart/failure and
authenticated HTTP/CSRF acceptance are not established by these in-memory
standalone scenarios. Client UI integration is separate. Unsupported older or
missing original evidence, second approved corrections, historical fuel
reconciliation and reconciliation of previously consumed maintenance remain
explicit limits, not silently accepted cases.

## Changed Files

Under `src/Cosmix.Application/HR/VehicleFuel/`:

- New `VehicleTripCorrectionContracts.cs`
- New `VehicleTripCorrections.cs`
- `VehicleTripAttribution.cs`
- `VehicleFuelService.cs`
- `VehicleFuelWorkflow.cs`
- `VehicleAdjustmentContracts.cs`
- `VehicleAdjustmentPolicy.cs`
- `VehicleMonthlyAdjustments.cs`

Also changed:

- `src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs`
- `docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs`
- This new document.

The existing repository contracts and implementation already support the new
request type and bounded history; no repository change was necessary.
