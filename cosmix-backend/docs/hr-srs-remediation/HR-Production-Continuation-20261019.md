# HR Remaining Work and Live Acceptance - 19 October 2026

## Scope

Continue the existing dirty checkout. No Git synchronization, new test projects,
database migrations, reset, restore or cleanup. Real face/SMS and legal activation
remain outside the approved acceptance scope. Do not equate build/standalone
success with complete production acceptance.

The user subsequently explicitly authorized tests against existing
`Cosmix_Testing`. That is not evidence that every employee is synthetic. Use only
the three known `.invalid` test accounts and clearly marked newly created records;
do not update, delete, seed over or export unrelated employee records.

## Actual Read-only SQL Findings

Direct integrated-auth metadata reads (no alias auto-start) established:

- Pipe: `np:\\.\pipe\LOCALDB#2E40033B\tsql\query`.
- Server: `Software-PC1\LOCALDB#2E40033B`, SQL process 24152.
- Operator and catalog owner: `COSMIXRND\Huma.Khan`.
- Catalog: `Cosmix_Testing`; family `D9C34627-18A2-41CF-A804-821E39516303`.
- Files: `C:\Users\Huma.Khan\Cosmix_Testing.mdf` and
  `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`.
- All 25 current migration IDs are present, ending
  `20261015144424_AddFinancialAdjustmentsAndOutstationDeployments`.
- Three accounts match the existing synthetic login/phone/name guard. Twenty-six
  employees exist; only three have `.invalid` emails. No employee values were
  printed. Five leave types exist, but no policy versions/leave ledger entries.
- Zero enabled user triggers, synonyms and nonterminal OTP deliveries.
- The existing API process has no 7240/7481 listener. It was not killed/resumed.
  The frontend remains on 5173.

These findings supersede the earlier *unverified current schema* blocker. The
LocalDB named-instance inventory still says Stopped; the live explicit pipe and
server PID establish the connection actually inspected. No instance was started.

## Bounded C01 Test Plan

Owned host: `https://localhost:7481`, production controllers/services, actual web
password/MFA/CSRF/session checks and SQL repositories. No authentication bypass.
The existing local-only SMS inbox is used, not an external provider.

New private paths only:
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261019C01\`:
`config/host.json`, fresh `keys/files.key`, `keys/data-protection`, `files`,
redacted `results` and private host output. Credentials are read-only from the
existing private `LocalTesting/Test-Accounts.json`, never printed/copied to Git.
All six business background workers are removed from this owned test process.
Existing nonterminal OTP deliveries make startup fail rather than process them.

Initial write budget:

- Up to two logins/MFA sessions (HR and Admin), no resend or refresh. Associated
  authentication/rate/audit rows and the known accounts' normal login state only.
- At most 120 bounded HTTP requests, including local OTP polls.
- One new `TEST-C01-*` Department, concurrent same-key create requests, one winning
  optimistic update, and an inactive-state update. Immutable history/receipt/audit
  rows are retained. No deletion or automatic cleanup.
- Zero employee, role, permission, salary, leave-policy, payment or migration
  writes in this initial slice. Existing employee rows are compared by SQL digest.
- Owned host stops afterwards; existing SQL/API processes and databases remain.

`Invoke-HrAcceptanceC01.ps1 -Mode Inspect` passed read-only preflight. PowerShell
parsing and HTTP helper tests passed using an in-memory transport, including body,
CSRF/key binding, request ceiling, route bound and retained-cookie parsing. They
are preparation checks, not authenticated acceptance. Execute awaits the guarded
profile and a coordinated frozen Release build. Any partial run is retained and
reviewed; setup is not automatically repeated.

## Work Ownership

- Euler: approved legacy vehicle attribution correction and explicit assignment
  evidence in module-owned vehicle files; no shared schema/DI edits.
- Bernoulli: rich-HR durable export/PDF service, repository, controller and tests.
- Locke: factual site operational dates/history/source fixes and regressions.
- Pasteur: separate C01 existing-target guard, minimal `LocalTesting.cs` wiring
  and standalone wrong-target checks; old A01/A02/B01 profiles unchanged.
- Mill: report export-job frontend against Bernoulli's exact contract.
- Parent: shared registration/worker integration, live test runner/execution,
  combined build, affected regression selection and final evidence.

## Verification Ledger

- Integrated Release `hr-production-build01`: zero warnings/errors. Log:
  `D:/CosmixVerification/hr-backend-20261019/hr-production-build01.log`.
- Frozen-build standalone regressions: site facts 31, vehicle dated attribution
  50, succession 4, adjustments 45, entitlements 46, repository 64, policy 71:
  all 311 passed. Evidence directory:
  `D:/CosmixVerification/part-c-parent-20261013/managed-50675dece1184ad48fbc557b89f8660e`.
  These are synthetic service/query tests, not authenticated SQL acceptance.
- Combined frontend `npm run build`: PASS, 2,443 modules; existing large-chunk
  warning remains. `npm run lint`: PASS.
- Frozen build01 benefit workflow 517, usage 84, evidence 75, KPI 79, quota 19,
  recovery fulfilment 38, recovery work claims 79, outstation 65 and deployment
  policy boundaries 36: all 992 passed. Logs:
  `D:/CosmixVerification/part-c-parent-20261013/managed-e5e4919cd9514e9caaa0eef378284d08`.
- Synthetic three-page PDF generated by the actual exporter parsed successfully;
  all glyphs remained within print bounds, the final record and Unicode escapes
  were retained, and all three rendered pages were inspected without overlap or
  clipping. Output:
  `D:/CosmixVerification/part-c-parent-20261013/managed-234dae53d1c449dd9c91deda7bd99218`.
  Poppler warned about optional Symbol/ArialUnicode fonts; the built-in Courier
  content rendered. Escaped Unicode is not localized readable Urdu support.
- C01 initial Execute created only its reviewed private paths/keys/settings.
  Startup exited at `CurrentHrTestingHost.ConfigureServices` before `Build`,
  database verification, listening, login or business API calls. HTTP requests: 0.
  Its guard rejected the additional `ZoneClaimRetentionWorker` registered by
  Infrastructure, outside the six API business workers. The initial suspicion
  about a framework registration was incorrect; the guard correctly prevented
  an unreviewed retention worker from running. Original failure,
  start manifest, stdout/stderr and exited host state are retained privately.
  No database reset, cleanup, migration or automatic setup replay occurred.
- A narrowly reviewed `ResumeReviewedStartupFailure` mode requires the exact
  original guard failure, zero prior requests/checks, exited host, unchanged
  database/employee digest and private source ownership. It preserves existing
  keys/settings and original evidence, and writes separate `results/attempt02`.
  It cannot run after a completed/partially requested test or repeat attempt02.
  The reproduced guard correction passed the full pre-Build composition check
  plus 11 rejection cases and the original 122 checks. A separate API-only build,
  `hr-live-guard-build01`, passed with zero warnings/errors; all three application,
  infrastructure and domain DLL hashes match build01. The original frozen output
  was preserved.
- The explicit reviewed resume failed at its FIRST read-only SQL preflight:
  original process 24152 and pipe `LOCALDB#2E40033B` were no longer available.
  No attempt02 files, API host, login or business writes occurred. A normal start
  of the existing named `MSSQLLocalDB` instance produced process 21700 / server
  `Software-PC1\LOCALDB#F1D694CB`, but that instance has no registered Cosmix
  database. Connecting specifically to `Cosmix_Testing` failed. No MDF attach,
  database creation, reset, restore, cleanup or migration was attempted. This
  currently running named instance was left running, not stopped by the runner.
  The user was asked for the current working SSMS server/connection. The old
  verified database identity must not be assumed to describe this new process.
- Independent export review found a global-authentication lock held across
  expensive generation/IO, and retained-job authorization coupled to mutable
  report filters. These are being repaired before final report acceptance;
  build01 alone is not acceptance of that workflow.

Final results will follow actual execution. An implemented API or synthetic
passing test is not a checked live gate.

## Final Integrated Update

The earlier absent-catalog result was limited to the tools' redirected LocalDB
view. The user's normal Windows-session instance was subsequently identified by
its existing process arguments and owner, then queried through its actual pipe:
`Software-PC1\LOCALDB#F9CAED57`, PID 29836. `Cosmix_Testing` is ONLINE/MULTI_USER,
with the same retained family GUID and original MDF/LDF paths. See
[LocalDB diagnostics](LocalDB-Diagnostics-20261019.md). No recovery, attach,
replacement database or migration was necessary or performed. The earlier SSMS
startup failure's underlying cause is not established; current connectivity works.

The export review fixes are implemented and frozen:

- Optional prepared durable handlers capture source data before completion,
  render/store output outside the global authorization lock, and revalidate
  exact claim/job identity, lease, current permission and retained scope before
  atomic publication. Existing ordinary handlers remain compatible.
- Saved export authorization uses retained employee IDs, not mutable original
  status filters or newly matching unrelated employees. Replay/status/download
  still enforce current authority over original and generated resource scopes.
- Independent final static review found no actionable critical regression.
  Serializable source-row contention and real competing-request latency remain
  unverified. This is not zero-blocking or live concurrency acceptance.

Final integrated Release `hr-production-build02`: PASS, 0 warnings / 0 errors.
Log: `D:/CosmixVerification/hr-backend-20261019/hr-production-build02.log`.

Frozen build02 standalone checks passed:

| Suite | Checks |
| --- | ---: |
| Prepared durable jobs | 22 |
| Durable HR exports | 365 |
| Report boundary/service | 370 |
| Report repository/query translation | 250 |
| Report result projection | 204 |
| Retained overtime integration | 12 |
| XLSX ZIP/XML parsing | 81 |
| Registered MVC/OpenAPI contracts | 293 |

All 1,597 numbered checks passed, plus the durable route-surface check. OpenAPI
generation inspected 523 registered paths, not 523 completed workflows.
Evidence: `D:/CosmixVerification/part-c-parent-20261013/managed-52cf4b6dca4a4f2a9e83dc139c02b78d`.
Standalone compilation reports retained dependency reference-unification warnings;
these are separate from the zero-warning product build.

Shared-handler regression follow-up against the same frozen build02 also passed:
notification/outbox 78, missing-checkout reminders 44 and document-expiry
reliability 58. The document-expiry suite exercised the actual worker/repository
with synthetic relational IO, including batch boundaries and retry/cancellation;
it was not a live SQL worker run. Logs:
`D:/CosmixVerification/part-c-parent-20261013/managed-e20f15b4631d465788bc3d8cc51d6b21`.
Final build02 numbered total: 1,777 passed, plus the route-surface check.

No authenticated HR flow or real SQL concurrency test completed in this turn.
The C01 first attempt had zero HTTP requests, and its reviewed resume never passed
SQL preflight. The now-accessible original database does not retroactively turn
those attempts or the standalone checks into live acceptance.

## Integration File Groups

The checkout contains extensive pre-existing work. These are this continuation's
owned surfaces, not permission to stage or revert all working-tree changes:

- Vehicle module: `VehicleTripCorrectionContracts.cs`, `VehicleTripCorrections.cs`,
  attribution/workflow/monthly-adjustment/policy helpers and `VehicleFuelControllers.cs`.
  Exact contracts and limits: [vehicle corrections](Vehicle-Legacy-Corrections-20261019.md).
- Rich HR reports: Application `HR/Reports` export service/contracts/authorization/
  renderers/registration, Infrastructure HR report repositories/registration,
  `HrReportsController.cs`; [integration contract](HR-Durable-Export-Contract-20261019.md)
  and [verification](HR-Durable-Export-Verification-20261019.md).
- Shared durable jobs: `DurableJobContracts.cs`, `DurableJobProcessor.cs`,
  `DurableJobRepository.cs`; `NotificationTemplates.cs` retains the existing
  report template ID with a generic HR-compatible title.
- Site facts: `SiteOperationalContracts.cs`, `SiteOperationalRepository.cs`,
  `SitesController.cs` and the focused standalone verification.
- Local acceptance: `CurrentHrTesting*` guarded profile files, minimal
  `LocalTesting.cs` wiring, `Invoke-HrAcceptanceC01.ps1`. Original profiles and
  partially executed evidence are preserved. No migration files added or edited
  by this continuation.
- Report frontend: `HrReportsPage.tsx`, `reports-api.ts`, `report-export-jobs.ts`,
  `HrReportExportJobs.tsx`, and its frontend verification note. No live browser
  acceptance or separate React Native implementation is claimed.
- Standalone verification and this ledger: prepared jobs, durable exports,
  PDF rendering, report queries/results/contracts, vehicle/site cases and
  diagnostic evidence. No backend test project was created.

Remaining gates: authenticated HR/SQL atomicity and concurrent requests on the
verified target; real encrypted export worker/restart/storage-failure acceptance;
business configuration and separately approved consumed-benefit recovery policy;
documented unsupported legacy/consumed vehicle cases; deferred face/SMS/legal
activation and readable localized PDF acceptance. No salary payment was released.
