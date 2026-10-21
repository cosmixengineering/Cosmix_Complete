# Employee Lifecycle Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for the parent service and disjoint domain/repository workers.

**Goal:** HR requests suspension, resignation or termination; a different fixed Admin approves it with effective-time access blocking and preserved history.

**Architecture:** Reuse EmployeeOperation, ApprovalRequest/ApprovalDecision and EmployeeStatusChange. Store request snapshots in existing private encrypted storage. Authentication already checks effective status on each login/session validation; a bounded worker reconciles cached status and revokes sessions without being the security timing boundary.

**Tech Stack:** .NET 10, EF Core, SQL Server, existing four-project Clean Architecture.

**Spec:** SRS HR-005, HR-006, AUTH-007, APR-002, ROLE-004, AC-009 and PM's approved HR-request/Admin-decision flow.

## Constraints
- Backend only, no push/commit, no new test projects in the repository.
- No reactivation, rehire, backdating or automatic policy decisions.
- Existing fixed-role owners cannot be offboarded through ordinary staff HR APIs.
- No physical deletion or rewriting old migrations, attendance or financial history.
- One pending request or scheduled approved change per employee. Returned requests are corrected using a new request.

## Review Focus
- Self-approval, mobile tokens and custom roles cannot cross the fixed-Admin decision boundary.
- Scheduled access ends at effective time even while reconciliation is delayed.
- Stale request/employee versions, duplicate retries and changed payloads cannot double-apply a decision.
- Return/rejection/cancellation preserve evidence without blocking employment.
- Restrictive FK and append-only guards preserve all prior status/approval evidence.

## Tasks
- [x] Add temporary failing checks and approved request/status contracts; preserve repository test-project ban.
- [x] Domain: validate due approved blocking transitions and optional approval reference; restrictive unique mapping, no new tables.
- [x] Application: separate lifecycle permissions, HR request, fixed-Admin decisions, requester cancellation, private details and bounded histories.
- [x] Infrastructure/API: bounded repository, transaction-safe reconciliation, authenticated routes and registrations.
- [x] Verify domain/services, authorization, SQL translation/rollback and HTTP contracts; add/review incremental migration only.
- [x] Back up the original development database before applying the reviewed migration; validate history/constraints/empty business data.
- [x] Update handover with actual checks and unresolved deployment/business limits. No frontend or Git publication.

## Completion Evidence

Release build: zero warnings/errors. Domain: 76 checks. Lifecycle services: 115 checks. Protected-role
grant guard: 37 checks. Seven SQL lifecycle scenarios/87 assertions passed after fixing a temporary test
transaction adapter, with all 125 table counts restored and zero accounts. Existing hiring/authentication
regressions and hosted OpenAPI/anonymous-denial checks passed. See Employee-Hiring-Verification.md for
exact boundaries, remaining deployment work and the verified backup. No full-SRS completion claim.
