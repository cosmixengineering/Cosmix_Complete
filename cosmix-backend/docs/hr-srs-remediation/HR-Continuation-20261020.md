# HR continuation - 20 October 2026

## Scope and status

**Latest live result:** C04 subsequently executed successfully after the user's
SSMS screenshot and explicit new-pipe approval. **28/28 checks, 35 HTTP requests**;
155 protected snapshots and 24 prior files unchanged, zero migrations.
The earlier SQL stop below is historical, not the current connectivity status.
See [C04 actual acceptance and target](HR-Business-C04-20261020.md).

**V1 subsequent result:** 27 checks passed across 36 actual HTTP requests,
including all 18 planned vehicle/custody requests. The run then stopped at an
incorrect audit-correlation assertion; logout was not reached. A separate
read-only diagnostic verified exact persisted business state, three linked
receipts/audits and six encrypted objects. The owned host exited, 155 protected
snapshots and 33 old files were unchanged, and no migration was applied.
See [V1 evidence, reader defects and remaining coverage](HR-Vehicle-V1-20261020.md).
This is not an overall V1 acceptance pass.

**Latest build:** build09 at
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-v1-build09/bin/Cosmix.Api/release`
passed Release with zero warnings/errors; all 1,157 source input hashes remained
unchanged during the build. Fuel reconciliation 42/42 and generated MVC/OpenAPI
328/328 (529 paths) passed against this frozen output, with two existing CS1701
warnings in the standalone OpenAPI verifier only. Exact retained run:
`D:/CosmixVerification/part-c-parent-20261013/managed-ee18c610dc674a6d9b9dbf2523141295/`.
These are offline checks, not a new live SQL result.

Five further build09 vehicle suites passed **340/340**: remaining historical
boundaries 72, dated attribution/chains 173, policy succession 4, adjustments 45
and entitlements 46. All 64 frozen runtime input hashes remained unchanged.
Exact logs:
`D:/CosmixVerification/part-c-parent-20261013/managed-1cec4f0e4441448992ab1f3bd8a57bc4/`.
The supported historical transition and all-None monthly cases, integrity
restrictions and remaining coverage gaps are recorded in the
[current decision matrix](Vehicle-Remaining-Decision-Matrix-20261020.md).

This continuation covers historical vehicle corrections and affected HR backend
verification. No migration is applied. Real face, external OTP and legal
activation remain explicitly excluded by the user. Backend contracts and offline
checks do not certify separate web/mobile clients.

## Confirmed defect

The actual role factory and inspected test database use `ADMIN` and `SUPERADMIN`.
Leave review previously compared against title-case `Admin`/`SuperAdmin`; benefit
management had the same mismatch. Current authorization now uses authoritative
uppercase system-role codes while preserving fixed-assignment, explicit-grant,
scope, MFA and self-review restrictions.

New benefit award/revocation/correction/waiver evidence uses canonical role
codes. Historical title-case evidence remains readable without rewriting bytes,
hashes or exact audit/proof crossbindings. Historical aliases do not grant live
authority. Tests use `Role.CreateSystem(...).Code` rather than invented live role
values, and separately retain legacy-evidence cases.

## Historical vehicle continuation

The new historical fuel closure context and action close a superseded pending
schema-1 fuel claim using an independent reviewer, exact source/version,
protected decision, audit and idempotency. The old amount, calculation, trip,
mileage and receipt remain unchanged. This is not a replacement claim, refund,
settlement or permission to reuse the one-claim-per-trip slot.

Consumed-maintenance equivalence is a separately bounded continuation. The
existing next-service submission/review can accept a proven equivalent correction
to the first consumed service. Exact retained membership and per-trip quantities
through the original service date are required. Equal totals alone, guessed
repricing, recovery or a silent adjustment are not acceptable. Commit and receipt
replay recheck the retained source, independent decision and predecessor.
The later approved continuation now validates subsequent equivalence-bearing
services and ordinary pre-correction ancestors through the original root.
Every retained member, quantity, decision, predecessor and chronology must match;
equal totals alone are insufficient. Changed quantities and policy transitions
are not silently reconciled. See the superseding
[chain verification](Vehicle-Maintenance-Chain-20261020.md).

Approved legacy fuel claims now support an exact dated-policy comparison and
separate independently reviewed HR reconciliation proposal. The original claim
and receipt remain immutable. This is not Accounts posting, settlement, refund
or a payment instruction. Retried legacy claims also validate retained monetary
evidence instead of accepting changed stored amounts. See
[fuel contracts and verification](Vehicle-Fuel-Reconciliation-20261020.md).

### Latest frozen build06 verification

Release build06 passed with zero warnings and zero errors:
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build06/bin/Cosmix.Api/release`.

| Suite against build06 | Passed checks |
| --- | ---: |
| Fuel reconciliation / legacy integrity | 42/42 |
| Generated MVC/OpenAPI contracts | 328/328 (529 paths) |
| Vehicle integrity repository | 218/218 |
| Vehicle repository | 64/64 |
| Vehicle policy publication | 71/71 |
| C04 profile / boundary / composition | 256/256 |
| Dated attribution / historical chains / chronology | 173/173 |
| Vehicle policy succession | 4/4 |
| Vehicle adjustments | 45/45 |
| Vehicle entitlements | 46/46 |

Total: **1,247/1,247**, both standalone runners exited zero with frozen inputs.
These are synthetic application, persistence translation and contract checks,
not live SQL transactions. The OpenAPI standalone compiler emitted two CS1701
reference-unification warnings; the product Release build remained clean.

Exact logs/manifests:
- Parent six suites:
  `D:/CosmixVerification/part-c-parent-20261013/managed-a71f4319943449efb35c4c26d435fb24/`.
- Four maintenance suites:
  `D:/CosmixVerification/part-c-parent-20261013/managed-77fd76897d4a46399842191a348ff485/`.

A separate frozen build02 baseline rerun passed 23 suites: 3,207 counted
checks plus route and PDF-layout checks. It is not a build06 result:
`D:/CosmixVerification/hr-backend-20261020/hr-baseline-sidecar-e9914f56acd442109d8e232099642736/REPORT.md`.
No database or provider operations were performed by these suites.

## Actual SQL evidence

C03 performed 21 HTTP requests: both synthetic HR/Admin password, local MFA and
permissions checks passed, and HR was denied the Admin leave queue. The expected
Admin queue success failed and execution stopped before a department was
created. The runner did not retain that response status; no exact status is
claimed. The role-code mismatch above was then confirmed in source and readonly
SQL. C03 is a failed business acceptance run, not a pass.

After stopping C03, 155 protected snapshots and 15 prior C01/C02 files were
unchanged. No business record or migration changed; auth/session/rate/audit
changes were within the approved flow. The owned host exited. C03's unfinished
sessions were not silently revoked or claimed as logged out.

The user approved a separate C04 batch. On resume, the old SQL process was gone.
The user approved starting only the existing `MSSQLLocalDB` instance and readonly
inspection. It started as `Software-PC1\LOCALDB#D8AD654D`, PID 18860, operator
`COSMIXRND\Huma.Khan`, but `Cosmix_Testing` was not registered on it. This does
not establish database deletion. Database work stopped without attach, restore,
reset, replacement, migration or fixture writes. C04 did not execute.

The prepared C04 profile accepts only its exact approved paths and identifiers;
it does not accept arbitrary targets or bypass C03 guards. Its stored old target
binding has not been silently changed to the newly started instance. Before
execution, the intended existing database and exact target identity must be
available and confirmed.

## Verification record

Results below distinguish offline regression checks from actual HTTP/SQL.
See the companion [C03/C04 execution record](HR-Business-C03-20261019.md)
for target, counts, private-evidence locations and the explicit stop boundary.

### Frozen final Release and regression results

Release build02 passed with **zero warnings and zero errors**. Output:
`D:/CosmixVerification/hr-backend-20261020/hr-business-c04-build02/bin/Cosmix.Api/release`.

| Suite against final build02 | Passed checks |
| --- | ---: |
| Leave role authorization and retained evidence | 78/78 |
| Leave benefit workflow | 624/624 |
| Vehicle dated attribution / historical correction | 124/124 |
| Vehicle policy succession | 4/4 |
| Vehicle adjustments | 45/45 |
| Vehicle entitlements | 46/46 |
| C04 bounded profile, boundary and composition | 256/256 |
| Generated MVC/OpenAPI contracts | 310/310 (526 paths) |

**1,487 checks passed** across these final-output suites. They exercise actual
application code with synthetic collaborators or generated contracts, not
authenticated business SQL or device acceptance. The standalone OpenAPI compiler
emitted CS1701 reference-unification warnings; those are distinct from the clean
product build. The verification runners exited successfully and checked their
frozen Release inputs.

Retained logs and input manifests:

- Final leave rerun:
  `D:/CosmixVerification/part-c-parent-20261013/managed-60d559a9c8c84633bb9d9ecefd3ece9a/`.
- Vehicle suites:
  `D:/CosmixVerification/part-c-parent-20261013/managed-5f130bae4b5744ea90861b5509db339b/`.
- C04 and OpenAPI:
  `D:/CosmixVerification/part-c-parent-20261013/managed-e2d27212100f4b6ea99a410c7bed6f76/`.

Earlier integrated build01 also passed the eight leave suites (1,113 final checks:
roles 78, leaves 67, benefits 624, recovery replay 51, fulfilment 38, work claims
132, encashment workflow 31 and valuation 92). Separate read-contract checks
passed: queries 36, persistence 15, ESS consistency 38 and team calendar 58.
These were build01 results, not additional live acceptance.

The first work-claims fixture run was **116/120**, not a pass; the corrected
fixture and expanded regression run was **132/132**. Both logs were retained:
`D:/CosmixVerification/part-c-parent-20261013/managed-9c025abf8901441b8fe41b220cb312f9/`
and
`D:/CosmixVerification/part-c-parent-20261013/managed-f1b9ca9d58bd4247afde156e342f5406/`.
The maintenance regression first failed against the old frozen output
(101/117), then passed against build02 (124/124 including expanded cases).
The C04 runner's 43 offline checks passed without executing HTTP or SQL.

### Files for owner integration

This lists this continuation's relevant files, not every unrelated local change.
No commit, synchronization, schema change or migration application was performed.

- Leave authorization/evidence: `LeaveRoleCodes.cs`, `LeaveReviewAuthority.cs`,
  `LeaveBenefitService.cs`, `LeaveBenefitResolutionService.cs`,
  `LeaveBenefitEvidence.cs`, `LeaveBenefitCorrectionEvidence.cs`,
  `LeaveBenefitResolutionEvidence.cs`, `LeaveRules.cs`, `LeaveRecoveryEvidence.cs`
  and `LeaveRecoveryWorkEvidence.cs` under `src/Cosmix.Application/HR/Leaves/`.
- Vehicle: `VehicleHistoricalFuelClosure.cs`,
  `VehicleHistoricalFuelClosureContracts.cs`, `VehicleConsumedMaintenance.cs`,
  `VehicleFuelWorkflow.cs` and `VehicleFuelService.cs` under
  `src/Cosmix.Application/HR/VehicleFuel/`; historical closure routes in
  `src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs`.
- Later vehicle continuation: `VehicleLegacyFuelEvidence.cs`,
  `VehicleCorrectedFuel.cs`, `VehicleFuelReconciliationContracts.cs`,
  `VehicleFuelReconciliation.cs`, `VehicleConsumedMaintenance.cs` and
  `VehicleFuelWorkflow.cs`; four reconciliation controller operations.
  Affected standalone sources: `VerifyVehicleFuelReconciliation.cs`,
  `VerifyVehicleDatedAttribution.cs`, `VerifyVehiclePolicyPublication.cs` and
  `VerifyRemediationContracts.cs`. No schema or permission provisioning.
- Bounded testing: `src/Cosmix.Api/Development/HrBusinessTestingGuard.cs`,
  `Invoke-HrBusinessC04.ps1`, `VerifyHrBusinessRunnerC04.ps1` and the affected
  standalone leave, vehicle, profile and contract verification sources.
  The C04 runner is prepared, **not executed**.
- Handoff: this ledger, `HR-Business-C03-20261019.md`,
  `HR-Closure-Client-Contracts.md`, `Vehicle-Consumed-Maintenance-20261020.md`,
  `Boundary-UAT.md` and `docs/SRS-Module-Map.md`.

## Still not accepted

- Changed historical maintenance quantities, monthly-adjustment/policy-transition
  chains and unsupported or missing retained evidence need their own approved
  reconciliation rules. Supported same-quantity original and equivalence-bearing
  chains are now implemented and offline verified; no payable amount or recovery
  is invented for unsupported cases.
- Real SQL transactions, concurrency and encrypted-storage restart/failure
  acceptance for benefit corrections, recovery fulfilment, vehicle/outstation
  transactions and report workers remain outstanding.
- C04 department concurrency subsequently passed; it establishes only the exact
  bounded workflow described in [its evidence](HR-Business-C04-20261020.md), not
  acceptance of the other HR business workflows.
- Full HR production acceptance is therefore not achieved by this continuation.
