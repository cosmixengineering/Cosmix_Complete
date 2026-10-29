# HBL salary upload templates supplied on 28 October 2026

The user confirmed that salary files are uploaded to the HBL portal. Two distinct
Excel layouts were supplied. The original files remain unchanged outside the repository;
employee names, account numbers and customer references must not be copied into Git.

| Layout | Worksheet | Data columns | Populated beneficiary rows | Amounts present | Original SHA-256 |
| --- | --- | --- | --- | --- | --- |
| Mixed banks and wallets (IBFT) | Sheet1 | 11 | 10 | 0 | `56c2380c543b8933ca3b9d8b7d06d8ba7b6c3ba21e59fb9092cb609956d92c04` |
| HBL to HBL | Sheet1 | 9 | 16 | 0 | `a810b52e3a4776d4f0a973e01138f3be31fe736c611a4334c4f29cef5f67978b` |

The HBL workbook has formatting extending to column AJL, but all populated cells
are within A:I. The export must use the nine actual columns, not the inflated used range.
Both files retain account identifiers as text, contain no formulas, and use the
text purpose code `021`. Preserve the supplied header spelling, spaces and order.

IBFT headers: `Beneficiary  Name`, `Beneficiary Account Number`, `Transaction Amount`,
`Customer Reference Number`, `Bank Code`, `Beneficary Contact Number`,
`Beneficiary Email Address`, `Purpose of Payment `, `Reference # 3`,
`Invoice Number `, `Account Title `.

HBL internal headers: `Beneficiary  Name`, `Beneficiary Account Number`,
`Transaction Amount`, `Customer Reference Number`, `Contact Number`,
`Beneficiary Email Address`, `Purpose of Payment `, `Invoice Number `, `Account Title `.

Amounts must come from explicit employee payments bound to the approved frozen
payroll obligation. Account identifiers come from effective verified protected bank
records. Supplied sample recipients are not production configuration or test fixtures.
Bank-code mappings and allowed recipient banks require explicit reviewed profile values;
do not guess a bank code or silently route the wrong type of recipient.

These files establish the requested upload layout. Their blank amounts do not provide
a historical salary calculation or proof of payment. Generated-file structure tests,
bank portal acceptance, actual transfers and independent settlement confirmation are
separate checks.
