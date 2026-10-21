# Remaining Vehicle Boundaries - 20 October 2026

## Later pilot-build09 verification

The subsequent `hr-pilot-build09` output is distinct from the older
`hr-vehicle-v1-build09` below. Remaining boundaries now pass **109/109**,
including EngineOverhaul transition composition, original dated attribution,
competing-root protection and predecessor receipt replay after successor
approval. Fuel reconciliation passes **42/42** on the same frozen output.

A reproduced historical replay defect (four failures in the preceding
105/109 run) is fixed: replay validates membership in the complete successor
chain without comparing an older approved service to the successor's newly
consumed entitlement. Fresh submissions/reviews retain their stricter checks.
Missing successor decisions and competing roots still reject replay.

See [pilot results, exact logs and limits](HR-Mobile-Pilot-Readiness-20261020.md).
These checks close the listed offline composition gaps, not live historical
SQL acceptance. Changed consumed quantities remain a blocked business case,
not an assumed adjustment or payment.

## Final GREEN - Frozen Build09

Parent approved unchanged historical per-trip quantities with existing explicit retained `CarryConsumedKilometers` transitions. **Build09 GREEN: 340/340, runner exit 0**, with no product, verifier or expectation edits during verification. The build08 results below remain historical evidence for the earlier None-only scope.

Frozen product:
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-v1-build09/bin/Cosmix.Api/release`.
Parent reported build09 succeeded with zero warnings/errors and all 1157 source hashes unchanged; this worker did not rebuild.

| Suite | Result | Execution log |
| --- | --- | --- |
| Remaining boundaries / explicit None and carry composition | 72/72 PASS | `VerifyVehicleRemainingBoundaries.log` |
| Dated attribution / historical maintenance chains | 173/173 PASS | `VerifyVehicleDatedAttribution.log` |
| Policy succession | 4/4 PASS | `VerifyVehiclePolicySuccession.log` |
| Adjustments | 45/45 PASS | `VerifyVehicleAdjustments.log` |
| Entitlements | 46/46 PASS | `VerifyVehicleEntitlements.log` |

Exact directory for all five logs, standalone assemblies, retained source copies, compiler response files and `inputs.json`:
`D:/CosmixVerification/part-c-parent-20261013/managed-1cec4f0e4441448992ab1f3bd8a57bc4/`.
Each log independently contains its stated number of PASS lines and zero FAIL lines. All five standalone compilations completed without emitted diagnostics; no `*-compile.log` files were emitted. The runner checked unchanged verifier sources and frozen Release inputs; a separate post-run check confirmed **64/64 input-manifest hashes unchanged**. The three frozen source hashes recorded below matched before and after execution.

### Current Supported and Blocked Cases

| Boundary | Verified build09 distinction |
| --- | --- |
| Unchanged historical per-trip quantities, own retained node policies and exact explicit carry transitions | Supported through submission, independent review, later proof-bearing services and both protected receipt paths; original thresholds may differ |
| Applicable approved monthly history explicitly all `MaintenanceEffect=None` | Supported with complete retained evidence validation and latest-approved selection per employee/month |
| Missing, wrong or malformed required transition; altered original policy, authority or transition-bearing fingerprint | Rejected; no implicit carry or replacement historical evidence |
| Changed historical per-trip quantities, even with enough aggregate mileage | Still blocked; carry does not authorize changing consumed historical entitlement |
| Existing predecessor with `Once`, selected mixed/ReduceOutstation history even at zero, missing/unknown monthly mode | Still rejected |
| Lost or changed original evidence, decision, chronology, membership or consumption; mutation during commit/replay | Still rejected; original retained records are not rewritten |

The build06/build08 expected-blocked explicit carry observations are **superseded by build09 GREEN only for this narrow unchanged-quantity scope**. This is synthetic offline service verification, not SQL, provider, host or database-concurrency acceptance. Specific coverage gaps remain for EngineOverhaul transition composition and the combined original dated-attribution plus transition-chain correction path; neither was added in this frozen run.

Executed command (fixture environment path/hash is retained in Evidence and Reproduction below):

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:/CosmixVerification/hr-backend-20261020/hr-vehicle-v1-build09/bin/Cosmix.Api/release' `
  -Sources @('docs/hr-srs-remediation/VerifyVehicleRemainingBoundaries.cs',
    'docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs',
    'docs/hr-srs-remediation/VerifyVehiclePolicySuccession.cs',
    'docs/hr-srs-remediation/VerifyVehicleAdjustments.cs',
    'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs')
```

### Explicit Carry Implementation

Product changes are confined to `VehicleConsumedMaintenance.cs` and the two private proof call sites in `VehicleFuelWorkflow.cs`. No existing calculation, snapshot, service amount, decision, mileage row, policy version or receipt is rewritten. No new rate, settlement, recovery rule, schema or authority is introduced.

### Required Invariants

| Invariant | Frozen implementation and focused verification |
| --- | --- |
| Each original node uses its own policy | Original service date and retained policy ID load that node's threshold, maintenance rule, staff and workshop eligibility. Rule/staff hashes and threshold must match its retained snapshot. The next service's threshold is no longer passed into historical equivalence. Tests alter root/middle thresholds and an original maintenance rule. |
| Explicit retained transition at each changed link | Select exactly one child-policy transition for its service kind and actual predecessor's policy ID; require `CarryConsumedKilometers`. Existing strict adjustment-policy validation still requires authority evidence. Tests remove the transition or alter kind, predecessor, mode and authority. |
| Exact original transition-bearing fingerprint | Reconstruct original mileage membership and original dated attribution first, then hash the same anonymous shape as the original workflow: `new { Mileage = fingerprint, Transition = transitions[0], PreviousServiceId = predecessor.Record.Id }`. A changed nonempty authority reference must fail fingerprint equality, even if quantities and thresholds are unchanged. No replacement fingerprint is persisted to old evidence. |
| Recorded consumption is authoritative | Each predecessor's recorded `ConsumedThroughKm` must equal its child's `ConsumedBeforeKm`. Existing snapshot validation still requires `ConsumedThroughKm = ConsumedBeforeKm + that node's RequiredKm`, sufficient retained mileage and root consumption zero. Tests require 50 -> 110 -> 150 -> 180 -> 210 under thresholds 50/60/40/30/30, and 50 -> 70 for a decreased next threshold. |
| Exact per-trip quantities, membership and cutoff | Every original node independently proves all its original trip members and quantities through its own service date. Upward/downward corrected quantities remain rejected even with explicit transitions and enough overall mileage. No equal-total substitution is introduced. |
| Approval and chronology | Existing independent decisions, header/service timestamps, predecessor approval before child creation, predecessor identity, full approved inventory, cycles and limits remain. Tests remove a middle decision/file and change predecessor approval/header timestamps. |
| Current next policy remains authoritative | The existing workflow still requires its own transition for a changed next threshold/cycle and rejects `Once` when a predecessor exists. Tests cover absent/wrong next transition and current Once; historical reconstruction also rejects a non-root original Once service. |
| Monthly boundary stays narrow | Existing all-None selection/evidence checks remain. Selected mixed/ReduceOutstation history remains unsupported even with a zero monthly delta. No general monthly maintenance reconciliation is added. |
| Commit and replay revalidate | Existing submission/review/commit/replay hooks are unchanged. New tests alter an original transition authority during submission calculation/result/audit, review result/audit, and both protected receipt reads. They require exact source conflict and unchanged committed cardinality, with a mutation-reached assertion. |

Positive tests cover a corrected root followed by a new explicit threshold transition, an already-approved original transition before correction, three original thresholds followed by two proof-bearing services, independent reviews, both receipt paths and immutable original record/evidence identities. Synthetic policy changes/removals in negative tests are fault injection only, never product mutation behavior.

### Executed RED on Frozen Build08

Frozen product: `D:/CosmixVerification/hr-backend-20261020/hr-vehicle-none-build08/bin/Cosmix.Api/release`.

Final verifier compiled without emitted diagnostics and ran **53/72**, exit **1**. All **19 failures** were at the existing `vehicle_maintenance_equivalence_policy_required` or `vehicle_service_cycle_evidence_unavailable` threshold/chain prohibition. These include positive paths and downstream negative assertions that could not yet reach their specific check. RED alone did not prove those later invariants; the same frozen verifier now passes 72/72 on build09 as recorded above.

Exact final RED sources, executable, compiler response, execution log and input manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-1d328a35bbb342508262e1e57f63220d/`.
The runner stops after the expected failing suite. A separate post-run read independently checked **64/64 frozen Release input hashes unchanged**. The working verifier and retained runner source hashes also match.

The initial RED also ran 53/72 in `managed-0768d3d0c0564de0bca84a8800222816`. One monthly-limit case changed policy bytes after correction approval and therefore failed at correction-source integrity. The fixture was corrected to configure ReduceOutstation before correction approval; the final RED above then failed that case at the expected historical chain prohibition. No product validation was weakened to accommodate it.

Frozen source SHA256 for parent build09:

- `src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs`: `691D5AA72F6CCBD9C99E0C7693123377C0ABE7DBFE15537B56A9BA3A7686CD91`
- `src/Cosmix.Application/HR/VehicleFuel/VehicleFuelWorkflow.cs`: `0CAB8A2F5BBB5D3537996F436E2B7F17ABE398AF1767E995ADB439E51B6806BD`
- `docs/hr-srs-remediation/VerifyVehicleRemainingBoundaries.cs`: `523016E258CF39AE8DF5EE9C735047E7761DBBDCFC157CB7FD2E9E39A1674549`

The same previously reviewed fixture assembly/hash below is reused. No product build, host, SQL, provider, migration, Git write or unrelated shared-file edit was performed. The original chain173/succession4/adjustments45/entitlements46 sources were not edited or rerun for this RED; all four and the expanded 72-scenario suite subsequently passed unchanged against build09.

## Historical GREEN - Frozen Build08

Parent coordination approved only explicit `MaintenanceEffect=None` composition under the existing unchanged-kilometre design. This is a code-support gap, not a new business formula. All five requested suites passed against frozen build08 with unchanged product/verifier sources and no expectation changes.

Frozen product:
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-none-build08/bin/Cosmix.Api/release`.
Parent reported its Release build succeeded with zero warnings/errors; this worker did not rebuild.

| Suite | Result | Execution log |
| --- | --- | --- |
| Remaining boundaries / explicit None composition | 43/43 PASS | `VerifyVehicleRemainingBoundaries.log` |
| Dated attribution / historical maintenance chains | 173/173 PASS | `VerifyVehicleDatedAttribution.log` |
| Policy succession | 4/4 PASS | `VerifyVehiclePolicySuccession.log` |
| Adjustments | 45/45 PASS | `VerifyVehicleAdjustments.log` |
| Entitlements | 46/46 PASS | `VerifyVehicleEntitlements.log` |

Total: **311/311**, runner exit **0**. All five standalone sources compiled without emitted diagnostics. No `*-compile.log` files were emitted for these silent successful compiles; the compiler response files are retained.

Exact directory for the five execution logs, frozen source copies, standalone assemblies, compiler response files and `inputs.json` Release hash manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-d7a5e91b26304a5bb058387ac00ed4ff/`.
The runner confirmed unchanged frozen Release inputs and verifier sources. The two frozen source hashes listed below were also checked before and after execution and still match. No product or test file changed during this GREEN run.

### Build08 Acceptance Delta

The historical build06 blocked-None observations below are **superseded for build08**:

| Case | Verified build08 behavior |
| --- | --- |
| Applicable approved single-rule or partitioned monthly history, explicitly all `None` | Historical unchanged-km service submission and independent approval succeed; exact submission and approval receipts replay |
| A `None` revision is approved after service submission or approval | Pending service review and both retained receipt paths remain usable |
| Monthly adjustment is nonzero but its maintenance effect is `None` | Maintenance retains unchanged eligible kilometres; the monthly amount is not applied to maintenance |
| Multiple original service ancestors and an equivalence-bearing successor with all-None history | Existing consumed-km proofs, further independent services and submission replay succeed |
| Latest approved None replaces older approved ReduceOutstation | Older revision no longer controls maintenance selection; its retained evidence and decision still must validate |
| Pending None after an approved ReduceOutstation revision | Pending revision does not replace the approved one; maintenance remains blocked |
| Selected mixed/ReduceOutstation history, even zero adjustment; malformed/missing mode; source/proof corruption | Still rejected |
| Changed historical quantities or threshold transitions, even with an approved None month | Still rejected; no additional support was introduced |

These are synthetic service-level checks, not vehicle SQL/concurrency or provider acceptance.

Executed command (fixture environment path/hash is recorded below):

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:/CosmixVerification/hr-backend-20261020/hr-vehicle-none-build08/bin/Cosmix.Api/release' `
  -Sources @('docs/hr-srs-remediation/VerifyVehicleRemainingBoundaries.cs',
    'docs/hr-srs-remediation/VerifyVehicleDatedAttribution.cs',
    'docs/hr-srs-remediation/VerifyVehiclePolicySuccession.cs',
    'docs/hr-srs-remediation/VerifyVehicleAdjustments.cs',
    'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs')
```

## Implemented Boundary - Build08

The product change is confined to [VehicleConsumedMaintenance.cs:173](../../src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs#L173). `RequireNoEffectMonthlyHistory` first loads the entire history through existing `MonthlyRows`, preserving snapshot, fingerprint, revision/predecessor and decision validation. It then applies the maintenance consumer's service-date cutoff and latest-approved selection per employee/month. Every selected base rule must explicitly say `None`; if partitions exist, the list must be nonempty and every partition rule must explicitly say `None`. A selected `ReduceOutstation` partition still blocks even with zero adjustment. Missing, null or unknown modes do not qualify. There is no caller-supplied bypass.

Existing monthly aggregation, exact consumed membership/quantities, historical policy/threshold, chronology, independent decision, commit and receipt-proof checks remain. No shared workflow or monthly-consumer file changed. Threshold-transition and changed-quantity support were not added.

### Executed RED

Frozen input: `D:/CosmixVerification/hr-backend-20261020/hr-c04-rebind-build07/bin/Cosmix.Api/release`.
Updated standalone verifier compiled successfully and ran **31/43**, exit **1**. All 12 failures reported `vehicle_maintenance_attribution_reconciliation_required`, the existing blanket monthly guard; no compilation or setup error was observed. This is expected RED, not a passing result for the new feature.

Exact source, executable, compile/execution logs and Release input manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-42ec2bb91bce47dca745f5c3d2db9e70/`.
The runner stops after an expected suite failure; its final post-run input comparison is therefore not claimed for this RED.

The 43-scenario verifier includes single-rule/all-None partition submission, independent review, both receipt paths, late None approval, nonzero monthly adjustment with no maintenance effect, multiple original ancestors and equivalence-bearing successors. Negative coverage includes mixed/zero ReduceOutstation, unknown/null/missing modes, empty partitions, monthly evidence/decision corruption, original source/proof loss, and retention of changed-quantity/threshold rejection. A synthetic, self-consistently rebound older monthly snapshot tests latest-approved selection and complete superseded-history validation; it is not a product route for mutating history. Some integrity probes are reached only after the new positive path succeeds, so RED alone does not prove them.

Frozen source SHA256:

- `VehicleConsumedMaintenance.cs`: `A26222F7D9F2F1DE5B1624298D46DAD3441745F270BF61DFA129DE6831E235F8`
- `VerifyVehicleRemainingBoundaries.cs`: `81CA9AE3F77E1085A07A5552D5BBE108C487DDC46AA4937748232B42E55511B5`

No product build, SQL, host, provider or migration was run here. The following build06 record is preserved as historical evidence. Its no-effect availability restrictions are superseded by build08 GREEN; its explicit carry threshold-composition restrictions are superseded by the narrow build09 GREEN above. Changed-quantity restrictions remain.

## Historical Build06 Code References and Findings

- [VehicleConsumedMaintenance.cs:98](../../src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs#L98): historical equivalence rejects **any approved monthly row for the vehicle whose month ends by the next service date**. It does not filter by maintenance effect, delta, employee, or latest approved revision. Consequently even an approved zero adjustment with `MaintenanceEffect=None` blocks a same-quantity correction. This is broader than the actual maintenance consumer.
- [VehicleMonthlyAdjustments.cs:331](../../src/Cosmix.Application/HR/VehicleFuel/VehicleMonthlyAdjustments.cs#L331): the ordinary consumer selects the latest approved revision per employee/month, selects only `ReduceOutstation` partitions, revalidates their exact sources, and adds their signed kilometre effect. `None` has no maintenance effect.
- [VehicleFuelWorkflow.cs:92](../../src/Cosmix.Application/HR/VehicleFuel/VehicleFuelWorkflow.cs#L92) and [VehicleConsumedMaintenance.cs:108](../../src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs#L108): historical equivalence runs before the ordinary transition rule and requires equal thresholds/cycle modes. An explicit `CarryConsumedKilometers` transition therefore does not unblock a threshold change combined with historical equivalence.
- [VehicleConsumedMaintenance.cs:42](../../src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs#L42): an already-approved historical chain containing different thresholds/cycle modes fails traversal, even when its original transition was explicitly configured and accepted.
- [VehicleConsumedMaintenance.cs:158](../../src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs#L158): each trip's eligible kilometres through each consumed service date must match, not just the vehicle total. Changed quantities fail at line 169. Neither the monthly policy nor `CarryConsumedKilometers` defines a correction to an already-consumed historical entitlement.
- [VehicleFuelWorkflow.cs:87](../../src/Cosmix.Application/HR/VehicleFuel/VehicleFuelWorkflow.cs#L87): a next policy with `CycleMode=Once` rejects an existing approved predecessor before transition processing. A configured carry transition does not override that guard.

**No new incorrect calculation, unauthorized acceptance, or integrity bypass was demonstrated.** The monthly no-effect and configured-transition cases are demonstrated availability restrictions in the documented historical boundary, not evidence of a new regression. They should not all be described as missing company rates or authority. Existing policies already state their ordinary numerical treatment; combining that treatment with historical proof validation remains unsupported code behavior. Product changes require parent coordination.

## Historical Build06 Decision Matrix

All examples below are synthetic kilometres and policy values, not proposed company settings. "Supported" means implemented behavior; it does not certify vehicle SQL acceptance.

| Input / branch | Build06 behavior and exact boundary | Do existing policies resolve it? | Next acceptance classification |
| --- | --- | --- | --- |
| Ordinary predecessor consumed 50; next threshold 60; exact previous-policy transition names `CarryConsumedKilometers` | Independently approves with 110 eligible and 110 consumed-through | Yes: carry the recorded 50 and add the new 60 | Supported positive case |
| Same ordinary change without that transition | `vehicle_service_cycle_policy_transition_unconfigured` | No transition configured; no default is authorized | Configuration-negative case |
| New policy ID, unchanged threshold/cycle, unchanged historical per-trip quantities | Independently approves; 110 eligible, 100 consumed-through | Yes; changing policy identity alone is not prohibited | Supported historical positive case |
| Same quantities, but next threshold changes 50 to 60 with explicit carry | `vehicle_maintenance_equivalence_policy_required` before ordinary transition processing | Carry defines the next ordinary milestone; it does not currently compose with the equivalence validator | Unsupported composition; not a request to invent a new rate |
| Original 50 -> 60 service chain already independently approved under explicit carry, then unchanged correction | Next service fails `vehicle_service_cycle_evidence_unavailable`; two approved originals remain | Original transition already resolved the arithmetic; historical traversal still requires equal thresholds | Unsupported historical transition chain |
| Next policy is `Once`, with an existing approved predecessor and explicit carry | `vehicle_service_once_already_consumed` | Carry does not override `Once`; permitting another service would change cycle meaning | Expected negative case, no bypass |
| Historical trip changes 110 -> 120 or 110 -> 100, even with explicit carry and enough km for the next 100 milestone | `vehicle_maintenance_equivalence_policy_required` | No. Carry preserves recorded consumption; it does not decide how changed historical entitlement affects an already-approved service | Business decision needed before changed-quantity implementation |
| Approved September monthly revision has zero adjustment and `None`; unchanged correction; next service October 1 | `vehicle_maintenance_attribution_reconciliation_required` | `None` already says no maintenance effect. Blanket historical guard still blocks | Unsupported no-effect composition; parent can scope a narrow implementation separately |
| Same approved zero revision with `ReduceOutstation` | Same rejection | Current zero arithmetic is explicit; proof binding/revalidation of monthly evidence is not implemented for this historical path | Unsupported composition, not permission to omit evidence |
| Either approved zero revision above; next service September 30 | Independently approves: September month is not applicable until October 1 | Yes: `MonthStart.AddMonths(1) <= serviceDate` is the cutoff | Supported cutoff positive case |
| Ordinary chain, no attribution correction, approved zero September revision, October 1 service | Independently approves for both `None` and `ReduceOutstation` | Yes | Supported ordinary monthly case |
| Monthly revision Pending, Returned, or Rejected; no earlier Approved revision | Historical equivalence independently approves | Yes: unapproved revisions do not affect maintenance | Supported negative-state cases; an older Approved revision would still count |
| Approve a `None` zero month after an equivalence service is submitted | Review fails with the historical reconciliation code; service stays Pending | No numerical effect is unresolved; proof guard prevents progress | Unsupported late monthly composition |
| Approve the same month after that service is Approved | Both exact submission and approval receipt replays fail with the historical reconciliation code; retained service stays Approved | Same blanket guard; failure is not reversal or repayment | Unsupported replay boundary |
| Changed correction makes old monthly revision stale; explicit replacement monthly revision is independently approved | Monthly entitlement moves `SourcesChanged` / null -> `Approved` / 120 km; next historical maintenance service still fails the reconciliation code | Existing monthly rules resolve the monthly result, but do not resolve consumed maintenance | Supported monthly revision; separate unsupported historical consumer |

The new probes demonstrate both upward and downward changes even where the next milestone could still be reached. "Enough total kilometres" therefore does not establish equivalence. Offsetting per-trip changes, moving kilometres beyond an ancestor's service date, missing evidence, cycles, competing branches and chronology were already covered by chain173; those suites were not rerun here.

## Source-Only Boundaries

These observations were traced in source, not added to the 20 new executed scenarios:

- Historical chains reject `MonthlyAggregationFingerprint` at [VehicleConsumedMaintenance.cs:23](../../src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs#L23). Ordinary services separately reject a changed aggregation fingerprint at [VehicleFuelWorkflow.cs:100](../../src/Cosmix.Application/HR/VehicleFuel/VehicleFuelWorkflow.cs#L100). Removing this field or substituting equal totals would discard retained source identity.
- Nonzero `ReduceOutstation` arithmetic and daily aggregation have explicit rules, but those rules do not specify changed historical consumption reconciliation. A stale selected month fails `vehicle_monthly_adjustment_stale` before equivalence. A negative adjusted maintenance total also fails closed.
- Dated trip segments, separate policy partitions, equivalent-trip selection and combined daily travel already exist in [VehicleMonthlyAdjustments.cs:180](../../src/Cosmix.Application/HR/VehicleFuel/VehicleMonthlyAdjustments.cs#L180). Older documentation describing every cross-day or multi-policy month as unsupported is superseded. Missing configuration, conflicting baselines/policies and unspecified nonzero daily deficit allocation still fail explicitly.
- [VehiclePolicySuccession.cs:9](../../src/Cosmix.Application/HR/VehicleFuel/VehiclePolicySuccession.cs#L9) resolves dated policy selection, not historical consumption. It is distinct from `MaintenanceTransitions`. A later policy publication cannot be treated as authority to rewrite retained historical policy bytes.
- Review authority remains existing fuel/maintenance permissions, current session/grants, independent actors, and configured monthly reviewer roles. Monthly review checks every partition's roles at [VehicleMonthlyAdjustments.cs:91](../../src/Cosmix.Application/HR/VehicleFuel/VehicleMonthlyAdjustments.cs#L91). No new role or financial authority is inferred.

## Evidence and Reproduction

New standalone [VerifyVehicleRemainingBoundaries.cs](VerifyVehicleRemainingBoundaries.cs): **20/20 boundary scenarios PASS**, runner exit **0**, no compiler diagnostics. This count means observed positive and expected-blocked behaviors matched the probes; it does not mean all 20 workflows are supported.

Frozen product:
`D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build06/bin/Cosmix.Api/release`.

Final source, executable, compiler response/log, execution log and Release input manifest:
`D:/CosmixVerification/part-c-parent-20261013/managed-e093c4e0eab34ddab4e9bc3992a7be09/`.

Final verifier source SHA256:
`A5FE5A825A49E35204A70B2DB492083E85D3AED1777A833FC2EB90036CF3B9F1`.
The working source and retained runner copy matched after execution.

The verifier loads the already-compiled public `DatedFixture` from the final chain173 run, without invoking that assembly's entry point or duplicating its repository implementation:

- Assembly: `D:/CosmixVerification/part-c-parent-20261013/managed-77fd76897d4a46399842191a348ff485/VerifyVehicleDatedAttribution.dll`
- SHA256: `D604753ED9E704AFA615BE3EA00C74EE68813FD01E4D4DF68E563A70E5035668`

```powershell
$env:COSMIX_VEHICLE_FIXTURE_ASSEMBLY = 'D:/CosmixVerification/part-c-parent-20261013/managed-77fd76897d4a46399842191a348ff485/VerifyVehicleDatedAttribution.dll'
$env:COSMIX_VEHICLE_FIXTURE_SHA256 = 'D604753ED9E704AFA615BE3EA00C74EE68813FD01E4D4DF68E563A70E5035668'
& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:/CosmixVerification/hr-backend-20261020/hr-vehicle-close-build06/bin/Cosmix.Api/release' `
  -Sources @('docs/hr-srs-remediation/VerifyVehicleRemainingBoundaries.cs')
```

The runner confirmed unchanged frozen Release inputs. An earlier subset passed 17/17 in `managed-4720ab06a031465aa5264ee01fe27603`; it is superseded, not an additional 17 final scenarios. No failed runs occurred. Fuel42 and chain173 were not rerun.

Only this document and the standalone verifier were created in the checkout. No product/shared file edits, new project, rebuild, host, SQL, provider, migration or Git write occurred. The existing runner created isolated verification output on D. All new calls used real service code with synthetic in-memory collaborators; no new production values were supplied.

## Parent Acceptance Handoff

The parent reports **C04 live 28/28 passed and the existing target verified** in the latest instruction. That supersedes the older handoff's C04 target-blocked status. This worker did not independently execute or inspect C04, and does not use its pass to claim vehicle SQL/concurrency acceptance.

For the next vehicle acceptance batch, include explicit all-None submit/review/replay and unchanged-quantity historical carry composition, including different original node thresholds, exact recorded consumption, independent reviews and both receipt paths. Retain ordinary positive/cutoff/revision cases. Assert blocked contracts for selected ReduceOutstation/mixed history, malformed or changed evidence, changed historical quantities, Once reuse and missing/invalid required transitions. Do not reuse build06 expected blocked-None or build08 expected blocked-carry outcomes as build09 expectations. The two coverage gaps noted above remain explicit, not implied passes.

Changed historical per-trip quantities still require a decision about their effect on retained consumed entitlement and future eligibility. The available policies do not answer that question. This matrix proposes no repricing, debt, refund, settlement, payment, salary deduction or invented approval authority. The parent-approved None and existing explicit carry composition gaps are implemented and offline verified. Only this matrix was edited during build09 verification; product and verifier sources remain frozen. No further product repair or scope expansion is proposed.
