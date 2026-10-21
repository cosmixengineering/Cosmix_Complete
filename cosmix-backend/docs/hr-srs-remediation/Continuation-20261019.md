# Backend Continuation - 19 October 2026

## Outcome and Boundaries

Fresh Release build passes with zero warnings and errors. The bounded checks below
execute actual application/domain/repository or HTTP-component code with synthetic
collaborators. They are not authenticated live SQL, real encrypted-storage restart,
browser/mobile, external OTP or biometric acceptance. Full backend acceptance is
not achieved. No completion percentage is inferred from assertions or route counts.

No database was connected, created, seeded, reset or migrated during this turn.
No API host, real provider, payment, permanent configuration or Git write was used.
Existing dirty changes, migration sources and the already-dirty snapshot were kept.
The A02 packet is historical and has not been rerun or regenerated.

## Fixes in This Turn

1. `PayrollHandoffService.ScheduleAllowanceAsync` rejected every outstation
   deployment decision because it correctly contained protected review evidence,
   unlike the older meal-claim decision. Three new regression scenarios reproduced
   this integration failure. The service now uses the existing DeploymentService
   validator for the exact approved claim, parent, calculation, immutable
   submission and Admin decision. Missing validator or damaged evidence fails
   closed. The generic evidence check is not removed for meal claims.
2. Finance scheduling now also binds exact source type, claim/employee identity
   and submission version. A stale version cannot schedule a newer claim. Valid
   lock and same-key retry create exactly one InPayrollHandoff record, not payment.
3. Fixed a compiler error in the unexecuted financial carry-forward test's async
   failure-path lambda. The suite now executes and passes its 66 checks.
4. Expanded generated OpenAPI checks to include deployment/home-break/claim and
   financial adjustment routes, CSRF, idempotency and documented failure responses.

No business rate, entitlement, legal authority, salary release or paid state was
invented. The 40-hour outstation example remains pending without complete approved
counting/partial-day/rounding configuration.

## Executed Verification

Candidate Release:
`D:\CosmixVerification\hr-backend-20261019\build\bin\Cosmix.Api\release`

Fixed Release, including the Finance integration repair:
`D:\CosmixVerification\hr-backend-20261019\fixed-build\bin\Cosmix.Api\release`

Both builds passed with zero warnings/errors. The first build process lost its
execution session before completion and is not counted as a pass; a fresh serial
build completed successfully. Frozen artifacts were not overwritten while used.

| Standalone suite | Result | Build |
| --- | --- | --- |
| VerifyFinancialCarryForward | 66 checks passed | Fixed |
| VerifyMonthLocks | 85 checks passed | Fixed |
| VerifyPayrollAdjustmentTotals | 5/5 | Fixed |
| VerifyConfirmedLoanPosting | 68 checks passed | Fixed |
| VerifyLoanAndPostingBoundaries | 31/31 | Fixed |
| VerifyOutstationAllowances | 65 scenarios passed, zero failed | Fixed |
| VerifyEssLeaveConsistency | 37/37 | Fixed |
| VerifyEssFinancialBoundary | 21/21 | Fixed |
| VerifyOtFinalizationBoundary | 19/19 | Fixed |
| VerifyPayrollInputService | 102 checks passed | Fixed |
| VerifyPayrollAttestations | 195 checks passed | Fixed |
| VerifyAttendanceComposition | 46 checks passed | Fixed |
| VerifyRemediationContracts | 157/157; 483 generated paths | Fixed |
| InspectContinuationModel | 25 migrations; zero model operations; zero SQL attempts | Fixed |
| VerifyLeaveRecoveryReplay | 51/51 | Candidate |
| VerifyEncashmentWorkflow | 30/30 | Candidate |
| VerifyEncashmentValuation | 80/80 | Candidate |
| VerifyVehicleEntitlements | 46/46 | Candidate |
| VerifyVehicleRepository | 35/35; zero SQL attempts | Candidate |
| VerifyApiExceptionDisclosure | 78 checks passed | Candidate |
| VerifyAuthenticationCancellation | 72 checks passed | Candidate |
| VerifyWebBoundary | 70 checks passed | Candidate |
| VerifyHrReportXlsx | 81/81, generated ZIP/XML parsed | Candidate |
| VerifyHrReportBoundary | 342 checks passed | Candidate |
| VerifyHrReportResults | 204/204 | Candidate |
| VerifyHrReportsRemaining | 398/398 | Candidate |
| VerifyHrReportQueries | 242/242; offline query translation | Candidate |
| VerifyHrReportOvertime | 12/12 | Candidate |
| VerifyDocumentExpiryReliability | 58/58; synthetic relational/outbox/worker checks | Candidate |

Candidate-only suites exercise source files unchanged by the subsequent narrowly
scoped PayrollHandoffService repair. Affected Finance, composition and OpenAPI
checks were rerun against the fixed build. Standalone OpenAPI/SqlClient compilation
reports existing CS1701 framework-reference unification warnings; product builds
have zero warnings. These are component tests, not signed-token/network acceptance.
All listed invocations completed with exit code 0. The longer reminder suite also
completed; no background verification process was left running. A focused separate
read-only review of the Finance/deployment repair returned no findings.
`git diff --check` passed; Git only reported the existing snapshot line-ending
normalization warning. That snapshot was not edited by this turn.

The authentication checks prove request cancellation propagates without granting
access and unexpected exception details are not returned publicly. They do not
prove the user's running Visual Studio/browser session or its database is fixed.

### Evidence Locations

All directories below are under
`D:\CosmixVerification\part-c-parent-20261013\` and contain source copies,
compiler responses, logs and frozen dependency hashes in `inputs.json`.

| Directory | Evidence |
| --- | --- |
| managed-94a3035c2b7a42c1b32d766de329d4eb | Initial financial test compilation failure |
| managed-3de02d66a008426da523aa2e8a247e85 | Candidate financial/month-lock/model checks |
| managed-105625fcd6744cf4baeb7d1dc5fa8670 | Candidate outstation, leave, vehicle and earlier OpenAPI checks |
| managed-c25fc579e8a14db4af587bfa2ed7dac2 | Authentication cancellation/disclosure/web-boundary checks |
| managed-c209967fbf804ebf84282a3bd9d4eda9 | Three failing Finance/deployment regression scenarios before repair |
| managed-b6dbc05a9bfe4088b55143f279593997 | Fixed Finance/deployment, ESS, OT, payroll, composition and expanded OpenAPI checks |
| managed-aa3933d1cd9f49df989842e38dc363ce | Fixed carry-forward, month-lock, totals and model checks |
| managed-bbfbda092be3415e9086652e0dfd3661 | Report/XLSX/value/query/OT and reminder reliability checks |

Commands use the existing standalone harness, not a new test project:

```powershell
dotnet build Cosmix.Backend.sln -c Release `
  --artifacts-path D:/CosmixVerification/hr-backend-20261019/fixed-build `
  -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -tl:off

& ./docs/attendance-gap-c/VerifyManaged.ps1 `
  -ReleaseDirectory 'D:\CosmixVerification\hr-backend-20261019\fixed-build\bin\Cosmix.Api\release' `
  -Sources @('docs/hr-srs-remediation/VerifyOutstationAllowances.cs',
    'docs/hr-srs-remediation/VerifyConfirmedLoanPosting.cs',
    'docs/hr-srs-remediation/VerifyLoanAndPostingBoundaries.cs',
    'docs/hr-srs-remediation/VerifyEssLeaveConsistency.cs',
    'docs/hr-srs-remediation/VerifyEssFinancialBoundary.cs',
    'docs/hr-srs-remediation/VerifyOtFinalizationBoundary.cs',
    'docs/attendance-completion/VerifyPayrollInputService.cs',
    'docs/attendance-completion/VerifyPayrollAttestations.cs',
    'docs/attendance-completion/VerifyAttendanceComposition.cs',
    'docs/hr-srs-remediation/VerifyRemediationContracts.cs')
```

## Still Pending, Not Hidden Behind a Passing Build

- **Financial edge cases:** former employees without authoritative target-month
  inputs, cross-month late-reset recalculation, special reviewed-work corrections
  and histories exceeding the current 31 factual-ID bound remain explicit
  dependencies. The actual SQL financial source-reader graph and full authenticated
  Preview -> Approve -> Posting -> later correction -> month lock need acceptance.
- **Benefits/recovery:** audited commitments exist; qualifying recovery work,
  partial fulfilment/correction, benefit election/exclusivity, leave credits and
  monetary conversion are not a completed workflow. Configurable policy support
  must be extended with the required approved semantics, not assumed values.
- **Outstation:** configured deployment/home-break valuation and Admin claim
  decisions work in standalone checks; cross-policy trip valuation, WorkTravel,
  actual Finance preview/preparation/external settlement and live persistence still
  require work or acceptance. The new Lock regression is not that entire flow.
- **Vehicles:** configured eligibility, workshop, milestone and incident proposal
  checks pass. Policy publication/provisioning, historical cycle transitions,
  monthly adjustment/assignment precedence, amended incidents and settlement are
  not completed by these checks.
- **Legal:** authority is still proposed. Signed applicable jurisdiction/worker/
  rule evidence and formal activation approval are missing. Financial penalties
  remain unavailable where unverified; no role toggle can certify them.
- **Operational acceptance:** no current authenticated SQL/HTTP, independent
  connection concurrency, actual encrypted-storage failure/restart, restore/load
  or client-owner UAT has run for these changes. Migration application remains
  prohibited by the newest request. Existing databases are not assumed synthetic.
- **Providers:** real face/model/device and company SMS-provider acceptance remain
  unavailable/deferred. No synthetic success is production approval.

## Next Work Order

1. Cover the financial source-reader/factual evidence graph and remaining financial
   edge cases, retaining exact settlement provenance and immutable history.
2. Finish configurable benefit/recovery fulfilment and unsupported outstation/
   vehicle transitions in their owning modules. Do not invent missing business
   semantics or disguise blocked transactions as complete.
3. When authorized separately, review the actual isolated database target and
   pending migration range, then execute bounded authenticated SQL, storage,
   concurrency and restart scenarios. No setup is performed under this report.
4. Formal legal/provider approvals and owner UAT precede full acceptance. Frontend
   owners may use the verified contracts but must not assume pending workflows
   are production ready.

## Files Changed on 19 October

- src/Cosmix.Application/HR/PayrollHandoff/PayrollHandoffService.cs
- docs/hr-srs-remediation/VerifyFinancialCarryForward.cs
- docs/hr-srs-remediation/VerifyOutstationAllowances.cs
- docs/hr-srs-remediation/VerifyRemediationContracts.cs
- docs/hr-srs-remediation/G2-Deployment-Integration-20261015.md
- docs/hr-srs-remediation/Plan-Progress-20261015.md
- docs/hr-srs-remediation/Verification.md
- docs/SRS-Module-Map.md
- docs/sql/README.md
- This report

Earlier dirty files are not attributed to this turn. Migration
`20261015144424_AddFinancialAdjustmentsAndOutstationDeployments` was already
prepared; no migration was created, edited or applied on 19 October.
