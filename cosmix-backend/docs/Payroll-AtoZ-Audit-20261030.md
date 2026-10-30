# Payroll and Finance source audit — 30 October 2026

This is a source and test-coverage audit of the active `cosmix-backend` project. It is not company payroll acceptance, legal approval, a bank approval, a payment instruction or a production-readiness certificate. Existing user changes were preserved. No company data, real payments, migrations or live bank calls were performed for this audit. Test execution is coordinated by the parent audit; results must be recorded separately from test existence.

## Blocking defect: provisional historical and parallel payroll

The newly added historical/parallel services were registered in Application/Infrastructure DI and exposed by `PayrollReconciliationController`. They were not merely unused examples.

The original `Infrastructure/Repositories/HR/PayrollReconciliationRepository.cs` did **not** compare an approved payroll run:

- It selected currently active employees, omitting historical leavers and failing to pin an approved run or historical population.
- It summed salary components without approval state, successor, frozen attendance, rate basis or proration validation. Actual base compensation is stored in `CompensationRecords`; falling back to the first fixed earning could count the same allowance twice.
- It invented EOBI of 300 and a 5% tax above 50,000, set SESSI/OT to zero, and represented those guesses as ERP figures. These are not approved statutory or payroll rules.
- It retained parallel runs in a static process-memory dictionary. Restart loses approvals; independent read/version-check/save calls do not provide atomic compare-and-swap, audit, immutable evidence or idempotency.

Further service defects remain behind the availability gate and must be resolved before any enabling change:

- `HistoricalPayrollReconciliationService.ParseCell` converts invalid/missing financial input to zero; multiple employee aliases can bypass raw-code duplicate detection. Optional columns and deduction/statutory meanings need an explicit mapping contract.
- `ParallelPayrollService.HrReviewAsync` marks all variances explained on approval and permits client replacement of financial variance records without validating exact source membership/amounts. The initiated run ignores `ErpBatchId`.
- Reviewer independence is based only on account strings, not the underlying employee. The services do not use the established current web/MFA/All-scope transaction and protected evidence pattern. Finance booleans are client assertions, not ledger reconciliation.
- The controller has an account-ID fallback to IP/`anonymous_actor`; this must not become approval authority when the workflow is implemented.
- A plain SHA-256 summary digest is not durable tamper-protected approval evidence and excludes detailed variances.

Remediation is intentionally fail-closed: the production repository must reject historical reads and parallel get/list/save operations with `503 payroll_reconciliation_workflow_not_available`. Existing routes and permission attributes remain, and the API exception handler maps that code to ProblemDetails. The invented financial calculations and volatile approval dictionary are removed, while the provisional contracts and isolated algorithm tests are retained for later implementation. No migration or shared DI change is required. No frontend consumer of these provisional historical/parallel routes was found in the active HR web module.

`PayrollReconciliationAvailabilityTests` supplies four production-repository cases and five actual controller/service/repository cases. These exercise the absence of fake financial/approval success; they are not HTTP authorization or real historical acceptance. RED/GREEN evidence is maintained by the parent runner. Existing `HistoricalPayrollHblAcceptanceTests` and `ParallelPayrollSignOffAcceptanceTests` use supplied synthetic repositories: their names must not be interpreted as bank acceptance or durable production sign-off coverage.

## Scope and traceability

| Area | Production source and existing coverage | Practical limit / follow-up |
|---|---|---|
| Effective salary | `HR/Compensation` holds append-only approved compensation; `PayrollSalaryProjection` consumes the exact retained attendance compensation for each day. `PayrollSalaryCalculationTests`/`PayrollSalaryProjectionTests` cover mid-month changes and joins/leaves, gross reconciliation and final rounding. | Configuration coverage is not entitlement. Do not sum allowances as base pay. Add explicit leap-February/month-length boundary cases if extending calculation rules. |
| Monthly/daily/hourly | `PayrollSalaryCalculation` uses exact rational arithmetic; monthly salary retains separate attendance deductions; daily/hourly use capped retained payable time. `PayrollSalaryAttendanceTests` covers paid leave, unpaid leave and avoiding a second wage-loss deduction. | Policy remains explicitly approved data. No generic pay rate or divisor may be guessed from the calculator. Existing focused cases primarily use October. |
| Import | `PayrollImportService` uses protected source hashes, explicit column mappings, current source permissions, canonical pending requests and preview fingerprints. Parser tests cover CSV quoting/Unicode, formula/DTD/external-link rejection and limits. Workflow tests cover duplicate rows/source, stale employee version, authorization and idempotency. | Import submission does not approve salary. Workflow unit fixture covers components; compensation/service/persistence integration must be assessed separately. Uploaded real sheets and historical validation remain company acceptance. |
| Salary approval/return | `PayrollSalaryLifecycle` and handoff services retain frozen salary with distinct HR, management and Finance stages. Lifecycle tests cover beneficiary review through another account, maker independence, invalid ordering, rejection, cancellation and return preservation/retry. | Return is only supported before management lock/processing; it does not rewrite posted salary or reopen locked attendance. Production concurrency requires SQL evidence, not only proxy tests. |
| Own payslip | `PayslipService`, settlement readers and frozen reader restrict employee membership before loading protected sources. Tests cover PDF/XLSX totals, another employee's amounts, proof-required paid status, mismatched totals and overpayment. | Own document tests use synthetic stores; generated bytes alone do not establish browser/mobile/download access or real payment. |
| Statutory | Approved employee/employer entries are validated against protected source evidence; Finance retains separate employer expense/payable. `PayrollFinanceIntegrityTests` and `PayrollFinanceSourceTests` cover header-only/legacy rejection, exact totals, statutory account mapping, partial employee groups and altered proof. | Company/legal decisions and actual rates/effective coverage remain authoritative configuration. No hardcoded statutory defaults are acceptable. |
| Salary accounting | `PayrollAccountingService`, `PayrollFrozenBatchReader` and posting readiness validate immutable salary, period, independent profiles and accounts. Tests cover missing evidence, one-cent mismatch, closed period, wrong mapping/kind and duplicate source posting. | Loan, statutory and attendance sources must remain traceable; database/persistence tests supplement service tests. |
| Payment lifecycle | Accounting/payment services distinguish pending/posted/confirmed/reversed/failed. Tests cover retained confirmation, reversal ordering, over-reservation, wrong employee/accrual, independent actors and proof tampering; concurrency test serializes at obligation level. | Posted/exported is not paid. Real confirmation evidence and bank execution remain human/bank operations. |
| Bank export | Approved templates plus verified protected bank data and exact source fingerprint; export tests cover expiry, changed beneficiary data, unverified/stale accounts, formulas and wrong membership/accrual. | Supplied HBL template checks are file-layout tests, not bank acceptance. Actual routing, beneficiary bank validation and HBL acceptance remain deferred. |
| Trial balance/ledger | `FinanceLedgerService`, SQL view and trial-balance projection validate dates, pair completeness, currency separation, duplicate sources and balances. Source labels now derive from selected journals. `FinanceLedgerTests` and SQL integration cover opening movements and project/site costs. | The only view source is Payroll. Current projection explicitly requires two legs per source journal; a future compound-journal integration needs an approved representation and tests. |
| Project/site payroll costing | `PayrollCostService`/source reader use protected independent plans and exact payroll expense, with cent-preserving allocation and unallocated reporting. Workflow tests cover changed intent, closed period, stale review and own approval. | These distribute payroll expense; they do not create purchasing/inventory/project revenue accounting. |
| Bank reconciliation | Statement import/proposal/independent close retain evidence, exact amount/reference matching and ledger fingerprints. SQL tests cover backdated postings and closed-month update/delete guards. | Repository reads `PayrollJournalEntries` directly. `OutsidePayrollReason` explains unmatched non-payroll rows but does not post or verify company non-payroll transactions. Month close is not company-wide bank/GL reconciliation. |
| Permissions/readiness | Sensitive operations, source authorization expiry tests and HTTP contract tests cover current grants, MFA, mobile denial and employee scope. Company configuration readiness now checks actual compensation, schedules, whole-month PKR posting profile/accounts and verified bank coverage. | Current active approved employees define configuration population; historical leavers and final payroll eligibility are separate. A green setup report is not approval or acceptance. |

## Company-wide Finance is not implemented by a payroll-only view

`20261028154436_ExposeSharedFinanceLedger` creates `finance.GeneralLedgerEntries` solely from `hr.PayrollJournalEntries`, projecting debit and credit legs with SourceType `Payroll`. `FinanceLedgerRepository` reads that view. `BankReconciliationRepository` reads the payroll journals themselves. Source enumeration under active `src` found no sales/customer invoice, supplier/purchase, AP/AR, inventory valuation or general expense posting module feeding either path.

The current shared ledger is useful, but not company-wide integrated accounting. Adding source names to a response cannot supply absent journals. Before expansion, the company must identify the authoritative upstream modules and transactions, approve chart mappings and posting/reversal/period rules, and provide reconciled opening balances and sample transactions. Do not import the separate `Cosmix_new`/mock project as if it were the active backend or manufacture accounting workflows from folder names.

## Test inventory and targeted execution

The existing `tests/Cosmix.Tests/Cosmix.Tests.csproj` targets .NET 10 and references all four production projects. No new project is introduced. The main solution's four-project build does not by itself discover or run these tests.

Fast focused commands from the repository root (the parent runner may supply an isolated `--artifacts-path` and exact output location):

```powershell
dotnet test cosmix-backend/tests/Cosmix.Tests/Cosmix.Tests.csproj -c Release --filter "FullyQualifiedName~PayrollReconciliationAvailabilityTests" --logger "trx;LogFileName=payroll-reconciliation-availability.trx"
dotnet test cosmix-backend/tests/Cosmix.Tests/Cosmix.Tests.csproj -c Release --filter "FullyQualifiedName~Payroll|FullyQualifiedName~FinanceLedgerTests|FullyQualifiedName~BankReconciliation" --logger "trx;LogFileName=payroll-finance-focused.trx"
```

Do not count skipped SQL cases as executed. These gates require exact authorized targets and have side effects:

| Test group | Environment / behavior |
|---|---|
| `PayrollSqlPersistenceTests`, `PayrollPaymentConcurrencySqlTests` | `COSMIX_PAYROLL_VERIFICATION_CONNECTION`; explicitly disposable verification database; persistence/transactions/trigger/concurrency checks. |
| `PayrollMonthAcceptanceTests` | `COSMIX_PAYROLL_ACCEPTANCE_CONNECTION`; exact specifically authorized one-employee month database/workbook. |
| `PayrollPaymentsAcceptanceTests` | `COSMIX_PAYROLL_PAYMENTS_CONNECTION`; exact prepared payment clone and copied private evidence; never substitute the original month. |
| `PayrollFinanceSqlIntegrationTests` | `COSMIX_FINANCE_SQL_CONNECTION`; selected LocalDB master, creates/deletes its own unique synthetic database and applies real migrations. Optional `COSMIX_FINANCE_SQL_EVIDENCE`. |

The separately recorded fresh Finance SQL integration established four payroll/contribution journals, eight legs and equal debits/credits of PKR 34,500; allocation 20,700 project/site + 13,800 general costs. It made no payment. Preserve its evidence as that bounded synthetic result, not real bank or company acceptance.

Legacy standalone verification sources are not automatically executed by `dotnet test`:

- `docs/attendance-policies/VerifyPayrollInputs.cs`, `VerifyPayrollInputQueries.cs`, `VerifyOfficePayrollSources.cs`, `VerifyOfficePayrollJurisdictionRegistration.cs`.
- `docs/attendance-completion/VerifyPayrollReconciliation.cs`, `VerifyPayrollInputService.cs`, `VerifyPayrollInputModel.cs`, `VerifyPayrollAttestations.cs`.
- `docs/hr-srs-remediation/VerifyPayrollAdjustmentTotals.cs`, `VerifyLoanAndPostingBoundaries.cs`, `VerifyConfirmedLoanPosting.cs`, `VerifyEncashmentValuation.cs`, `VerifyEncashmentWorkflow.cs`, and the benefit/recovery suites.
- `docs/access-provisioning/VerifyHrDashboardPayroll.cs`.

Use each retained harness's documented compilation/runner against the exact current Release output; do not assume old assembly paths or historical pass counts prove this checkout. `scripts/Compare-PayrollMonth.py` is a comparison utility, not an approved historical input or a bank run. The readiness scripts report configuration and do not grant activation authority.

## Remaining acceptance

Historical payroll and HBL acceptance (Task 11) remain deferred. An actual company parallel month requires approved source sheets, historical employee population and rates, retained attendance and source policies, verified statutory entries, account mappings, bank evidence and independent human HR/Finance/management decisions. The provisional workflow cannot supply those facts. Real employee/device, provider and company sign-off are outside synthetic source/test proof.
