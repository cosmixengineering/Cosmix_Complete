# Bounded consumed-maintenance equivalence

Source implementation remains frozen at the parent-coordinated build02. The
parent reported a Release build with zero warnings/errors; all four standalone
vehicle suites passed against that frozen output on 20 October 2026.
This is not whole historical vehicle closure or authenticated SQL acceptance.

The existing next-service submission and independent maintenance approval can
recognize an equivalent correction to the first consumed service. The original
service must retain its protected calculation and matching independent approved
decision, have no earlier consumed chain, prior applied corrections, equivalence
proof, or applicable approved monthly adjustments. The original policy evidence,
threshold and cycle are checked; the next threshold and cycle must match.

The original calculation's mileage fingerprint is reconstructed with exact source
membership, references and any original dated attribution. Every retained trip's
eligible kilometres through the original service date must match individually.
Offsetting changes across trips and whole-trip totals that move kilometres past
the consumed date are not equivalence. Changed quantities remain blocked pending
an approved recovery policy.

The next protected calculation retains the original service/calculation/decision
references, original evidence fingerprint and per-trip correction references,
decision references, fingerprints and compared quantities. Legacy calculations
omit the nullable proof field. No original service amount, consumed threshold,
trip, mileage, decision or calculation is rewritten.

Submission commit, independent approval, approval commit, and both receipt replay
paths revalidate the proof. Revalidation pins the original predecessor and allows
the current service itself to be latest after approval without consuming its
threshold again. A competing approved predecessor fails closed. Existing
transaction/session/grant checks apply through result/audit storage and replay.

Third and subsequent services after this equivalence-bearing service remain
blocked, even when all quantities are unchanged. Earlier chains, monthly
adjustments, changed quantities, and broader historical recovery are outside this
slice. There is no new authority, payment, default formula, database schema,
migration, provider, SQL execution or private operational storage write.

## Verification

Standalone RED used only frozen C03 build02:
`D:/CosmixVerification/hr-backend-20261019/hr-business-c03-build02/bin/Cosmix.Api/release`.
The test source compiled and ran **101/117** scenarios successfully; the 16
failures were the absent equivalence path and its expected downstream checks.
Evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-abf356f565a7429e8a8c7e53e2217c55/VerifyVehicleDatedAttribution.log`.

After RED, the same standalone source additionally asserts retained proof
references, third-service denial, competing predecessor rejection at review and
both replay paths, and original protected-file loss during submission/review
result and audit storage. All of these checks passed in the GREEN run below.
No product build was run by this worker.

### GREEN - frozen C04 build02

Release input:
`D:/CosmixVerification/hr-backend-20261020/hr-business-c04-build02/bin/Cosmix.Api/release`.

| Standalone suite | Result | Log |
| --- | --- | --- |
| Dated attribution and historical corrections | 124/124 PASS | `VerifyVehicleDatedAttribution.log` |
| Policy succession | 4/4 PASS | `VerifyVehiclePolicySuccession.log` |
| Adjustments | 45/45 PASS | `VerifyVehicleAdjustments.log` |
| Entitlements | 46/46 PASS | `VerifyVehicleEntitlements.log` |

Total: **219/219 PASS**, verifier exit code **0**. Standalone compilation
succeeded for all four sources. The verifier confirmed its frozen release
inputs were unchanged after the run.

All logs, individual compile logs and the `inputs.json` release hash manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-5f130bae4b5744ea90861b5509db339b/`.

These are synthetic collaborator checks. No SQL, host, provider, operational
private storage or Git network work was performed. Third/subsequent service
chains and changed-quantity recovery remain blocked as documented above.

Executed command:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory 'D:/CosmixVerification/hr-backend-20261020/hr-business-c04-build02/bin/Cosmix.Api/release' -Sources @(
  'docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs',
  'docs/hr-srs-remediation/VerifyVehiclePolicySuccession.cs',
  'docs/hr-srs-remediation/VerifyVehicleAdjustments.cs',
  'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs')
```

## Changed files

- `src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs` (new)
- `src/Cosmix.Application/HR/VehicleFuel/VehicleFuelWorkflow.cs`
- `src/Cosmix.Application/HR/VehicleFuel/VehicleFuelService.cs`
- `docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs`
- `docs/hr-srs-remediation/Vehicle-Consumed-Maintenance-20261020.md` (new)
