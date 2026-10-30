# COSMIX ERP — BATCH 2 WORKFLOW EVIDENCE
## Requirements 4, 5, and 6: Step-by-Step Workflow Walkthrough

**Date:** 2026-10-29  
**Branch:** `codex/payroll-payments-22-30`  
**Execution Context:** Verification Test Suite & LocalDB Integration  

---

## 1. Requirement 4: Payroll Bank-Statement Reconciliation Workflow

The bank reconciliation workflow connects physical bank statements to recorded payroll ledger disbursements, enforcing strict accounting reconciliation and month closing.

```
┌─────────────────┐       ┌────────────────────┐       ┌──────────────────────┐       ┌───────────────────────┐
│ Bank Statement  │  ──>  │ Parse & SHA-256    │  ──>  │ Propose Matches      │  ──>  │ Close Month           │
│ (CSV / XLSX)    │       │ Verification       │       │ (1-to-many & rev)    │       │ (Different Officer)   │
└─────────────────┘       └────────────────────┘       └──────────────────────┘       └──────────┬────────────┘
                                                                                                 │
                                                                                                 ▼
                                                                                      ┌───────────────────────┐
                                                                                      │ SQL Trigger Activated │
                                                                                      │ TR_ClosedBankMonth    │
                                                                                      │ Blocks Backdating     │
                                                                                      └───────────────────────┘
```

### Step 4.1: Statement Import & Integrity Verification
1. **Upload Action:** The finance officer uploads a statement file (`bank_statement_oct_2026.csv`).
2. **Payload:**
   ```json
   {
     "accountCode": "1010-MEEZAN-PAYROLL",
     "period": "2026-10-01",
     "format": "CSV",
     "openingBalance": 1500000.00,
     "closingBalance": 850000.00,
     "mapping": {
       "format": "CSV",
       "dateColumn": "Date",
       "referenceColumn": "Description",
       "debitColumn": "Debit",
       "creditColumn": "Credit",
       "dateFormat": "yyyy-MM-dd"
     }
   }
   ```
3. **Execution Logic:**
   - Computes SHA-256 hash of file content. Rejects duplicate submissions with HTTP 409 (`bank_statement_duplicate_import`).
   - Parses statement rows:
     - Row 1: `2026-10-05, "BULK-PAYROLL-SALARY-01", Debit: 600000.00, Credit: -` -> Amount: `-600000.00`
     - Row 2: `2026-10-06, "REVERSAL-EMP-042", Debit: -, Credit: 50000.00` -> Amount: `+50000.00`
     - Row 3: `2026-10-10, "BANK-CHARGE-SERVICE", Debit: 100000.00, Credit: -` -> Amount: `-100000.00`
   - Validates balance equation:
     $$\text{Opening} (1,500,000) - 600,000 + 50,000 - 100,000 = 850,000 \quad (\text{Equal to Closing})$$
   - Persists `BankStatementSnapshot` with state `PendingReconciliation`.

### Step 4.2: Matching Proposal (1-to-Many & Reversals)
1. **Ledger Retrieval:** System loads unreconciled `BankLedgerMovement` entries for account `1010-MEEZAN-PAYROLL` for October 2026:
   - Movement A: `2026-10-05, BULK-PAYROLL-SALARY-01, -250000.00 PKR` (Site A employees)
   - Movement B: `2026-10-05, BULK-PAYROLL-SALARY-01, -350000.00 PKR` (Head Office employees)
   - Movement C: `2026-10-06, REVERSAL-EMP-042, +50000.00 PKR` (Returned payment)
2. **Matching Resolutions Submitted:**
   ```json
   {
     "statementId": "d3b07384-d113-4f96-8588-43d78909618f",
     "matches": [
       {
         "statementRowNumber": 1,
         "ledgerMovementIds": ["uuid-mov-A", "uuid-mov-B"],
         "nonPayrollReason": null,
         "overrideNote": null
       },
       {
         "statementRowNumber": 2,
         "ledgerMovementIds": ["uuid-mov-C"],
         "nonPayrollReason": null,
         "overrideNote": null
       },
       {
         "statementRowNumber": 3,
         "ledgerMovementIds": [],
         "nonPayrollReason": "Monthly bank account maintenance fee confirmed with Meezan Bank statement advice",
         "overrideNote": null
       }
     ]
   }
   ```
3. **Projection Validation:**
   - Statement Row 1 (-600,000) == Sum of Movements A & B (-250,000 + -350,000). Net variance: `0.00`.
   - Statement Row 2 (+50,000) == Movement C (+50,000). Net variance: `0.00`.
   - Statement Row 3 (-100,000) classified as non-payroll expense with valid reason.
   - All statement rows resolved: `CanClose = true`.

### Step 4.3: Final Month Closure (Four-Eyes Enforcement)
1. **Close Request:** Submitted by Finance Approver (different from creator).
2. **Enforcement:**
   - If `currentActor.EmployeeId == statement.CreatedByEmployeeId`: throws HTTP 403 (`Forbidden: Segregation of duties violation. Importer cannot close statement`).
   - If a new bank journal was posted in the background between proposal and close: throws HTTP 409 (`bank_ledger_changed`).
3. **Execution:**
   - Reconciliation marked `Closed = true`, `ClosedAt = UtcNow`, `ClosedBy = ApproverId`.
   - Trigger `[hr].[TR_PayrollJournal_ClosedBankMonth]` is now armed for month `2026-10`. Any retroactive journal write to `[hr].[PayrollJournalEntries]` targeting October 2026 is immediately rolled back by SQL Server.

---

## 2. Requirement 5: TOTP Authenticator, Recovery Codes & MFA Reset Workflow

```
┌─────────────────────────┐       ┌────────────────────────┐       ┌─────────────────────────┐
│ User Re-Auth & Setup    │  ──>  │ Confirm Code & Save    │  ──>  │ Active Authenticator    │
│ (Password Re-entry)     │       │ Secret (Encrypted)     │       │ Sessions Re-keyed       │
└─────────────────────────┘       └───────────┬────────────┘       └────────────┬────────────┘
                                              │                                 │
                                              ▼                                 ▼
                                   ┌──────────────────────┐        ┌─────────────────────────┐
                                   │ 10 Hashed Recovery   │        │ Independent Admin Reset │
                                   │ Codes Generated      │        │ (Self-Reset Prohibited) │
                                   └──────────────────────┘        └─────────────────────────┘
```

### Step 5.1: TOTP Enrollment & Key Generation
1. **Endpoint:** `POST /api/web/mfa/authenticator/begin`
2. **Request:**
   ```json
   {
     "currentPassword": "SecureEmployeePassword#2026",
     "browserFingerprint": "browser-hash-88fa01"
   }
   ```
3. **Validation & Output:**
   - Validates current password via Argon2id / ASP.NET Identity.
   - Generates 160-bit cryptographically secure shared secret.
   - Encrypts secret via `IDataProtector` and stores in `MfaCredential` (State: `PendingConfirmation`).
   - Returns setup payload:
     ```json
     {
       "credentialId": "cred-uuid-1",
       "base32Secret": "JBSWY3DPEHPK3PXP",
       "otpAuthUri": "otpauth://totp/Cosmix%20Engineering:user@cosmix.com?secret=JBSWY3DPEHPK3PXP&issuer=Cosmix%20Engineering&algorithm=SHA1&digits=6&period=30"
     }
     ```

### Step 5.2: Confirmation, Session Invalidation & Recovery Codes
1. **Endpoint:** `POST /api/web/mfa/authenticator/confirm`
2. **Request:**
   ```json
   {
     "credentialId": "cred-uuid-1",
     "code": "489201",
     "browserFingerprint": "browser-hash-88fa01"
   }
   ```
3. **Action:**
   - Verifies code against unconfirmed secret using RFC 6238 TOTP engine.
   - Confirms credential (`ConfirmedAt = UtcNow`).
   - Sets `UserAccount.AuthenticatorRequired = true`.
   - Increments `UserAccount.SecurityVersion` from `1` to `2`.
   - Revokes all existing active user sessions in `[auth].[UserSessions]`.
   - Generates exactly 10 single-use recovery codes (e.g. `cosmix-8f3a-99b1-21c0`).
   - Persists recovery codes as SHA-256 hashes (`RecoveryCodeHash`).
   - Returns plaintext recovery codes to user with one-time display warning.

### Step 5.3: Authentication with TOTP / Single-Use Recovery Code
1. **Primary Login:** `POST /api/web/auth/login` -> Returns HTTP 200 with `nextStep: "enter_authenticator"`.
2. **Challenge Verification:**
   - If using TOTP: `code: "651920"`, verified against decrypted secret.
   - If using Recovery Code: `code: "cosmix-8f3a-99b1-21c0"`. System computes SHA-256 hash, matches against unrevoked recovery code, and immediately revokes it (`RevokedAt = UtcNow`).
   - If code fails: increments `FailedAttempts`. On 5th failure, challenge is revoked.
3. **Anti-Downgrade:** Attempting to invoke SMS OTP endpoint returns HTTP 403 (`mfa_sms_downgrade_forbidden`).

### Step 5.4: Independent Administrator MFA Reset
1. **Initiation:** An employee loses their authenticator device and recovery codes.
2. **Admin Request:** `POST /api/web/mfa/authenticator/reset/{targetAccountId}`
3. **Payload:**
   ```json
   {
     "expectedVersion": "AAAAAAABAAA=",
     "reason": "Employee in-person identity verification confirmed by HR manager",
     "ticketReference": "IT-SEC-2026-0891"
   }
   ```
4. **Validation:**
   - Checks admin permissions (`accounts.web.manage`, `role_permissions.manage`).
   - If `admin.AccountId == targetAccountId`: throws HTTP 403 (`Cannot reset own MFA`).
   - If `admin.EmployeeId == targetEmployeeId`: throws HTTP 403 (`Self-reset prohibited`).
5. **Execution:**
   - Marks all active MFA credentials as revoked.
   - Sets `AuthenticatorRequired = false`.
   - Increments `SecurityVersion`.
   - Revokes all user sessions.
   - Writes append-only record to `[auth].[MfaResetEvents]`.

---

## 3. Requirement 6: Production Android Release Readiness Workflow

```
┌─────────────────────────┐       ┌────────────────────────┐       ┌─────────────────────────┐
│ release-config.mjs      │  ──>  │ Validate Origins,      │  ──>  │ Generate                │
│ Config & Environment    │       │ SPKI Pins & Keystore   │       │ network_security_config │
└─────────────────────────┘       └───────────┬────────────┘       └────────────┬────────────┘
                                              │                                 │
                                              ▼                                 ▼
                                   ┌──────────────────────┐        ┌─────────────────────────┐
                                   │ verifyCosmixRelease- │        │ Release APK / AAB       │
                                   │ Signing (Gradle)     │        │ Fail-Closed Protection  │
                                   └──────────────────────┘        └─────────────────────────┘
```

### Step 6.1: Origin & SPKI Pin Validation (`release-config.mjs`)
1. **Configuration Evaluation:**
   - `COSMIX_API_BASE_URL`: `https://erp.cosmixengineering.com/api`
   - `COSMIX_CERT_PIN_PRIMARY`: `sha256/WoiWRyIOVNa9ihaBciRSC7XHjliYS9VwUGOIud4PB18=`
   - `COSMIX_CERT_PIN_BACKUP`: `sha256/r/mIkG3eEpVdm+u/ko/cwxzOMo1bk4TyHIlByIBA5Pa=`
2. **Validation Rules Enforced:**
   - Exactly matches authorized production domain. Rejects `http://`, `192.168.*`, `localhost`.
   - Rejects single pin (must provide primary + disaster recovery backup).
   - Validates base64 SHA-256 SPKI format.

### Step 6.2: Network Security Policy Generation (`release-security.gradle`)
1. **Generated File:** `android/app/src/release/res/xml/network_security_config.xml`
2. **Policy Directives:**
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <network-security-config>
       <domain-config cleartextTrafficPermitted="false">
           <domain includeSubdomains="true">erp.cosmixengineering.com</domain>
           <pin-set expiration="2027-12-31">
               <pin digest="SHA-256">WoiWRyIOVNa9ihaBciRSC7XHjliYS9VwUGOIud4PB18=</pin>
               <pin digest="SHA-256">r/mIkG3eEpVdm+u/ko/cwxzOMo1bk4TyHIlByIBA5Pa=</pin>
           </pin-set>
           <trust-anchors>
               <certificates src="system" />
           </trust-anchors>
       </domain-config>
   </network-security-config>
   ```
3. **Security Properties:**
   - Cleartext traffic is disabled.
   - User-added CA certificates are rejected.
   - Connections to any other origin or domain fail immediately at OS network layer.

### Step 6.3: Release Signing Verification (`verifyCosmixReleaseSigning`)
1. **Verification Checks:**
   - Fails if `debug.keystore` is detected in `signingConfigs.release`.
   - Validates that keystore file path is external to the git repository.
   - Verifies key algorithm is RSA 2048+ or EC 256+.
   - Verifies certificate validity period and SHA-256 fingerprint against approved release configuration.
2. **Native Injection:**
   - Injects configuration into React Native runtime through `ReleaseConfigurationPackage.kt`, ensuring that the API base URL utilized by the mobile UI strictly mirrors the pinned origin.
