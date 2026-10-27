# Historical Vehicle V2 - 26 October 2026

Latest continuation **passed at 15:16 PKT**:
[V2Continue01 execution ledger](Vehicle-V2-Continue01-Execution.md).
74 actual HTTP requests / 63 named checks: eight approved services, concurrent
pending approval, historical retries, dated correction and independent fuel
reconciliation passed. Original1000 / corrected1500 / difference+500 remains
NotRecorded, not paid. Preservation passed against the continuation baseline;
the original OTP limitation below remains. Host exited; no migrations.

## Initial V2 execution

Partial live acceptance, not complete HR acceptance. Approved synthetic legacy
approvals were fixture data, not historical HTTP actions, journeys or payments.

- Target: `Software-PC1\LOCALDB#8C03C2BC`, PID22112, `Cosmix_Testing` ONLINE.
- Owner: `COSMIXRND\Huma.Khan`; family `D9C34627-18A2-41CF-A804-821E39516303`.
- Existing MDF/LDF: `C:\Users\Huma.Khan\Cosmix_Testing.mdf` and
  `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`.
- 26 migrations unchanged. No migration, reset, provider, legal activation,
  business grant change or payment.
- Runtime: `D:/CosmixVerification/hr-backend-20261026/v2-final-build02/bin/Cosmix.Api/release`.
- Packet: `E3E350BC34181A31A8645EE869A26B175117E9D07D9DA66AEB848F27C822D7BD`.
- Evidence: `C:/Users/Huma.Khan/AppData/Local/Cosmix/HrAcceptance/Huma20261026V2/results/`.
- 29 HTTP requests. All three synthetic HR/Admin/SuperAdmin password, local MFA,
  pre-MFA denial and authenticated permission checks passed. No real SMS.
- Concurrent creates returned the same single vehicle. Twelve approved source
  rows persisted, followed by one pending maintenance request. Approval returned409;
  the remaining business chain did not run.
- Owned HTTPS7481 host8032 exited. Database remains running. Failure preceded
  logout: three new test sessions remain retained, not reported as revoked.
  No automatic retry, cleanup or reseeding.

## Observed changes

Read-only count reconciliation after host exit:

| Table/group | Added |
|---|---:|
| Vehicle | 1 |
| Policy / versions | 1 / 3 |
| Historical custody / trip / mileage / fuel | 1 each |
| Maintenance service | 1, pending |
| Approval requests / decisions | 3 / 2 |
| Idempotency receipts | 2 |
| Sessions / refresh tokens / OTP challenges / deliveries | 3 each |
| Audit events | 17 |

The two decisions are the authorized synthetic baseline. No maintenance,
trip-correction or fuel-reconciliation approval succeeded. Counts alone do not
prove prior-row preservation; retained snapshot reconciliation is separate.

## Defect and fix

Maintenance compared serialized JSON text. SQL preserves decimal values but
returns their declared scale: odometer1200 became1200.000 and amount50 became50.00.
Unchanged eligibility was therefore rejected.

`InspectVehicleV2ReadOnly.cs` read the actual row and authenticated encrypted
snapshot, then recalculated with the real SQL repository and saves forbidden:
`textEqual=False`, `structuralEqual=True`. Evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-cac8d3025dc849bebd4fa1b9cc5ec84b/`.

Three comparisons now use complete structural numeric equality. Fields, array
contents, fingerprints and genuine changes remain checked; stored evidence is
not rewritten. Eleven regressions cover scale, changed-value denial, retries
and preserved evidence. RED reproduced7failures; GREEN426/426 vehicle checks
passed against the rebuilt Application verification runtime. That mixed runtime
was not hosted; full current-build results are recorded separately below.

The runner also hit a final snapshot-query error after409. This does not prove
preservation or undo preceding successful writes. Its fix and reconciliation
must be verified before continuation.

## Final verification of the fix

- Full current Release build: zero warnings/errors, runtime
  `D:/CosmixVerification/hr-backend-20261026/v2-fix-build03/bin/Cosmix.Api/release`.
- Against that full runtime: dated attribution184, adjustments45, entitlements46,
  remaining boundaries109 and fuel reconciliation42: **426/426 passed**.
  Artifacts: `D:/CosmixVerification/part-c-parent-20261013/managed-93a591453e99480bad1a932787dae678/`
  and `D:/CosmixVerification/part-c-parent-20261013/managed-457cda42015f4ffeb52959bf67ee8688/`.
  The first invocation of the last two suites lacked their required fixture
  environment binding; rerunning with the verified fixture hash passed.
- Runner verification: **185 offline checks passed**. The printed preservation
  warning is an intentional failure-path test, not a new live execution.
- Actual retained SQL/private evidence, with saves prohibited, passed the fixed
  production `SameServiceEligibility` comparison in build03. Artifact:
  `D:/CosmixVerification/part-c-parent-20261013/managed-4e10cf02cfc440f1a01d930f06bfdf3d/`.
  This is not an authenticated approval replay. The standalone diagnostic has
  CS1701 reference-unification warnings; the application Release build has none.
- Runner snapshot failure fixed: empty membership JSON becomes `[]`; primary
  failures survive secondary reconciliation errors; ProblemDetails title is
  retained and future row-membership baselines are recorded. Existing private
  results have not been rewritten.

## Preservation and limitations

Read-only reconciliation artifact:
`D:/CosmixVerification/hr-vehicle-v2-readonly-reconciliation-20261026-01.json`,
SHA256 `CD5D3A9E9F16033D13EB8031B8012115258D67D1028EFBAB390369FC047C537D`.
161/162 prior table hashes match; `auth.MobileOtpChallenges` differs. One old
synthetic SuperAdmin WebLogin challenge, issued October22, was revoked at
`2026-10-26T08:31:47.0684974Z`, consistent with normal login invalidation.
The original individual row values were not retained, so equality of all other
fields in that table is unknown. Do not claim full prior-row preservation or
exclude the entire table to force a pass. All166 prior evidence files and allfive
V2 result files match their retained hashes. No cleanup or repair was performed.

## Changed modules

Production: `VehicleConsumedMaintenance.cs` and `VehicleFuelService.cs` compare
complete evidence structurally; `LeaveRecoveryWorkEvidence.cs` rejects orphaned
decision facts. Verification includes dated vehicle regressions, recovery and
benefit checks, the Development-only V2 host/guards/fixture/boundary, its runner,
read-only diagnosis and documentation. No migrations were added or applied by
this work. Other chat changes were preserved and included in the full build.

## Continuation and remaining acceptance

V2Continue01 reused the vehicle, twelve sources and pending service with no
reset or duplicate seed. Approval, seven remaining services, trip correction,
fuel reconciliation, denial/retry checks and logout now passed live. This is
acceptance of the executed bounded scenario, not every vehicle variation.

Benefit/recovery positive HTTP/SQL acceptance remains separate. Their standalone
1428+157+46 checks are not business SQL acceptance. Real face/device, SMS, legal
activation and IIS deployment remain outside this run.
