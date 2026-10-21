# HR six-gap continuation - 20 October 2026

## Scope

User requested backend completion before frontend acceptance. No migration,
database reset or real face/OTP/legal activation. Existing private acceptance
records and unrelated changes remain preserved.

This record separates source fixes, standalone verification, read-only SQL and
authenticated write acceptance. A passing build is not production acceptance.

## Ownership and current work

| Area | Owner | Work |
| --- | --- | --- |
| Benefit/recovery | Sagan | Approved delegated-benefit authority, current-role checks and regression coverage |
| Vehicle history | Einstein | Engine-overhaul and dated-attribution/transition chain coverage; evidenced fixes only |
| Outstation | Cicero | Configured-policy lifecycle, retries and effective-date correctness |
| Reports | Lagrange | Export scope, download and worker integrity |
| Shared integration/SQL | Parent | Preserve shared files, integrate build, read-only SQL and acceptance boundaries |

No agent is authorized to perform SQL/HTTP writes, start a host, apply a migration
or change permissions. Shared authorization/DI/database files remain parent-owned.

## Approved design decision

The user explicitly approved genuine active fixed Admin authority plus a separate
explicit All-scoped `attendance.leaves.benefits.review` custom-role grant on the
same account. SuperAdmin can assign that custom role through the existing access
workflow, which protects administrator targets. Fixed roles remain immutable.
Current MFA, role revalidation, self-review denial and audit must remain.
This approves implementation, not silently changing a live account's grants.

## Actual read-only SQL

Target rechecked:

- `Software-PC1\LOCALDB#9F3001F7`, process 25768.
- `Cosmix_Testing`, ONLINE, owner/operator `COSMIXRND\Huma.Khan`.
- Family `D9C34627-18A2-41CF-A804-821E39516303`.
- Existing `C:\Users\Huma.Khan\Cosmix_Testing.mdf` and `_log.ldf`.
- 25 applied migrations. No migration or database mutation performed.

`VerifyHrReportsReadOnlySql20261020.cs` executed the actual 14 report repository
methods against SQL, with exact synthetic HR selection and empty selection.
**28/28 checks passed; 39 SELECT commands.** No authentication or permission grant
was simulated as a live verdict. Missing source rows stayed empty; missing daily
summary was not invented as attendance. Output exposed only method/count results.

Release input: `D:/CosmixVerification/hr-backend-20261020/hr-vehicle-v1-build09/bin/Cosmix.Api/release`.
Passing run:
`D:/CosmixVerification/part-c-parent-20261013/managed-577096c5ce314fe3b63cce7985162818/`.
First attempt could not load the native SQL client dependency before connecting;
the rerun supplied the existing build's `runtimes/win-x64/native` directory only
in the executing process PATH, restored afterward. No permanent configuration
changed. Two standalone CS1701 reference-unification warnings were emitted.

## Acceptance limits

The earlier C04 passed its department/authentication boundary. V1 retained its
27 passing HTTP checks and separately verified vehicle/custody/private evidence,
but did not reach logout. It is not rerun or relabelled here.

Benefit/recovery, policy-backed vehicle/outstation and report-worker write tests
need explicit bounded synthetic fixtures, current grants and private storage.
Existing C04/V1 approvals do not authorize silently modifying roles/policies or
consuming unrelated outbox work. Frontend production acceptance is not yet claimed.

## Later integrated fixes and verification

The user separately approved R1. Its live result below supersedes the unexecuted
report-worker slice above, not the other fixture/permission boundaries.

### Source fixes

- Benefit award, correction and resolution accept an explicit delegated review
  grant only alongside independently verified genuine fixed Admin membership.
  Fixed Admin expiry/revocation is checked against fresh time after awaited role
  reads, including before commit and receipt replay. No permission was granted
  in SQL.
- Vehicle maintenance now validates the full retained chain even for a single
  original ancestor. A competing root can no longer escape the chain-inventory
  check. Engine-overhaul and dated monthly/transition histories with unchanged
  per-trip quantities have positive/negative coverage; changed quantities remain
  explicit reconciliation conflicts.
- Outstation exact retry retains the already-approved claim rather than failing
  merely because review changed its state. Changed payload/source remains denied;
  new keys do not bypass existing-claim rules. Rates/counting remain configured.
- Durable report export rechecks retained permission-grant effective dates after
  the awaited scope read. Expiry during that IO cannot publish an export.

### Frozen build11 results

Release build: **zero warnings/errors**.
`D:/CosmixVerification/hr-backend-20261020/hr-six-gap-build11`.

| Standalone suite | Passed |
| --- | ---: |
| Benefit workflow, delegated Admin authority and timing boundaries | 1387/1387 |
| Recovery work claims | 132/132 |
| Recovery fulfilment | 38/38 |
| Vehicle dated attribution | 173/173 |
| Fuel reconciliation | 42/42 |
| Historical vehicle remaining boundaries | 97/97 |
| Outstation policy/replay boundaries | 52/52 |
| Durable reports, actual encrypted-file round trips | 449/449 |
| Report result repository expressions | 204/204 |
| XLSX ZIP/XML parsing | 81/81 |
| Generated MVC/OpenAPI contracts | 328/328; 529 paths |
| R1 exact-job adapter | 89/89 |
| R1 host/profile boundaries | 126/126 |
| R1 PowerShell runner route/array/safety checks | 66/66 |

These standalone assertions use synthetic repositories/identities where stated;
they are not relabelled as live SQL acceptance. The vehicle reconciliation suites
initially stopped because their required frozen dated-fixture path was omitted.
They subsequently passed using the build11 dated-fixture DLL and verified SHA256
`70DBA15CB4EA6F891A302EC5387D2A9F61A92358F71E52C5CE54FFB5D8342A38`.

Retained integrated suite directories:

- `D:/CosmixVerification/part-c-parent-20261013/managed-78eb0d81607146d6b907074d2801717b/`
  (benefit/recovery/dated attribution passed; first fuel harness invocation stopped).
- `D:/CosmixVerification/part-c-parent-20261013/managed-6ad6d2d457bd4d0a87a387c4e1643e8a/`
  (fuel, historical vehicle, outstation, reports and contracts passed).
- `D:/CosmixVerification/hr-backend-20261020/report-target-offline-1c8c6a18cab44ea2b971b2930de37343/`
  and `report-target-offline-6d9b2bd92a49416ebd1d9f1bcb79a7f9/`.

Build10 read-only SQL additionally confirmed 933 mapped columns across 77
HR/support tables and passed the same 28 report queries with 39 EF SELECTs.
This verifies column presence, not every type/index/write contract.
Evidence: `D:/CosmixVerification/part-c-parent-20261013/managed-95fba7bef777440592c14fb0470d0bb3/`.

Some standalone compiler runs emitted existing CS1701 package-reference
unification warnings; the full Release build was clean.

### Actual R1 acceptance

**21/21 checks, 20 real HTTP requests passed**, including current HR login/local
MFA, exact employee preview, CSV/XLSX, one durable PDF, same-key replay, authenticated
download, SQL/private-file integrity and logout/revocation. After host exit,
155 protected SQL snapshots and 48 previous acceptance files were unchanged.
No migration. Global queues were not dispatched.

[Exact target, counts, evidence and limits](HR-Reports-R1-20261020.md).

### What is still not accepted

- Live benefit/recovery awards, consumption/correction and fulfilment contention:
  require bounded synthetic policy/KPI/leave fixtures and explicitly provisioned
  current review grants. The existing catalog lacks the new benefit permission.
- Historical vehicle/fuel reconciliation and outstation business SQL:
  need policy/trip/maintenance/deployment fixtures and missing policy/deployment
  permissions. Existing V1 only exercised vehicle/custody.
- Changed historical maintenance quantities remain blocked without an approved
  reconciliation valuation rule; no amount/threshold is invented.
- Full report-type fixtures, global worker/restart/notification-delivery tests
  and frontend/client UAT have not run.
- Real face, external OTP and legal activation remain skipped as instructed.

Full HR production acceptance is **not achieved** by these narrower passing runs.
The next backend acceptance batch must explicitly enumerate synthetic provisioning
and business fixtures; C04/V1/R1 did not authorize silent role/policy changes.

### Changed module/file handoff

Product changes: `HR/Leaves/LeaveBenefitService.cs`,
`LeaveBenefitCorrectionService.cs`, `LeaveBenefitResolutionService.cs`;
`HR/VehicleFuel/VehicleFuelWorkflow.cs`; `HR/Allowances/DeploymentClaims.cs`;
`HR/Reports/HrReportAuthorization.cs`.

R1-only integration: five `Cosmix.Api/Development/HrReportTesting*.cs` files
(Guard, Boundary, Database, Host, Dispatcher), plus profile selection in
`LocalTesting.cs`, `HrBusinessTestingGuard.cs` and `Program.cs`.

Standalone verifiers/runner and this evidence are in `docs/hr-srs-remediation`.
Unrelated face changes and existing dirty work were preserved; no Git
synchronization, migrations, permanent configuration edits or frontend changes.
