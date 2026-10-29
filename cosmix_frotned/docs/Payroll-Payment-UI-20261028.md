# Payroll payment UI · 28 October 2026

The existing Payroll Accounting screen now exposes an independent Reports tab for `payroll.reports.read`. Its paged frozen-run selector does not require posting, accounting setup or handoff-read authority. Finance users also retain the report alongside a selected salary obligation. Server permissions and employee/account scopes remain authoritative.

- **Settlement report:** frozen employee gross earnings, deductions and net entitlement, separate confirmed/reserved/outstanding payments, signed component rows and reconciliation issues. `payroll.reports.read` controls the report; `payroll.reports.export` separately controls CSV. The browser displays server totals and does not calculate salary or post reconciliation adjustments.
- **Payment outcomes:** confirmation, failed-payment reversal request, ordinary reversal request and independent reversal approval. The form requires a single-line reason (up to 500 characters), retained evidence text (up to 8000 characters), the current payment version, and a reversal posting date for approval. Maker/requester exclusions follow server metadata. Failed, reversed and unposted records do not expose these actions.
- **Bank profiles:** the server supplies the two HBL workbook structures extracted from the owner's templates. Configuration requires explicit bank routing mappings for IBFT or a reviewed beneficiary-bank-name list for internal transfers. No routing codes or bank-name mapping is fabricated. Exact columns, literals, template hash and output rules are visible for independent review. Configuration uses `payroll.accounting.configure`; approval uses `payroll.accounting.configure.approve`; listing/templates/export use `payroll.accounting.bank.export`.
- **Protected bank export:** selects 1–500 actual posted, employee-bound payments without an earlier export or pending reversal. It binds the obligation fingerprint, reviewed currency-matched profile and explicit value date. The response supplies retained-file metadata. Existing files are recovered through the obligation's export list; renewal binds the same export ID and SHA-256 plus a fresh explicit reason. Download/renewal revalidate sources on the server. No screen submits a bank transfer or marks a downloaded file as paid.

The new outcome and bank-file write controls show a concrete review before submission. The shared Finance action adapter supplies the idempotency key. The reviewed payload stays in memory for an explicit unchanged retry when the response is uncertain; definitive rejection unlocks corrections. Private evidence is not stored in browser storage. Downloads use authenticated transport, session/unmount cancellation, MIME/filename validation and SHA-256 checks. Bank downloads also validate the XLSX file signature. Object URLs are released after download.

Read endpoints:

- `/api/v1/hr/payroll-settlement/{batchId}` and `/export?format=csv`
- `/api/v1/hr/payroll-settlement?page=1&pageSize=25` for report-only run selection
- `/api/v1/payroll-accounting/bank/templates`
- `/api/v1/payroll-accounting/bank/formats`
- `/api/v1/payroll-accounting/bank/exports?obligationId={obligationId}`
- `/api/v1/payroll-accounting/bank/exports/{exportId}/download`

Write endpoints use the existing authenticated POST/idempotency convention:

- `/api/v1/payroll-accounting/payments/{paymentId}/confirm`
- `/api/v1/payroll-accounting/payments/{paymentId}/reversal-request`
- `/api/v1/payroll-accounting/payments/{paymentId}/reversal-approve`
- `/api/v1/payroll-accounting/bank/formats`
- `/api/v1/payroll-accounting/bank/formats/{formatId}/approve`
- `/api/v1/payroll-accounting/bank/exports`
- `/api/v1/payroll-accounting/bank/exports/{exportId}/renew`

The same increment corrects TypeScript narrowing for the attendance calendar's explicit unavailable locked-day record. Missing locked summaries remain unavailable, never zero worked time or absence, and do not open calculation/evidence actions as though a retained summary existed.

Controlled source, schema, permission, rendering, export and retry checks live outside Git at `D:/CosmixPayrollVerification20261028/web/`. They use synthetic transport or controlled React ports; they are not bank certification or browser acceptance with real payroll data. The verification report in that directory records the latest command results. Real bank portal upload, real transfers, complete role acceptance and HR/Finance sign-off remain separate. No production data, credentials, test fixture or generated screenshot is checked in.
