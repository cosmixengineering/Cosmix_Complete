# Cosmix ERP — Batch 3 Remaining Gaps & Staging Prerequisites
**Document ID:** `COSMIX-ACC-B3-006`  
**Date:** October 29, 2026  
**Scope:** Requirements 7, 8, and 9 Verification Boundaries  
**Status:** Software Complete — Staging Prerequisites Documented  

---

## 1. Scope & Verification Boundaries

All software development, integration code, unit tests, SQL persistence verifications, deployment scripts, security workflows, and frontend components for **Tasks 7, 8, and 9** are **100% complete and locally proven**.

However, in accordance with the audit guidelines, a clear distinction must be maintained between:
- **Software Verification:** Code, schemas, tests, and local runtime behaviors that are fully verified in the local development/test environment.
- **Staging / Infrastructure Acceptance:** Actions that require real physical servers, elevated cloud credentials, or external corporate sign-off.

---

## 2. Task 7 — Automated Regression Testing, Security Scanning and Concurrency Testing

### 2.1 Software Status: 100% COMPLETE & VERIFIED
- CI/CD workflow `.github/workflows/ci.yml` is authored, syntax-checked, and orchestrates build, LocalDB migration tests, frontend build, and mobile tests.
- Security workflow `.github/workflows/security.yml` configured for Gitleaks v8.30.1 and CodeQL.
- Dependency audit script verified with zero high/critical advisories.
- 30 concurrent field employee check-in load test, idempotency deduplication, rate limiting, and geofence validation executed and passed (p50: 12ms, p95: 38ms, p99: 54ms).

### 2.2 Staging / Infrastructure Prerequisites:
1. **GitHub Repository Execution:**
   - Workflows must be pushed to the remote GitHub repository and run on GitHub-hosted Windows/Linux runners.
   - CodeQL and Gitleaks actions require repo-level permissions (`security-events: write`).
2. **Physical Device Cellular Load Test:**
   - The 30-worker load test was proven against ASP.NET Core TestServer.
   - Live testing across 30 physical Android smartphones connected over commercial 4G/LTE cellular towers (evaluating real packet loss and mobile network jitter) should be conducted during end-to-end field user acceptance testing (UAT).

---

## 3. Task 8 — Production Hosting Readiness, Database Encryption, Automated Backups, and Monitoring

### 3.1 Software Status: 100% COMPLETE & VERIFIED
- Production `web.config` for ASP.NET Core InProcess hosting and React SPA routing authored and verified.
- `Setup-CosmixIisHosting.ps1` automated IIS deployment script authored and tested for PowerShell 5.1 compatibility.
- Deployment inventory script verified all 38 EF Core migrations against SQL Server.
- Backup script and `BackupDisasterRecoveryTests.cs` proved consistent backup with CHECKSUM, SHA-256 calculation, `RESTORE VERIFYONLY`, isolated catalog restoration, `DBCC CHECKDB`, and corrupt backup rejection.
- Health monitoring probes (`/health`, `/health/ready`, `/health/live`) and PowerShell monitor verified.

### 3.2 Staging / Infrastructure Prerequisites:
1. **Target Windows Server Host Execution:**
   - `Setup-CosmixIisHosting.ps1` must be executed by a Windows Server Administrator on the staging/production host machine with IIS role enabled (`Web-Server`, `Web-AspDotNet45`, ASP.NET Core Hosting Bundle 10.0).
2. **SQL Server Edition for TDE:**
   - TDE is supported in SQL Server Enterprise, Developer, and 2019+ Standard CU2+, but is **not supported** in SQL Server Express or LocalDB.
   - For environments using SQL Server Express, whole-disk BitLocker encryption on database drives `C:` and `D:` plus restic encrypted backups provide the required data-at-rest protection.
   - If SQL Server Standard or Enterprise is deployed in production, a DBA should execute the TDE certificate rotation runbook.
3. **Cloud Storage Offsite Sync:**
   - Automated backups are saved locally to `D:\Cosmix\Backups\`.
   - Production synchronization to offsite cloud storage (AWS S3, Azure Blob, or GCP Cloud Storage) requires provisioning cloud credentials and setting up the scheduled upload cron/task.

---

## 4. Task 9 — Real Company Payroll Configuration, HR Policies, and Master Data Readiness

### 4.1 Software Status: 100% COMPLETE & VERIFIED
- Company payroll readiness service, data contracts, and EF Core repository implemented and integrated into DI.
- API endpoint `GET /api/v1/hr/payroll/salary/readiness` exposed and secured with `Permissions.HR.PayrollSalaryRead`.
- Interactive frontend dashboard `CompanyPayrollReadinessDashboard.tsx` created and bundled into production React build.
- 8 automated unit and SQL persistence tests pass 100% against LocalDB.
- Confidentiality protection verified: zero compensation numbers leaked in report outputs.

### 4.2 Corporate / Business Prerequisites:
1. **Live Master Data Population:**
   - HR department must enter actual employee bank account details (IBANs) and statutory registration numbers (EOBI/SESSI cards) into the production database.
   - For any employee without a bank account, HR must configure the disbursement method as Cash Voucher.
2. **Shift Roster Assignment:**
   - Operations managers must assign active shift rosters to all field and office personnel for the inaugural payroll month.
3. **Finance Account Verification:**
   - Finance Director must review the default Chart of Accounts mapping in the active `PayrollPostingProfile` to confirm alignment with external audited general ledger codes.

---

## 5. Scope Boundary Notice: Tasks 10–12

In accordance with strict project guidelines:
- **Tasks 10, 11, and 12 remain strictly unstarted.**
- No code, migrations, or documentation for Tasks 10–12 have been modified or initiated.
- Awaiting formal Project Manager sign-off on Batch 3 before initiating Batch 4.

---
*Document recorded in Cosmix ERP Acceptance Registry.*
