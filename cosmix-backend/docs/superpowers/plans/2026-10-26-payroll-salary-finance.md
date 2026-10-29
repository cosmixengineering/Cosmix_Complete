# Payroll tasks 12–21 implementation and verification

User-authorized scope: effective salary master, CSV/XLSX salary import, complete salary calculation, controlled approvals, employee payslips, actual statutory entries and source-bound accounting. Monthly, daily and hourly wages are all required. Company amounts, applicability, proration and approval policies remain configurable by authorized HR/Management/Finance; synthetic test values are not company policy.

## Shared boundaries

- `PayrollHandoffBatch.Id` remains the payroll identity. New complete runs are explicitly `salary-v2`; historical adjustment-only batches remain readable and cannot be represented as complete salary.
- Salary uses protected, approved compensation and daily attendance evidence. Monthly entitlement and retained absence deductions must not be duplicated. Daily/hourly entitlement uses configured payable time; overtime remains separately approved.
- Components and imports create canonical pending requests. Independent approval remains necessary; imports never silently overwrite salary master or manufacture attendance evidence.
- HR approval, independent Management lock and Finance handoff are distinct. Prepared lines are immutable. Corrections preserve prior history and cannot reuse settled sources.
- Statutory employee deductions reference actual approved employee entries. Employer contributions never reduce employee net pay. Accounting derives its totals from the approved locked run and explicit approved posting configuration.
- Payslips disclose only the requesting employee's frozen approved salary, or an explicitly authorized management selection. Documents and private source uploads are not public files.

## Ownership

| Owner | Files / work |
|---|---|
| Engine agent | Salary policies/components, handoff calculation and lifecycle, monthly/daily/hourly attendance input compatibility, source reader and engine tests (12,14,15,16) |
| Finance agent | Actual statutory entries, account/period/profile configuration, source-bound journals and Finance tests (18–21) |
| Client agent | Web/mobile workflows for these APIs, error handling and client tests |
| Coordinator | Import (13), payslips (17), shared catalog/composition/context/migrations, integration and acceptance verification |

Only the coordinator generates/applies migrations and runs heavy builds. Agents coordinate public contracts before consuming them; unrelated existing changes are preserved.

## Acceptance

1. Validate source-backed monthly, daily and hourly calculations including join/leave, revisions, paid/unpaid leave, overtime, rounding and duplicate loans/components.
2. Test CSV/XLSX mapping, malformed files/formulas/limits, duplicate rows/files, stale source and atomic apply; retain source hash and canonical request linkage.
3. Reject self-approval, stale versions, unauthorized scopes, wrong lifecycle and altered evidence; exercise approved successor/correction rules.
4. Check own payslip isolation and PDF/XLSX totals against frozen salary, including zero amounts and deductions.
5. Verify employee statutory totals, approvals, missing applicability evidence, employer separation and exact source binding.
6. Verify balanced source-derived journals, configured account types, closed periods, currency, duplicate posting and idempotent retries.
7. Release build, appropriate regression tests and isolated SQL/API acceptance. Apply additive reviewed migrations to the authorized testing target after backup. No real salary payment or invented real company configuration.

The 28 October continuation retains fresh evidence under `D:/CosmixPayrollVerification20261028`; the former `D:/CosmixPayroll12to21` directory was unavailable on resumption. Passing builds alone do not mark tasks complete.

## Executed continuation — 28 October

Tasks 12–21 have implementation, service/controller checks and SQL persistence verification. The final backend run passed 463 tests with no skips, and the Release API build passed with zero warnings/errors. Four reviewed migrations were applied to a restored clone and then `Cosmix_Testing` after a verified backup; migration count is 30 and existing business-table row counts were preserved. See [the verification report](../../Payroll-Salary-Finance-Verification-20261028.md) for task-specific coverage, client evidence and operational acceptance boundaries. A real company payroll-month comparison and physical employee/provider acceptance are not implied by these results.
