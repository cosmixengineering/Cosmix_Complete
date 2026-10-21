# Finance, Loan and ESS Boundary Repairs

Date: 15 October 2026. Local dirty checkout, HEAD
`ddc1a9f5c98aac90263261e8b03b5714f1886371`. No Git writes, DB/instance writes,
host, fixtures, keys, provider calls or migration changes in this continuation.

## Behavior Changed

1. Payroll handoff ProcessingAsync appends a reviewable processing record only.
   It no longer creates PayrollDeduction repayment entries. Preparation, bank
   advice and processing are not proof that a salary deduction actually posted.
2. Approve/reject/cancel receipt payloads bind the route request ID; repayment and
   reversal payloads bind the agreement ID before shared receipt replay. A key
   reused on a different route conflicts rather than returning the old resource.
3. Loan decisions retain the real assigned role. Ledger fallback retries compare
   actor, agreement, schedule, kind, amount/currency/date/source and reversal ID
   where applicable. A foreign schedule is rejected. A second reversal of one
   original entry conflicts even under a different key.
4. A manually selected PayrollDeduction is refused with
   `payroll_posting_evidence_required` until authoritative exact posting integration
   exists. This is a safe dependency, NOT implementation of that integration.
   Existing authorized received-payment/positive-adjustment workflows remain.
5. ESS financial read requires explicit `ess.finances.read`; `auth.self.read` alone
   no longer grants access to salary. Session/grants are checked before and after
   data IO inside the existing authorization transaction. Actual-role read audit
   must persist before returning the response. Identity stays server-derived.
6. ESS financial compensation uses the existing Asia/Karachi business date and
   inclusive start/exclusive end. Future and expired records are not current salary.
   Loan balances sum all signed ledger entries once, independently of the 20-entry
   history window. Negative reversals are not negated a second time.
7. Swagger descriptions now distinguish handoff from posting/payment and document
   the explicit financial permission and blocked unproved payroll repayment.
   Preview documentation no longer describes missing required sources as skipped.

Existing historical records are untouched. In particular, this repair does not
delete or reverse earlier unproved deduction entries, reopen a locked month,
certify settlement or create a payment. Historical receipts made with the old
route-less digest fail closed with `idempotency_key_reused`; no unsafe legacy
replay exception is added. Review their underlying record before any new request.

## Failure-First Verification

- Finance/loan baseline: 14/24; ten demonstrated failures. Final: 24/24.
  Baseline: `D:\CosmixVerification\part-c-parent-20261013\managed-c7eeea74a7c14be4ad1d630e52d99760`.
- ESS financial baseline: 7/21; fourteen demonstrated failures. Final: 21/21.
  Baseline: `D:\CosmixVerification\part-c-parent-20261013\managed-c79f1ee0a31f436db483171c8d1805df`.
- Harness setup errors before the ESS reproduction were corrected: a Self grant
  needs its employee target, and effective end is exclusive. They are not counted
  as product regressions or passing evidence.

Final Release: exit 0, **zero warnings/errors**, 61.80 seconds.

```powershell
dotnet build Cosmix.Backend.sln -c Release --artifacts-path D:\CosmixVerification\hr-remediation-finance-20261015\final --property:RestoreLockedMode=true --property:UseSharedCompilation=false -m:1 -nr:false -flp:logfile=D:\CosmixVerification\hr-remediation-finance-20261015\final-build.log

& ./docs/attendance-gap-c/VerifyManaged.ps1 -ReleaseDirectory 'D:\CosmixVerification\hr-remediation-finance-20261015\final\bin\Cosmix.Api\release' -Sources @(
  'docs/hr-srs-remediation/VerifyLoanAndPostingBoundaries.cs',
  'docs/hr-srs-remediation/VerifyEssFinancialBoundary.cs',
  'docs/attendance-completion/VerifyPayrollInputService.cs',
  'docs/attendance-completion/VerifyPayrollAttestations.cs',
  'docs/attendance-completion/VerifyPayrollReconciliation.cs',
  'docs/attendance-completion/VerifyMonthLocks.cs',
  'docs/attendance-completion/VerifyAttendanceComposition.cs',
  'docs/hr-srs-remediation/VerifyRemediationContracts.cs'
)
```

Eight suites / **569 passing checks**, exit 0. Counts in the order above:
24, 21, 95, 195, 48, 70, 46, 70. Generated MVC/OpenAPI: 467 registered paths.
Exact frozen inputs, dependency hashes and logs:
`D:\CosmixVerification\part-c-parent-20261013\managed-c5f85337a03c49bcbd8f9d8122662324`.
The OpenAPI standalone compiler emitted two known CS1701 reference-compatibility
warnings; runtime assertions passed. Product build had no warnings.

The new suites execute actual services and production repository predicates with
synthetic collaborators. The ESS query harness explicitly forbids SQL connections;
it does NOT prove SQL translation/execution. Shared financial suites use explicitly
synthetic legal dependencies. No real legal approval was inserted or inferred.

Bounded read-only independent review found no concrete new P1/P2 defects in these
modified financial paths. It did not independently run tests or SQL. `git diff
--check` passed. Neither substitutes for full acceptance.

## Changed Production Files and SHA256

All paths are relative to the existing cosmix-backend checkout. Preexisting edits
in these files remain; this list does not claim ownership of their entire Git diff.

| File | SHA256 |
| --- | --- |
| src/Cosmix.Application/HR/AdvancesLoans/AdvanceLoanService.cs | 81D35421ED499D69C03743EA48A337B90DF75046ACA5E2F7A024AF8EE6D25384 |
| src/Cosmix.Application/HR/PayrollHandoff/PayrollHandoffService.cs | F300EFF0D681ADD95D0D4FF4617A72680B9A86F63505B4DB1A73D35E1951F908 |
| src/Cosmix.Application/HR/EmployeeSelfService/EmployeeSelfService.cs | 2145DCA39A7119950487580483358B33A9E3019034FC2F23424FA69554D48845 |
| src/Cosmix.Infrastructure/Repositories/HR/EmployeeSelfServiceRepository.cs | DE7C9846DD3A738866B9B6BA11FF0B8D192A8958DC29B879D8762594BF4B7246 |
| src/Cosmix.Api/Controllers/EmployeeSelfService/EmployeeSelfServiceController.cs | BE3C464E2C1768A04D975EA5B9717FD1B249E9A200401688379BD2FE8ECB9C6C |
| src/Cosmix.Api/Controllers/HR/AdvanceLoanControllers.cs | C918E720DEE0DD1AF12ABC3619DA1D87414B55F44455A09640C5A95BDEBA40A8 |
| src/Cosmix.Api/Controllers/HR/PayrollHandoffController.cs | 2410DC86BFCE7963F582DF5788D5C08E3903405EEC9D4064B4131E12EA3ADBAB |

New standalone files: VerifyLoanAndPostingBoundaries.cs and
VerifyEssFinancialBoundary.cs. Extended VerifyRemediationContracts.cs. Status,
verification, this evidence record, the 19-task progress matrix and SRS mapping
are updated. No new test project, migration, permission/role, DI registration or
separate client implementation.

## Pending Authenticated UAT

On the exact approved synthetic target, not an arbitrary existing database:

1. Process a locked synthetic handoff; independently assert unchanged repayment
   rows/balance and exactly one processing/audit/receipt result on retry.
2. Reuse approval/repayment/reversal keys against another route; expect 409 and no
   second resource change. Race separate HTTP connections on one reversal and
   verify one committed reversal plus its atomic audit/receipt.
3. Submit PayrollDeduction without exact posting evidence; expect 409 and no ledger
   write. Actual posting-to-loan integration remains unfinished, not a test shortcut.
4. Test financial self read with explicit permission, generic-only permission,
   wrong employee scope, revoked session/grant and audit-storage failure. Never
   return another employee's data or a success whose required audit failed.
5. Compare salary start/end/local-midnight and whole signed loan ledger against SQL,
   including 25 repayments and a reversal; history remains capped at 20.

Full Attendance acceptance remains **NO**. Financial adjustment/carry-forward,
remaining ESS leave/assignment/calendar work and the broader G/H backlog remain.
Formal legal activation, real authenticated SQL/storage/concurrency and owner UAT
are outstanding. Real face/OTP and separate clients are excluded, not passed.
