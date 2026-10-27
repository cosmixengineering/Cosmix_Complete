# Payroll Activation Implementation Plan

> Execute the reviewed plan sequentially. User authorized completing the operational workflow and tests; preserve concurrent changes and do not commit or push.

**Goal:** Replace the permanently unavailable month-close activation dependency with explicit, independently approved company operational activation.

**Architecture:** Reuse existing generic approval requests/decisions, encrypted private evidence, AttendanceOperation transactions/audit/idempotency, and immutable saved payroll input revisions. Each approval applies to exactly one source revision and its complete policy/jurisdiction/coverage binding. This is operational company authorization, not software certification of law or payment release.

**Tech stack:** .NET 10, EF Core SQL Server, existing ASP.NET controllers and permission catalog. No migration.

## Design and alternatives

The selected approach stores a new fixed request type in existing workflow tables. A new activation table would duplicate existing request/version/decision infrastructure and require an unnecessary migration. Removing the activation gate or substituting a test financial reader would not implement the requested behavior and is excluded.

New sensitive permissions `attendance.payroll-activation.read`, `.request`, `.approve` require current web MFA and explicit All scope. No application grants are seeded. Requests declare documentary reference/checksum and an explicit responsibility statement, and bind the exact server-resolved ready/latest input, private source SHA, policy version IDs, jurisdiction configuration IDs, dates and coverage fingerprint. Makers and beneficiaries cannot approve. Source changes invalidate pending decisions. Retained reads validate both encrypted request and decision proof and their identities/times before computing the fingerprint included in the existing month manifest.

Existing input DTOs keep payrollReady=false/paymentInstruction=false and prepared_review_required. Activation is a separate state; approval does not change salary or move money. Real company activation remains unset. All live fixture writes target a new named clone and use TEST ONLY statements.

## Steps

- [x] Add exact contracts and focused failing validation/service tests, run the red test.
- [x] Add source binding, permissioned request/read/decision service, bounded repository and controller. Preserve current authority and exact source rechecks after private I/O before commit.
- [x] Wire catalog/DI and the actual month financial reader. Missing approvals stay explicit readiness issues; retained fingerprints must validate.
- [x] Run focused tests and reviewed suite, excluding the unrelated business-database acceptance suite.
- [x] Build separate current-source 7242 clone host with one explicit UTC test clock shared by application and token lifetime validation.
- [x] Through ordinary APIs create the synthetic February2030 sources, save actual daily/payroll producer evidence, request/independently approve activation, finalize month, prepare payroll, independently lock and export CSV.
- [x] Reconcile SQL rows/audits and protected evidence, report exact runtime hashes and limitations. Preserve original database and old missing historical objects.

## Completion evidence

The reviewed Release suite passed345/345, including20 protected activation tests,14 supplementary All-authority cases and two SQL-provider translation regressions. Actual API acceptance produced140 required employee-date cells, five genuine saved payroll sources/handoffs/independent operational approvals, one normal global month close, one normally prepared and independently locked batch, and a five-row CSV. The eight completed positive-flow phases contain256 passing HTTP checks; parent lifecycle/release/correction checks are recorded separately. SQL confirms exact business/audit/receipt counts and no processing/payment rows. Bounded ciphertext tampering fails both activation and actual retained-month reads, and exact bytes/attributes restore successful verification.

Evidence is retained at `D:/CosmixFirst11Verification/final-acceptance/positive-month/POSITIVE-MONTH.md`; runtime/test PDB hashes match1,163/1,163 original source documents. The source and batch are produced normally; only initial synthetic identity/assignment prerequisites are fixtures. The run preserves existing future compensation and therefore uses February2030 rather than January2027. Exported amounts are attendance deductions, not a claimed gross payroll or payment instruction. Actual company activation, real SMS and biometric acceptance remain outside this synthetic proof.

## Review focus

Duplicate/stale requests and consumed idempotency keys cannot append another approval. A source changed during protected I/O cannot activate. Expired authority cannot commit. Self/maker/beneficiary and narrower scopes are rejected. Missing/tampered/identity-mismatched request or decision evidence cannot authorize retained month reads.
