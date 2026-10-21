# HR Benefits, Recovery and Vehicle Continuation

## Approved Boundary

This continuation implements HR-owned recovery fulfilment, extra/KPI leave
benefits and vehicle adjustments. The owner confirmed Admin review for extra/KPI
benefits and policy-selected encashment eligibility. Rates, thresholds, units and
period rules remain explicit published configuration, not SRS-example defaults.
No Accounts settlement, salary payment or production legal activation is added.
No database migration may be applied. Existing databases, configuration and
private production storage are not used by the standalone checks.

## Implementation Design

- Reuse the existing authorization transaction, immutable policy versions,
  approval/audit records, append-only leave ledger and protected evidence store.
  Do not introduce another employee, payroll or approval system.
- Extra/KPI awards use authoritative finalized KPI, current source and dispute
  verification. A prepared score is not an earned benefit. The natural duplicate
  key is employee plus source KPI year, independent of policy/KPI revision.
- Award minutes, minimum service, exact KPI threshold, target-year offset, award
  window, leave kind and encashment eligibility are explicit v4 leave settings.
  The Admin needs a dedicated assigned permission; configuration does not grant it.
- An award appends a positive entitlement and protected evidence binding the
  policy, employment, exact KPI, actor, period and units. It is available from its
  actual recorded date, never as a fabricated past-period grant.
- Earned credits are separate from recorded usage. Their original day-minute
  denominators survive later policy changes. Other leave-kind credits cannot
  fund unrelated leave through the shared annual ceiling.
- Encashment shares the ordinary consumption/reservation ledger. Only explicitly
  cash-eligible benefits enter its capacity. Without per-award spend allocation,
  cash capacity conservatively counts all net usage against eligible capacity;
  it does not guess which prior leave consumed a leave-only award.
- An unused award may be reversed by a new immutable entry with the exact
  expected evidence fingerprint. Any consumption/reservation/encashment history
  in its year prevents this shortcut. Consumed-credit adjustments need an explicit
  allocation/adjustment design; a positive aggregate balance proves nothing.
- Recovery claims must identify actual retained work intervals, not a promise or
  client-provided duration. The same interval cannot also produce OT or make-up
  credit, regardless of creation order. Pending policy choices stay pending.
- Vehicle adjustments retain approved mileage, dated commute evidence, selected
  policy and revision history. An HR adjustment is not insurer settlement or an
  automatic salary deduction.

## Verification Plan

1. Observe focused behavioral regressions against the frozen previous Release.
2. Build the existing four-project solution to a fresh D-drive artifacts path.
3. Run affected standalone service, evidence, policy and exact-unit checks.
4. Re-run existing leave, encashment, recovery, OT/work-treatment and vehicle
   regressions; verify DI/OpenAPI and the EF model without starting an API host
   or opening a SQL connection.
5. Record actual results and residual limits below. Compilation and generated
   routes are not authenticated SQL or production acceptance.

## Executed Verification

The initial quota regression reproduced missing earned-credit capacity and
balance display against `hr-scope-build`. One test fixture initially used a
different selected leave kind without updating its type subtotal; that fixture
was corrected, without changing the production validation.

First integration build (`hr-benefits-build01`) stopped at an in-progress
recovery scope-field mismatch. It is not reported as a passing build.

Build02 and build03 exposed additional regression cases. Evidence validation
needed explicit actor/policy identities; adding an empty `Credits` property had
also changed historical leave/KPI hashes. Credits are now nullable and omitted
when absent, and retained hashes are regression-tested. The final KPI reader
rejects malformed pending evidence before evaluating period overlap.

Review found authority could expire during private receipt IO and vehicle
incident lists could show the original outcome after an approved amendment.
Fresh authority checks now surround replay callbacks and follow write callbacks;
vehicle lists use the effective retained review. Build04 correctly denied expired
benefit sessions but returned 403 instead of the required 401 in twelve cases.
That error contract was corrected without relaxing a denial assertion.

The follow-up read-expiry regression failed 50/54 against build05: successful
read callbacks could outlive their session. Build06 adds the final current-session
and retained-scope check before read audit, and rechecks the exact Admin role
after recovery-detail evidence IO. The first build06 batch then stopped on an
ESS expectation: the earlier authoritative guard correctly returned
`403 access_denied`, rather than the outer ESS guard's `403 forbidden`. The
updated regression requires the exact earlier error and verifies that no
successful-read audit is recorded; it does not accept a successful response.

Final frozen Release:
`D:\CosmixVerification\hr-backend-20261019\hr-benefits-build06\bin\Cosmix.Api\release`.
Build **passed, zero warnings and zero errors**, exit 0. Build log:
`D:\CosmixVerification\hr-backend-20261019\hr-benefits-build06.log`.
No frozen build was overwritten. The standalone OpenAPI compiler reports the
existing CS1701 framework-reference unification warnings; those are not product
build warnings or suppressed source errors.

| Final-build suite | Actual result |
| --- | --- |
| Benefit quota | 15/15 |
| Benefit retained evidence / legacy compatibility | 75/75 |
| Benefit settings | 59/59 |
| Benefit finalized KPI and pending/dispute evidence | 79/79 |
| Benefit award, revoke, authorization, notifications and replay workflow | 332/332 |
| Attendance operation expiry inside read/final/replay callbacks | 54/54 |
| Recovery fulfilment policy | 46/46 |
| Actual recovery work claims, progress, notifications and exclusivity | 79/79 |
| Existing leaves | 67/67 |
| Existing recovery replay | 51/51 |
| Recovery reservation integrity | 38/38 |
| Existing encashment workflow | 30/30 |
| Encashment valuation and benefit integration | 88/88 |
| ESS leave consistency | 38/38 |
| Self leave reads / balances | 43/43 |
| Leave EF query translation | 36/36; no SQL execution |
| Leave model / append-only guards | 15/15; no SQL connection |
| Actual DI composition | 54/54; no host started |
| Generated MVC/OpenAPI contracts | 256/256; 511 paths, not live HTTP |
| Vehicle adjustments | 45/45 scenarios |
| Vehicle entitlement regressions | 46/46 |
| Vehicle repository / query regressions | 53/53; no SQL connection |
| Vehicle policy publication | 71/71 |
| EF compiled model inspection | 25 compiled migrations, zero model differences, zero connection attempts |
| Work-treatment C03/C06 service regressions | 53/53 |
| Overtime service | 156/156 |
| HR overtime report | 12/12 |
| Historical scope evaluation | 23/23 |
| Capture authority projection | 14/14 |
| Capture service | 147/147 |
| Correction service | 196/196 |
| KPI workflow | 22/22 |
| Month-lock guards | 85/85 |
| Daily attendance calculation / source service | 193/193 |

The final 24-suite primary/vehicle run exited 0 at:
`D:\CosmixVerification\part-c-parent-20261013\managed-fa0e8bddaeb341d2a1239e699e0d8d8c`.
The expanded 46-case recovery-policy suite is included in this run; it replaces
the earlier two-check smoke test. The nine shared regressions passed 708 checks,
exit 0, against the same build06 at:
`D:\CosmixVerification\part-c-parent-20261013\managed-5471b2f1158d4aa19c303a3795ba2d56`.
The earlier stopped build06 batch is retained at:
`D:\CosmixVerification\part-c-parent-20261013\managed-9327ac1031e7409a8798a1c384d2ed24`.
These directories retain exact copied sources, frozen DLL hashes, compiler
responses and logs. Distinguish scenario counts from assertion counts.

The affected daily-calculation fixture also passed 193 checks, exit 0, on the
same build06 at:
`D:\CosmixVerification\part-c-parent-20261013\managed-6c599973cc534515a69f9d171b417362`.
In total, 34 distinct standalone suites completed successfully on build06.
This includes behavioral suites, composition/contracts and offline EF inspection;
it is not 34 live integration tests. No product source was newer than the final
assembly at the final source-drift check. `git diff --check` passed; its existing
snapshot CRLF notice was not addressed by rewriting unrelated migration files.

The latest compiled migration remains
`20261015144424_AddFinancialAdjustmentsAndOutstationDeployments`. This is an
assembly/model observation, **not** this laptop's database migration history.
No migration was generated or applied by this slice; no database was connected,
created, migrated, seeded or reset. Permanent configuration, real accounts,
providers and existing private keys were not used or modified.

## Remaining Boundaries

- Standalone workflows use real application services with synthetic repository,
  authorization, transaction and private-storage collaborators. They do not
  prove SQL rollback, actual HTTP identity/CSRF, concurrent connections, real
  notification delivery or an application's restart against persisted HR data.
- Company policies and permission grants must be explicitly configured. No
  entitlement, monetary amount, KPI threshold or production authority was seeded.
- A consumed benefit cannot be revoked using the unused-credit shortcut. An
  auditable per-award spend/adjustment design remains required for that case.
  Encashment capacity remains deliberately conservative without such allocation.
- Recovery supports explicit exact-minute outside-scheduled-duty fulfilment.
  Changed/cancelled parents retain allocations and block unsupported adjustments;
  no financial refund or recovery is guessed.
- Vehicle monthly adjustment requires one applicable policy and unambiguous
  single-day trip attribution. Multiple eligible assignments on one day,
  cross-midnight/multi-day monthly attribution, mid-month policy changes,
  retroactive rules for legacy versions and immutable open-ended policy succession
  remain unsupported. See the vehicle handoff for exact pending codes.
- Accounts posting/payment, legal activation and real face/OTP acceptance are
  outside this HR continuation. Separate web/mobile clients were not implemented.

**Full HR/Attendance production acceptance is not claimed.** The implemented
configured flows are ready for a later permitted authenticated integration run;
this report does not authorize database writes.

## Changed-File Handoff

New benefit application files: `LeaveBenefitContracts.cs`,
`LeaveBenefitSettings.cs`, `LeaveBenefitKpiReader.cs`, `LeaveBenefitEvidence.cs`,
`LeaveBenefitService.cs`, under `src/Cosmix.Application/HR/Leaves/`.
Persistence/API: `LeaveBenefitRepository.cs`, `LeaveBenefitsController.cs` in
the existing Infrastructure HR repository and API HR controller directories.

New recovery application files: `LeaveRecoveryFulfilmentSettings.cs`,
`LeaveRecoveryWorkContracts.cs`, `LeaveRecoveryWorkParent.cs`,
`LeaveRecoveryWorkSources.cs`, `LeaveRecoveryWorkEvidence.cs`,
`LeaveRecoveryWorkService.cs`. Persistence/API: `LeaveRecoveryWorkRepository.cs`
and `LeaveRecoveryWorkController.cs` in the same owning module directories.

Shared integration changes: application/infrastructure dependency injection,
permission catalogue, `LeaveContracts.cs`, `LeaveSelfReadContracts.cs`,
`LeavePolicySettings.cs`, `LeaveTreatmentRules.cs`, `LeaveRepository.cs`,
`LeaveEncashmentValuation.cs`, `AttendanceOperation.cs`, work-credit contracts/
reader and OT service. These preserve historical schemas and use existing
transactions, ledgers, approval records and audit entities.

Exact vehicle inventory and verification are in
[Vehicle-Adjustments-20261019.md](Vehicle-Adjustments-20261019.md).
Client handoff and acceptance cases are in
[HR-Benefits-Recovery-Client-Contract.md](HR-Benefits-Recovery-Client-Contract.md).
New standalone tests are the `VerifyLeaveBenefit*`,
`VerifyAttendanceOperationAuthorityTime`, `VerifyRecoveryWork*` and
`VerifyVehicleAdjustments` sources. Existing affected fixtures, composition and
OpenAPI checks were extended, not replaced with new test projects. No Git write,
branch, commit, worktree, synchronization or PR was made.
