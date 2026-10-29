# Accounts and Finance

This module owns Payables, Receivables, Expenses, Payroll handoff, Financial reports.

The Finance ledger panel reads `/finance/ledger` and `/finance/trial-balance` with current all-scope `finance.ledger.read` authority. It is embedded through this module's public export in payroll accounting. Amounts and opening balances come from committed server journals; the browser never creates ledger entries or supplies totals.

The shared Finance view currently includes payroll accruals, statutory postings, salary payments and reversals. Payables, receivables and other Finance journals are not implemented yet and are explicitly identified as outside this report's connected sources. Future sources must join the shared ledger contract without duplicating existing payroll entries.

Project/site cost allocation and bank-statement reconciliation are separate workflows still being implemented. Do not infer their completion from the ledger panel.

- `pages/`: route-level screens.
- `components/`: UI used only by this module.
- `api/`: this module's endpoint functions; use the shared HTTP client.
- `hooks/`: query and feature hooks.
- `schemas/`: client-side validation and server-response schemas.
- `types/`: frontend DTOs and view models, not EF entities.
- `utils/`: pure helpers specific to this module.
- `index.ts`: public entry for app composition. Internal imports stay relative.

Reuse `src/components` for general controls. Do not deep-import another module. Agree cross-module contracts with the owner. Server authorization and policy calculations remain authoritative.
