# Shared Authentication Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for orchestration; bounded domain and transport tasks may run independently.

**Goal:** Implement the confirmed shared mobile SMS OTP authentication backend, with honest configuration gates.

**Architecture:** One existing identity and DbContext. Application services orchestrate module-specific repositories; Infrastructure owns SQL concurrency, phone parsing and SMS/cryptographic adapters. API owns HTTP and JWT presentation.

**Tech Stack:** .NET 10, EF Core 10, SQL Server, ASP.NET Core JWT bearer, maintained phone-number parser.

**Spec:** ../specs/2026-10-06-shared-authentication-design.md and the user's attached implementation prompt.

## Global Constraints
- No test projects, real SMS, database changes, seed identities, commit or push.
- Preserve all four existing migrations and lifecycle fixes.
- Mobile never acquires web/management authority; privileged MFA and unapproved recovery remain fail-closed.
- Unselected SMS provider must not report successful delivery.
- Work in the requested checkout on codex/shared-mobile-authentication; no separate worktree without user consent.

## Review Focus
- Parallel OTP verification/resend and refresh reuse must not create multiple usable outcomes.
- Current employment, approval, role/scope, phone binding and account version must be checked after proof verification.
- No token claim, device label or role name may substitute for server-side permission/scope checking.
- Unknown numbers and ineligible accounts receive indistinguishable request contracts.
- Interrupted SMS dispatch must not retry blindly, leak codes or report delivery.

## Task 1: Domain and Persistence
- [x] Add explicit approved login-phone selection, historical binding, passwordless activation and channel entitlements.
- [x] Add bounded OTP, encrypted-delivery and rate-budget models and restrictive mappings.
- [x] Run temporary offline domain regression checks before/after implementation.

## Task 2: Cryptography and SMS Adapter
- [x] Add phone normalization, keyed verifiers, encrypted expiring delivery payloads and high-entropy refresh secrets.
- [x] Implement optional HTTPS gateway sender, with honest acceptance/failure states and no logs/retries of secrets.
- [x] Run temporary offline checks of normalization, tampering, wrong proof and missing configuration.

## Task 3: Mobile Authentication and Sessions
- [x] Define DTOs, repository/provider contracts and validated opt-in settings.
- [x] Implement request/resend/verify, SQL-serialized rate budgets, session rotation/replay, logout, device/session management and audited provisioning.
- [x] Add server-side current-session/access resolution and self-only mobile permissions.
- [x] Verify negative paths and mapping construction without connecting to SQL.

## Task 4: HTTP Integration and Handover
- [x] Add versioned controllers, JWT issuance/current-state validation, permission policies and safe ProblemDetails.
- [x] Add durable SMS dispatch and expired-secret cleanup.
- [x] Generate only a NEW migration and review SQL; do not apply it.
- [x] Run Release build, offline checks and independent review; fix substantive findings.
- [x] Document exact routes, configuration, HR provisioning contract, client storage, deployment prerequisites and pending decisions.

## Execution Record
- Baseline: develop 34e8e077283039a830cff7258bb37c2514f73d57; only prior design document untracked.
- Ruling: proceed with actual implementation as explicitly requested; web credential policy, privileged MFA, first-admin bootstrap and phone recovery are unavailable, not silently simulated.
- Ruling: security thresholds must be explicitly configured and validated before enabling mobile login; no invented production policy values.
- Ruling: no live SQL verification or new migration application in this task without renewed named-target authorization.
- Tasks 1-4 implemented and verified for the confirmed mobile/configuration-gated scope. The original SRS is preserved.
- Verification: Release build; 16 domain checks; 30 mapping assertions; 165 provider assertions; 109 service/dispatch scenarios/306 assertions; 75 access assertions; 37 HTTP/OpenAPI checks; 55 migration/SQL assertions. No SQL or SMS integration claimed.
- Independent review fixes: record DTO validation, shared timezone configuration, and self-service permission enforcement. Regression checks pass; no outstanding substantive finding from that review.
- Ruling: current-session logout remains available without a session-management permission, while logout-all/device/session administration requires it. Cost if changed: users could be prevented from explicitly ending their own authenticated session.
- Ruling: use a compatible optional HTTPS gateway contract; arbitrary vendor protocols still require an adapter. Cost if assumed universal: selected-provider onboarding would fail until translated.
- Ruling: use one shared database authentication lock for correctness and a single-attempt dispatch lease. Cost: throughput must be load-tested, and ambiguous SMS failures require a fresh user-requested code, not automatic resend.
- Mapping correction: encrypted payload uses bounded varchar(8000), not nvarchar(max); only the newly prepared, unapplied migration was generated for this feature.
- New migration prepared but not applied. No commit/push. Remaining rollout gates are documented in Shared-Authentication.md and Shared-Authentication-Verification.md.

## Continued Verification and Fixes
- [x] Extend HR provisioning service coverage and align existing-account enrollment with channel delegation checks. Three denial scenarios failed before the fix; all 17 scenarios passed afterward.
- [x] Reproduce/fix device revocation outside the bounded listing and verify ownership/session behavior.
- [x] Exercise actual JWT middleware and issued tokens against synthetic current-state dependencies.
- [x] Complete a fresh independent correctness review, final Release/regression checks and handover.
- Ruling: continue the existing local implementation, not a second architecture or identity system. No commit/push or database authorization is inferred from the request to test.
- Ruling: provisioning a new approved account with no rights remains the existing authorized HR contract; first enrollment of an existing account must not bypass the channel-enablement delegation rules. Retry remains non-mutating.
- Final review: fixed active-account role replacement lockout. Revoked and expired original-role cases failed before the fix; the 117-scenario/321-assertion service regression copy passed afterward, including invalid replacement and first-activation denials.
- Ruling: keep initial assignment/scope IDs as onboarding evidence, not permanent ongoing access requirements. Authorized current paired access governs activated accounts; all existing session-version, employment, phone, permission and MFA checks remain. No schema or migration change is needed.
- Device verification: original older-device lookup and page-two listing failures reproduced, then corrected. Parent rerun passed 115 service/HTTP scenarios with 361 assertions; no SQL/SMS used.
- JWT verification: parent rerun passed 171 checks across 301 HTTP requests, with all 183 captured production source files matching live code and all hosts stopped.
- Final Release build: zero warnings/errors; domain 16/16, provisioning 17/17 and migration/model 55/55 checks passed. Prior shared migrations unchanged. No commit, push, database connection, real SMS or persistent test project.
- Remaining approvals/integrations are not completion claims: SMS vendor/limits, privileged MFA, web credential/browser workflow, bootstrap, phone recovery, actual HR approval integration and named-target SQL verification remain gated.
