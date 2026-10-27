# Final Acceptance Close-Out Evidence Package — HR, Attendance and Payroll Compliance

**Repository:** `Cosmix_Erp_software`  
**Base Commit:** `f3305cd3bf07a7b703ecf043f6f0527285a79c1d`  
**Branch:** `feature/hr-payroll-compliance`  
**Execution Environment:** Windows 11 / .NET 10.0.12 / ASP.NET Core / Microsoft SQL Server `(localdb)\MSSQLLocalDB` (Database: `Cosmix_Testing`) / Node.js v22.14.0 / Vite 8.3.2 / React Native 0.87.1  
**Audit Date:** 27 October 2026  

---

## 1. Executive Summary & Release Verdict

This package delivers verifiable runtime execution artifacts, test run telemetry (`acceptance_tests.trx` and `all_compliance_tests.trx`), sanitized HTTP middleware logs, SQL Server reconciliation JSONs, migration records, and reviewable source files for all HR, Attendance, and Payroll compliance workstreams.

### Acceptance Summary Table

| Scenario ID | Workstream / Scenario | Execution Target | Verdict | Primary Evidence File |
|---|---|---|---|---|
| **SCEN-01** | Exact Tested Version & Hashes | Git Repo & File System | **PASS** | `version/file_hashes.sha256`, `version/working_tree.patch` |
| **SCEN-02** | Real HTTP Middleware Authorization, MFA/Scope & CSRF | ASP.NET Core TestServer + JWT Handler + CSRF Middleware | **PASS** | `http_captures/*.json` |
| **SCEN-03** | Real SQL Server Payroll, Limits, Second-Person & Case E Rollback | Microsoft SQL Server (`Cosmix_Testing`) | **PASS** | `sql_reconciliation/*.json` |
| **SCEN-04** | Historical Data Upgrade, Retention Policy & Hosted Worker Execution | Microsoft SQL Server (`Cosmix_Testing`) | **PASS** | `retention_upgrade/*` |
| **SCEN-05A** | Web Compliance Workflows & State Persistence | React 19 / Vite Web Client | **BROWSER_ACCEPTANCE_PENDING** (Components, types, build, and API integration verified; automated browser run pending browser environment) | `ui_workflows/web_workflow_evidence.json` |
| **SCEN-05B** | Native Mobile Runtime Execution | Android Virtual Device / Physical Phone | **BLOCKED** (No device/emulator provisioned) | `ui_workflows/native_mobile_execution_status.json` |
| **SCEN-05C** | Corporate Bank Host-to-Host Export | Direct Host-to-Host Bank Protocol | **BLOCKED** (Awaiting bank partner specifications) | `ui_workflows/bank_export_status.json` |
| **SCEN-06** | Disposable Database Migration & Statutory Verification | Isolated SQL Server DB (`Cosmix_Disposable_*`) | **PASS** | `retention_upgrade/08_disposable_database_migration_verification.json` |

---

## 2. Tested Version Identification & Reviewable Source

- **Commit Baseline:** `f3305cd3bf07a7b703ecf043f6f0527285a79c1d`
- **Working Tree Patch:** Stored in full in `version/working_tree.patch`
- **File Integrity Hashes:** All modified and untracked implementation and test files are indexed with SHA-256 in `version/file_hashes.sha256`.
- **Status Summary:** `version/status.txt` records all porcelain status entries.
- **Reviewable Source:** The complete set of tested source files is directly included in `acceptance_package/reviewable_source/` for independent review.

---

## 3. Real HTTP Middleware Authorization & CSRF Evidence

Executed against ASP.NET Core `TestServer` utilizing real `JwtBearerHandler`, `AuthorizationMiddleware`, `CurrentPermissionHandler`, `WebCsrfMiddleware`, and `CosmixDbContext`:

1. **Unauthenticated Denial (`401 Unauthorized`)**:
   - `GET /api/v1/me/location-consent`
   - `POST /api/v1/admin/location-consent/policy`
   - `POST /api/v1/payroll-accounting/obligations`
   - `POST /api/v1/statutory-contributions`
   - All returned `401 Unauthorized` with `WWW-Authenticate: Bearer` response header. Captured in `http_captures/01_unauthenticated_denials.json`.

2. **Permission & MFA / Client-Scope Denial (`403 Forbidden`)**:
   - Token without `location.consent.admin` denied access to `POST /api/v1/admin/location-consent/policy`.
   - Token without `payroll.accounting.accrue` denied access to `POST /api/v1/payroll-accounting/obligations`.
   - Mobile client scope attempting sensitive management write denied access.
   - Non-MFA authenticated tokens denied access to sensitive financial endpoints. Captured in `http_captures/02_permission_and_mfa_denials.json`.

3. **Authorized Success (`200 OK`)**:
   - Authorized administrative token published location tracking notice.
   - Location notice verified published with incremented version and `IsActive: true`. Captured in `http_captures/03_authorized_successes.json`.

4. **Zero Database Changes on Denied Writes**:
   - Database record counts before denied requests and after denied requests strictly verified equal. Captured in `http_captures/04_no_db_changes_from_denied_writes.json`.

5. **Ordinary Employee Consent Journey**:
   - Employee token fetches notice via `GET /api/v1/me/location-consent`.
   - Employee accepts notice via `POST /api/v1/me/location-consent/accept` with device metadata.
   - Identity binding strictly enforced from JWT claims (`sub` / `employee_id`); server timestamp applied.
   - Re-fetching status shows `Status: Consented`. Captured in `http_captures/05_ordinary_employee_consent_flow.json`.

6. **Ordinary Employee Administrative Publish Denial**:
   - Ordinary employee attempting administrative policy publication `POST /api/v1/admin/location-consent/policy` rejected with `403 Forbidden`. Captured in `http_captures/06_ordinary_employee_admin_denial.json`.

7. **Web Cookie CSRF Enforcement vs Mobile Bearer Tokens**:
   - A POST request bearing browser session cookie `__Host-Cosmix.Access` without an anti-forgery CSRF token is intercepted by `WebCsrfMiddleware` and rejected with `400 Bad Request` (`invalid_csrf_token`).
   - A direct Bearer token request (mobile app or API integration) without cookies bypasses browser CSRF checks and authenticates cleanly (`200 OK`). Captured in `http_captures/07_web_cookie_csrf_enforcement.json`.

---

## 4. Real SQL Server Payroll Demonstration (`Cosmix_Testing`)

Executed on Microsoft SQL Server against database `Cosmix_Testing`:

1. **Synthetic Fixture Setup**:
   - **Employee A**: Base Gross 100,000.15 PKR, Deductions: EE EOBI 1,000.10, EE SS 500.05, Loan repayment 2,000.00, Employer EOBI 5,000.20, Employer SS 3,000.10 → **Net Salary: 96,500.00 PKR**.
   - **Employee B**: Base Gross 80,000.25 PKR, Deductions: EE EOBI 800.10, EE SS 400.05, Loan repayment 1,500.00, Employer EOBI 4,000.10, Employer SS 2,400.20 → **Net Salary: 77,300.10 PKR**.
   - **Aggregate**: Total Gross: 180,000.40 PKR, Total Net Payable: 173,800.10 PKR.

2. **Resolution of 6,200.30 Reconciliation Discrepancy**:
   - Net Salaries Payable (Account 2010) is credited with **TotalNetAmount** (`173,800.10 PKR`), not gross salary.
   - Accrual deductions are posted to itemized liability/clearing accounts:
     - Employee EOBI Payable (Account 2020): `1,800.20 PKR` credit
     - Employee Social Security Payable (Account 2030): `900.10 PKR` credit
     - Staff Loans Clearing (Account 1050): `3,500.00 PKR` credit
   - Gross Salaries Expense (Account 5010): `180,000.40 PKR` debit. Total debits equal total credits (`180,000.40 PKR`).
   - When disbursement payments debit Account 2010 by `173,800.10 PKR`, the ending balance of Account 2010 is **strictly 0.00 PKR** (Credits: 173,800.10, Debits: 173,800.10, Outstanding: 0.00).
   - Real statutory GL journals (Kind: `StatutoryContribution`) posted:
     - EOBI Employer Contribution: Debit 5020 `9,000.30`, Credit 2020 `9,000.30`
     - PESSI Employer Contribution: Debit 5030 `5,400.30`, Credit 2030 `5,400.30`
   - Total employment costs (5010 + 5020 + 5030): **194,401.00 PKR**. Total liabilities reconciled before remittance:
     - Account 2010 (Net Salary Liability): `0.00 PKR` ending balance after payments
     - Account 2020 (Total EOBI Liability): `10,800.50 PKR` (1,800.20 EE + 9,000.30 ER)
     - Account 2030 (Total Social Security Liability): `6,300.40 PKR` (900.10 EE + 5,400.30 ER)
     - Account 1050 (Staff Loan Principal Reduction): `3,500.00 PKR`
   - Captured in `sql_reconciliation/01_payroll_accrual_journal.json`, `02_statutory_postings.json`, `03_combined_liability_reconciliation.json`, and `07_finance_confirmations.json`.

3. **Rejection of Unallocated Payments**:
   - `RecordSalaryPaymentAsync` strictly enforces a non-empty `EmployeeId`. Requests with `EmployeeId: null` are rejected with `InvalidOperationException("Salary payment must specify a valid EmployeeId. Unallocated or batch-level payments are not permitted.")`.

4. **Case A: Overpayment Rejection**:
   - Attempted disbursement of 100,000.00 PKR for Employee A (whose net salary is 96,500.00 PKR) rejected with `InvalidOperationException`.
   - Verified 0 mutations occurred in `hr.SalaryPayments` or `hr.PayrollJournalEntries`. Captured in `sql_reconciliation/04_employee_a_overpayment_rejected.json`.

5. **Case B: Valid Payment & Second-Person Enforcement**:
   - Valid payment of 96,500.00 PKR recorded for Employee A in `Posted` state.
   - Creator of payment (`hr-preparer`) attempted to confirm payment: rejected with `"The creator cannot confirm their own payment disbursement."`
   - Independent authorized Finance officer (`finance-confirmer`) confirmed payment: transitioned to `Confirmed` state. Captured in `sql_reconciliation/05_employee_a_valid_payment.json`.

6. **Case C: Installments & Replay Protection**:
   - Employee B net salary (77,300.10 PKR) disbursed in two installments:
     - Installment 1: 40,000.00 PKR recorded.
     - Reference replay (`PAY-B-202610-PART1`) rejected with duplicate reference error.
     - Excessive installment 2 (40,000.00 PKR, exceeding remaining 37,300.10 PKR) rejected.
     - Exact remaining installment (37,300.10 PKR) successfully recorded.
   - Captured in `sql_reconciliation/06_employee_b_installments.json` and `07_finance_confirmations.json`.

7. **Case D: Subsequent Month Obligation**:
   - November 2026 batch finalized and processed for Employee A without collision or cross-period bleed. Captured in `sql_reconciliation/08_subsequent_month_obligation.json`.

8. **Case E: Real Application Concurrency, Injected Failure & Complete Rollback**:
   - Concurrent duplicate payment request on independent connection rejected with replay protection.
   - Physical database write flushed inside open transaction before injecting failure (`AddSalaryPaymentAsync` throwing).
   - SQL Server transaction was automatically rolled back via `await using var transaction` in `DbTransactionCore`.
   - Independent secondary SQL connection verified count of staged payments and journals equals **0** (0 partial/orphaned records).
   - Clean retry succeeded with 0 orphaned records and full replay protection. Captured in `sql_reconciliation/09_case_e_concurrency_and_rollback.json`.

---

## 5. Historical Upgrade & Retention Worker Demonstration

1. **Migration Verification**:
   - Database schema migrated up to `20261022133924_AddComplianceWorkstreams` and `20261026193000_PersistStatutoryEmployeeItemsAndJournalLinks`. Compliance tables and indexes confirmed present on SQL Server. Captured in `retention_upgrade/01_database_migration_log.json` and `01_database_migration_log.txt`.

2. **Missing Configuration Fail-Safe**:
   - When `PunchRetention:RetentionDays` is unconfigured (null) or <= 0, `PunchRetentionService` throws `InvalidOperationException("PunchRetention:RetentionDays must be configured (tested assuming 180-day policy pending company confirmation).")`. 0 records touched or modified. Captured in `retention_upgrade/02_missing_config_failsafe.txt`.

3. **Configured Retention Cleanup (180-Day Rule)**:
   - Synthetic historical punch (>180 days old) and recent punch (within retention window) evaluated.
   - Historical punch Latitude and Longitude nullified; redaction audit row inserted in `att.PunchLocationRedactions`.
   - Recent punch coordinates preserved.
   - Factual attendance segment (20 minutes duration, start/end timestamps, employee identity, approval history) strictly preserved.
   - Minimal DTO export in `03_configured_retention_redaction.json` and `04_attendance_facts_preserved.json` excluding all employee personal information.
   - Idempotent rerun produced 0 additional redactions. Captured in `retention_upgrade/05_idempotent_rerun.json`.

4. **Retention Policy Parameterization**:
   - Documented in `retention_upgrade/06_approved_retention_policy.json` with explicit note: *tested assuming 180-day policy pending company confirmation*.

5. **Hosted Retention Worker Execution**:
   - Hosted background service `PunchRetentionWorker` demonstrated executing a complete cycle via `ExecuteCycleAsync(CancellationToken.None)` and verified lifecycle (`StartAsync` / `StopAsync`) with `PeriodicTimer` scheduling. Captured in `retention_upgrade/07_retention_worker_execution.json`.

---

## 6. Disposable Database Migration & Statutory Persistence (SCEN-06)

Executed in a completely isolated, dynamically generated SQL Server database (`Cosmix_Disposable_*`):
- Created disposable database on `(localdb)\MSSQLLocalDB` without touching shared or production environments.
- Applied baseline migrations up to `20261022133924_AddComplianceWorkstreams`.
- Inserted pre-upgrade `hr.StatutoryContributions` record.
- Applied target migration `20261026193000_PersistStatutoryEmployeeItemsAndJournalLinks`.
- Verified pre-upgrade record survived with intact amounts and schema defaults.
- Verified physical presence of new columns (`JournalEntryId`, `EmployeeItemsJson`, `HasEmployeeEntries`, `ObligationId` on `hr.StatutoryContributions`; `Scheme`, `StatutoryContributionId` on `hr.PayrollJournalEntries`) via `sys.columns`.
- Verified physical presence of new indexes (`IX_StatutoryContributions_JournalEntryId`, `IX_StatutoryContributions_ObligationId`, `IX_PayrollJournalEntries_StatutoryContributionId`) via `sys.indexes`.
- Created statutory contribution with individual employee items, disposed `DbContext`, reloaded from new `DbContext` to confirm persistence, executed second-person approval, posted to accounting, and generated RFC 4180 CSV export.
- Verified exact employee registration numbers and amounts in exported CSV.
- Disposed and cleanly dropped disposable database. Captured in `retention_upgrade/08_disposable_database_migration_verification.json`.

---

## 7. User Workflows and Platform Limitations

### Web Application Workflows (`BROWSER_ACCEPTANCE_PENDING`)
- Interactive controls and API integration validated:
  - **PayrollAccountingPage.tsx**: Beneficiary employee selection dropdown, installment amount input, and separate Finance confirmation actions.
  - **StatutoryContributionsPage.tsx**: EOBI, PESSI, SESSI contribution filing, approval, posting and RFC 4180 CSV export.
  - **PayrollHandoffPage.tsx**: Hardcoded fake IBAN fallbacks removed; bank advice decoupled from payment settlement.
  - Automated browser execution remains labeled BROWSER_ACCEPTANCE_PENDING pending environment configuration. Captured in `ui_workflows/web_workflow_evidence.json`.

### Native Mobile Platform Limitation (`BLOCKED`)
- Documented in `ui_workflows/native_mobile_execution_status.json`:
  - `adb devices` exited with code 1. No physical device or Android Virtual Device (AVD) is provisioned in this execution host.
  - Code-level static typing (`npx tsc --noEmit`) passes with 0 errors.

### Bank Host-to-Host Integration (`BLOCKED`)
- Documented in `ui_workflows/bank_export_status.json`:
  - Standard internal CSV bank advice export is fully implemented and tested.
  - Direct corporate host-to-host bank API / SFTP transport is marked **BLOCKED** pending corporate banking partner protocol specification (e.g. 1Link, HBL Corporate Connect, or Meezan Direct).

---

## 8. Test Results Summary

- **Acceptance Suite Tests**: `6 / 6 Passed` (100%) — `acceptance_tests.trx`
- **Total Test Suite**: `355 / 355 Passed` (100%) — `all_compliance_tests.trx`
- **Web Frontend Build**: `0 Errors` (Vite v8.3.2 production build in 1.54s)
- **Mobile TypeScript Static Check**: `0 Errors` (`npx tsc --noEmit` exited code 0)
