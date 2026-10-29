# CEO HR/payroll gaps implementation plan

> **For agentic workers:** Use superpowers:executing-plans, implementing and verifying one task at a time. Continue from the execution ledger; do not repeat completed work.

**Goal:** Close the twelve reviewed HR/payroll gaps while preserving approved history, independent review and explicit external acceptance boundaries.

**Architecture:** Extend the existing Domain/Application/Infrastructure/API and React clients. Frozen salary revisions remain immutable. Corrections supersede a returned run rather than editing approved amounts. Financial operations retain server authorization, audit, evidence, versions and idempotency.

**Tech Stack:** .NET 10, EF Core SQL Server, React/TypeScript, React Native Android.

**Spec:** D:/CosmixPayrollVerification20261028/ceo-gap-review/CEO-HR-Payroll-Gap-Review-20261028.md and the user's subsequent instruction to implement its twelve gaps sequentially.

## Constraints and rulings

- Preserve the substantial existing working-tree changes; work on current `codex/payroll-payments-22-30`. An empty checkout would omit the implementation under review. No broad commit/reset.
- User already requested immediate implementation; no repeated design approval for the supplied gap list.
- Policies remain dynamic. No guessed actual salary, statutory rate, account mapping, approval identity or business sign-off.
- Mobile remains employee-facing. Management/acceptance belongs on web or a named review record.
- Real bank/historical, SMS delivery, spoof and three-phone tests retain their explicit deferred status unless actual prerequisites arrive.
- Regression execution excludes one-shot business-data acceptance fixtures; disposable SQL tests use guarded clones only.

## Review focus

- A concurrent lock must defeat a return using stale rowversion.
- Returning an approved run must not make the old money evidence usable for Finance/payslips.
- A replacement month must not pay the returned run as well.
- Employee approvers must never approve their own earnings through an alternate account or stage.
- Bank and ledger retries must neither double post nor fabricate external settlement.

## Tasks

### 1. Return an HR-approved, unlocked run

Files: PayrollHandoffBatch.cs, PayrollSalaryLifecycle.cs, PayrollHandoffContracts.cs, PayrollHandoffRepository.cs, PayrollFrozenBatchReader.cs, PayrollHandoffController.cs, PayrollHandoffBatchConfiguration.cs, additive migration/snapshot; PayrollHandoffPage.tsx and its schema; PayrollSalaryLifecycleTests.cs and SQL persistence tests.

Interface: `ReturnForCorrectionAsync(Guid, LockPayrollHandoffRequest, string, AuthRequestContext, CancellationToken)` and POST `batches/{id}/return-for-correction`. Requires current HR-review authority, reason and exact version. Preserve prior HR approval; retain return actor/time/reason as protected audit/approval evidence. Set terminal `returned` state; preparation may create a new fully rechecked salary for that period. No lock, posting or payment of returned evidence.

- [ ] Add failing service tests: approved return preserves evidence; prepared/locked/processed return rejects; unauthorized/stale replay rejects; returned run cannot lock or read as approved salary.
- [ ] Implement domain/service/API/storage and fresh additive migration. Add SQL tests for constraints and period re-preparation.
- [ ] Add web action with reason/version, server-state refresh and explicit return/reprepare explanation.
- [ ] Focused tests, guarded SQL migration rehearsal, Release build and web verify.

### 2. Employee approver coverage

- [ ] Allow explicit, nonoverlapping employee groups within a month. Each group's complete salary retains its own immutable fingerprint and existing independent HR/Management/Finance approvals. Paid approvers' salaries go in separately reviewed groups; never relax self-approval or maker checks. Reject an employee already present in any active batch for the period.
- [ ] Add employee group selection and clear coverage information to batch preparation. Retain existing whole-month preparation compatibility.
- [ ] Test all approvers included in payroll, incomplete coverage, own-employee denial, different-account same employee, changed source/version, double review and concurrent stage completion. Migrate and verify on a clone.

### 3. Finance ledger and project allocation

- [ ] Define one source-bound payroll posting bridge and explicit project/General allocation with approved source provenance; retain balanced account/period rules.
- [ ] Add combined ledger/trial-balance and project labour reports and web surfaces; test account/currency balance, duplicate posting and allocation totals.

### 4. Bank-statement reconciliation

- [ ] Add bounded CSV/XLSX statement mapping, preview/source hash and duplicate detection.
- [ ] Add exact matching proposals, independent reviewed matching/unmatched rows and immutable reconciliation close. Do not turn a statement upload into payment authorization.
- [ ] Test amounts/date/reference, duplicate/replayed imports, partial/failed/reversed payments, mismatched currency and closed periods; expose web workflow.

### 5. Authenticator and recovery

- [ ] Wire TOTP enrollment/confirmation, hashed single-use backup codes and independently approved reset into existing web authentication; preserve current mandatory proof and session revocation.
- [ ] Add web management/recovery UI and tests for replay, expiry, recovery-code reuse and revoked sessions. Vendor SMS remains a deferred external prerequisite.

### 6. Android release

- [ ] Add production host pin configuration with rotation support and release validation; secure external signing settings and release build checks. No generated signing identity or invented production host.
- [ ] Verify debug/release separation and fail-closed missing release configuration. Actual Play distribution requires the company signing/account prerequisites.

### 7. CI and repeatable checks

- [ ] Add safe backend regression, disposable SQL integration, web/mobile verification and dependency/secret checks. Never run existing one-shot acceptance fixtures on a company database in CI.
- [ ] Add configurable shared-NAT concurrency harness and security evidence checklist. Independent assessment/branch-protection settings need actual provider access.

### 8. Production operations

- [ ] Extend readiness/runbook/scripts for always-on service, TLS, backup encryption, off-site copy, separate keys, restore checks and monitoring/security digest. Do not enable machine encryption or publish a service blindly.
- [ ] Verify safe local execution/metadata; explicitly record missing real host/key/off-site owner inputs.

### 9. Configuration readiness

- [ ] Expose per-employee/month setup readiness covering policies, compensation, statutory applicability, Finance mappings and approver coverage. Existing dynamic policy editors remain authoritative.
- [ ] Exercise configured and missing synthetic cases; no sample is published as company policy.

### 10. Attendance acceptance

- [ ] Investigate retained face timeout evidence; test employee-bound session/device/consent/radius/capture/punch/history pipeline using current controlled environment.
- [ ] Real camera requires current operator availability; do not claim direct-component tests equal full acceptance. Keep spoof/three-phone tests deferred.

### 11. Historical/HBL

- [ ] Keep authentic-source comparison and bank export acceptance ready with clearly separated synthetic evidence. Real historical records and bank authorization cannot be manufactured.

### 12. Live month/review

- [ ] Prepare web-side review/evidence and parallel-month reconciliation workflow. Final company decision remains a named human act; no mobile administrator panel.

## Execution ledger

2026-10-28: Scope accepted by user. External blockers remain unchanged; tasks 11/12 cannot be certified using generated records.

Task 1: Implemented terminal return-for-correction with preserved HR proof and immutable salary lines, current HR authority, reason/version/idempotency, API and web action. Five new tests failed before implementation. Service/HTTP suite 26/26, guarded SQL persistence suite 7/7, web verify passed. Migration 20261028150542 applied only to disposable Cosmix_Payroll_20261028; business deployment remains pending. Evidence: D:/CosmixPayrollVerification20261028/ceo-gap-fixes.

Task 2 design ruling: use nonoverlapping employee batches instead of introducing partial approvals inside one batch. This preserves existing signed whole-batch evidence and four-stage actor separation, while allowing independent review of approvers' salaries. The month's remaining employees must still be explicitly prepared and approved; no automatic approval or omitted salary is represented as paid.

Tasks 1/2 verification update: 565 retained regression tests passed, zero failed/skipped, on the guarded disposable SQL target. This includes return versions/history, employee selection/overlap, alternate-account self-approval denial, split statutory posting and SQL append-only/unique entry coverage. Source reservation, salary-component edits, dashboard and employee history exclude returned batches. Two migrations rehearsed (return fields and statutory employee posting coverage). Business runtime/database deployment and a UI smoke test remain pending. Web group verification passed; latest statutory UI verification is being checked.

Task 3 finding: `modules/accounts/README.md` confirms Finance has only a folder scaffold. Implement a shared ledger read boundary over committed payroll journals first, with explicit included-source metadata, before describing anything as a company-wide general ledger. Project/site allocation and its approval remain separate work within task 3. No non-payroll Finance transactions currently exist to claim integrated.

Task 3 implementation: shared payroll ledger SQL view, bounded audited ledger/trial balance API and web tab, immutable source-bound project/site/General cost revisions, independent employee-aware approval and monthly project/site report with unallocated-batch coverage. Expense basis reconciles to salary accrual, expense deductions and posted employer contributions. Reviewed migrations 20261028154436 and 20261028161132 applied only to the disposable SQL clone. Regression 577/577 passed before the final monthly-report addition; that addition's focused run passed 28 tests with 7 SQL tests skipped because its process did not set the disposable connection variable. This skip is not SQL verification; a subsequent explicit SQL run is required. Web verify passed (existing large-bundle warning).

Task 4 implementation in progress: bounded CSV/XLSX inspection/mapping/preview, protected retained raw statements and corrections, duplicate detection, one-to-many payment/reversal journal matching, explicit non-payroll classifications, independent close and immutable evidence. Ruling: this is a payroll bank reconciliation; non-payroll bank rows require explicit reviewer classification and remain outside payroll ledger totals. It must not be described as a complete non-payroll Finance reconciliation. Statements do not authorize or confirm payments. The month must have ended before closing. Same-file mapping/balance corrections create retained revisions; exact duplicate imports remain blocked. Migration 20261028164326 (not yet applied) adds statement/proposal/close tables and a database trigger to block closed-month bank postings. First parser/projection tests 3/3 and service workflow tests 3/3 pass. Web bank verify passed. Latest migration/build/SQL tests still pending; no business runtime deployment yet.

Tasks 3/4 verification update: migration 20261028164326 applied successfully to the disposable clone. Focused API/service/SQL suite 43/43 passed, including direct SQL-trigger rejection of backdated bank journals, repository checks, rowversion, protected evidence and duplicate/independent-review cases. Full retained regression 586/586 passed, zero failed/skipped (`bank-ledger-regression.trx`). Web full verify passed, followed by targeted ESLint and complete TypeScript/Vite build after final labels; only the preexisting bundle-size warning remains. Business runtime and business database have not been deployed. The trigger requires coordinated binary/schema rollout.

Task 5 discovery: existing domain MFA entities are not wired operationally. Web authentication/session refresh, administrative SQL authority checks and self-service password change explicitly require SMS proof. A TOTP-only controller would therefore be incomplete. Implementation must update all those bindings, preserve verified approved phone/employment/session/version checks, disable SMS downgrade once an authenticator is enrolled, and retain independently approved factor reset with session revocation. Production SMS remains absent for initial onboarding/reset fallback. TOTP reference consulted: https://github.com/kspearrin/Otp.NET and https://www.rfc-editor.org/rfc/rfc6238 ; current NuGet release verified as Otp.NET 1.4.1. No authentication changes have been written yet.

Task 5 implementation/verification: browser-bound TOTP login, encrypted enrollment, once-only hashed recovery codes, replay-safe session validation/refresh, independent administrator reset and password-change support are wired through Domain/Application/Infrastructure/API/web. Administrative SQL authority supports both valid SMS and TOTP proofs; enrolled accounts cannot downgrade to SMS. Reset requires a different employee with target-delegation authority, preserves protected review evidence and revokes sessions/factors. This is independent review, not a new two-administrator approval chain. Migration 20261029051812 applied only to the disposable clone. Full retained regression 610/610 passed, zero failed/skipped, including an actual SQL authority/reset test. Web verify passed. Production SMS, real enrollment and business deployment remain pending. Runbook: cosmix-backend/docs/Web-Authenticator-Recovery.md.

Task 6 implementation in progress: external approved release origin/pin/fingerprint/version configuration, native API origin, generated production network security policy with current/backup SPKI pins, external signing inputs and release guard. Configuration tests 4/4 pass. Mobile verify/native Gradle verification are still running; no actual company signing identity or production host is available. Do not describe an unsigned build or synthetic certificate as a production release.
