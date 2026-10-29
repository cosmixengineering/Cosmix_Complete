# Payroll deployment and historical comparison evidence — 28 October 2026

This is an operational checklist and repeatable comparison contract for tasks 27–30. It does not certify a production deployment, real employee month, bank format, physical-phone acceptance, biometric calibration or company sign-off. The actual historical salary sheet and bank-approved operating profile remain pending. Supplied HBL template definitions are handled by the bank-export module; a template sample alone is not an approved company profile or a successful bank submission.

## What was actually inspected

Evidence is retained outside Git in `D:/CosmixPayrollVerification20261028/task27-30/`.

| Gate | Actual evidence | Remaining action |
|---|---|---|
| Migration history | Initial read-only inventory found 30 migration IDs on isolated `Cosmix_Payroll_20261028`. Coordinator subsequently applied additive migration `20261028114154_ProtectPayrollPaymentSettlementAndBankExports` to both isolated payroll databases and normal Development `Cosmix_Testing` after a verified COPY_ONLY backup | Migration success is distinct from provider/deployment acceptance. Coordinator reported normal Development employee/payment counts unchanged (14/0); no fixtures were placed there |
| Operational API readiness | New `PayrollDeploymentReadinessCheck` checks pending/unknown migrations, pending model changes and mapped payroll column existence; no employee rows or business profiles queried | Separate `payroll-readiness` route/tag is wired by the coordinator; inspect actual deployment result and configuration independently |
| Existing health | `/health/ready` originally checked `Departments` only; `/health/live` is process liveness | Keep liveness independent of SQL; payroll probe is separate and must not stand in for business approvals |
| Configuration | Explicit base JSON, Development JSON and known development user-secret layers inspected in memory; no values/secrets copied to evidence. Mobile/web authentication and attendance dispatch disabled in these layers; external SMS incomplete | Configure actual deployment privately, then verify running-host options, OTP delivery and recovery. These selected layers do not prove the effective configuration of another launch profile |
| Private storage | Existing object directory and 32-byte key file metadata observed | ACLs, retained key rotation, durable data-protection keys, storage read/write and restore drill remain required on deployment host |
| Background jobs | OTP worker gated by mobile authentication; attendance outbox gated by `AttendanceJobs.Enabled`. Document expiry worker starts after 15 seconds without an enable switch | Operator must understand initial writes, verify retry/dead-letter/queue-age monitoring and job execution. Do not launch the business host merely for inventory |
| Login/recovery | Services gate on authentication options, OTP protector and SMS provider; controlled provider configuration is distinct from carrier delivery | Actual carrier OTP, password recovery, independently authorized phone recovery, revocation and restart verification required |
| Android | ADB inventory: 1 online emulator, 0 other device candidates | Three physical Android phones and the device matrix below remain unperformed |
| Face server | Configured private endpoint `https://192.168.1.50:8000`; TCP unavailable during this run | Restore network/server, then check trusted TLS, provider health, actual consenting-person liveness/match and failures. TCP alone is never a face-verification result |
| Historical payroll | Comparison against preserved **synthetic** September evidence: 1 employee, gross 33,000; deductions 800; net/unpaid 32,200; employee contribution 300; employer contribution 1,500; zero differences | Obtain approved real salary sheet and independent accounting/settlement ledger and normalize both against the system export |
| Accounting month | Original retained September evidence remains accrual-only: 4 journals, debit=credit=34,500, zero payments. Separate cloned-month continuation passed: 4 payment attempts, 10 journals, net confirmed/ledger 32,200, debit=credit=100,700 and zero ledger difference | Successful clone scenario uses synthetic authority/receipts and no actual transfer. Original closed month and private evidence were preserved |
| Signatures | None collected | HR, Finance and Management must review the actual comparison/differences and sign the exact evidence version |

Fresh execution results: Python comparison validation **13/13 passed**; PowerShell readiness validation **7/7 passed**. CLI test runners and generated fixtures are external; C# tests extend the existing authorized test project. Heavy .NET/Gradle builds are coordinator-owned. See the coordinator's final validation ledger for those results.

The additional strict continuation test in `PayrollPaymentsAcceptanceTests.cs` **passed** on the coordinator-prepared clone. It is guarded by `COSMIX_PAYROLL_PAYMENTS_CONNECTION`, accepting only `(localdb)\CosmixUat_d893653346` / `Cosmix_Payroll_Payments_20261028` with Windows authentication. It loads the existing frozen month, uses copied encrypted evidence under `D:/CosmixPayrollPayments20261028/private`, and never seeds or resets the original database. Retained `D:/CosmixPayrollPayments20261028/payment-acceptance-result.json` confirms four payment attempts (two confirmed, one failed with reversal, one reversed), ten journals, net confirmed salary 32,200, zero payment-ledger difference and debit/credit movements of 100,700 each. The prior four journals, frozen fingerprint, 30 attendance days and copied prior encrypted objects remained unchanged. The scenario also exercised partial/history/payslip agreement, actor independence, duplicate/reference/replay handling, overpayment/closed-period denial and scoped report/employee access. Authentication authority and receipts remain explicitly synthetic; no external transfer, real login, physical-device acceptance or company sign-off occurred. Do not rerun the scenario against this now-populated accepted clone.

## Readiness inventory

Run with PowerShell 7. Settings paths are explicit JSON layers in increasing precedence. `-IncludeEnvironment` overlays only the documented subset in the script; this is an inventory, not an ASP.NET configuration emulator. No external SMS/biometric request is sent. `-ProbeFace` opens only the configured TCP port; `-ProbeDevices` runs ADB inventory. SQL uses Windows authentication and a fixed SELECT of migration history. It neither applies migrations nor reads employee data.

```powershell
./scripts/Test-PayrollDeploymentReadiness.ps1 `
  -SettingsPaths ./src/Cosmix.Api/appsettings.json,./src/Cosmix.Api/appsettings.Development.json,"$env:APPDATA/Microsoft/UserSecrets/Cosmix-Backend-HR-Development/secrets.json" `
  -SqlServer '(localdb)\CosmixUat_d893653346' -Database Cosmix_Payroll_20261028 `
  -ProbeDevices -ProbeFace `
  -OutputPath D:/CosmixPayrollVerification20261028/task27-30/readiness-next.json
```

Use a new external output filename; prior evidence is not overwritten. `Observed` means the stated metadata was observed, not behavior accepted. `production_ready` remains false by design. The API probe uses the actual running DI configuration: migration/schema failure is Unhealthy; unavailable authentication/storage/job configuration is Degraded. A healthy probe still does not test delivery, login, restore or payroll business authority. The production SMS check rejects LocalTesting as an external provider. Default public health responses should expose status only; detailed codes contain no secrets but belong in protected operational diagnostics.

Physical schema probing checks payroll table/column existence, not every SQL type/index/constraint; migrations and controlled transactional tests remain necessary. Encrypted private-store construction validates path/key metadata but does not prove successful encryption, recovery or write permission. Health checks never initialize an owner, modify policy, approve a posting period, start jobs or transfer funds.

## Historical comparison input contract

Use a stable employee number/code or UUID present in **all** sources, never names as the join. Do not include CNIC, bank account, phone, OTP, access tokens or evidence-storage references. The original workbook may be supplied privately; retain it unchanged, then have HR/Finance review the normalization. Do not invent a month's data from screenshots or infer missing values as zero. Explicit zero is allowed only when the source establishes it.

The `reference.csv` and `system.csv` headers are exactly:

```csv
employee_id,month,currency,gross,deductions,net,employee_contribution,employer_contribution,paid,unpaid
```

The independently extracted `accounting.csv` headers are exactly:

```csv
employee_id,month,currency,salary_expense,deduction_payable,employer_expense,contribution_payable,salary_payable,cash_paid
```

One row per employee for the chosen calendar month and ISO currency. Header order may vary; duplicate/unknown/missing columns are rejected. IDs are case-sensitive and limited to letters, digits, dots, underscores and hyphens, starting with a letter/digit. UTF-8 (optional BOM), comma-delimited CSV, plain nonnegative decimal amounts with at most two decimals; no thousands separators, currency symbols, formulas, exponent notation, NaN, rounding or tolerance. Each file is bounded to 32 MiB and 100,000 employees. All three employee populations must agree; totals alone never hide a missing employee.

| Input field | Required meaning |
|---|---|
| `gross` | Gross salary earnings for this month |
| `deductions` | All employee deductions **including** employee statutory contribution |
| `net` | `gross - deductions`, nonnegative |
| `employee_contribution` | Employee statutory component, no greater than total deductions |
| `employer_contribution` | Additional employer expense; never reduces employee net |
| `paid` | Confirmed/disbursed amount at the agreed cutoff, net of reversals. Pending and unconfirmed Posted reservations do not count. Use the exact same state/cutoff interpretation for every salary source |
| `unpaid` | `net - paid`, nonnegative |
| `salary_expense` | Salary gross expense debit movement for this month |
| `deduction_payable` | Other employee deduction credit movement before remittance: deductions minus employee contribution |
| `employer_expense` | Employer contribution expense movement |
| `contribution_payable` | Employee plus employer statutory credit movement before remittance |
| `salary_payable` | Remaining month's salary payable after effective salary payments/reversals |
| `cash_paid` | Net effective salary cash/bank credit movement; salary-payment debit and credit must independently balance in the ledger |

The accounting file is a **month-specific employee attribution**, not the company's unfiltered closing trial balance. Exclude opening balances, unrelated transactions and statutory/deduction remittances from the contribution/deduction movement columns. Unconfirmed posted payments can make ledger cash/payable differ from confirmed salary paid/unpaid; retain and review that difference instead of changing the ledger extract to hide it. For consolidated journal entries, Finance must supply a traceable employee subledger; do not spread totals arbitrarily. Multi-currency and multi-month files must be split. Negative-net adjustments, remittance reconciliation, component-by-component overtime/tax validation and trial-balance assurance need separate reviewed schedules; the checker rejects unsupported negative salary inputs.

```powershell
python ./scripts/Compare-PayrollMonth.py `
  --reference D:/PrivatePayroll/approved-reference.csv `
  --system D:/PrivatePayroll/system-export-normalized.csv `
  --accounting D:/PrivatePayroll/independent-ledger-normalized.csv `
  --month 2026-09 --currency PKR --evidence-kind Real `
  --provenance D:/PrivatePayroll/original-approved-salary.xlsx `
  --provenance D:/PrivatePayroll/original-ledger.csv `
  --output-directory D:/PrivatePayroll/review-run-001
```

These filenames are examples, not available real sources. The output directory must be new and outside the repository. Restrict access because output contains employee financial data. `comparison.json` retains SHA-256 hashes and byte lengths of the exact normalized inputs, optional original files and executed tool; `differences.csv` lists employee membership and exact amount differences. Original files are hashed only, never parsed or copied by `--provenance`. Review normalization and retain the mapping script/spreadsheet, cutoff timestamp, source approval and ledger extraction query alongside the hashes.

Exit code **0** means exact match; **1** means valid inputs with differences; **2** means invalid input or IO failure. `evidence_kind=Real` is operator classification, never an independent claim that data is authentic. `company_acceptance_established` always remains false. Distinct input files are required; the tool cannot prove organizational independence of files supplied by the same operator. Review every difference, correct the source/process, then run again into a new directory rather than editing reports.

Retained synthetic run: `preserved-synthetic-comparison/comparison.json` and `differences.csv`. Normalization script: `normalize_preserved_synthetic.py`; sources remain at `D:/CosmixPayrollAcceptance20261028`. Original month database and fixtures were not rerun, deleted or modified.

## Source APIs for the approved month

The new employee endpoints are `GET /api/v1/self-service/finances/payment-history` and `GET /api/v1/self-service/finances/payment-history/{batchId}`. They use the server-linked employee and existing self-financial audit/permission transaction. The employee cannot supply another employee ID. Period totals cover all valid payment attempts; individual payments are paginated separately. Pending/posted amounts reserve entitlement; only confirmed payments count as paid. Failed/reversed amounts remain visible without consuming the replacement-payment balance. Retained confirmation/reversal proofs are checked internally and never returned.

Finance selects batches with `GET /api/v1/hr/payroll-settlement?page=1&pageSize=25`, under `payroll.reports.read`; payment-posting permission is not required. This bounded list includes finalized obligations linked to approved locked salary batches with matching fingerprint/period/currency metadata. Full protected-source validation occurs when opening `GET /api/v1/hr/payroll-settlement/{batchId}` under the same read permission. Export is `GET /api/v1/hr/payroll-settlement/{batchId}/export?format=csv` under `payroll.reports.export`. These operations use the sensitive management/all-scope boundary, with current web MFA/session validation and audit. The JSON download envelope contains `fileName`, `contentType`, `base64` and `sha256`. Export includes employee gross/deductions/net/paid/unpaid, components and component totals, payment ledger net and explicit reconciliation issues. CSV fields use the existing formula-injection neutralization. Frozen-source discrepancies or malformed linked payments fail closed; orphan payment journals and net ledger discrepancies are explicit Finance report issues.

Employee and employer statutory contributions already have a dedicated source: `GET /api/v1/statutory-contributions/{id}/export`, permission `statutory.contributions.manage`. Its service validates retained contribution/entry evidence before exporting per-employee `EmployeeContribution`, `EmployerContribution`, `TotalContribution` and a company-total row. Use that actual schedule to normalize the contribution columns; do not mistake employer contribution for an employee payroll deduction. No additional business rates or registrations are invented by the settlement report.

## Required real-device and sign-off record

Use `Physical-Device-Acceptance-Template.csv` for three actual phones. A connected emulator or screenshot does not fill a physical-device result. Record installation/version, Android/permission state, consent version, actual test timestamp, expected/actual outcome and protected evidence reference; keep employee identity/biometrics outside this checklist.

For each device verify genuine OTP login/recovery; grant/deny/revoke camera and location; current consent and withdrawal; normal check-in with actual face/liveness; wrong-person/spoof rejection; unreachable face server with no success punch; check-out without face; retry/offline/reconnect without duplicate attendance; session revocation; own-only salary/payment/history access; another-employee access denial; clean install/restart and server errors. Retain the existing fail-closed camera/provider behavior. No biometric success has been established by this work.

Before company sign-off, HR approves employee membership and salary components; Finance approves accounting attribution, paid/unpaid cutoff and all reconciled differences; Management approves the payroll/release version and unresolved exception decisions. Record role, person, UTC timestamp, month/currency, SHA-256 of the comparison report and evidence references. Blank fields and unperformed steps remain pending. Bank certification and an actual transfer require their own authorized evidence.
