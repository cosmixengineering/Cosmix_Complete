# HR backend verification - 26 October 2026

Latest V2Continue01 status and exact evidence:
[continuation execution ledger](Vehicle-V2-Continue01-Execution.md).
**Passed at15:16PKT:** 74actualHTTP requests /63named checks, eight maintenance
approvals, historical replay, dated trip correction and independent fuel
reconciliation. Preservation passed; test host exited; no migration/payment.
This closes that vehicle scenario, not benefit/recovery or full HR acceptance.

Latest continuation: [V2 live vehicle result](HR-Vehicle-V2-Live-20261026.md).
Actual authenticated writes found a SQL decimal-scale eligibility defect;
the narrow fix and partial persisted state are documented there. Not full HR acceptance.

## Decision

**Latest live execution:** Auth01 passed16/16checks in22HTTP requests; Read01
passed32/32checks in38HTTP requests, including16business read/denial assertions.
Both preserved156protected SQL snapshots and exited their hosts. This supersedes
the earlier preparation-only statement for those two batches, not positive
benefit/recovery/historical-vehicle acceptance. See
[actual live results](HR-Live-API-Checks-20261026.md).

**Later 26 October update:** the intended SSMS-context database is now verified
ONLINE on explicit pipe `LOCALDB#48AA6F9F`, PID 26564, with the original family,
owner and MDF/LDF paths. It has 26 applied migrations matching current source.
This supersedes only the unavailable-target finding below. No migration was
applied. See [current business-test manifest](Business-SQL-Manifest-20261026.md).

Fresh Release build and all nine selected standalone regression suites passed.
This is not full HR production acceptance. No new product-code defect was
demonstrated by these checks; no unrelated implementation was rewritten.
The client contract's obsolete same-assignment requirement was corrected to
match the approved, implemented fixed Admin plus delegated permission design.

## Fresh verification

Command, from the existing backend checkout:

```powershell
dotnet build Cosmix.Backend.sln -c Release --artifacts-path D:/CosmixVerification/hr-backend-20261026/hr-final-build01 -p:NuGetAudit=false -p:RestoreLockedMode=true -m:1 -v:minimal
```

Result: exit 0, **0 warnings, 0 errors**. Frozen runtime:
`D:/CosmixVerification/hr-backend-20261026/hr-final-build01/bin/Cosmix.Api/release`.

`docs/attendance-gap-c/VerifyManaged.ps1` ran the following existing standalone
sources against that frozen output and finished with exit 0:

| Source in docs/hr-srs-remediation | Checks passed |
| --- | ---: |
| VerifyRecoveryWorkClaims.cs | 132/132 |
| VerifyRecoveryFulfilment.cs | 38/38 |
| VerifyLeaveBenefitWorkflow.cs | 1,387/1,387 |
| VerifyLeaveBenefitKpi.cs | 79/79 |
| VerifyVehicleRemainingBoundaries.cs | 109/109 |
| VerifyVehicleFuelReconciliation.cs | 42/42 |
| VerifyVehicleBenefitQueryTranslation20261020.cs | 60/60 |
| VerifyOutstationAllowances.cs | 71/71 |
| VerifyRemediationContracts.cs | 328/328 |
| Total | **2,246/2,246** |

Exact per-suite logs, staged binaries and runner evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-541bb0a9b936478dac38b100b5e23aa3/`.

Vehicle fixture binding was process-only, restored afterwards, and validated
against SHA256
`D604753ED9E704AFA615BE3EA00C74EE68813FD01E4D4DF68E563A70E5035668`.
Its retained assembly path was
`D:/CosmixVerification/part-c-parent-20261013/managed-77fd76897d4a46399842191a348ff485/VerifyVehicleDatedAttribution.dll`.

The query suite exercised 73 repository expressions, 30 ToQueryString checks
and 46 intercepted pre-open scalar compilations; **zero SQL connections opened**.
Generated MVC/OpenAPI checks covered 550 paths, without starting an HTTP host.
Service suites use synthetic collaborators. These counts are not independent
business requirements and do not establish live SQL, race or device acceptance.

A separate read-only source review found no actionable defect in the inspected
benefit correction/resolution and recovery source/approval paths. That review
does not replace persistence or concurrent-request testing.

## Existing SQL target: unavailable in this process context

The existing `(localdb)\MSSQLLocalDB` was initially stopped. Only the existing
instance was started, under the prior permission to do so, then inspected:

- Version: 17.0.4025.3; owner: `COSMIXRND\Huma.Khan`.
- State: Running; observed pipe: `np:\\.\pipe\LOCALDB#CDD46C5D\tsql\query`.
- Windows-authenticated SQLCMD connection to `master` succeeded.
- Read-only `sys.databases WHERE name=N'Cosmix_Testing'` returned **zero rows**.

This does not establish that the database was deleted. It establishes that the
requested catalog is not registered on the instance visible to this connection.
Database work stopped at that result, as instructed. No replacement target,
attach, detach, reset, restore, migration, schema change or business/auth fixture
write was performed. The instance is left running; no API host was started.

To resume live acceptance, the existing `Cosmix_Testing` must be accessible in
the intended Windows/SQL context and its original owner/family/files/schema must
match the approved target. Old pipe bindings and expired temporary permissions
cannot be reused without revalidation. Do not create a substitute or renew grants
silently. This report does not expand any earlier bounded test approval.

## Remaining acceptance and implementation boundaries

1. Benefit award, consumption, correction/resolution and actual recovery-work
   fulfilment still need authenticated HTTP/SQL, encrypted-storage and concurrent
   request acceptance using explicitly approved synthetic prerequisites.
2. Historical trip/fuel/maintenance adjustment chains and additional outstation
   home-break, partial-day and effective-policy scenarios need live acceptance.
   Offline passes do not close these cases. Unsupported changed consumed-distance
   or policy-transition cases stay blocked rather than inventing a valuation.
3. Real mobile enrollment, liveness, matching, spoof rejection and session/device
   failure scenarios are not accepted: the user confirmed app/device testing is
   not ready. No real face calls or activation changes were made this turn.
4. Real SMS delivery and legal activation remain outside this run. No provider,
   legal approval, financial posting or payment was fabricated or enabled.
5. IIS/environment-specific storage identity, TLS, restart, backup/restore,
   monitoring and deployment acceptance remain unexecuted.

Earlier bounded live evidence remains valid only for its recorded scope; see
[20 October pilot report](HR-Mobile-Pilot-Readiness-20261020.md) and
[R1 report acceptance](HR-Reports-R1-20261020.md). Those runs were not repeated
on 26 October. No React/React Native implementation or UI acceptance is claimed.

## Changed files in this continuation

- `docs/hr-srs-remediation/HR-Benefits-Recovery-Client-Contract.md`
- `docs/hr-srs-remediation/HR-Backend-Verification-20261026.md`
- `docs/SRS-Module-Map.md`

Existing concurrent source edits and migrations were preserved. No Git
synchronization, commit, migration application, database repair or cleanup.

## Subsequent auth preparation and user redirection

The user approved a bounded current-target auth-only run, then clarified that
the required remaining work is benefit/recovery and historical vehicle business
testing. No auth-only execution or successful business-flow claim was made.
The fresh auth profile and runner were prepared without changing the old C02
runner or retained evidence:

- `AuthOnlyTestingGuard` retains C02 bindings and adds separately selected
  `HumaHrAcceptance20261026Auth01`, pinned to PID26564/pipe48AA6F9F/26 migrations.
- The auth-only host excludes the newly registered PunchRetentionWorker, keeping
  the existing OTP/data-protection worker allowlist. Ordinary hosts are unchanged.
- `LocalTesting.CreateBuilder` accepts the new explicit profile; shared auth
  routes, MFA, CSRF, request ceiling and business-route denial remain unchanged.
- New runner/private target: `Invoke-HrAuthOnly20261026.ps1` /
  `C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261026Auth01\`.
  Private target was absent; no settings/keys were created or host started.

New profile regression first failed against the old frozen build, as expected.
Updated Release build `D:/CosmixVerification/hr-backend-20261026/auth01-build01`
passed with zero warnings/errors. New and old guard tests passed 116 each;
new and old composition tests passed11 each; boundary tests passed143;
new runner helper tests passed32: **429 standalone checks total**.
Logs: `D:/CosmixVerification/part-c-parent-20261013/managed-496cf8a95fa84715abc2034f8d97f9d8/`.
These are preparation checks, not executed authentication or business acceptance.

Read-only preflight passed target, identity, schema, private-source ACL, account,
port and protected snapshot checks: 156 protected snapshots and seven retained
C01 files. That initial inspect used the prior frozen runtime and is not an
execution packet for the updated build; never reuse its hash to launch the host.

The user then approved **preparation of the exact business fixture manifest**,
not fixture execution. SQL inventory confirmed no leave/recovery/trip/fuel/service
history and expired P1 grants. No role renewal or synthetic history insertion was
performed. An initial metadata SELECT had a reserved-alias syntax error; the
corrected SELECT succeeded without writes. Prior actual test evidence is intact.
