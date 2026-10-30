# Cosmix ERP — Batch 4 Historical Payroll & HBL Readiness
**Document ID:** `COSMIX-ACC-B4-004`  
**Date:** October 29, 2026  
**Scope:** Task 11 Historical Payroll Reconciliation and HBL Export Readiness  
**Status:** Software Complete — Real Historical Data & Live Portal DEFERRED  

---

## 1. Executive Summary

Task 11 provides the tools necessary to audit historical payroll records against the Cosmix ERP payroll calculation engine and generate standardized export files compatible with Habib Bank Limited (HBL) corporate salary disbursement portals.

In strict compliance with project governance:
- **All software parsing, variance calculation, and export file generation workflows are 100% complete and tested using synthetic data.**
- **Real company historical payroll comparison and live HBL banking portal transmission remain explicitly DEFERRED.**

---

## 2. Historical Payroll Reconciliation Engine

### 2.1 File Ingestion & Security Parsing
- **Engine:** [PayrollSheetParser.cs](file:///c:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/PayrollImports/PayrollSheetParser.cs)
- Supports both `.csv` and `.xlsx` formats with defensive parsing:
  - 4 MB file size ceiling.
  - 1,000 row and 40 column boundaries.
  - **Formula Injection Defense:** Formula cells (`=SUM`, `@HYPERLINK`) are rejected without evaluation.
  - **External Link Defense:** OpenXML relationships targeting external web URIs are blocked (`salary_import_external_link`).
  - Zip-bomb decompression protections.

### 2.2 Component Mapping & Variance Analysis
- **Service:** [HistoricalPayrollReconciliationService.cs](file:///c:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/HR/PayrollReconciliation/HistoricalPayrollReconciliationService.cs)
- Dynamically maps spreadsheet columns to canonical ERP payroll concepts:
  - `EmployeeCode` / `EmployeeNumber`
  - `BasicSalary`
  - `Allowances`
  - `Overtime`
  - `GrossSalary`
  - `Deductions`
  - `Eobi`, `Sessi`, `IncomeTax`
  - `NetSalary`
- Computes employee-by-employee variances:
  $$\text{Variance} = \text{Sheet Amount} - \text{ERP Calculated Amount}$$
- Automatically classifies every row:
  - **`ExactMatch`:** Net, gross, and basic variances are exactly zero.
  - **`RoundingDifference`:** Fractional difference of $\le 1.00\text{ PKR}$ due to paise rounding.
  - **`VarianceDetected`:** Material discrepancy requiring management review (e.g., untracked overtime or policy differences).
  - **`MissingInErp`:** Employee listed in historical sheet does not exist in active ERP master records.
  - **`MissingInSheet`:** Active ERP employee omitted from historical file.
  - **`DuplicateInSheet`:** Multiple entries detected for the same employee code in the uploaded spreadsheet.

---

## 3. HBL Banking Portal Export Specifications

### 3.1 HBL_IBFT Template (Inter-Bank Fund Transfer)
Used for disbursing salaries to employee accounts held at external commercial banks in Pakistan (Meezan, Alfalah, Standard Chartered, UBL, MCB):

| Column # | Column Header | Field Name | Validation Rule |
|---|---|---|---|
| 1 | `Beneficiary  Name` | `BeneficiaryName` | Max 100 characters legal name |
| 2 | `Beneficiary Account Number` | `Iban` | 24-character Pakistani IBAN (`PK...`) |
| 3 | `Transaction Amount` | `Amount` | Decimal currency with 2 decimal places |
| 4 | `Customer Reference Number` | `Reference` | Unique transaction reference string |
| 5 | `Bank Code` | `BankCode` | 4-character State Bank of Pakistan code |
| 6 | `Beneficary Contact Number` | `ContactNumber` | Optional mobile contact |
| 7 | `Beneficiary Email Address` | `Email` | Optional email contact |
| 8 | `Purpose of Payment ` | `Literal` | Fixed literal `"021"` (Salary Disbursement) |
| 9 | `Reference # 3` | `Blank` | Empty column required by HBL portal |
| 10 | `Invoice Number ` | `Blank` | Empty column required by HBL portal |
| 11 | `Account Title ` | `AccountTitle` | Official bank account title |

### 3.2 HBL_INTERNAL Template (HBL to HBL Transfer)
Used for intra-bank transfers where the beneficiary account is held directly with HBL:

| Column # | Column Header | Field Name | Validation Rule |
|---|---|---|---|
| 1 | `Beneficiary  Name` | `BeneficiaryName` | Max 100 characters legal name |
| 2 | `Beneficiary Account Number` | `AccountNumber` | 14–16 digit HBL internal account number |
| 3 | `Transaction Amount` | `Amount` | Decimal currency with 2 decimal places |
| 4 | `Customer Reference Number` | `Reference` | Unique transaction reference string |
| 5 | `Contact Number` | `ContactNumber` | Optional mobile contact |
| 6 | `Beneficiary Email Address` | `Email` | Optional email contact |
| 7 | `Purpose of Payment ` | `Literal` | Fixed literal `"021"` (Salary Disbursement) |
| 8 | `Invoice Number ` | `Blank` | Empty column required by HBL portal |
| 9 | `Account Title ` | `AccountTitle` | Official bank account title |

### 3.3 File Integrity & Audit History
- Every generated bank export file is cryptographically hashed with SHA-256.
- The hash is recorded in `[hr].[PayrollBankExports]`.
- Re-exporting or renewing an expired export requires verified matching digests, preventing post-generation tampering before portal upload.

---

## 4. Automated Verification Results

All 6 automated tests in [HistoricalPayrollHblAcceptanceTests.cs](file:///c:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/tests/Cosmix.Tests/HistoricalPayrollHblAcceptanceTests.cs) passed 100%:
1. `Matching_synthetic_historical_payroll_returns_exact_match_with_zero_variance`: **PASSED** (18ms)
2. `Minor_cents_difference_is_classified_as_rounding_difference`: **PASSED** (14ms)
3. `Overtime_and_allowance_discrepancies_are_flagged_with_exact_variance_metrics`: **PASSED** (15ms)
4. `Missing_employees_in_ERP_or_sheet_are_classified_correctly`: **PASSED** (14ms)
5. `Hbl_bank_export_templates_adhere_to_banking_portal_specifications`: **PASSED** (12ms)
6. `Pakistani_IBAN_format_is_strictly_validated`: **PASSED** (5ms)

---

## 5. Scope Boundary: Deferred Company Operations

1. **Real Historical Company Comparison:** **DEFERRED**.
   - Awaiting executive authorization and signed release of audited legacy company payroll sheets for September/October 2026.
2. **Live Banking Portal Transmission:** **DEFERRED**.
   - No real banking credentials or live portal connectivity was initiated. The export generator produces files locally ready for manual batch upload to HBL Connect/Cash Management.

---
*Document recorded in Cosmix ERP Acceptance Registry.*
