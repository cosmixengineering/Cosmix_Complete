# Cosmix ERP — Batch 3 Company Configuration & Payroll Readiness
**Document ID:** `COSMIX-ACC-B3-005`  
**Date:** October 29, 2026  
**Scope:** Task 9 Real Company Payroll Setup, Policy Readiness, and Finance Mapping  
**Status:** Implemented, Tested & Verified  

---

## 1. Executive Summary

Task 9 establishes the production readiness gate for the Cosmix ERP payroll engine. Before any payroll calculation or salary batch can be initiated, the system must verify that:
1. Every active employee has an approved, active base compensation structure.
2. Every active employee is assigned to a valid work shift for the payroll month.
3. Statutory coverage decisions (EOBI, SESSI, and Income Tax brackets) are explicitly defined.
4. An approved Payroll Posting Profile exists with valid Finance General Ledger debit/credit account mappings.
5. All corporate HR policies governing leaves, overtime, and deductions are published and approved.

To prevent runtime calculation aborts or unposted payroll batches, a dedicated **Company Payroll Readiness Service**, API endpoint, and interactive React dashboard were built and proven.

---

## 2. Policy-Driven Statutory Engine (Pakistan Compliance)

### 2.1 Dynamic, Non-Hardcoded Statutory Calculation
The Cosmix ERP payroll calculation engine rejects hardcoded statutory rates. All statutory deduction algorithms are governed by versioned database policy tables:
- **EOBI (Employees' Old-Age Benefits Institution):**
  - Employer share and employee share are queried from `EmployeeStatutoryCoverages` and `PolicyVersions`.
  - Configurable minimum wage baselines and contribution caps without requiring binary recompilation.
- **SESSI (Sindh Employees' Social Security Institution):**
  - Wage ceiling limits and employer contribution percentages are managed as dynamic policy records.
- **Income Tax (FBR Withholding Tax):**
  - Progressive tax slabs (Finance Act schedules) are stored as dynamic brackets, calculating monthly withholding based on annualized taxable earnings.

---

## 3. Finance General Ledger Chart of Accounts Mapping

### 3.1 Payroll Posting Profile Structure
Payroll entries cannot be posted to Finance without an active, approved `PayrollPostingProfile`. The profile defines the exact accounting rules:

```
+---------------------------------------------------------------------------------+
|                          PAYROLL POSTING PROFILE                                 |
+------------------------------------+--------------------------------------------+
| Component                          | Default GL Account Mapping                 |
+------------------------------------+--------------------------------------------+
| Basic Salary & Allowances (Debit)  | 501000 - Direct Salaries & Wages Expense   |
| Site / Project Direct Labor (Debit)| 501100 - HVAC Project Labor Cost           |
| Net Salary Payable (Credit)        | 201000 - Accrued Payroll Clearing Account  |
| Income Tax Payable (Credit)        | 202000 - FBR Withholding Tax Payable       |
| EOBI Payable (Credit)              | 203000 - EOBI Employer/Employee Payable    |
| SESSI Payable (Credit)             | 204000 - SESSI Contribution Payable        |
+------------------------------------+--------------------------------------------+
```

### 3.2 Preflight Validation Rules
The readiness service validates that:
- An approved profile exists whose `EffectiveFrom` <= calculation month start and `EffectiveToExclusive` > calculation month start.
- Every mapped account code actually exists in `[finance].[ChartOfAccounts]` and is active.
- If any account mapping is missing or unapproved, the engine raises a **Blocking Issue**, preventing payroll generation.

---

## 4. Company Payroll Readiness Service Architecture

### 4.1 Clean Architecture Decoupling
To adhere to Clean Architecture principles:
1. `Cosmix.Application` defines `ICompanyPayrollReadinessRepository` and `CompanyPayrollReadinessService`.
2. `Cosmix.Infrastructure` implements `CompanyPayrollReadinessRepository`, performing optimized LINQ queries against EF Core DB sets (`Employees`, `PayrollSalaryComponents`, `EmployeeShiftAssignments`, `EmployeeBankDetails`, `EmployeeStatutoryCoverages`, `PayrollPostingProfiles`, and `PolicyVersions`).
3. Application layer has zero direct references to `Microsoft.EntityFrameworkCore` or DbContext.

### 4.2 Issue Classification Matrix

| Issue Code | Category | Severity | Condition | Remediation Guidance |
|---|---|---|---|---|
| `missing_base_salary` | Compensation | **Blocking** | Active employee has no fixed earning component. | Define base compensation in employee contract before calculation. |
| `missing_shift_assignment` | ShiftScheduling | **Blocking** | Active employee has no shift schedule for target month. | Assign standard or project shift schedule to employee. |
| `missing_payroll_posting_profile` | FinanceLedger | **Blocking** | No active approved posting profile found. | Create and approve a Payroll Posting Profile in Finance settings. |
| `missing_ledger_accounts` | FinanceLedger | **Blocking** | Posting profile accounts missing in Chart of Accounts. | Ensure all clearing and expense accounts exist in Chart of Accounts. |
| `missing_bank_account` | EmployeeMasterData | **Warning** | Active employee has no verified bank account. | Add employee IBAN/bank account or confirm manual cash voucher disbursement. |
| `missing_statutory_coverage` | PolicyGovernance | **Warning** | Active employee missing explicit statutory record. | Configure EOBI/SESSI coverage or document statutory exemption. |
| `unapproved_policy_version` | PolicyGovernance | **Warning** | Draft policy version exists in calculation period. | Review and approve pending policy version before finalizing payroll. |

### 4.3 Confidentiality & Data Protection Guard
A critical architectural constraint verified in `CompanyPayrollReadinessTests.cs`:
> **The readiness report must never expose individual salary amounts, compensation values, or net pay figures.**

All outputs strictly contain:
- Employee ID / Employee Code
- Full Legal Name
- Classification Category
- Specific defect description and remediation action

No currency or monetary amount properties exist on the report DTO.

---

## 5. API Endpoint Specifications

- **Route:** `GET /api/v1/hr/payroll/salary/readiness`
- **Query Parameters:**
  - `year`: Target calendar year (e.g., `2026`)
  - `month`: Target calendar month (e.g., `10`)
- **Required Authorization:** Bearer token with `Permissions.HR.PayrollSalaryRead`
- **Example Response Payload:**

```json
{
  "year": 2026,
  "month": 10,
  "generatedAt": "2026-10-29T14:55:00Z",
  "status": "ActionRequired",
  "isReadyForPayrollCalculation": false,
  "blockingIssuesCount": 2,
  "warningsCount": 1,
  "issues": [
    {
      "code": "missing_base_salary",
      "severity": "Blocking",
      "category": "Compensation",
      "employeeId": 14,
      "employeeCode": "EMP-0014",
      "employeeName": "Ali Hassan",
      "message": "Employee Ali Hassan (EMP-0014) has no active base salary structure configured.",
      "remediationAction": "Navigate to Employee Profile > Compensation and configure fixed base salary component."
    },
    {
      "code": "missing_shift_assignment",
      "severity": "Blocking",
      "category": "ShiftScheduling",
      "employeeId": 14,
      "employeeCode": "EMP-0014",
      "employeeName": "Ali Hassan",
      "message": "Employee Ali Hassan (EMP-0014) has no active shift schedule assigned for 10/2026.",
      "remediationAction": "Assign employee to Morning Shift or Field Roster in Attendance Management."
    },
    {
      "code": "missing_bank_account",
      "severity": "Warning",
      "category": "EmployeeMasterData",
      "employeeId": 22,
      "employeeCode": "EMP-0022",
      "employeeName": "Tariq Mahmood",
      "message": "Employee Tariq Mahmood (EMP-0022) does not have verified bank details configured.",
      "remediationAction": "Add verified IBAN or designate employee for Cash / Manual Voucher disbursement."
    }
  ],
  "coverage": {
    "totalActiveEmployees": 45,
    "employeesWithConfiguredSalary": 44,
    "employeesWithShiftAssignment": 44,
    "employeesWithVerifiedBankAccount": 42,
    "employeesWithStatutoryDecisions": 45,
    "hasApprovedPostingProfile": true,
    "hasAllRequiredLedgerAccounts": true
  }
}
```

---

## 6. Frontend Interactive Dashboard

**Location:** [cosmix_frotned/src/modules/hr/components/finance/CompanyPayrollReadinessDashboard.tsx](file:///c:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix_frotned/src/modules/hr/components/finance/CompanyPayrollReadinessDashboard.tsx)

### UI Capabilities:
1. **Status Banner:** Dynamically shifts color and alert styling:
   - Green (`Ready`): "Company is fully configured. Payroll calculation can proceed safely."
   - Yellow (`Action Required`): "Warnings detected. Calculation permitted, but manual review advised."
   - Red (`Blocked`): "Blocking issues detected! Payroll calculation cannot proceed until resolved."
2. **Key Metric Summary Tiles:**
   - Active Employees count.
   - Compensation Coverage percentage.
   - Shift Scheduling percentage.
   - Bank Account Coverage percentage.
3. **Tabbed Issue Management:**
   - Tab 1: **Blocking Issues** (High-priority action list with employee names, codes, and exact remediation instructions).
   - Tab 2: **Warnings** (Actionable non-blocking alerts such as missing bank accounts or unapproved policies).
4. **Finance & Policy Verification Badges:**
   - Approved Posting Profile: Checked & Verified.
   - Chart of Accounts Mapping: Checked & Verified.

---
*Certified ready for operational business deployment.*
