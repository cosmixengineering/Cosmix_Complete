# HR closure continuation - 19 October 2026

## Scope

Close remaining supported HR backend behavior, then verify affected workflows.
The user requested ten agents; the runtime allows six concurrent agents, so
independent implementation and review assignments run in waves. Existing dirty
changes, applied migrations, private evidence and databases must be preserved.
No database migration, database repair, reset, alternate database or production
provider activation is authorized by this continuation.

The user delegated the remaining commute and consumed-benefit design decisions,
requiring manageable, dynamic rules. Implement explicit versioned configuration,
not active default rates or invented salary deductions. Missing configuration
must remain visible. A saved calculation is not payment or settlement evidence.

## Ownership

| Area | Owner | Boundary |
| --- | --- | --- |
| Vehicle correction and configurable daily commute | Bacon | Vehicle application files and focused standalone tests |
| Benefit correction and Admin resolution | Einstein | Benefit application files and focused tests |
| Recovery work source and duplicate protection | Franklin | Recovery application files and focused tests |
| Outstation policy and deployment | Sartre | Allowance/deployment application files and focused tests |
| HR report/export correctness | Sagan | Report application files and focused tests |
| Auth transport investigation | Dirac | Read-only review and optional diagnostic source, no host/SQL execution |
| Vehicle repository gap review | Lovelace | Read-only repository/service contract review |
| Durable export review | James | Independent report race and boundary review |
| Vehicle repository integration | Planck | Vehicle repository methods and standalone translation/integrity checks |
| Recovery and outstation review | Hypatia | Independent replay, history and evidence review |
| Shared integration, controller contracts, frozen build and final evidence | Parent | Shared files only after coordination |

## Acceptance boundaries

- Prior build/test results are historical evidence, not this continuation's run.
- A standalone service test with synthetic repositories is not authenticated SQL
  acceptance. Physical encrypted-file testing is not a SQL worker restart test.
- Existing C02 is auth-only: it cannot establish all HR business acceptance or
  authorize broader business writes. Its encryption and target guards stay intact.
- Real face/SMS, legal approval, deployment malware scanner, backup/restore,
  production load and separate mobile/client UAT remain separate gates unless
  actually exercised with the required approved configuration and access.
- No full-production acceptance claim until supported by executed evidence.

## Results

### Integrated build01 and regression results

Release `D:/CosmixVerification/hr-backend-20261019/hr-closure-build01` passed,
including a final incremental build after integration: 0 warnings, 0 errors.

| Suite | Executed checks passed |
| --- | ---: |
| Benefit workflow / configured waiver | 624 |
| Benefit settings / usage / evidence / quota | 72 / 84 / 75 / 19 |
| Recovery work claims | 115 |
| Vehicle dated attribution / monthly adjustments / entitlements | 77 / 45 / 46 |
| Vehicle repository integrity and query translation | 218 |
| Outstation valuation / deployment policy history | 71 / 48 |
| Durable HR exports / report boundary | 442 / 370 |
| C02 profile / HTTP boundary / composition | 116 / 143 / 11 |
| Generated MVC/OpenAPI contracts | 301 |
| C02 PowerShell helper checks | 32 |

Business-suite evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-25210c502a724557b3af5330d457c859`.
Auth/contract evidence:
`D:/CosmixVerification/part-c-parent-20261013/managed-700a2baa71fd465cb5318dc1e31c5d95`.
Standalone CS1701 dependency reference-unification warnings remain; the product
build itself has no warnings. Generated OpenAPI has 524 paths, not 524 accepted
workflows. The tests include actual encrypted CSV/XLSX/PDF files and independent
reader processes, but repository/transaction/auth-state adapters are synthetic.

Reproduced and fixed defects include expiry/scheduled-scope changes during report
IO, recovery receipt/allocation inconsistency, missing historical deployment
evidence, overlapping physical vehicle trips, ambiguous commute histories,
historical custody conflicts and future-dated benefit-release misuse.

### Actual bounded authentication acceptance

Following explicit user approval of the local-only transport exception, the
final preflight and execution used reviewed packet
`8FBCA32F230B9C27A18C7E22A0992550C4A414AF9C9F5A0F3691AC4C91B11C0E`.

- Exact target: `Software-PC1\LOCALDB#F9CAED57`, SQL process 29836,
  `Cosmix_Testing`, owner/operator `COSMIXRND\Huma.Khan`.
- Family: `D9C34627-18A2-41CF-A804-821E39516303`; original MDF/LDF paths retained.
- Transport: local named pipe, server-reported encryption FALSE, explicitly
  approved for this test only. Not production TLS acceptance.
- Real owned HTTPS host: `localhost:7481`, process 28168, now exited.
- Exactly two synthetic HR/Admin login/MFA flows; 24 HTTP requests; 16 PASS
  assertions: anonymous/pre-MFA denial, successful actual MFA, permissions,
  missing-CSRF rejection, logout and retained-cookie revocation for each account.
- No authentication or CSRF bypass and no real SMS/face calls.
- All 148 protected snapshots (including unapproved accounts) and seven existing
  C01 evidence files matched before/after; an additional read-only comparison
  AFTER host termination passed. Port 7481 has no listener; SQL remains running.
- Final database totals: 3 accounts, 25 migrations, 122 sessions, 128 challenges,
  128 deliveries, 45 rate buckets, 2,530 audit events. These are total counts,
  not a claim that this run created every record. No business fixture, migration,
  database reset, cleanup or permanent application configuration change ran.

Private evidence remains under
`C:/Users/Huma.Khan/AppData/Local/Cosmix/HrAcceptance/Huma20261019C02/results/`.
Credentials, cookies, keys and raw host logs are not included in this document.
The execution is completed: do not rerun or reset its private directory.

### Remaining limits

This closes bounded authentication acceptance, NOT all HR business HTTP/SQL
acceptance. The approval excluded employee, policy, benefit, vehicle and other
business writes. Their genuine concurrent SQL workflows and full client UAT
remain unexecuted. No face/SMS/legal or other production deployment gate is
marked accepted.

Admin waiver resolves the consumed benefit review obligation only when explicitly
configured. An existing leave recovery commitment is not also evidence of benefit
recovery. Different same-day vehicle policy/commute contexts, previously consumed
maintenance reconciliation and historical fuel repricing remain distinct limits.
No compensation or Finance settlement is fabricated.

See [client contract additions](HR-Closure-Client-Contracts.md) for the dynamic
commute setting, configured waiver API and corrected-trip first fuel claim.

### Corrected-trip first fuel claim: build02

Added `src/Cosmix.Application/HR/VehicleFuel/VehicleCorrectedFuel.cs` and extended
the existing service. A previously unclaimed corrected trip can submit a first
fuel claim using exact approved daily segments and configured dated rates.
Protected schema-3 evidence binds the correction, calculation and review.
Source changes around file/result/audit writes, submission/review replay,
duplicate submission, self-review and substituted fingerprints are rejected.

Historical approved fuel amounts remain unchanged. Existing old pending fuel
claims cannot be replaced under the one-claim-per-trip rule. Historical replay
now requires the retained claim and calculation evidence to remain available;
missing evidence fails closed. This additional availability dependency is
intentional and must be included in retention/deployment acceptance.

Fresh Release `D:/CosmixVerification/hr-backend-20261019/hr-closure-build02`:
0 warnings, 0 errors. Affected checks passed: dated attribution 86/86, monthly
adjustments 45/45, entitlements 46/46, repository integrity 218/218 and generated
OpenAPI contracts 301/301 (524 registered paths, not accepted workflows).
Exact frozen verification output:
`D:/CosmixVerification/part-c-parent-20261013/managed-0a43a9fdfe8144ffb5d095b4f5829d74`.
These are standalone synthetic repository tests, not new SQL acceptance.
Build01 and its real auth evidence remain retained and unmodified.

Cross-module checks rerun against the same frozen build02 also passed: benefit
workflow 624/624, recovery 115/115, outstation 71/71 and deployment policy history
48/48. Exact output:
`D:/CosmixVerification/part-c-parent-20261013/managed-d6865637f3ff4c138bb22017cca70a8f`.

Final independent-review follow-up added three test-only cases: mutate the source
during protected submission receipt reading, mutate it during review receipt
reading, and remove historical calculation evidence while keeping both successful
receipts intact. All three reject safely without changing approved claim amounts
or adding successful receipts/decisions. Restoring identical synthetic evidence
allows the original historical replay again. The final attribution suite is
**89/89 PASS** against unchanged build02:
`D:/CosmixVerification/part-c-parent-20261013/managed-dea2400044c649e9b1a0163885145241`.
No product code was changed after build02; these tests do not simulate real SQL
concurrency. Final whitespace check passed, with an existing migration snapshot
line-ending warning only; no migration file was edited by this closure work.

## Changed implementation groups for owner integration

All paths below are relative to this existing backend checkout. Preserve other
dirty changes; this is a task ownership list, not permission to discard files.

- `src/Cosmix.Application/HR/VehicleFuel/`: service, workflow, attribution,
  corrections, monthly adjustment policy/contracts and repository interface;
  `src/Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs` implements
  the added overlap/history checks.
- `src/Cosmix.Application/HR/Leaves/`: benefit contracts/settings/service,
  correction/resolution service and evidence, recovery work service/evidence;
  `src/Cosmix.Infrastructure/Repositories/HR/LeaveBenefitRepository.cs` retains
  resolution audit facts and receipts.
- `src/Cosmix.Api/Controllers/HR/LeaveBenefitsController.cs`: existing route
  family extended with the protected Admin correction-resolution action.
- `src/Cosmix.Application/HR/Allowances/DeploymentClaims.cs` and
  `DeploymentContracts.cs`: immutable prior-return evidence for resubmissions.
- `src/Cosmix.Application/HR/Reports/HrReportAuthorization.cs` and
  `HrReportExportService.cs`: expiry and date-sensitive authorization revalidation.
- `src/Cosmix.Api/Development/AuthOnlyTestingGuard.cs`,
  `AuthOnlyTestingDatabase.cs` and `LocalTesting.cs`: explicit bounded local-pipe
  test transport validation. Ordinary production configuration is not changed.
- `docs/hr-srs-remediation/`: focused standalone verification sources, C02 runner
  preparation/helper checks, readonly transport diagnostic and closure/contracts
  records. `docs/SRS-Module-Map.md` and
  `docs/attendance-completion/Attendance-UAT-Guide.md` distinguish results from
  still-unexecuted business UAT.

No applied migration, permanent connection string, real provider configuration,
Git history or separate client application was changed by this closure slice.
