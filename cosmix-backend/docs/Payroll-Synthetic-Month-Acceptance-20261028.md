# Synthetic payroll month acceptance — 28 October 2026

**Synthetic acceptance passed: backend month, authenticated Android payslip history and native PDF viewing.** The explicitly authorized fictional September 2026 scenario completed against SQL. The separate full regression passed 484 tests, with zero failures/skips; the persisted month scenario passed once from a fresh migrated database. Excel export generation is verified, while native Excel viewing is unavailable because this emulator has no compatible handler. These are synthetic acceptance results, not real company sign-off.

## Verified backend results

- Release solution test-project build: zero warnings/errors (`acceptance-final-build.log`). The external mobile API host also builds with zero warnings/errors (`mobile-host-build-verified.log`).
- Full regression: **484 passed, zero failed, zero skipped** (`results/regression-final.trx`). This includes six actual SQL persistence/transaction tests, HTTP contracts, payroll calculations/imports/approvals/payslips, and new statutory-source checks. The old business-database-writing `AcceptanceExecutionSuite` and the separately executed month scenario are deliberately excluded.
- Complete persisted month: **1 passed, zero failed/skipped** (`results/acceptance-verified.trx`, 21 seconds). Workbook import, canonical approvals, salary preparation, independent HR review/Management lock/Finance handoff, actual statutory setup and four journal postings, closed-period/duplicate protections, own PDF/XLSX exports and foreign/unknown-document denial all executed.
- SQL source and journals match the expected amounts below. Salary remains **Unpaid**, with no payment rows.
- Independent source-file reconciliation (`salary-sheet-comparison.json`, `SYNTHETIC-salary-sheet-comparison.csv`) derives the base, earnings and fixed deduction from the imported source CSV and uses the planned fictional contribution inputs. All seven compared monetary totals have a **zero difference** from persisted Finance results.
- Latest two-page PDF rendered and visually inspected; extracted text confirms employee label, gross 33000.00, deductions 800.00, net 32200.00 and Unpaid. Excel ZIP content and both export SHA-256 values were verified in the persisted test. The PDF currently uses the existing plain audit-report layout; this test does not claim a redesigned payslip template.
- Original `Cosmix_Testing` was read-only in this continuation: 14 employees, zero payroll batches and 30 migrations (`business-db-readonly-check.txt`). The dedicated synthetic database has all 30 migrations.
- Independent cross-reviews found no critical/important issues in the decimal-proof fix and statutory transaction/source fixes. Relevant production source hashes are retained in `verified-production-source-hashes.json`.
- Final SQL inspection confirms one synthetic employee, four balanced journals, no payments, one real mobile session and registered device, consent acceptance and audited ESS financial reads. `DBCC CHECKCONSTRAINTS` and `DBCC CHECKDB` reported no integrity violations (`final-sql-integrity.txt`).

Earlier failed attempts are retained with logs/TRX and separate disposable-database backups. They are not counted as passing runs.

## Scenario and expected reconciliation

One fictional employee, **SYNTHETIC Payroll Acceptance**, employee number **1**, is included in September 2026 payroll. The fixture creates no second employee. All rates and contributions are test inputs, not approved company policy or certified legal rates.

| Item | Expected PKR |
|---|---:|
| Monthly basic pay | 30,000.00 |
| Fixed travel earning | 3,000.00 |
| Gross earnings | 33,000.00 |
| Fixed deduction | 500.00 |
| Employee statutory contribution | 300.00 |
| Total employee deductions | 800.00 |
| Employee net pay | **32,200.00** |
| Employer statutory contribution, separate expense | 1,500.00 |
| Employee plus employer statutory payable | 1,800.00 |
| Total journal debits and total journal credits | 34,500.00 each |

The baseline contains 30 retained daily summaries for September 1–30, each with eight scheduled and paid hours, no unpaid leave, no overtime and no ordinary or late deduction. It expects four journal entries, salary payable of PKR 32,200, fixed-deduction payable of PKR 500 and statutory payable of PKR 1,800. It creates **no salary payment**; own payslip payment status must be **Unpaid**. Other salary/time edge cases remain separate regression tests.

## Executable path

- `tests/Cosmix.Tests/PayrollMonthAcceptanceTests.cs` drives the single persisted scenario and checks totals, independent HR/Management transitions, own exports, and denial of another employee's payslip.
- `tests/Cosmix.Tests/PayrollMonthAcceptanceFixture.cs` owns the strict database guard, synthetic upstream data, isolated authority adapter, real SQL transactions, import and source approvals, and encrypted evidence.
- `tests/Cosmix.Tests/PayrollMonthFinanceAcceptance.cs` invokes actual Finance setup/statutory/accounting services and independently reconciles SQL journals. It also checks self-approval denial, duplicate posting, changed totals/fingerprint, replay and closed-period rejection.
- `tests/Cosmix.Tests/PayrollSalaryFrozenEvidenceTests.cs` includes the regression for SQL decimal scale and rejection of changed amounts and source identities. SQL transaction ownership checks are in `PayrollSqlPersistenceTests.cs`.

The source workbook is uploaded through `PayrollImportService.InspectAsync`, parsed and mapped, previewed, then applied to pending canonical compensation/component requests. Separate actors approve the compensation, two salary components and salary policy. The actual retained compensation record supplies the baseline attendance evidence. Actual salary services then execute Preview → Prepare → independent HR review → independent Management lock → separate Finance handoff. Actual Finance services configure accounts/period/profile and statutory coverage, prepare and approve an employee contribution entry, finalize the frozen salary obligation and post salary and employer contribution journals. Actual Employee Self Service and Payslip services read the same frozen batch and produce PDF/XLSX files.

## Reproduction and run boundary

The test defaults to **skipped** unless `COSMIX_PAYROLL_ACCEPTANCE_CONNECTION` is explicitly present. Its constructor accepts only Windows integrated security, server `(localdb)\CosmixUat_d893653346`, and database `Cosmix_Payroll_Acceptance_20261028`. There is no business-database fallback.

Provision a **new empty, fully migrated dedicated database** before running. The test itself neither creates/migrates nor deletes/resets databases. It asserts an empty employee table and employee sequence number 1. A failed partial run must be preserved for diagnosis and the dedicated disposable target reset explicitly before another full run. Never run the old `AcceptanceExecutionSuite` for this scenario: that suite has a different database boundary.

The private source inputs are `D:/CosmixPayrollAcceptance20261028/source-import.xlsx` and its review companion `source-import.csv`. The workbook has one `Salary` sheet and three canonical data rows: Compensation 30,000 monthly base/gross, TRAVEL FixedEarning 3,000 and ADJUSTMENT FixedDeduction 500. Supported column names are mapped automatically; the visible synthetic notice column is ignored. Effective dates are September 1, 2026. Import and approval use an explicitly controlled August 31 test clock; the month run uses October 1. These timestamps represent test chronology.

Provision a private Windows ACL for `D:/CosmixPayrollAcceptance20261028/private`. The actual `EncryptedPrivateFileStore` uses:

- Object directory: `D:/CosmixPayrollAcceptance20261028/private/objects`
- Separate 32-byte key file: `D:/CosmixPayrollAcceptance20261028/private/storage.key`
- Non-secret key reference: `synthetic-month-v1`

The fixture creates a key only if absent. Keys, database files, source employee data and generated binary exports stay outside Git. Production private-storage validation, purpose binding and encrypted integrity checks remain active.

After the target, workbook and private ACL are prepared, the bounded command is:

```powershell
$env:COSMIX_PAYROLL_ACCEPTANCE_CONNECTION = 'Server=(localdb)\CosmixUat_d893653346;Database=Cosmix_Payroll_Acceptance_20261028;Integrated Security=True;TrustServerCertificate=True'
dotnet test tests/Cosmix.Tests/Cosmix.Tests.csproj -c Release --filter 'FullyQualifiedName~PayrollMonthAcceptanceTests'
```

The coordinating verifier may use an external artifacts directory and a prior verified build. Build and execution output must be retained before changing this document's pending status.

## What is synthetic, and what is exercised

The service acceptance uses a named test-only current-session/permission/role adapter with distinct actor identifiers. It does **not** claim those identities completed production web login/MFA or received real company authority. Transaction handling uses SQL serializable transactions, rejects nested/staged writes and clears tracking at the authorization boundary; business/audit/receipt writes use actual production repositories. A rollback failure is recorded separately without replacing the original error.

Hiring and upstream attendance capture are fixtures. The employee is materialized through domain submission/decision operations, with a separately persisted role and approval record. One actual dormant `PendingAccess` account is created for the same employee, satisfying the attendance ledger's account foreign key. Its approved mobile login choice is the fictional test phone `+923001234567`; the payroll fixture creates no credentials, access grant, binding, active session or mobile activation. The external mobile harness owns those subsequent actions.

The 30-day upstream attendance evidence uses the actual daily-time and attendance-payroll calculators, actual SQL summaries/policy/compensation/input revision/month membership, content fingerprints and encrypted retained files. Its schedule, jurisdiction and prior no-charge financial membership are explicitly synthetic upstream fixtures. The retained snapshot is read from encrypted storage and passed through production `MonthLockEvidence` validation, but this does not claim the full device/punch/biometric/attendance-policy/legal-activation workflow ran.

Salary import, canonical compensation/component approvals, salary projection/preparation/review/locking/handoff, statutory coverage/contribution services, Finance postings, frozen evidence validation and own payslip services are actual application implementations against SQL. They do not return fabricated successful monetary service responses.

The single-employee SQL scenario verifies own listing/export, an unknown batch denial and a foreign-identity document denial before audit persistence. It does not invent a second employee or attempt to persist an audit for a nonexistent session employee. Cross-employee listing/enumeration remains covered by the controlled isolation tests; this run does not claim a two-account SQL/mobile isolation exercise.

The separate mobile host uses the same database and encrypted files with normal application authentication, token and ESS endpoints plus an isolated synthetic SMS delivery sink. Its exact dormant account was provisioned with self-only permissions and an unverified phone binding; HTTPS health passed without disabling certificate validation. Normal OTP verification subsequently activated the account and created the mobile session/device; consent was accepted through the app. No production authorization bypass is part of the payroll implementation.

## Android and client verification

- A fresh isolated Android API-37 emulator ran the existing October-20 debug APK with current checkout JavaScript served by Metro. No new Gradle/native build or physical phone test is claimed.
- Its declared debug local-network permission initially blocked the local HTTPS target. Granting that permission on this emulator enabled normal OTP request/verify; TLS verification remained enabled. SMS delivery used only the restricted synthetic inbox, not an external SMS provider.
- Normal login, consent and History → Payroll showed the actual saved September batch, gross 33,000, deductions 800, net 32,200, Management approved and Unpaid. No seeded session or fake monetary API response was used.
- The actual generated two-page PDF opened in the installed native PDF viewer and reopened successfully after the final JavaScript changes. Excel reached Android's open intent, but no spreadsheet handler is installed. Failed Excel cache cleanup and a clear "Try PDF or check that a compatible viewer is installed" message were verified. Native Excel rendering remains unverified; the backend XLSX payload/content/hash checks passed.
- Small presentation fixes show an inclusive employee pay period (1–30 September) and a readable approval label while preserving the backend exclusive-end contract. Native viewer failures have a distinct user-facing error; authentication/integrity checks and cache cleanup remain in place.
- Final mobile `npm run verify` passed ESLint, formatting and TypeScript (`mobile/source-final-verify.log`). Eleven presentation checks and 17 scoped controlled checks passed (`mobile/presentation-green.log`, `mobile/controlled-final.log`); the latter includes four web payroll DTO checks. This is not a new whole-web-suite pass: the earlier combined client script still exposed an unrelated leave-route export assertion, recorded in `mobile/controlled-regression.log`.
- Screenshots: `mobile/08-final-payslip.png`, `mobile/10-final-native-pdf-reopen.png`, and `mobile/09-excel-viewer-message.png`. Detailed device/runtime evidence is in `mobile/report.md`, with final source/APK hashes in `mobile/final-source-and-apk-hashes.json`. The emulator is left on a clean payslip card; Metro and the restricted local test API remain running for this test session.

## Evidence locations and findings

All scenario evidence is retained under `D:/CosmixPayrollAcceptance20261028`, including:

| Artifact | Meaning |
|---|---|
| `source-import.xlsx`, `source-import.csv` | Marked fictional source inputs |
| `synthetic-employee.json` | Payee identity, approved onboarding/login choice and private-storage paths, never key bytes |
| `import-preview.json`, `import-submission.json` | Actual parser validation and canonical request identifiers |
| `synthetic-attendance-manifest.json` | Persisted upstream fixture identifiers and boundary |
| `salary-preview.json`, `approved-salary-batch.json` | Expected/approved salary source and frozen lines |
| `finance-reconciliation.json`, `SYNTHETIC-journals.csv` | Independent SQL journal reconciliation |
| `SYNTHETIC-EOBI_Contribution_2026_09.csv` | Actual service employee/employer contribution export |
| `SYNTHETIC-payslip-2026-09-1.pdf`, `.xlsx` | Own service exports from the same frozen batch |
| `acceptance-result.json` | Final assertion summary, emitted only at successful scenario end |
| `results/*.trx`, `acceptance-*.log` | Per-run test execution records, including failed attempts |
| `private/objects/*` | Purpose-bound encrypted source, approval, calculation, audit and receipt evidence |

Prior attempts exposed a missing fixture account foreign key, premature transaction commit in the statutory repository, and salary evidence comparison that treated SQL decimal scale (`30000.00`) as different from retained JSON (`30000`). The latter was confirmed by independently reading SQL and decrypting the protected proof; the diagnostic copy remains private. The verified reader repair preserves schema-2 hashes by validating the retained proof fingerprint, then compares every SQL line/total by typed value. Five regression cases confirm equal scale variants remain valid while a one-cent change or changed source/trace/approval identity is rejected. The fixture's impossible non-existent-employee list request was also corrected; direct foreign-document denial remains tested before an audit insert.

Additional discovered source regressions were fixed: an accounting fallback used nonexistent model members and bypassed frozen evidence; statutory saves committed the outer authorization/audit transaction; approved contributions could be rebound; legacy JSON/header-only contributions, fabricated registrations and missing private proofs could bypass actual-entry requirements; legacy posting guessed accounts/periods. Current code fails closed for those paths, requires the explicit source-bound posting request, and exports only validated actual entry snapshots. New tests cover caller-owned SQL rollback/locks, mandatory evidence dependencies, registration/coverage, approved binding, legacy posting and export behavior.

The synthetic acceptance above is complete within the stated boundaries. Company HR/Management/Finance sign-off remains a separate human acceptance of real employee data and approved policy. There was no bank payment, real SMS delivery, physical-phone/biometric run or live company payroll release.

No real employee payroll, bank transfer, payment release, company signoff, biometric acceptance or legal-rate approval is claimed by this synthetic scenario.
