# COSMIX ERP — BATCH 1 REMAINING GAPS & DEPLOYMENT PREREQUISITES
## Transparent Engineering Disclosure & Staging Readiness Assessment

**Date:** 2026-10-29  
**Branch:** `codex/payroll-payments-22-30` (Commit: `03e913a`)  
**Scope:** Requirements 1, 2, and 3 ONLY  

---

## 1. Classification & Acceptance Status

Per enterprise ERP auditing standards, local automated test passes—regardless of test count or code coverage—cannot serve as a substitute for live environment deployment. The status of each requirement is classified as follows:

| # | Requirement | Classification | Local Verification Result | Staging / Prod Deployment Status |
| :---: | :--- | :--- | :---: | :---: |
| **1** | HR-Approved Payroll Return for Correction | **LOCAL VERIFIED — PASS** | **100% Pass** (5 unit/SQL tests) | Pending Staging Migration & HR Sign-off |
| **2** | Independent Salary Approval for HR/Finance/Admin | **LOCAL VERIFIED — PASS** | **100% Pass** (5 unit/SQL tests) | Pending Identity Role Provisioning |
| **3** | Payroll Integration with GL & Project/Site Costing | **LOCAL VERIFIED — PASS** | **100% Pass** (6 unit/SQL tests) | Verified on Shared SQL View; Staging Prerequisite |

---

## 2. Granular Gap Analysis

### 2.1 Requirement 1: HR-Approved Payroll Return for Correction
- **Local Verification:**
  - Full domain state transitions, database schema columns, EF Core migration, API endpoints, and React frontend UI modals are implemented, integrated, and verified against SQL Server LocalDB.
  - Maker-checker constraints (preparer cannot return own batch, locked batches cannot be returned, blank reasons rejected) are rigorously enforced by unit and SQL tests.
- **Remaining Production / Staging Gaps:**
  1. **Database Migration Execution:** Migration `20261028150542_ReturnApprovedPayrollForCorrection` has only been applied to the local verification database (`Cosmix_Payroll_20261028`). It must be executed on Staging and Production databases through the CI/CD release pipeline before frontend deployment.
  2. **Multi-User Browser UAT:** While the React component compiles without error and the state changes have been verified via API/domain tests, manual User Acceptance Testing (UAT) with real browser sessions simulating two distinct authenticated users (an HR Preparer and a separate Management Reviewer) is required in the Staging environment.

### 2.2 Requirement 2: Independent Salary Approval for HR/Admin/Finance
- **Local Verification:**
  - Non-overlapping cohort preparation via `EmployeeIds`, self-approval exception guards (`DomainValidationException`), and UI cohort selection filters are fully functional.
- **Remaining Production / Staging Gaps:**
  1. **Identity & Access Management (IAM) Role Mapping:** The system relies on caller user IDs (`actorUserId`) and mapped roles (`HR_APPROVER`, `MANAGEMENT_REVIEWER`, `MANAGING_DIRECTOR`). In Staging/Production, identity claims in JWTs from Azure AD / Keycloak / IdentityServer must be verified to ensure that the designated non-beneficiary reviewers possess the exact expected permission scopes (`Payroll.Review.Executive`).
  2. **Automated Notification Triggers:** When a batch for Cohort B (HR/Finance) is prepared, an automated email/push notification should ideally alert the independent Managing Director. Currently, notification dispatch for segregated cohorts is handled manually via the dashboard queue.

### 2.3 Requirement 3: Payroll Integration with Finance GL & Project/Site Costing
- **Local Verification:**
  - SQL Server view `[finance].[GeneralLedgerEntries]` projects balanced payroll debits and credits with zero trial balance variance. Project and site cost allocation calculations honor the 10,000 basis points rule and penny rounding invariant.
- **Remaining Production / Staging Gaps:**
  1. **Broader Finance Module Integration (Requirements 4–12 Dependency):** The shared ledger view currently ingests payroll vouchers (`IncludedSources: ["Payroll"]`). The broader Accounts Receivable (AR) and Accounts Payable (AP) ledger views are scheduled for subsequent batches (Requirements 4–12). The payroll side of the ledger is complete and self-balancing, but company-wide consolidation with live vendor invoices remains a dependency on future phases.
  2. **Site Master Data Sync:** The project and site allocation engine references engineering project codes (e.g., `PRJ-KHI-HVAC-01`). A synchronization check between the Project Management module and the Cost Allocation service should be validated once real project codes are seeded on Staging.

---

## 3. Staging & Production Deployment Prerequisites Checklist

Before promoting Batch 1 to Staging or Production, the following actions must be taken by the DevOps and Database Administration teams:

- [ ] **Database Migration:** Apply EF Core migration `20261028150542_ReturnApprovedPayrollForCorrection` to Staging Azure SQL / SQL Server.
- [ ] **Column Verification:** Verify presence of `[ReturnApprovalRequestId]`, `[ReturnedReason]`, `[ReturnedAtUtc]`, and `[ReturnedByUserId]` on `[payroll].[PayrollHandoffBatches]`.
- [ ] **Role Assignment:** Ensure at least one user with role `MANAGING_DIRECTOR` (independent reviewer) is provisioned on Staging who is NOT an employee in the HR or Finance departments.
- [ ] **UI Deployment:** Deploy updated `cosmix_frotned` build artifact to Staging web host.
- [ ] **UAT Session:** Execute the scripted 18-step acceptance scenario with the HR and Finance project stakeholders.

---

## 4. Scope Boundary Guarantee

In strict accordance with project rules:
- **Requirements 4 through 12 have NOT been started.**
- No code, migrations, or tests for subsequent phases have been created or modified.
- No Git commits, pushes, merges, or rebases have been executed.
