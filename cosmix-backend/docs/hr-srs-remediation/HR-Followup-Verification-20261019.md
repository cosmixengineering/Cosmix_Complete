# HR Correction and Management Follow-up - 19 October 2026

For the later same-day vehicle/export/site changes, final build02 and SQL
connectivity findings, see [the continuation ledger](HR-Production-Continuation-20261019.md).
The no-private-write statement below describes this earlier checkpoint, not the
later C01 startup attempt that retained private artifacts but sent zero requests.

## Scope and Acceptance

This continuation implements the owner's approved consumed-benefit correction,
dated vehicle attribution and related HR management repairs. It does not extend
HR into Accounts posting, settlement, payment release or automatic salary cuts.
Company rules remain explicit and effective-dated. Missing approved rules remain
unavailable or pending, never an invented rate, score, quota or zero amount.

**Full HR production acceptance is not established.** The source and offline
results below must not be described as authenticated SQL/concurrent HTTP UAT.
No database migration, SQL fixture, account, key, permanent environment change,
real face/SMS call or Git synchronization was performed in this continuation.
The user's instruction not to apply migrations remains in force.

## Implemented Backend

### Benefit correction

- Admin reads retained award and usage fingerprints, explicitly attributes
  consumed minutes to exact source records and records the correction reason.
- Unused credit is removed. Used credit becomes an immutable Admin-review
  obligation, without recovering salary or reducing future leave.
- Protection applies only against exact still-consumed evidence. Exact source
  cancellation removes that protection; it cannot turn into spendable credit.
- Original awards, utilization, policy units, encashment and audit evidence stay
  immutable. No FIFO or base-quota-first spending assumption is introduced.
- Duplicate allocation, stale sources, outstanding reservations, locked periods,
  self review and revoked roles/sessions are rejected. Completed but unlocked
  years support correction because encashment occurs after the year ends.
- A valid fully released utilization history no longer blocks unused revocation.
  A merely net-zero ledger is not sufficient proof of release.

See [the exact API and client contract](Benefit-Correction-Contract.md).
`pending_admin_review` for consumed credit is the business state approved by the
owner, not a claim that salary recovery has been implemented. Resolving that
obligation through recovery requires a separately approved policy/workflow.

### Vehicle attribution and policy changes

- Multi-day trips use explicit contiguous dated segments, exact totals and
  independently reviewed evidence. No guessed proportional split is used.
- Month calculations support explicitly configured policy partitions and retain
  source versions. Dated fuel and maintenance consume the retained segments.
- Explicit future policy succession preserves predecessor records and historical
  selection. Old settings are not silently overwritten.
- A trip ending at local midnight belongs to its preceding day/month, not both.
- Attribution approval uses a request-ID-bound mileage key distinct from ordinary
  trip approval. Reusing a client key across those operations cannot collide.

See [the dated vehicle contract and test history](Vehicle-Dated-Attribution-20261019.md).
Multiple equally eligible same-day assignments still require an explicit selection
rule. Already-approved legacy trips lacking daily evidence need a separate
evidenced correction design; this pending-trip flow does not rewrite old claims.

## Executed Backend Verification

Product build04: `dotnet build Cosmix.Backend.sln -c Release` with isolated build
output under `D:/CosmixVerification/hr-backend-20261019/hr-corrections-build04`:
**PASS, zero warnings and zero errors**. Build03 also passed before the two
reviewer-discovered vehicle fixes. Those two product lines are the only backend
changes between those builds.

The existing `docs/attendance-gap-c/VerifyManaged.ps1` compiled standalone sources
against copied frozen Release DLLs. No new test project or SQL connection was
needed. Synthetic collaborators test actual service logic, not real persistence.

| Suite / area | Actual result | Boundary |
| --- | --- | --- |
| Benefit workflow | 517/517 | Service, evidence, authorization/replay and controlled failure collaborators; build03 |
| Benefit usage / evidence / quota | 84/84, 75/75, 19/19 | Exact source validation, retained proofs and quotas; build03 |
| Benefit policy / KPI / encashment valuation | 59/59, 79/79, 92/92 | Explicit settings and calculation checks; build03 |
| Recovery policy / claims / fulfilment / replay | 46/46, 79/79, 38/38, 51/51 | Synthetic actual-work service workflows; build03 |
| Encashment workflow / ESS consistency / leave | 30/30, 38/38, 67/67 | Selected existing service regressions; build03 |
| Self reads / leave query / persistence contracts | 43/43, 36/36, 15/15 | Offline queries/model contracts, not SQL writes; build03 |
| Authority-time checks / composition | 54/54, 54/54 | Auth revalidation and DI construction; build03 |
| Generated MVC/OpenAPI / historical scope | 281/281 over 516 paths; 23/23 | Real registered metadata and pure scope evaluator, not hosted authenticated requests; build03 |
| EF model | 25 compiled migrations, zero model diff, zero SQL connections | Latest compiled migration `20261015144424_AddFinancialAdjustmentsAndOutstationDeployments`; not database history |
| C03/C06 / OT / HR report OT | 53/53, 156/156, 12/12 | Affected shared regressions; build03 |
| Capture authority / capture / corrections | 14/14, 147/147, 196/196 | Selected existing capture/correction checks; build03 |
| KPI / month locks / daily calculation | 22/22, 85/85, 193/193 | Selected shared regressions; build03 |
| Outstation / deployment boundaries | 65/65, 36/36 | Effective policies, partial days, home-break/source evidence; build03 |
| HR report context / boundary / query | 12/12, 370/370, 242/242 | Synthetic repository execution and offline SQL translation; build03 |
| HR XLSX / remaining reports / attendance results | 81/81, 422/422, 204/204 | Actual output parsing plus synthetic queries, not Excel or HTTP UAT; build03 |
| Real encrypted-file storage | 152 assertions across 20 scenarios, plus 2 restart-write and 5 restart-read checks (159 total) | Actual bounded temporary filesystem, unchanged storage implementation; frozen earlier build06; no SQL/auth test |

Final build04 affected batch: vehicle dated attribution **25/25**, succession
**4/4**, monthly adjustments **45/45**, entitlements **46/46**, repository
**64/64** (zero SQL attempts), policy publication **71/71**, generated OpenAPI
**281/281 over 516 paths**, all PASS, batch exit 0. Both reviewer regressions are
now GREEN. The OpenAPI harness reports CS1701 reference-unification warnings for
its retained .NET 8 OpenAPI package; the product Release build had no warnings.
Do not combine scenario counts and assertion counts into a misleading
integration-test total.

### Retained logs

All managed directories below are under
`D:/CosmixVerification/part-c-parent-20261013/`:

- `managed-4d6307af842e41c3bb42ed0850dc40a9`: passing primary benefit suites,
  followed by a vehicle test-fixture failure. This whole batch is not PASS.
- `managed-9f05320b3aad4b09bcd752619a8491fb`: 19 remaining build03 suites, exit 0.
- `managed-c754234dcd16425b82cd095617152ce7`: nine shared build03 suites, exit 0.
- `managed-7e510c594a284be381f1fc789fc80013`: final build03 OpenAPI/scope, exit 0.
- `managed-faa8a1b3a19449509107429e3709078e`: eight report/outstation suites, exit 0.
- `managed-768b175b443546adb82f462467ce0e2a`: seven final build04 vehicle/OpenAPI suites, exit 0.
- Storage: `D:/CosmixVerification/hr-backend-20261019/storage-tests-d5801b68fa3547639a96e2c631857a59/results.log`.

The vehicle fixture had reversed currency/receipt arguments. Named arguments
fixed the fixture; a negative wrong-currency check stayed in place. The product
currency guard was not weakened. Separately, two new reviewer regressions failed
against build03 (23/25) before the midnight-boundary and mileage-key fixes.
Their RED log is `managed-4bd3dd67375540f79714187e2ee8377e/VerifyVehicleDatedAttribution.log`.

## Web Scope and Real Acceptance

Targeted repairs replace fabricated attendance, employee dossier, biometric,
site and shift states with their real authorized sources. Failed writes do not
produce fake success; missing fields do not get sample personnel values.
Biometric metadata is read-only until the actual enrollment/provider workflow
is available. Capture success alone is not proof of enrollment.

The owner confirmed employee personal workflows belong on mobile. HR web exposes
management/review rather than personal leave, attendance or claim submission.
Backend/mobile endpoints remain intact. The management UI follow-up and its exact
checks are recorded in the final checkpoint, not inferred from the API inventory.

The final combined frontend TypeScript/Vite build passed after all workers froze
their implementation files: 2,441 modules, exit 0, existing large-chunk warning.
Repository ESLint passed. `npm run verify` stopped at formatting, including this
work's frontend report (subsequently formatted) and five unrelated existing files;
those unrelated changes were preserved. Scoped changed-file checks passed.

The completed web slice includes benefit/correction and recovery-work management,
Admin encashment review, typed v4 leave-policy publication, vehicle policy/monthly/
incident/dated-trip management and outstation/home-break management. Personal
claims and balances are not placed in the HR management panel. Vehicle checks
passed 66 controlled cases; leave policy checks passed 27 blank-number and 39
invalid/conflicting cases plus retention/render/intent checks. Outstation passed
44 author checks and 17 independent parent checks. These are synthetic frontend
checks, not evidence of database persistence. A final finite source review found
no actionable P1/P2 in the corrected vehicle navigation/denial paths and leave
publisher mapping. Full details are in
[the frontend checkpoint](../../../cosmix_frotned/docs/HR-Management-Followup-2026-10-19.md).

The attempted current-page inspection redirected to sign-in with a connection
error. Frontend port 5173 was running; neither API port 7240 nor 7481 had a
listener at inspection. No mock authenticated session, real employee action or
unapproved backend host was used to manufacture acceptance.

## Exact Live-Test Blocker

[Read-only SQL evidence](SQL-ReadOnly-20261019.md) could not establish an owned,
wholly synthetic, current-schema compatible target. Named LocalDB inventory and
process state disagreed; retained evidence covered older schema and partially
identified fixture populations. Existing credentials, keys and 132 private files
were not reused on that ambiguity. No old A02 approval was replayed.

Live acceptance needs a verified current-schema target and matching private
evidence/keys, synthetic actors with the exact grants, explicit test fixtures,
and authorization for bounded authentication/business writes. This document is
not that authorization. Migrations remain prohibited. If no already-compatible
target exists, that constraint must be resolved before this class of test can run.

Required live cases include authenticated MFA/CSRF, cross-employee denial,
same-key retry and changed payload, competing separate SQL requests, atomic
ledger/audit/receipt/outbox persistence, restart/replay, private-object failures
and delivered notifications. Standalone tests do not certify these properties
under a real database or worker process.

## Owner Integration List

This is a scoped continuation list, not a claim that every dirty file belongs to
this work. Existing unrelated and prior-turn changes must be preserved. Many
feature files were already untracked; a plain `git diff --stat` is incomplete.

Backend paths are relative to the existing `cosmix-backend` checkout:

- `src/Cosmix.Application/HR/Leaves/`: new `LeaveBenefitUsage.cs`,
  `LeaveBenefitCorrectionEvidence.cs`, `LeaveBenefitCorrectionService.cs`;
  changed `LeaveBenefitContracts.cs`, `LeaveBenefitService.cs`,
  `LeaveBenefitEvidence.cs`, `LeaveContracts.cs`, `LeaveTreatmentRules.cs`,
  `LeaveEncashmentValuation.cs`.
- `src/Cosmix.Infrastructure/Repositories/HR/LeaveBenefitRepository.cs` and
  `src/Cosmix.Api/Controllers/HR/LeaveBenefitsController.cs`.
- `src/Cosmix.Application/HR/VehicleFuel/`: new `VehicleTripAttribution.cs`,
  `VehicleTripAttributionContracts.cs`, `VehicleDatedFuel.cs`,
  `VehiclePolicySuccession.cs`; changed `VehicleAdjustmentContracts.cs`,
  `VehicleAdjustmentPolicy.cs`, `VehicleMonthlyAdjustments.cs`,
  `VehicleFuelService.cs`, `VehicleFuelWorkflow.cs`, `VehiclePolicyContracts.cs`,
  `VehiclePolicyRulesCodec.cs`, `VehiclePolicyPublication.cs`,
  `IVehicleFuelRepository.cs`.
- `src/Cosmix.Infrastructure/Repositories/HR/VehicleFuelRepository.cs` and
  `src/Cosmix.Api/Controllers/HR/VehicleFuelControllers.cs`.
- Standalone checks under `docs/hr-srs-remediation/`: benefit workflow, usage,
  quota, encashment valuation, vehicle repository, dated attribution, succession,
  and generated remediation contracts. Existing other suites were rerun, not
  recreated as new projects.
- `docs/SRS-Module-Map.md`, the correction/daily-vehicle client contracts, this
  verification record, the finite SRS/SQL audit, and the Attendance verification
  index/UAT guide.

No Domain entity, migration, DbContext, permission catalogue or shared DI file was
changed by this correction/daily-vehicle slice. Model inspection reports zero
pending differences, not permission to apply the 25 compiled migrations.

Frontend paths are relative to the existing `cosmix_frotned` checkout:

- `src/modules/attendance/pages/AttendanceProcessingPage.tsx`,
  `EmployeeAttendanceDetailPage.tsx`, `ShiftsPage.tsx`,
  `LeaveManagementPage.tsx`, `LeaveEncashmentPage.tsx`.
- `src/modules/attendance/operations/routes.tsx`, `navigation.ts`,
  `leave-policy/LeaveSettingsPage.tsx`, new `leave-benefits/` management package,
  and `src/modules/attendance/utils/messages.ts`.
- `src/modules/hr/pages/EmployeeHiringPage.tsx`, `SiteDetailPage.tsx`,
  `AdvancesLoansPage.tsx`, `AllowancesPage.tsx`, `VehiclesPage.tsx`;
  `src/modules/hr/components/employee/EmployeeBiometricSection.tsx`.
- New HR `outstation/` and `components/vehicle-management/` packages with their
  module-owned API/schema helpers; shared application authentication unchanged.
- `src/modules/reports/pages/HrReportsPage.tsx` and `src/layouts/AppLayout.tsx`
  (leave-management active navigation and replacement of fabricated header alerts
  with a link to the existing real notifications workflow).

The frontend workers are now frozen. Additional v4 publisher files are
`src/modules/attendance/operations/leave-policy/LeavePolicyPublisher.tsx` and
`extended-policy.ts`. The source/build/controlled-check slice is complete;
authenticated runtime acceptance is not. Remaining engineering boundaries include
the separately scoped approved-legacy vehicle correction/ambiguous assignment
rules and durable rich-HR report export/PDF work. Missing legal approval and real
face/SMS provider acceptance have not been relabelled as completed tests.
