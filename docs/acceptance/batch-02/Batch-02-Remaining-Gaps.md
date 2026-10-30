# COSMIX ERP — BATCH 2 REMAINING GAPS & DEFERRED ITEMS
## Requirements 4, 5, and 6: Production Readiness & Boundary Analysis

**Date:** 2026-10-29  
**Branch:** `codex/payroll-payments-22-30`  
**Git Baseline Commit:** `03e913a`  
**Scope:** Requirements 4, 5, and 6 ONLY  

---

## 1. Requirement 4: Bank-Statement Reconciliation

### 1.1 Verified & Completed (In-Scope)
- **Payroll Disbursement Reconciliation:** Complete. Full support for statement parsing (CSV/XLSX), hash-based duplicate detection, 1-to-many journal matching, reversal handling, and non-payroll bank charge justification.
- **Closed-Month Immutability:** Complete. SQL Server trigger `[hr].[TR_PayrollJournal_ClosedBankMonth]` is fully operational and verified against LocalDB, preventing retroactive journal tampering.
- **Four-Eyes Segregation:** Complete. Importer cannot close reconciliation.
- **Web UI:** Complete. `BankReconciliationPanel.tsx` integrated with React query and verified in production frontend bundle build.

### 1.2 Out-of-Scope / Deferred to Subsequent Phases
- **General Corporate Non-Payroll Bank Reconciliation:**  
  *Context:* Reconciling general non-payroll corporate accounts (e.g., supplier accounts payable, customer accounts receivable, vendor checks, procurement advances) was explicitly excluded from the initial Phase 1 delivery scope.  
  *Action:* Dedicated corporate accounting reconciliation workflows will be addressed in a future general finance milestone.

### 1.3 Production Deployment Requirements
- **Finance Team Acceptance:** Prior to production go-live, the Cosmix Finance team must test bank statement formats from their actual banking partners (e.g., Meezan Bank, HBL, Bank Alfalah) to ensure column mapping compatibility with production exports.

---

## 2. Requirement 5: Authenticator (TOTP), Recovery Codes & MFA Reset

### 2.1 Verified & Completed (In-Scope)
- **RFC 6238 TOTP Engine:** Complete. Fully tested against official test vectors, HMAC-SHA1, 30s step, drift tolerance.
- **Secret Encryption:** Complete. Secrets encrypted at rest via ASP.NET Core Data Protection.
- **10 Single-Use Recovery Codes:** Complete. Generated cryptographically, stored as SHA-256 hashes, revoked immediately upon use.
- **Session Revocation & Version Bumping:** Complete. Re-authenticates, invalidates prior sessions, increments `SecurityVersion`.
- **Anti-Downgrade Defense:** Complete. Prevents SMS bypass once TOTP is enrolled.
- **Independent Admin MFA Reset:** Complete. Enforces separation of duties (no self-reset), requires reason and audit logging, invalidates sessions.
- **Web UI:** Complete. `AuthenticatorSettings.tsx` and `AuthenticatorReset.tsx` built and typechecked.

### 2.2 External Prerequisites (Non-Code Blockers)
- **Production SMS Gateway Provider:**  
  *Context:* While TOTP and recovery codes operate 100% independently without SMS, initial user account provisioning and fallback notifications rely on an external SMS provider (e.g., Twilio, Zong/Jazz enterprise SMS gateway).  
  *Action:* Production SMS API keys, sender ID, and webhook endpoints must be configured in production `appsettings.Production.json` / Azure Key Vault.

---

## 3. Requirement 6: Production Android Release Readiness

### 3.1 Verified & Completed (In-Scope)
- **Fail-Closed Release Signing (`release-security.gradle`):** Complete. Fails build if debug keystore is used; enforces external keystore path, RSA 2048+ / EC 256+ keys, and certificate fingerprint validation.
- **Network Security & SPKI Pinning (`release-config.mjs`):** Complete. Enforces SHA-256 certificate pinning (primary + backup pin), prohibits cleartext HTTP, and restricts connections strictly to the authorized origin.
- **Native Configuration Bridge (`ReleaseConfigurationPackage.kt`):** Complete. Bridges verified API base URL into React Native runtime.
- **Configuration Tests (`release-config.test.mjs`):** Complete. 4 automated test cases pass with 100% coverage.

### 3.2 External Prerequisites (IT / Operations Blockers)
- **Production Release Keystore:**  
  *Context:* Real corporate Android release keystores (`cosmix-release.keystore`) must never be committed to source control.  
  *Action:* Corporate IT / Release Management must generate and safeguard the official production keystore on the secure CI/CD build runner or offline signing machine.
- **Live Production Server SPKI Hashes:**  
  *Context:* The actual SHA-256 public key hashes (SPKI pins) must be extracted from the active production SSL certificate and intermediate certificate deployed on the production server (e.g., IIS / Cloudflare / Azure Application Gateway).  
  *Action:* Once the production domain SSL certificate is provisioned, its SPKI pins must be supplied to the release build configuration.

---

## 4. Scope Governance & Deferred Requirements

Per the project protocol and executive instructions:
- **Requirements 1, 2, and 3:** Verified and accepted in Batch 1.
- **Requirements 4, 5, and 6:** Verified and accepted in Batch 2.
- **Requirements 7 through 12:** **STRICTLY DEFERRED.** No implementation, inspection, or testing has been initiated for Requirements 7–12. Execution will begin only upon explicit project manager authorization for Batch 3.
