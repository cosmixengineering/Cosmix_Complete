# Confirmed Loan Posting and Interest-Free Principal

15 October 2026. Backend implementation and standalone verification, not actual
payroll posting, authenticated SQL acceptance, or whole Attendance completion.

## Confirmed Company Rule

Employee advances/loans are principal-only. No interest, markup, service fee or
other loan charge is permitted. The legacy request field
`OtherApprovedMonthlyAmount` remains compatible at zero only. Approval rejects
nonzero values; new domain schedule entries also reject them. A 10,000 PKR loan
over ten installments produces exactly ten 1,000 PKR principal installments.
Two-decimal division places its remainder in the final installment so that the
sum is exactly the approved principal. Repayment above the outstanding principal
is rejected. Historical records are not changed or automatically reversed.
Retained schedules containing extra charges cannot enter a new handoff or posting.

## Separate Financial Actions

`POST /api/v1/payroll-handoff/batches/{id}/processing` is still review/processing
only. It cannot reduce loan principal. A manually selected `PayrollDeduction`
on the generic repayment endpoint remains rejected.

`POST /api/v1/payroll-handoff/batches/{id}/lines/{lineId}/confirmed-loan-posting`
records an explicitly evidenced, already-posted external deduction. It is not a
command to make a deduction or release a payment. Required fields:

- Exact employee, agreement, schedule ID and source version from the frozen line.
- Exact positive two-decimal posted amount and `PKR` currency.
- Actual UTC posting time, not before the batch lock or in the future.
- External system, receipt/reference and external receipt line identity.
- Supporting evidence reference, SHA-256, attestation statement and reason.
- `Idempotency-Key`; normal authenticated web session, MFA and CSRF.

The actor needs explicit All-scope permissions:
`attendance.payroll-attestations.attest`, `attendance.payroll-handoff.read`,
`attendance.payroll-handoff.process`, and `attendance.advancesloans.repay`.
No role receives these automatically. Accounts/Finance must be provisioned through
the existing authorization system. The employee cannot post their own deduction.

The action rechecks the existing locked month, exact membership, protected line,
approved loan/schedule and remaining principal. Partial amounts or a repayment
made against the frozen installment after preparation conflict; the action does
not silently resize the line. A source-line or external-receipt identity cannot
produce another repayment, even with a new HTTP key. A reversal is a separate
auditable event, not permission to reuse the original posting line.

One existing repayment-ledger row, protected audit and HTTP retry receipt share
the existing serializable authorization transaction. No duplicate payroll module,
new table, migration, automatic processing receipt or payment instruction is added.
Private evidence writes precede SQL commit, so a failed transaction can leave
unreferenced encrypted objects for the existing storage lifecycle to handle.

`GET` on the same confirmed-loan-posting path reads the retained original posting
after the HTTP retry window. It validates the immutable ledger row and protected
audit, not a still-live retry receipt. It requires All-scope attestation read,
payroll-handoff read and loan review permissions. The loan ledger remains the
source for later reversals/current balance; this GET is original posting evidence.

The returned `VerificationMode=manual_authorized_attestation` is deliberate:
an authorized human attests the receipt. The backend does not claim independent
verification against an external payroll provider. `PaymentInstruction=false`.

## Verification

Failure-first runs reproduced the absent confirmed-posting operation and the
previous acceptance of an extra monthly loan charge. The initial corrected
Release build passed with zero warnings/errors at:
`D:\CosmixVerification\hr-backend-continuation-20261015\posting-build\bin\Cosmix.Api\release`.

Retained runner:
`D:\CosmixVerification\part-c-parent-20261013\managed-58fa7aef541f441c93d14d68748d9548`.

- Confirmed posting: 51 checks, exact source/amount, expiry-independent evidence,
  replay/payload conflicts, duplicate receipt/line, permissions/session/MFA,
  self-posting denial, changed balance, legacy charges and protected-file failures.
- Loan/processing boundaries: 31 checks including interest-free schedule, exact
  rounding, overpayment, route-bound retry and duplicate reversal prevention.
- The same run passed ESS consistency (37) and ESS finance boundaries (21).

These call actual services/operation with synthetic repositories, transaction and
storage collaborators. The simultaneous retry case is a synthetic serialized
race, not separate-connection SQL concurrency. Current final verification and
contract results belong in `Verification.md`; do not add old suite counts together
as whole-system coverage.

## Still Required

Post-review repair: the initial bank-advice implementation could still export an
installment already paid manually. RED reproduced this in
`managed-049dd9eb57a8491497bd1069b721b630`. Preparation now explicitly skips a fully
manually repaid installment. Partial repayments or aggregate scheduled debits above
remaining principal conflict. Lock and bank-advice export recheck the frozen loan
lines; a changed balance or already-posted installment blocks further advice rather
than silently resizing it. Reversing a prior posting does not authorize reuse of
its original export. Historical detail remains available.

Current standalone results: confirmed posting/lock/export 60 checks and
payroll-input/Finance projection 102 checks, including repayment during private
evidence preparation. Retained runner:
`D:\CosmixVerification\part-c-parent-20261013\managed-92ff758216314d8d987b17190e0b6360`.
Independent read-only re-review found no further actionable issue in that fix.

Real authenticated HTTP/SQL execution, independent connections, persisted rollback,
encrypted-storage restart/failure and revocation acceptance remain required.
The A02 setup/auth packet must be explicitly approved before execution.

This closes neither F3 financial credit/recovery carry-forward nor legal activation.
Corrected locked-month valuation and its later first-unlocked-month adjustments
must retain authoritative source and confirmed settlement evidence. Legal authority
and signed jurisdiction-specific evidence are still unapproved. No existing
database, real employee, legal activation or provider was touched here.

## Changed Product Files

- Application/HR/PayrollHandoff/PayrollLoanPosting.cs (new)
- Application/HR/PayrollHandoff/PayrollHandoffService.cs
- Application/HR/PayrollHandoff/PayrollHandoffContracts.cs
- Infrastructure/Repositories/HR/PayrollHandoffRepository.cs
- Api/Controllers/HR/PayrollHandoffController.cs
- Application/HR/AdvancesLoans/AdvanceLoanService.cs
- Application/HR/AdvancesLoans/AdvanceLoanContracts.cs
- Domain/HR/AdvancesLoans/RepaymentScheduleEntry.cs
- Api/Controllers/HR/AdvanceLoanControllers.cs

All paths above are below `src`. Existing dirty edits in these files are preserved;
the full working-tree diff also contains earlier/other owners' changes.
