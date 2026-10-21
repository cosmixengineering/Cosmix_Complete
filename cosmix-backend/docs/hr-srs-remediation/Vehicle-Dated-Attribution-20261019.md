# Vehicle Dated Attribution and Policy Succession

## Boundary

This continuation implements HR SRS section 10.2, especially VEH-002/003/006,
for completed trips crossing local midnight, month boundaries and vehicle-policy
periods. It uses existing vehicle, approval, decision, mileage, policy and private
evidence storage. No table, migration, shared registration or permission is added.
Parent owns integrated builds. No product build, host, SQL connection/write,
migration application, provider call or Git operation is authorized or run here.
Accounts posting, deductions, settlement, payment and legal certification are out
of scope. Company rates and policy choices remain explicit configuration, not
defaults derived from SRS example amounts.

## Actual Workflow

1. The existing self trip submission records the observed journey. Multi-day
   trips validate staff eligibility and configured attribution on every covered
   Pakistan-local date. Duration is bounded to 366 days. Legacy approve cannot
   silently assign a cross-day trip to its starting date.
2. A permitted manager reads attribution context, including the exact trip
   approval rowversion and latest proposal ID. The manager submits explicitly
   evidenced daily segments against those identities. No timestamp-proportional
   or equal-mileage splitting is performed.
3. Segments must be ordered, adjacent UTC intervals exactly covering the trip,
   one interval per Pakistan-local date. Odometer endpoints must join exactly;
   segment map distances must sum to the trip map distance. Zero distances are
   allowed only as explicit required values. Dates, decimals, nullable commute
   IDs and evidence fields cannot be omitted and become implicit defaults.
4. Each segment names its exact effective policy and, when required, retained
   commute revision. Its policy chooses the distance basis. Dated staff/custody,
   evidence, policy identity, decimal precision and route totals are validated.
5. A configured independent reviewer can Approve, Return or Reject. Approval
   revalidates exact sources, checks both proposal and trip approval versions,
   records both decisions and creates one aggregate mileage row. That row points
   to protected dated evidence rather than duplicating the trip in the ledger.
   Return/reject retains history, leaves the trip pending and permits a new
   explicit proposal. An approved trip/allocation cannot be rewritten.
6. Session, timed permission, matching reviewer role and independent-actor checks
   run after decision evidence IO, after result/audit IO and on protected replay.
   Business changes, decisions, audit and idempotency receipts share the existing
   serializable authorization transaction. Denied transactions can leave orphaned
   protected files, never successful HR receipts or decisions.

## APIs

All new routes use `/api/v1/hr/vehicles` and the existing
`attendance.vehicle.trips.manage` permission, current web MFA and All scope:

| Method | Route | Contract |
| --- | --- | --- |
| GET | `/trips/{tripId}/attribution-context` | Current trip approval token and latest proposal ID |
| POST | `/trips/{tripId}/attributions` | Exact concurrency identities, daily segments, evidence, reason, Idempotency-Key |
| POST | `/trips/{tripId}/attributions/{requestId}/review` | Existing strict ReviewVehicleAdjustmentRequest and Idempotency-Key |
| GET | `/trips/{tripId}/attributions` | Retained bounded proposal/decision history |

Read/write operations are audited. History is bounded to 1,000 proposals per trip.
No permission grants are created. Current self trip/fuel endpoints remain in their
existing controller; manager attribution is not a mobile self-review capability.

## Explicit Policy Choices

Existing vehicle rules schema 1 and 2 remain compatible. Adjustment schema 2 adds
two required nullable properties; null keeps the relevant capability unconfigured:

- `TripAttribution`: `Mode=ExplicitDailySegments`,
  `CommuteTreatment=OncePerTripDay`,
  `FuelRounding=PerSegmentTwoDecimalsToEven`, explicit `ReviewerRoleCodes` and
  `AuthorityReference`. Every selected role must exist and be active at publication.
- `MonthlyPeriods`: `Mode=SeparatePolicyPartitions` and `AuthorityReference`.
  It requires monthly `OffsetScope=SameEmployeeVehicleMonthPolicyApprovedWork`.

The existing single-policy monthly offset remains supported. A mixed-policy
month requires the explicit partition choice in every involved policy. No rule
priority, cross-policy transfer of unused allowance or alternative rounding is
inferred. Exact schemas, unknown fields, duplicate configuration properties and
missing required members fail validation.

## Real Consumers

- Monthly preview/review/history/entitlement flatten the approved dated evidence
  into the requested month, including trips whose aggregate row starts in the
  previous month. Each policy partition applies its retained cap/deficit and
  maintenance choice. The approved entitlement read exposes the summed adjusted
  kilometres only while source fingerprints remain current.
- Multi-policy calculations include `Partitions`. The legacy `PolicyVersionId`
  and `Rule` fields identify the first partition for compatibility; they are not
  a substitute for the full partition list. Lines carry exact policy/date identity.
- Maintenance includes only segments on or before its service date, then adds
  the approved monthly adjustments whose retained partition selects
  `ReduceOutstation`. It retains source membership fingerprints for review.
- New fuel claims use each segment's exact retained rates/currency and explicitly
  configured per-segment rounding, then sum retained HR claim quantities. Review
  verifies the protected calculation. Previously retained claims are never
  repriced or redistributed. This is HR claim evidence, not a payment.

The main synthetic case expects 60 adjusted eligible km from two policy partitions:
the first partition caps its 10 km nearer deficit at zero eligible km; the second
has 70 eligible km and a 10 km nearer adjustment. It also expects 700 fixture
currency units using the second policy's fixture rate. Neither amount is a company
default or financial authorization.

## Immutable Succession

Existing policy validation/preview/publication/list/detail APIs now accept vehicle
rules schema 3, requiring an explicit nullable `Succession` property. A successor
names `PreviousPolicyVersionId` and `AuthorityReference`. Publication must target
the same definition, a strictly later future date, the unbranched effective
predecessor and no competing global policy period. New successor periods remain
finite. Global queries run within the existing serializable operation.

Only future selection changes. Predecessor `EffectiveTo`, rules, claims and
historical evidence remain untouched. The logical predecessor end is the
successor start; successor expiry never resurrects an old open-ended policy.
Exact retained-ID reads still return the original row. Duplicate branches,
foreign predecessors, missing authority, stale preview/version and unrelated
overlap fail closed. Resolution is bounded to 1,000 active-definition versions;
larger histories need a separately designed paging/index strategy, not truncation.
No shared PolicyVersion infrastructure is modified.

## Verification Status

New standalone suites (no new test project):

- `VerifyVehicleDatedAttribution.cs`: 25 scenarios covering actual consumer
  values, context/read/retry, cross-month membership, per-date maintenance,
  malformed segments, missing primitives, null requests, independent review,
  optimistic concurrency, returned revisions and storage-time authority expiry.
- `VerifyVehiclePolicySuccession.cs`: publication, retry, unchanged predecessor,
  dated resolution, non-resurrection, competing definitions, branching and strict
  predecessor configuration. Additional repository checks use real repository
  expressions and offline SQL translation with a connection-attempt guard.

Executed RED against frozen `hr-benefits-build05`:

- Initial dated suite: 0/11, expected missing APIs/cross-day guard. Evidence:
  `D:\CosmixVerification\part-c-parent-20261013\managed-f5b49a9d0f174ae0893dc1b604aa3cb7`.
- Expanded dated suite: compiled, 0/23, expected missing behavior. Evidence:
  `D:\CosmixVerification\part-c-parent-20261013\managed-25cba54fc394403a9740554812581d2a`.
- Succession: compiled, 1/4; three positive scenarios reject unsupported schema
  on the old binary, while competing configuration remains denied. Evidence:
  `D:\CosmixVerification\part-c-parent-20261013\managed-6b0380b35844426ca0eac019962a9874`.
  The prior attempt had one unused test helper compiler error, subsequently
  removed; it is not counted as behavioral RED.

Product source remains frozen. The parent reported integrated
`hr-corrections-build03` PASS. Dated attribution was rerun against that frozen
build: **23/23 scenarios PASS**, exit 0, including 60 adjusted eligible km,
3.5 retained fuel litres, a PKR 700 claim and explicit wrong-currency rejection.
Exact evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-ba225f46658d4c41a5e137fbf0d92950`.

The parent's prior 22/23 run
(`managed-4d6307af842e41c3bb42ed0850dc40a9`) exposed a test-only positional argument
error: the fuel request constructor is `(VehicleTripId, Currency,
ReceiptReference)`, but the fixture supplied `receipt-proof` as Currency and
`PKR` as ReceiptReference. Named arguments now bind these explicitly; a negative
assertion proves USD is rejected without creating a claim. No product currency
guard or product source was changed. This run verifies the dated suite only;
final results for the other five suites remain with the parent.

### Focused Reviewer Corrections

Two additional regressions were written and run against frozen
`hr-corrections-build03` before either product fix. Result: **23/25 PASS**, exit 1;
the original 23 scenarios remained green and both new scenarios failed for the
reviewed defects. Exact evidence:
`D:\CosmixVerification\part-c-parent-20261013\managed-4bd3dd67375540f79714187e2ee8377e\VerifyVehicleDatedAttribution.log`.

- `midnight-ending legacy trip belongs only to its preceding month` failed with
  `vehicle_trip_attribution_required`. September's ordinary approved trip ended
  exactly at October 1 Pakistan-local midnight. Monthly source filtering now uses
  `EndedAt.AddTicks(-1)`, matching existing exclusive-end attribution validation.
  The regression requires September and October each to retain only their own
  70 eligible km and October to contain only its own trip.
- `attribution and ordinary approval isolate mileage keys with the same client key`
  failed because both emitted the same `(VehicleId, IdempotencyKey)` pair, which
  conflicts with the retained unique storage key. Attribution now emits
  `attribution:{requestId:N}` (44 characters), independent of the client key and
  distinct from ordinary approval's `:mileage` suffix. The regression checks the
  two generated keys, request identity, length, exact decision replay and no
  duplicate mileage. Ordinary approval and existing retained keys are unchanged.

Only two product lines changed: `VehicleMonthlyAdjustments.cs:145` and
`VehicleTripAttribution.cs:87`. Product source is frozen for the parent's next
build. **The expanded 25-scenario suite is not yet GREEN against rebuilt DLLs.**
No product build, SQL or shared-file edit was performed for these corrections.

Run all six suites using `docs/attendance-gap-c/VerifyManaged.ps1` against the
parent-confirmed frozen API Release directory on D:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory $ParentReleaseDirectory -Sources @(
  'docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs',
  'docs/hr-srs-remediation/VerifyVehiclePolicySuccession.cs',
  'docs/hr-srs-remediation/VerifyVehicleAdjustments.cs',
  'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs',
  'docs/hr-srs-remediation/VerifyVehicleRepository.cs',
  'docs/hr-srs-remediation/VerifyVehiclePolicyPublication.cs')
```

## Final Rebuilt Verification

The earlier awaiting-GREEN checkpoint above is superseded by the parent's frozen
`hr-corrections-build04`: Release build PASS with zero warnings/errors. The final
managed batch exited 0: dated attribution **25/25**, succession **4/4**, monthly
adjustments **45/45**, entitlements **46/46**, repository **64/64** (zero SQL
attempts), policy publication **71/71** and registered OpenAPI **281/281** over
516 paths. Both newly reproduced vehicle defects are now GREEN against rebuilt
product DLLs. Evidence directory:
`D:/CosmixVerification/part-c-parent-20261013/managed-768b175b443546adb82f462467ce0e2a/`.

These are standalone service/contract checks, not authenticated SQL acceptance.
No database migration was applied.

## Genuine Remaining Gaps

- Multiple eligible assignments on the same employee/vehicle/day still require an
  approved selection/commute allocation policy; no precedence is guessed.
- Already-approved legacy trips without daily evidence cannot be retroactively
  split by this pending-trip workflow. A separate evidenced correction mechanism
  preserving old claims and consumed maintenance is not implemented here.
- Actual SQL rollback/deadlocks/concurrency, real encrypted-storage restart/failure,
  authenticated HTTP/CSRF and client UAT remain unexecuted. No production authority,
  configuration, insurer settlement or Accounts behavior is implied by these tests.

## Changed Files

New under `src/Cosmix.Application/HR/VehicleFuel/`:
`VehicleTripAttribution.cs`, `VehicleTripAttributionContracts.cs`,
`VehicleDatedFuel.cs`, `VehiclePolicySuccession.cs`.

Modified in the same directory:
`VehicleAdjustmentContracts.cs`, `VehicleAdjustmentPolicy.cs`,
`VehicleMonthlyAdjustments.cs`, `VehicleFuelService.cs`, `VehicleFuelWorkflow.cs`,
`VehiclePolicyContracts.cs`, `VehiclePolicyRulesCodec.cs`,
`VehiclePolicyPublication.cs`, `IVehicleFuelRepository.cs`.

Also modified:
`src/Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs`,
`src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs`,
`docs/hr-srs-remediation/VerifyVehicleRepository.cs`.

New tests/docs:
`docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs`,
`docs/hr-srs-remediation/VerifyVehiclePolicySuccession.cs`, this handoff.
No domain, shared authorization/registration/permissions/DbContext/migration file
was edited. Earlier shared dirty changes remain preserved.
