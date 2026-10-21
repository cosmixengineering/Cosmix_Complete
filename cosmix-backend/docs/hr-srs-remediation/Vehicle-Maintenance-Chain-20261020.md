# Retained consumed-maintenance chain continuation

Final verification: frozen parent build06 passed all four vehicle suites,
**268/268**, including the chronology repairs. See the final GREEN section below;
earlier RED and pending-build entries are retained as historical evidence.

Scope: subsequent maintenance services following retained original approvals or
existing equivalence proofs, with unchanged retained per-trip quantities. Original
calculations, service amounts, decisions, mileage and correction evidence remain
immutable. Changed-amount HR review belongs to the parent continuation.

The chain is walked iteratively through actual service records, matching approved
independent decisions and protected calculations. Each link must match its pinned
predecessor, chronological service date, consumed kilometres, threshold and cycle.
Original pre-correction nodes reconstruct their retained mileage fingerprint and
individual quantities through their own service date, without inventing an old
equivalence proof. Existing equivalence proofs and their retained calculations
are reconstructed outward. New proofs bind the immediate
predecessor calculation and decision and carry the original compared trip members;
intermediate mileage membership is also bound by the retained calculations.

The existing approved-service page API checks complete approved membership for
the vehicle/service kind, including older branches that a latest-row lookup could
miss. The existing submission, approval, commit and receipt-replay hooks revalidate
this chain. No change to shared VehicleFuelService.cs is needed.

Limits: at most 64 predecessor services and 10,000 approved vehicle services.
Missing evidence, cycles, competing branches, changed sources/quantities,
ordinary snapshots already carrying applied corrections without the required
equivalence evidence, threshold/cycle changes and applicable monthly adjustments
fail closed. Replay still requires the pinned predecessor or
the replayed service to be the latest approved service. No guessed recovery,
repricing, settlement, finance posting, schema or migration is introduced.

## RED

Ran the existing VerifyManaged.ps1 against frozen
`D:/CosmixVerification/hr-backend-20261020/hr-business-c04-build02/bin/Cosmix.Api/release`.
Standalone source compilation succeeded; **126/144 scenarios passed** and 18
failed at the existing `vehicle_maintenance_attribution_reconciliation_required`
chain prohibition. This is the expected RED, not a passing suite.

Evidence directory:
`D:/CosmixVerification/part-c-parent-20261013/managed-cfd8644a991448c98f971092e0c7fddc/`.
It contains the dated-attribution log, standalone compile log and release-input
hash manifest. Later test expansion adds hidden competing branches and calculation,
result and audit source-race checks.

The expanded source also compiled against the same old frozen build and ran
**126/149**, with 23 expected chain-prohibition failures. Evidence directory:
`D:/CosmixVerification/part-c-parent-20261013/managed-f2f6ac5e309f466da75639d3b9641542/`.
The original RED manifest's 64 release input hashes were independently rechecked
unchanged after the run.

## Integration

Product builds belong to the parent. These standalone checks use synthetic
collaborators, not SQL, providers, operational private storage or authenticated
production acceptance.

### GREEN - frozen build03

Release input:
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build03/bin/Cosmix.Api/release`.

The first run was **145/149**, not a pass. Four competing-branch fixtures tried
to decide before submission and failed the domain chronology guard. The fixture
now submits the competitor before the selected predecessor is approved, and uses
a valid older decision time. No production validation was weakened.
Failed-run evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-7b44533fd7474c09969ae9349d061544/`.

After that fixture correction, all four suites passed:

| Suite | Result |
| --- | --- |
| Dated attribution and maintenance chains | 149/149 |
| Policy succession | 4/4 |
| Adjustments | 45/45 |
| Entitlements | 46/46 |

Total: **244/244**, verifier exit 0, frozen input hashes unchanged.
Logs, standalone compile logs and manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-307cd623b6fe4eb3adc9d3dfb3fe0162/`.

The parent then requested an opt-in `DetachedAdjustmentReads` fixture field.
It defaults to false; when enabled, adjustment-list queries clone scalar header
state and copy RowVersion bytes. Find-by-ID still returns the tracked original.
The updated dated-attribution source compiled and passed **149/149** against
the same frozen build03, with unchanged release hashes:
`D:/CosmixVerification/part-c-parent-20261013/managed-9a4212a10a4448fca2e57219e8b573ea/`.

Fixture DLL: `VerifyVehicleDatedAttribution.dll` in that last directory.
SHA-256: `5A39372F3290038F3F838290B6763F3C18417233F9E88B820F4A42344283DFAB`.
The existing suite leaves the flag false. The parent's separate reconciliation
suite owns verification with detached reads enabled.

### Ordinary ancestors - build05 source freeze

After parent build04, the user requested support for two or more original
approved services before the first correction. Traversal now follows every
PreviousServiceRecordId to the root, validates all links and the complete approved
inventory, then proves every original service independently at its own cutoff.
The original root guards are replaced only by exact validated predecessor ID and
consumed-through bindings for non-root originals. Existing proof-bearing records
still require exact proof and calculation reconstruction. No general transition,
changed-quantity reconciliation, recovery or payment logic is added.

New tests cover two and four original services, trips introduced after the root,
two subsequent independently approved services and replay, missing root/middle
evidence and decisions, missing records, cycles, consumption/fingerprint tampering,
changed source evidence, a changed quantity belonging only to a later original,
and middle-evidence loss during submission/review/receipt IO.

RED against frozen
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build04/bin/Cosmix.Api/release`
compiled and ran **148/165**. The first run had 16 expected chain-prohibition
failures and one fixture using the September 15 policy for a September 19 trip:
`D:/CosmixVerification/part-c-parent-20261013/managed-801af0fe474d4aad8efef2db9bebeb94/`.
After selecting that trip's actual dated policy, the repeated RED was **148/165**
with all 17 failures at the existing chain prohibition:
`D:/CosmixVerification/part-c-parent-20261013/managed-f7c7f71cf50a46dfaa53708fdd7a5690/`.

Production source and the 165-scenario verifier are frozen for parent build05.
GREEN for this latest extension is pending; the earlier 244/244 result above
applies only to the build03 scope. No product build was run by this worker.

### GREEN - ordinary ancestors on frozen build05

Against
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build05/bin/Cosmix.Api/release`,
the dated-attribution suite passed **165/165**, policy succession **4/4**,
adjustments **45/45**, and entitlements **46/46**. Total **260/260**;
VerifyManaged exited 0 and confirmed unchanged frozen input hashes.
Logs and manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-a03ca70357614071b5fb8baf2f9ea6ef/`.

### Separate chronology RED - frozen build05

The parent requested a check of actual Submit construction invariants. The
service API constructs the service and approval header with the same actor.Now.
It selects an already approved predecessor before creating the successor.
The retained-chain validator did not enforce either header.RequestedAt ==
service.CreatedAt or predecessor.ClosedAt <= child.CreatedAt.

Eight new scenarios independently change only the original ancestor's header
requested time or its matching decision/header approval time by one tick beyond
the valid boundary. They cover submission, approval and both receipt-replay
paths. All eight unexpectedly succeed on build05: **165/173**, proving the missing
guards. Compilation succeeded; this is a separate RED after the earlier GREEN.
Evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-9d4d7f0c687340cf8fb3b24279e833df/`.

The user authorized the narrow repair after RED. Chain traversal now requires a
predecessor approval time and rejects it only when greater than the child service
CreatedAt. Approval-evidence validation also requires header.RequestedAt to equal
service.CreatedAt. Equal predecessor approval/child creation timestamps remain
valid. No other scope was added.

Source is frozen for parent build06. The 173-scenario verifier is unchanged from
the chronology RED; GREEN awaits the new frozen output. No product build was
run by this worker.

### Final GREEN - frozen build06

Release input:
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build06/bin/Cosmix.Api/release`.

| Standalone suite | Result | Log |
| --- | --- | --- |
| Dated attribution, historical chains and chronology | 173/173 PASS | VerifyVehicleDatedAttribution.log |
| Policy succession | 4/4 PASS | VerifyVehiclePolicySuccession.log |
| Adjustments | 45/45 PASS | VerifyVehicleAdjustments.log |
| Entitlements | 46/46 PASS | VerifyVehicleEntitlements.log |

Total: **268/268 PASS**, VerifyManaged exit code **0**. All four standalone
sources compiled successfully. The runner confirmed the frozen Release input
hashes were unchanged. Valid same-timestamp predecessor approval/child creation
chains still pass; all eight chronology-negative scenarios now reject.

Exact output directory containing all four logs, standalone compilation logs,
copied verifier sources and the `inputs.json` Release hash manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-77fd76897d4a46399842191a348ff485/`.

Executed command:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory 'D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build06/bin/Cosmix.Api/release' -Sources @(
  'docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs',
  'docs/hr-srs-remediation/VerifyVehiclePolicySuccession.cs',
  'docs/hr-srs-remediation/VerifyVehicleAdjustments.cs',
  'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs')
```

No product edits or product builds were performed during this final run.
This verifies the bounded unchanged-quantity maintenance continuation with
synthetic collaborators. SQL/concurrency, real providers, operational private
storage, settlement and whole-HR production acceptance are not established.
The scope and explicit limits at the start of this note still apply.

Owned files:

- `src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs`
- `src/Cosmix.Application/HR/VehicleFuel/VehicleFuelWorkflow.cs`
- `docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs`
- `docs/hr-srs-remediation/Vehicle-Maintenance-Chain-20261020.md`
