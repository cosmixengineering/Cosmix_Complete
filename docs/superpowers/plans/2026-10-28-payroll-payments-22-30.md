# Payroll Payments and Employee Records Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for the independent implementation units below. Preserve all pre-existing work; no commits, pushes or real transfers are part of this execution.

**Goal:** Complete the supported salary-to-payment software flow, employee visibility and acceptance tooling for owner tasks 22–30.

**Architecture:** Extend the existing frozen payroll obligation and audited Finance operations. Payment events, protected evidence and balanced reversal journals remain bound to employee entitlements. The existing mobile History screen consumes server-scoped self-service records. Bank formats require a supplied bank template; synthetic verification is never company acceptance.

**Tech Stack:** .NET 10, EF Core SQL Server, React TypeScript web, React Native Android.

**Spec:** Owner's tasks 22–30 in this conversation, and existing backend/mobile AGENTS.md plus payroll verification documents.

## Global constraints
- Preserve locked payroll fingerprints, server-derived amounts, independent actors, existing scopes, idempotency and audit atomicity.
- Coordinator owns shared DI/controller integration and database migrations; one heavy .NET build at a time.
- No actual transfer, production sign-off, bank certification or three-phone result may be inferred from test fixtures.
- Do not modify unrelated files or embed banking/evidence/credential secrets in source, logs, general DTOs or reports.
- Existing approval to implement and make routine choices applies; no additional design permission gate.

## Review focus
- Repeated submissions and concurrent partial payments cannot overpay an employee.
- Failed/reversed posted payments restore ledger and available entitlement exactly once, with independent authorization and evidence.
- Payment confirmation cannot be self-approved or accepted without retained evidence.
- Employee history cannot expose another employee or banking/payment-evidence secrets.
- Missing bank format, provider, device or actual month remains an explicit unmet acceptance condition.

## A — Payment lifecycle and protected exports (22–24)
Owner: payment implementer. Existing files: Domain/HR/PayrollAccounting/{SalaryPayment,Types}.cs; Application/HR/PayrollAccounting/{PayrollAccountingService,PayrollAccountingContracts}.cs; Infrastructure/Repositories/HR/PayrollAccountingRepository.cs and payment configuration. Add focused bank format/evidence/reconciliation services within those module folders.
- [ ] Audit existing pending/posting/confirmation flows and freeze proposed DTO additions with coordinator.
- [ ] Add failing tests for evidence, independence, partial balances, duplicate references, failure and reversal, closed periods, repeated/replayed operations.
- [ ] Implement protected banking export against explicit versioned format configuration, no invented bank-specific acceptance.
- [ ] Implement payment outcomes and controlled reversal/reconciliation with append-only journal/evidence, no historical journal mutation.
- [ ] Coordinator generates/reviews/applies migration on disposable target and runs SQL/API tests.

## B — Unified employee history and reports (25–26)
Owner: mobile implementer for mobile; coordinator for backend self-service and web reports. Files: mobile/src/screens/HistoryScreen.tsx, existing self-service service/contracts and components; backend Application/HR/EmployeeSelfService, Application/HR/PayrollAccounting (new settlement report service/contracts), Api/Controllers/HR.
- [ ] Inventory existing attendance, leave/balance, loan/advance, request and payslip history categories.
- [ ] Define employee-only paginated payment history DTO (no bank/evidence references) and implement under current self financial permission/session checks.
- [ ] Add mobile Payment category with readable pending/partial/failed/confirmed/reversed states, loading/error/empty/paging and tests.
- [ ] Add permission-protected reports/CSV for frozen payroll register, deduction/contribution totals, paid/unpaid and reconciliation differences, reusing source validation.
- [ ] Verify totals and scope isolation including failed/reversed transactions and CSV injection handling.

## C — Readiness and acceptance (27–30)
Owner: readiness implementer; coordinator integrates tests.
- [ ] Inspect migration/config/jobs/login and recovery readiness without dumping secrets; implement actionable checks for real deployment requirements.
- [ ] Run controlled permissions/calculation/retry/concurrency/consent tests; inspect actual Android/face-server availability and preserve existing camera fail-closed behavior.
- [ ] Add repeatable employee-wise historical payroll comparison tooling with source hashes and explicit mismatches; use synthetic fixtures until actual salary/accounting files supplied.
- [ ] Prepare/run a synthetic parallel-month salary-to-payment scenario, collecting actual service/SQL evidence. Real historical comparison, three physical phones and HR/Finance/Management signatures remain pending until actually performed.

## Validation and ledger
- Baseline: 495 backend tests passed on 28 October before this work. Business DB must not be targeted by fixture tests.
- Build/test output: `D:/CosmixPayrollVerification20261028/` (coordinator-controlled).
- [ ] Focused tests, full permitted regression, affected web/mobile verification.
- [ ] Independent code review and task-by-task implementation/test/real-acceptance status report.

Ruling: use the existing checkout on a new task branch, preserving substantial uncommitted implementation and the user's active project paths. Bank approved format and real-month source were requested; the user is clarifying which spreadsheet is needed. No credentials requested.
