# Payroll payment and bank-export verification — 28 October 2026

Latest continuation: [Tasks 22–30 final local verification](payroll-acceptance/Tasks-22-30-Final-Verification-20261028.md).
It supersedes the earlier blocked face network and disabled local dispatch status below;
real bank/device/company acceptance remains outstanding.

## Implemented scope

- Separate HBL IBFT and HBL-to-HBL XLSX definitions from the two supplied workbooks.
  Headers, order, worksheet name, text identifiers and numeric amounts were independently
  checked against the originals. Original workbooks were not edited or copied into Git.
- Protected effective employee banking sources, independently reviewed format profiles,
  explicit recipient bank mappings and same-bank restrictions. Profiles are not auto-approved.
- Versioned salary payment confirmation with protected evidence and independent reviewers;
  failures and reversals retain the original payment and create an exact compensating journal.
- Idempotent, protected bank exports with unchanged-source verification, exact-file download
  and audited renewal. Generating or downloading a file does not confirm a bank transfer.
- Employee payment history in the existing mobile History page, with totals over all attempts
  and separately paged transactions. Payslip paid status now validates payment proofs and journals.
- HR/Finance settlement register, deductions/components, paid/unpaid amounts and ledger differences,
  plus protected CSV. Report-only users do not need salary posting permission. Existing statutory
  schedules export actual employee/employer contribution entries under their own permission.
- Separate `/health/payroll-ready` configuration/schema probe and historical comparison tools.

## Executed evidence

| Check | Result |
| --- | --- |
| Final backend Release regression | 551 passed, 0 failed, 0 skipped |
| SQL payment continuation | Passed, one explicitly synthetic scenario |
| Controlled mobile/web checks | 57 passed across the separate client harnesses |
| Mobile verification | Passed lint, formatting and TypeScript checks |
| Final full web verification | Passed lint, formatting, TypeScript and production Vite build |
| Historical comparison tool tests | 13 passed using synthetic inputs |
| Readiness script tests | 7 passed |
| Independent XLSX reader | Both generated layouts match original headers; identifiers text, amounts numeric |
| Database migration | `20261028114154_ProtectPayrollPaymentSettlementAndBankExports` applied |

The backend regression includes SQL translation/model checks and a concurrent lock test proving
that two different employees in the same obligation share the payment allocation lock.
The separately executed SQL scenario used production services, repositories, transactions and
encrypted files, with explicitly synthetic authentication and role identities.

The preserved September synthetic salary had gross 33,000, deductions 800 and net 32,200.
On a separate clone, the test recorded partial payment, independent confirmation, failed-payment
reversal, confirmed-payment reversal and replacement. Four payment attempts and ten journals
reconciled to 32,200 confirmed, zero outstanding and zero payment-ledger difference.
Total debits and credits were both 100,700. The original salary source, four accrual/contribution
journals, 30 attendance days and copied prior encrypted objects remained unchanged.
No bank transfer was made.

Evidence is retained outside Git:

- `D:/CosmixPayrollVerification20261028/payments-final-regression.log`
- `D:/CosmixPayrollVerification20261028/payments-sql-acceptance.log`
- `D:/CosmixPayrollPayments20261028/payment-acceptance-result.json`
- `D:/CosmixPayrollVerification20261028/web/report.md`
- `D:/CosmixPayrollVerification20261028/web/final-workspace-verify.log`
- `D:/CosmixPayrollVerification20261028/task27-30/`
- `D:/CosmixPayrollVerification20261028/bank-workbook-check/bin/Release/net10.0/independent-check.json`

## Database preservation

The normal Development target was verified as `(localdb)\CosmixUat_d893653346`, database
`Cosmix_Testing`. Before migration, a COPY_ONLY checksum backup was created and independently
verified at `D:/CosmixPayrollVerification20261028/Cosmix_Testing_before_payment_settlement.bak`.
After migration, all 31 migrations were applied, DBCC CHECKDB was clean, and the target retained
14 employees and zero salary payments. No synthetic payment fixtures were written there.

Synthetic payments were written only to the new `Cosmix_Payroll_Payments_20261028` clone.
The earlier `Cosmix_Payroll_Acceptance_20261028` and its evidence were preserved. The new scenario
refuses reuse of a completed/populated fixture. It must not be rerun against a business database.

## Remaining real acceptance

The web production build retains a non-blocking bundle-size warning (main JavaScript approximately
1.49 MB before gzip). The final whole-workspace verification passed after adding a missing
employee-presence guard in the newly present attendance tracking loader; its other functionality
was preserved.

Tasks 27–30 are not fully accepted in the real company environment. Inspection still found
production SMS/login/recovery configuration incomplete, normal inspected authentication/report
job settings disabled, zero physical Android phones and an unreachable configured face server.
Three-phone camera/liveness acceptance has not been performed.

Both supplied bank workbooks have blank transaction amounts. They establish upload layouts,
not an actual historical payroll comparison. That comparison requires an actual completed salary
month and accounting/payment evidence. Bank portal validation, the live parallel month and named
HR/Finance/Management sign-off remain separate external steps. Synthetic approvals are not company sign-off.

The user's existing Visual Studio API process was not restarted by this verification. Release
artifacts used for tests are outside the workspace; the changed source must be loaded by the normal
development build/restart before its new routes are available in that existing process.

## Continuation: runtime, sample comparison and emulator

The earlier restart limitation above is superseded by this continuation. The API was published in
Release to `D:/CosmixPayrollVerification20261028/runtime-continuation` and restarted on HTTPS 7240
and HTTP 5240. The active frontend profile used the regular Windows MSSQLLocalDB pipe and a
different `Cosmix_Testing` from the packaged Codex LocalDB instance inspected earlier. Its starting
counts were 30 migrations, 32 employees and 123 salary-payment records; no inference of real bank
payments is made from those records.

That active database received a separate COPY_ONLY/checksum backup, successful RESTORE VERIFYONLY,
and migration `20261028114154_ProtectPayrollPaymentSettlementAndBankExports`. Afterwards it had
31 migrations with the same 32 employees and 123 payments, and DBCC CHECKDB passed. Backup:
`D:/CosmixPayrollVerification20261028/frontend-before-payment-migration.bak`.

Repeated recovery/connection delays were traced to this database's AUTO_CLOSE setting being on.
AUTO_CLOSE was disabled for this database and for the isolated payment clone, without resetting
either database. Three subsequent web database-health probes returned HTTP 200 in 529, 24 and
17 milliseconds. `/health/live` is healthy. `/health/payroll-ready` remains HTTP 503/Degraded:
synthetic login delivery is configured, external SMS is absent, and report-notification dispatch
is disabled. A healthy database does not establish production readiness.

The current frontend private settings and the default Visual Studio private settings now point
to the verified active database. Their previous files were retained as private backups with the
same ACL. The previously user-approved GPS policy (180 days) was applied to those settings.
The live worker completed a cycle, redacting zero expired GPS coordinate rows and processing zero
legacy payloads; no eligible old data existed. No new payroll/bank confirmation was inserted in
the active frontend database during this continuation.

After the final restart, the actual synthetic web password/OTP flow succeeded. Own payment history
returned HTTP 200; bank templates and settlement reports returned HTTP 403 for the fixture without
their specific grants. Logout returned HTTP 204. These checks verify route loading and denial;
the authorized financial service scenarios remain covered by the earlier SQL acceptance evidence.

At the user's request, a new clearly marked synthetic salary/payment workbook was authored from
the retained test expectation, approved test salary preview and SQL payment result. It contains
no real beneficiary accounts and is not an HBL upload file or statement. Its formulas recalculate;
both sheets were rendered and inspected, and an independent XLSX reader verified cached totals.
The normalized comparison CLI reports MATCH for one synthetic employee with zero differences:
gross 33,000, deductions 800, net and effective confirmed payments 32,200, outstanding zero;
debits and credits both 100,700. This does not convert synthetic inputs into historical evidence.

- Workbook: `D:/CosmixPayrollVerification20261028/outputs/payroll-continuation/Synthetic-Payroll-and-Payment-Comparison.xlsx`
- Comparison: `D:/CosmixPayrollVerification20261028/payment-month-comparison/comparison.json`
- Runtime evidence: `restarted-health.json`, `stable-database-health.json`,
  `authenticated-route-checks.json` and `runtime-stable.stdout.log` under the verification directory.

Only one emulator is available, as confirmed by the user. The external mobile verification host
was rebuilt against current application code. The earlier unchanged acceptance database lacks
the new payment columns, so a separate guarded external host now reads the already migrated
`Cosmix_Payroll_Payments_20261028` clone on loopback HTTPS 7241. It retains normal OTP/session
verification and self-service scope, permits no financial writes and sends no external SMS.
The original payroll acceptance database and its 30-migration baseline were not upgraded/reset.
Fresh native synthetic OTP login and the location-policy gate completed on the emulator.
The native History page then loaded the September payslip (gross 33,000, deductions 800,
net 32,200, Management approved, Paid). Its protected PDF opened in the Android PDF viewer
and displayed the same employee, approved source, payment state and component breakdown.
The Payments category independently showed net entitlement and confirmed paid both 32,200,
zero outstanding/posted/pending, historical failed 5,000 and reversed 12,000, and the confirmed
12,000 replacement entry with posting/confirmation timestamps. The history text explicitly
excludes failed/reversed outcomes from confirmed pay. No face enrollment or biometric punch
was fabricated; attendance remained unavailable for this self-service fixture.
Native evidence: `emulator-payslip-actions.xml`, `emulator-paid-pdf.png`,
`emulator-payments-success.xml`, `emulator-payments-detail.xml` and `emulator-paid-history.png`
in `D:/CosmixPayrollVerification20261028`.

Real bank portal acceptance, a real historical month, physical camera/liveness on three staff
phones and named HR/Finance/Management approval are still not established. The user requested
sample sheets and confirmed only an emulator is available; no actual approver identities were supplied.
