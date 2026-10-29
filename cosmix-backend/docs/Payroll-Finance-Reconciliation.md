# Payroll ledger, cost allocation and bank reconciliation

The shared `finance.GeneralLedgerEntries` view exposes the debit and credit legs of committed payroll journals. Included sources are explicitly reported as Payroll. It is not a claim that purchasing, receivables or other Finance modules have been implemented.

## Payroll costs

Post salary accrual and applicable employer contributions before preparing cost allocation. Expense deductions reduce the expense basis; salary payments do not create another expense. Choose explicit Project, Site or General destinations and distribute exactly 100% of each employee's expense. The server derives amounts and distributes rounding cents deterministically. An independently authorized employee approves the proposal. A different login belonging to the same employee does not satisfy independence.

Approved revisions remain effective until another revision is approved. Monthly reports include the latest approved revision per finalized batch and identify batches awaiting allocation. Totals exclude unallocated batches; they must not be presented as a complete month's project labour cost until coverage is complete.

## Bank statement workflow

1. Configure the actual bank ledger account in payroll Finance setup. Import a complete monthly statement for one bank account and currency. This is different from an HBL salary upload template.
2. Select the worksheet, date, reference, debit and credit columns and explicit date format. Use amounts with a decimal point and no currency symbols or thousands separators. A debit decreases the bank balance; a credit increases it. CSV must be UTF-8. XLSX formula cells, external links and merged cells are rejected. Limits are 4 MiB, 1,000 transactions and 40 columns.
3. Supply opening and closing balances. Opening plus all statement movements must equal closing. Each row needs a reference. Identical date/reference/amount rows are rejected for source review rather than silently deduplicated. Inspect the validation preview before importing.
4. Match each payroll transaction against posted salary payment or reversal journal entries. A bank bulk transfer may cover several entries, but amounts and directions must agree exactly. Dates may differ by at most seven days within the selected month. A differing reference requires an explanation. Classify non-payroll bank rows explicitly with a reason; those rows remain outside payroll ledger totals and require the applicable Finance process.
5. Save the proposal. Another authorized employee reviews every match and outside-payroll classification. Neither importer nor proposal maker may close the month. The calendar month must have ended, and every payroll bank ledger entry and statement row must be accounted for. Changed ledger data invalidates the saved proposal until reviewed again.
6. Closing retains immutable statement, proposal and reviewer evidence. Application checks and a SQL trigger reject subsequent backdated entries affecting that bank account/currency/month. Use a separately authorized correction in an open month. Closing does not confirm salary payments, send money, or upload anything to HBL.

Before closing, an incorrect statement may be replaced by a retained revision referencing the previous statement. The same raw file can be revised when mapping or opening/closing balances are corrected; an identical import is rejected. Previous evidence is not deleted. There is no silent reopening of a closed bank month.

## Permissions and operations

- `finance.ledger.read`: shared payroll ledger and trial balance.
- `finance.payroll-costs.read/manage/approve`: allocation workspace, proposals and independent decisions.
- `finance.bank-reconciliation.read/manage/close`: protected bank records, imports/proposals and independent close.

These are current all-scope web/MFA permissions, checked on the server and audited transactionally. They do not add an administrator panel to the employee mobile app. Grant permissions through the existing access administration workflow; introducing a permission does not itself authorize an account.

Apply reviewed migrations with the corresponding backend build. The bank journal trigger requires the updated EF model's trigger-aware insert behavior. Stop the old runtime during migration and restart the matching build; do not apply this trigger underneath an old running binary.

Synthetic test fixtures establish software behavior only. Real HBL acceptance, authentic historical comparisons and named company sign-off remain separate evidence.
