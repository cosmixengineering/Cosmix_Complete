# HR SRS Remediation Verification

## 19 October 2026 - Later HR-Only Scope

The owner clarified that this continuation completes HR against the supplied
SRS, not downstream Accounts posting/settlement/payment. The newer scope,
changed HR files, frozen build, executed checks and remaining **HR** requirements
are in [HR-Scope-Continuation-20261019.md](HR-Scope-Continuation-20261019.md).
This supersedes the earlier Accounts-first work order, not its historical test
records. No new SQL acceptance or migration application is authorized.

## 19 October 2026 Continuation

Fresh executed results, regression failure/repair evidence, frozen build paths,
changed files and remaining acceptance gates are recorded in
[Continuation-20261019.md](Continuation-20261019.md). No migration or database
write was performed. Older counts and pending descriptions below remain their
dated checkpoints, not the current whole-backend acceptance result.

## 15 October 2026 - Current Backend Continuation

This section supersedes earlier same-day summaries for the changed slices. Full
Attendance/HR acceptance is **not achieved**. F3 financial adjustment writes,
complete G1/G2/G3 workflows and authenticated SQL/storage acceptance remain open.
Legal activation remains unapproved; real face/device and external OTP delivery
are excluded. No frontend or salary payment release is claimed.

Final Release: **exit 0, zero warnings and zero errors**. Command:

```powershell
dotnet build Cosmix.Backend.sln -c Release --artifacts-path D:\CosmixVerification\hr-backend-continuation-20261015\posting-build --no-restore --property:UseSharedCompilation=false -m:1 -nr:false -flp:logfile=D:\CosmixVerification\hr-backend-continuation-20261015\build-final.log
```

Frozen Release directory:
`D:\CosmixVerification\hr-backend-continuation-20261015\posting-build\bin\Cosmix.Api\release`.

| Assembly | SHA256 |
| --- | --- |
| Cosmix.Api.dll | 5556506150506C48211027983ED3CF85A75D9F5C7173AAB3CE5AA3F68435A69E |
| Cosmix.Application.dll | 3C4D786214B1B49F5D9F9D08118C77D3A67CEFB5B1C68BA22B3F2EBC1BA7D0BF |
| Cosmix.Domain.dll | 1795D820E2AB4520796D14EBAD420B7D48D05337E3702A23BCA1EA63FB300E23 |
| Cosmix.Infrastructure.dll | 7288741423A56A4C6CDB57644EF5DAD1B73F321ED8370C36A70EAEFA2EEC89B5 |

### Final Current-Build Checks

Existing standalone runner, no test project added:

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory 'D:\CosmixVerification\hr-backend-continuation-20261015\posting-build\bin\Cosmix.Api\release' -Sources @(
 'docs/hr-srs-remediation/VerifyConfirmedLoanPosting.cs',
 'docs/attendance-completion/VerifyPayrollInputService.cs',
 'docs/hr-srs-remediation/VerifyLeaveRecoveryReplay.cs',
 'docs/hr-srs-remediation/VerifyVehicleEntitlements.cs',
 'docs/hr-srs-remediation/VerifyVehicleRepository.cs',
 'docs/hr-srs-remediation/VerifyRemediationContracts.cs'
)
```

Retained result:
`D:\CosmixVerification\part-c-parent-20261013\managed-94b94e35e8ba4fec812cb8461ac710f2`.
Results: confirmed loan posting **60**, Finance projection **102**, recovery replay
**16**, vehicle service **19 scenarios**, repository **21**, registered MVC/OpenAPI
**89 checks / 469 paths**. All passed. The standalone OpenAPI compiler emits two
CS1701 framework-unification warnings; the product build has none. Generated
registered contracts are not authenticated HTTP requests.

Offline schema/startup/target checks on the final build:
`D:\CosmixVerification\part-c-parent-20261013\managed-adb2e13d87834227a995da26319891f7`.
All **24 migration** SQL statements generated through EF's actual migrator;
startup inspection covers **17 compiled methods**, target guard **194 checks**
(173 unsafe snapshots rejected), runtime/worker/keyring **24 checks**, all passed.
No SQL connection, host, private root or key was created by these checks.

### Earlier Combined Regression Evidence

The earlier combined run is retained at
`D:\CosmixVerification\part-c-parent-20261013\managed-ad7fea45209f43d1a9136175630b644e`.
It passed principal/loan boundaries **31**, ESS consistency **37**, ESS finances
**21**, all report suites **942** (12/266/242/153/269), expiry reminders **53**,
notification outbox **78**, worker isolation **4**, startup **17 methods**, A02
guard **194** and runtime **24**. These are synthetic collaborator/query tests,
not SQL concurrency or physical encrypted-storage acceptance.

That combined command exited 1 solely because an old fixture asserted that the
historical A01 private root did not exist. The approved partial root already
existed. The verifier now snapshots root/keyring metadata and proves it unchanged;
it neither reads key contents nor deletes/creates the root to satisfy the test.
Corrected fresh-profile **24** and existing-profile **6** checks passed in
`managed-a99fa2f6f82343fcb29a903ca8c5f391`. The original failing log is preserved.

The later final build changes only an Application loan request-validation null
guard relative to that combined run. API, Domain and Infrastructure hashes are
unchanged; final critical loan/recovery/vehicle/contracts suites were rerun above.
Source/dependency copies, hashes and complete outputs are retained per runner.
Do not add historical run counts together as a whole-system completion score.

After the final build, the complete loan-boundary/ESS/report set was also rerun:
`D:\CosmixVerification\part-c-parent-20261013\managed-9e2ed2886db447b282f45baaeb4c2c66`.
All eight suites passed: loan boundaries **31**, ESS **37 + 21**, report OT **12**,
report authority **266**, report repository **242**, daily/monthly results **153**,
remaining reports **269**. No product rebuild or source change occurred between
that run and the frozen A02 review. These are current-build standalone results.

### A02 Execution Boundary

Later same-day update: the user approved the exact bounded setup/auth and then an
explicit Windows PowerShell 5.1 preflight retry. Both read-only attempts stopped
before setup. Runtime was confirmed on the retry, but the parent's invocation
omitted the reviewed process-only execution-policy argument and script loading
was denied. No database, keys, accounts or host were created. Exact runtime,
failure, counts and final absent target state are in `A02-Execution-20261015.md`.
The approval-pending paragraphs below describe the earlier preparation record,
not the latest authorization status. Do not auto-retry the stopped setup.

Reviewed target: NEW `(localdb)\CosmixAttendanceHuma20261015A02`, catalog
`Cosmix_Testing`, owner `COSMIXRND\Huma.Khan`. Final read-only review found the new
instance/private/system directories absent. It binds **1,140 inputs** and all
24 migrations. Existing databases/instances/configuration remain untouched.

Final review: `docs/attendance-a02/A02-Review.json`.
Packet SHA256: `C3AF9DBC3E624F4E82D568DA83EF9761C0BC5A8B2470739F453FFF1BB15D648A`.
Review-file SHA256: `45506838E641C2BB0BE3F71958D239E52138E21836ECCD30A8C2768AE9A40708`.
The same bytes were previously retained as a draft; only the final review path is
used for approval. Hash equality is not permission to execute.

Combined setup and one HR session approval has been requested, not received or
executed in this record. Exact private paths, schema/seed budgets, local OTP/MFA,
CSRF, logout/revocation checks, owned host and stop/no-cleanup policy are in
`docs/attendance-a02/A02-Setup.md`. The packet excludes business scenario fixtures,
Finance/Attendance writes and real providers. One passing login would not close
the separate business concurrency/storage/UAT acceptance gates.

### Changed Modules And Integration

- Advances/loans: zero-only extra-charge contract/domain guard and exact-principal
  schedule; preserved outstanding-balance and retry boundaries.
- Payroll handoff: explicit confirmed-loan-posting action/read, exact frozen
  source/receipt and ledger/audit binding, paid-installment prepare/lock/export
  guards. See `Confirmed-Loan-Posting-20261015.md` for its nine product files.
- ESS: service, contracts, module repository/interface and controller. Calendar
  now uses authoritative Attendance DTOs; leave uses exact fractions/fiscal periods
  and existing self-source permissions. Client owners must adopt these contracts.
- HR reports: service/contracts/authorization, retained OT reader and report/scope
  repositories. Nullable unknown values, retained evidence and corrected CSV
  values are documented in `Reports-Remaining-20261015.md`.
- Reminder worker: existing `DocumentExpiryReminderWorker.cs` uses bounded durable
  outbox transactions. Deployment durable-worker enablement and old-worker
  shutdown remain rollout responsibilities. Existing worker/EF layering debt is
  not presented as a new clean-architecture extraction.
- Leaves: narrow recovery replay/provenance fix in `LeaveEncashmentService.cs`;
  no fabricated recovery fulfilment or benefit award.
- Vehicles: service and module repository/interface; exact files and bounded
  coverage in `G3-Vehicle-Source-Integrity-20261015.md`.
- Local testing: existing startup/setup preserve grants instead of restoring
  revoked permissions; new separately guarded A02 target/database profile and
  reviewed scripts. No production settings or grants changed.

No migration, DbContext, new test project or Git synchronization was added in this
continuation. Existing changes by other owners were preserved. Detailed task exit
gates and next implementation order remain in `Plan-Progress-20261015.md`.
`git diff --check` passed after the documentation updates.

## 15 October 2026 - Finance/Loan and ESS (Latest)

Release 0 warnings/errors; eight affected suites / **569 checks passed** against
`D:\CosmixVerification\hr-remediation-finance-20261015\final\bin\Cosmix.Api\release`.
Retained runner output:
`D:\CosmixVerification\part-c-parent-20261013\managed-c5f85337a03c49bcbd8f9d8122662324`.
New failure-first regressions: Finance/loan 14/24 -> 24/24; ESS finances 7/21 ->
21/21. Generated MVC/OpenAPI: 70 checks, 467 paths, two standalone CS1701 warnings;
the product build has none. Exact commands, source hashes, changed files, reviewed
limits and still-pending live scenarios are in Finance-Loan-ESS-20261015.md.

This is synthetic collaborator/query-predicate verification, not authenticated
HTTP/SQL, actual encrypted-storage acceptance or legal activation. No database,
host, fixture, provider, migration or Git writes. Full Attendance is not accepted.

## 15 October 2026 - OT Retained-Finance Verification (Latest)

Current bounded continuation: Release 0 warnings/errors; 13 affected standalone
suites and 978 assertions passed. Exact commands, outputs, changed files and
synthetic/production limits are recorded in `Ot-Retained-Finance-20261015.md`.
Cross-month OT repository reproduction changed from 17/19 to 19/19. The old two
RED cases below are historical results, not current failures. No live SQL/HTTP,
actual storage/provider, legal activation or full Attendance acceptance is claimed.

## 15 October 2026 - Latest Post-Review Integration

This section supersedes the pre-review build/check counts below, not their retained
historical evidence. B1/B2 are implemented and offline verified; A2 and fresh
authenticated SQL acceptance remain pending. Full HR/Attendance acceptance is not
achieved. Face/OTP provider acceptance and clients remain excluded.

Final current Release build: exit 0, **0 warnings, 0 errors**.
Exact log: `D:\CosmixVerification\hr-remediation-ab-reviewed-20261015\build.log`.
Command:

```powershell
dotnet build Cosmix.Backend.sln -c Release --artifacts-path D:\CosmixVerification\hr-remediation-ab-reviewed-20261015\release --property:RestoreLockedMode=true --property:UseSharedCompilation=false -m:1 -nr:false -flp:logfile=D:\CosmixVerification\hr-remediation-ab-reviewed-20261015\build.log
```

Final parent combined run: **10 suites / 1,467 checks passed**, exit 0.
Source and dependency copies/hashes, compiler inputs and logs:
`D:\CosmixVerification\part-c-parent-20261013\managed-d45fa1c4195a4c789f71914baffc98c0`.

```powershell
& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory 'D:\CosmixVerification\hr-remediation-ab-reviewed-20261015\release\bin\Cosmix.Api\release' -Sources @(
  'docs/hr-srs-remediation/VerifyHrReportBoundary.cs',
  'docs/hr-srs-remediation/VerifyHrReportQueries.cs',
  'docs/hr-srs-remediation/VerifyTrackingBoundary.cs',
  'docs/attendance-corrections/VerifyAuditReads.cs',
  'docs/attendance-completion/VerifyAttendanceScopes.cs',
  'docs/attendance-corrections/VerifyCorrectionService.cs',
  'docs/attendance-capture/VerifyCaptureService.cs',
  'docs/attendance-completion/VerifyAttendanceComposition.cs',
  'docs/access-provisioning/VerifyDelegation.cs',
  'docs/attendance-gap-b/VerifyCaptureAuthorityTime.cs'
)
```

Passing counts in this exact order: **250, 255, 129, 59, 23, 196, 147, 40, 354,
14**. No SQL connections, host/authenticated requests, real encrypted storage or
provider calls. This verifies real application/evaluator/controller/query logic
with explicitly synthetic IO and offline SQL translation, not durable integration.

| Current assembly | SHA256 |
| --- | --- |
| Cosmix.Api.dll | A1A9CFD64236FBCF8C2855105FD095F46190070F9EAEEB2F35F86012B199DEAD |
| Cosmix.Application.dll | 32DB83BE05FE3FE5576DBBD2293E573A35F7216D70A180591D511E513F68E13E |
| Cosmix.Infrastructure.dll | 68666A7863BA009283B1840C47A097A74A86FB4DF6CEB356660677F5D6857646 |

The 19 scoped/preserved source hashes were identical before and after the final
combined verification, and are retained in Source-Inputs-20261015.json. Current HEAD remained
ddc1a9f; no Git write by this batch. `git diff --check` passed.

Independent review initially found the audit-role/range issues described below,
then medical-document metadata permission and three historical-filter/source-date
issues. These were fixed and regression tested. Its final targeted source/test-code
recheck found **no remaining P1/P2 findings**; it did not execute HTTP/SQL tests.

Additional failure-first evidence, all under the managed root used below:

- Medical metadata without medical authority: `managed-cf4a5ba84b914055ac1af80fdcec82ad`.
- UTC/current business-day rollover: `managed-079c2d6e8ab74f059d9fbf697cdf50b8`.
- Historical site, vehicle/zone/status filtering and padded source-end cases:
  `managed-1eb716b73fd7479b97c2f91a9876f8ae`, 9 failures/168 checks before repair.

The final report boundary requires scoped medical read before the entire existing
document-expiry report; it does not substitute an incomplete standard-only report.
Source/business dates reuse RosterSchedule.Today. Actual overnight source coverage
is retained; tomorrow is no longer included for daytime/midnight-exclusive duty.
Filters apply to relevant source records before scope footprints and limits.
See Boundary-UAT.md for the still-unexecuted runtime acceptance scenarios.

## 15 October 2026 - Pre-Review A/B Evidence

Only tracking/report authorization repairs are covered. Full Attendance/HR
acceptance is **not achieved**. Existing C-J correctness/workflow gaps remain in
the plan and Status ledger. No SQL instance, database, API host, fixture, key,
credential or external provider was changed/started by this batch.

### Baseline And Concurrency

Original audited HEAD: `686d5027e79d1dfa0c2c7df2aadd7d3975e79253`.
During this batch, a concurrent outside action moved HEAD to
`ddc1a9f5c98aac90263261e8b03b5714f1886371`, including ongoing local work. No
commit, fetch, pull, push, merge, branch or worktree action was performed by the
parent or its workers. Nothing was reverted. Final checks use current files,
not the old audit binaries. Nineteen scoped source/test and preserved concurrent
file hashes are retained in `Source-Inputs-20261015.json`.

The six preexisting PayrollHandoff/VehicleFuel modifications are not credited as
repairs by this batch; their workflow review belongs to E/G. A full F03-F14
reproduction sweep was not performed here. The audit's obsolete Leave permission
expectation remains a C1 task, not a reason to weaken authorization.

### Executed Build

```powershell
dotnet build Cosmix.Backend.sln -c Release --artifacts-path D:\CosmixVerification\hr-remediation-ab-final-20261015\release --property:RestoreLockedMode=true --property:UseSharedCompilation=false -m:1 -nr:false -flp:logfile=D:\CosmixVerification\hr-remediation-ab-final-20261015\build.log
```

Exit 0, **0 warnings, 0 errors**. Isolated output, not the normal project bin/obj.
API output: `D:\CosmixVerification\hr-remediation-ab-final-20261015\release\bin\Cosmix.Api\release`.

| Assembly | SHA256 |
| --- | --- |
| Cosmix.Api.dll | C394CCF44F6FCA34C4D9DE246B7D7B72E1FA68302B9AED1A9ACCB97F10BB55AA |
| Cosmix.Application.dll | 83E3B3AF837482B6A59BB2A665E56D968D21120F7509FB6CA6B711E6542D46D9 |
| Cosmix.Infrastructure.dll | 2B6D8D0C0B51FABF702E231800D6390DC80EA5181D492154EB52FB399F7BDD9C |

### Combined Regressions

Runner: `docs/attendance-gap-c/VerifyManaged.ps1`, frozen ReleaseDirectory above.
Eight suites, **996 passing checks**, exit 0. Exact source, dependency hashes,
compiler inputs and logs:
`D:\CosmixVerification\part-c-parent-20261013\managed-7f1caeec156e4a579ae949c2c619ab7a`.

| Source | Checks | Actual coverage |
| --- | ---: | --- |
| docs/hr-srs-remediation/VerifyHrReportBoundary.cs | 244 | All 14 service read/export permission decisions; current MFA/session, expiry/revocation, scope intersections, protected audit, storage failure, source-role basis, date bounds |
| docs/hr-srs-remediation/VerifyHrReportQueries.cs | 158 | All 14 actual query expressions; selected/foreign/empty rows, historical/source scope, limits, audit filtering and SQL translation; zero connections |
| docs/hr-srs-remediation/VerifyTrackingBoundary.cs | 129 | Real controller/service, retired write, metadata, current/historical/source authority and safe response |
| docs/attendance-corrections/VerifyAuditReads.cs | 59 | 47 service/authorization and 12 SQL translation |
| docs/attendance-completion/VerifyAttendanceScopes.cs | 23 | Historical scope alternatives |
| docs/attendance-corrections/VerifyCorrectionService.cs | 196 | Real correction intake/review with synthetic persistence |
| docs/attendance-capture/VerifyCaptureService.cs | 147 | Capture application logic with synthetic IO; no provider acceptance |
| docs/attendance-completion/VerifyAttendanceComposition.cs | 40 | Actual Development/Production dependency graph, including new report boundary/repository; synthetic storage |

Reproduce by passing the eight paths above as the runner's `-Sources` array.
This is **not** eight authenticated integration tests: collaborators are synthetic,
the SQL provider translates expressions without executing them, and no host runs.

### Failure-First Evidence

- Tracking original controller: 4 expected failures in 70 checks; managed runner
  `managed-50baed5a5cee40d7b7afc6326111a95a`. See Tracking-Boundary.md.
- Original report boundary: 5 expected failures in 6 checks;
  `managed-31f4ff7276cf4d5c9352e6fb8a6fb9fd`.
- Original report queries: 56 expected failures in 86 checks;
  `managed-6020cb3609fd475bb8106c1ad9089246`. See Report-Scope.md.
- New edge regressions reproduced grant-validity mutation not detected and derived
  year 9999 overflow: `managed-36ea1e032526468da8f62103b1a33c5f`.
- Independent review regressions reproduced wrong audit role and 367-day inclusive
  acceptance: `managed-84ff23d415ed4b7ca417d2a36fbca769`.
- All four latter cases are included and pass in the final boundary suite above.

All managed evidence paths use the existing root
`D:\CosmixVerification\part-c-parent-20261013\`.

### Acceptance Limits And Next SQL Work

See SQL-Preflight-20261015.md. All observed LocalDB instances were stopped. Retained
records disagree on selected instance/pipe and population; the existing profile
also pins an obsolete pipe and three actors while later results show five accounts
and fourteen employees. No target was guessed and no guard relaxed. No current
migration/history/data-state claim is made from old receipts.

Before live acceptance: resolve one exact named instance/catalog/owner/MDF/LDF,
verify synthetic provenance, and prepare the compatible bounded process profile.
Exclude DocumentExpiryReminderWorker and other unapproved business writers, bind
the current pipe/process, preserve normal settings, and review exact auth/audit
writes. Any changed setup/write scope requires the agreed explicit approval.

Remaining B1/B2 evidence: real login/MFA/CSRF, revocation/replay and populated
Self/Team/Site/Department/All allow/deny requests; independent SQL row/audit/receipt
deltas; concurrent requests; encrypted-storage failure/restart; actual generated
OpenAPI. Controller metadata and DI checks are not live OpenAPI acceptance.

### Changed Scope

- API: EmployeeTrackingController, HrReportsController.
- Application HR/Reports: authorization/selection, permission catalog definitions,
  registration, service and repository contract.
- Infrastructure HR: report repository and source-scope partial.
- Verification: three new boundary/query standalone sources, existing composition
  verifier extended, status/contracts/preflight/evidence and SRS trace.
- No schema/migration, shared authentication implementation, existing business
  workflow, React/React Native client or production-provider change.

Report numerical correctness (H1), settlement/legal/month-lock work (E/F), remaining
HR features (C/D/G/H) and final runtime acceptance (I/J) remain explicitly open.

## 15 October 2026 - Reviewed Attendance Continuation

This newer section supersedes earlier C/H source status, not any SQL/provider
acceptance boundary. Existing concurrent modifications were preserved.

### Source and Build

- HEAD remained ddc1a9f5c98aac90263261e8b03b5714f1886371; no Git write occurred.
- Reviewed Release: D:/CosmixVerification/hr-remediation-c-20261015/reviewed/bin/Cosmix.Api/release.
- Build command: `dotnet build Cosmix.Backend.sln -c Release --artifacts-path D:\CosmixVerification\hr-remediation-c-20261015\reviewed --property:RestoreLockedMode=true --property:UseSharedCompilation=false -m:1 -nr:false`.
- Exit 0, zero warnings/errors, 59.40 seconds. Source digest of 1,052 inputs was
  unchanged across this build: B03EBF717EBF6DF8580C8D87489FCA13BA80BACEA13559B3A517BAB2CD799AC8.
  See Source-Inputs-Continuation-20261015.json. Runner inputs.json files retain
  exact copied binary hashes and each test's frozen source, compiler and output.
- No migration was added or rewritten. Existing schema checks remain in the run.

### Executed Primary Regressions

The existing VerifyManaged.ps1 runner executed these 17 sources against that
reviewed Release, exit 0, 1,425 passing assertions. Evidence root:
D:/CosmixVerification/part-c-parent-20261013/managed-96d7fc12a0004bc490000929fcf90324.

| Source | Checks |
| --- | ---: |
| hr-srs-remediation/VerifyEncashmentWorkflow.cs | 30 |
| hr-srs-remediation/VerifyEncashmentValuation.cs | 80 |
| hr-srs-remediation/VerifyHrReportResults.cs | 153 |
| hr-srs-remediation/VerifyHrReportBoundary.cs | 266 |
| hr-srs-remediation/VerifyHrReportQueries.cs | 258 |
| hr-srs-remediation/VerifyIsolatedWorkers.cs | 4 |
| attendance-completion/VerifyPayrollAttestations.cs | 195 |
| attendance-completion/VerifyAttendanceComposition.cs | 42 |
| hr-srs-remediation/VerifyRemediationContracts.cs | 51 |
| attendance-completion/VerifyLeaves.cs | 67 |
| attendance-completion/VerifyLeaveSelfReads.cs | 43 |
| attendance-completion/VerifyLeaveQueries.cs | 36 |
| attendance-completion/VerifyLeavePersistence.cs | 15 |
| attendance-completion/VerifyPolicyLeaveModel.cs | 29 |
| attendance-completion/VerifyMonthLocks.cs | 66 |
| attendance-completion/VerifyMonthLockQueries.cs | 42 |
| attendance-completion/VerifyPayrollReconciliation.cs | 48 |

All source paths above are relative to docs/. Run with
`docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory <reviewed Release> -Sources @(<paths>)`.
The OpenAPI standalone compiler emitted two CS1701 .NET 8/10 assembly-unification
warnings; its generated-document checks passed over 467 actual MVC paths. The
product Release itself had no warnings. This was document generation without
starting an API host, not an authenticated network request.

### Failure-First and Review Evidence

- Initial C1 harness: 22 failed assertions/29 before changes, managed-c5d8c57f2440436eafb3584c7353ad92.
  Three self-service failures also contained an invalid synthetic Self grant
  (null employee target); the fixture was corrected, not production authorization.
  Final current workflow is 30/30, including both proper self and denied access.
- Isolated workers: managed-4b9e0b49825d405ea3298238e062a236 failed with unintended
  document-expiry/face-retirement registrations; final four checks pass and normal
  registrations remain untouched.
- Stale financial handoff: managed-c85a5b94515b4920a417d495bbebcab4 reproduced the
  chain-source exception; final tests expose unready dependencies without accepting
  a stale handoff, suppressing authorization errors or bypassing legal checks.
- Report metadata: managed-d0a970c452b9473f936cdac7f6b41d4b reproduced unconditional
  verified-production metadata. Final metadata makes no payroll certification.
- Independent C2 review found exact-KPI persistence overflow and a submission
  window incorrectly reapplied at approval. managed-501ea0fd847049a886690fdf4581875f
  reproduced both. Exact KPI remains the money input; four-decimal entity projection
  and immutable submission time now pass full submit/approve tests.
- Two-report value/revision regressions: final baseline source failed 51/89 at
  managed-a96f611860cc4102afde4d7cf8fa066d. Current expanded suite passes 153/153.
- Independent review found total-count loss after pagination. Service RED evidence
  managed-6723ab74773a4e8f84eec0868a1a31a0; final service and actual repository tests
  cover 150 matching employees across pages of 100, 50 and zero rows.
- Independent static re-review found both C2 defects and pagination defect resolved.
  Separate bounded review found no important defect in the stale-handoff catch or
  isolated-worker exclusion. This is not a whole-repository security assessment.

### Changed Files for Owner Integration

- Application HR/Leaves: LeaveContracts, LeaveEncashmentContracts,
  LeaveEncashmentService, LeavePolicySettings; new LeaveEncashmentSettings,
  LeaveEncashmentLedger and LeaveEncashmentValuation.
- Infrastructure HR: LeaveRepository, LeaveEncashmentRepository,
  HrReportRepository, HrReportScopeRepository; new HrAttendanceReportResultsRepository.
- Application HR/Reports: HrReportContracts and HrReportService.
- Attendance: new MonthLockOvertimeReadiness, MonthLockRepository and
  AttendanceMonthLockFinancialReader. Approved OT still cannot finalize.
- Shared/API: one LeaveEncashmentValuation DI registration; LeavesController and
  LeaveSettingsController metadata; BearerOperationTransformer recovery GET,
  new DeprecatedOperationTransformer plus registration; isolated LocalTesting
  worker exclusions only.
- Existing verifiers extended: Leaves, MonthLocks, MonthLockQueries,
  PayrollAttestations, AttendanceComposition and report boundaries.
  New verifiers cover encashment, report values, OT dependencies, isolated workers
  and generated OpenAPI. SRS mapping, contracts/UAT and continuation records updated.
- Unrelated ESS, AdvanceLoan, Finance and reminder-worker edits already present
  were preserved. Their presence is not proof of this batch's acceptance.

### Acceptance Boundary

No SQL instance/database, fixture/credential/key, API host or real provider was
started or changed by this continuation. SQL-Preflight-20261015.md still records
the unresolved target/provenance mismatch. No existing database or business
data was used. File-output evidence was written only by standalone verification.

Implemented and offline-checked is not fully accepted. Outstanding work includes
exact retained OT-to-Finance awards, financial chain supersession/confirmed-posting
adjustments, formally approved legal authority/evidence, actual recovery/benefit
fulfilment, remaining ESS/loan/allowance/vehicle/report backlog and approved
authenticated HTTP/SQL/storage/concurrency tests. Real face/model/device and
external OTP delivery remain excluded by current user scope. Separate clients
were not implemented or tested. Full Attendance acceptance remains NO.

### Final Wider Run and Known Failure

The same reviewed Release also passed six retained boundary regressions, exit 0:
TrackingBoundary 129, CaptureService 147, CorrectionService 196, AttendanceScopes
23, CaptureAuthorityTime 14 and KpiWorkflow 22. Their sources are respectively in
docs/hr-srs-remediation, attendance-capture, attendance-corrections,
attendance-completion, attendance-gap-b and attendance-gap-c. Frozen evidence:
D:/CosmixVerification/part-c-parent-20261013/managed-40f60fe5ee8049fb86eeafe18c5c2ce1.
Combined with the primary run, 23 suites passed 1,956 assertions.

The separate VerifyOtFinalizationBoundary.cs was deliberately executed against
the same reviewed Release and **failed: 17/19**, exit 1. Evidence:
D:/CosmixVerification/part-c-parent-20261013/managed-1cae9fe2c1d44531a05206a2cc6f7592.
September 30 duty approved October 2 is selected by the September Attendance lock,
but by the October Finance query: September lock=1/handoff=0; October lock=0/handoff=1.
The two corresponding assertions remain RED. The changed decision fingerprint,
missing/mismatched session/approval/decision guards and pending-OT checks pass.
Approved OT remains blocked; no incomplete test was relabelled complete or skipped.

This is why neither F2 nor full Attendance acceptance is closed by this batch.

## 2026-10-19: HR Benefits, Actual Recovery and Vehicle Adjustments

The latest HR-only continuation adds real benefit award/credit/reversal and
recovery work submit/review/progress workflows plus vehicle monthly/incident
adjustments. It is not an Accounts posting/payment or legal activation change.
Admin review and policy-selected encashment eligibility follow the owner's
explicit decision; the same earned balance cannot be spent twice.

Final frozen `hr-benefits-build06` Release passed with zero warnings/errors.
The benefit workflow passed 332/332, recovery claims 79/79, expanded recovery
configuration 46/46, vehicle adjustments 45/45 scenarios and legacy vehicle
assertions 170/170. Other leave, quota, KPI, evidence, callback-expiry, DI and
OpenAPI results and exact retained output paths are recorded in
[the full handoff](HR-Benefits-Recovery-Vehicles-20261019.md).

The final primary/vehicle batch passed all 24 suites, exit 0. The nine shared
OT/work-treatment/report/capture/correction/KPI/month-lock regressions passed
708 checks on the same frozen build. The new final-read expiry check passed
54/54 authority-time cases. ESS passed 38/38 after updating its exact denial
expectation to the earlier authoritative `403 access_denied` and asserting
absence of a successful-read audit; the initial stopped batch is retained.
The affected daily-calculation fixture also passed 193 checks on build06.
Across the three final commands, 34 distinct standalone suites completed with
exit 0; this count includes offline model/contract checks, not live acceptance.

The offline EF comparison found 25 compiled migration definitions and zero model
differences, with zero SQL connection attempts. This is not a database-history
inspection. No database or migration was created, seeded, applied, reset or
otherwise changed. Current service checks use synthetic IO, not live HTTP/SQL
transactions. Consumed-award adjustments, bounded vehicle attribution/succession
cases, company policy configuration and real integration acceptance remain
explicitly separate. Full Attendance acceptance is still not claimed.
