# Part A01 execution result — 13 October 2026

Scope: approved A01 code/consumer extension in the latest dirty backend and web checkouts. Live authentication acceptance is **not complete**. No commit/push, SQL connection/migration/application, provider activation, real account/owner provisioning or outbound message was performed.

## Implemented

- Initial and additional web access forms collect required Reason; shared schemas reject omitted/blank, >1000-character and control-character reasons and preserve valid normalized reasons. Backend JsonRequired/required reason validation remains intact.
- POST access role creation, role permission grant and account role assignment require Idempotency-Key (8–100 ASCII letters/digits/underscore/dot/colon/hyphen). Direct service signatures require key before AuthRequestContext.
- Existing IdempotencyReceipt storage binds actor/action/key to SHA-256 of target and exact payload. The existing serializable authentication transaction saves business/audit state, obtains rowversions, protects the original result and saves the completed receipt before commit. No schema/DI/migration changes.
- Identical authorized retries within 24 hours return original IDs and versions. Different payloads conflict; expired/incomplete receipts cannot replay. Current fixed administrator, stored web MFA and same-assignment All-scope rights precede receipt access. Assignments additionally recheck current target account, effective employment, active role/permissions, target scopes and protected-account SuperAdmin authority. Replays never restore access or invalidate target security again.
- Web uncertain assignment requests retain exact payload/key only in component memory for an explicit retry. Successful responses reload current access; refresh/employee/session/unmount discard intents. Credential payloads are not retained for replay.
- Existing mobile new-device in-app event now has a clear safe template: “New device signed in”, directing the recipient to their account sessions. Existing inbox repository queries select by current authenticated account; no recipient override, PII or secret was added.
- Published changed header/replay consumer contract, supported scope boundaries and updated existing access acceptance script/harness. Acceptance scripts were syntax-checked, **not executed**.

## Changed files

Backend:

- src/Cosmix.Application/Authentication/Access/AccessContracts.cs
- src/Cosmix.Application/Authentication/Access/AccessManagementService.cs
- src/Cosmix.Infrastructure/Authentication/Access/AccessManagementRepository.cs
- src/Cosmix.Api/Controllers/Authentication/AccessController.cs
- src/Cosmix.Application/Common/Notifications/NotificationTemplates.cs (single-template ownership authorized by root)
- src/Cosmix.Application/Authentication/README.md
- src/Cosmix.Infrastructure/Authentication/README.md
- docs/Authentication-Access.md
- docs/access-provisioning/Provisioning-Design.md
- docs/access-provisioning/VerifyDelegation.cs
- docs/access-provisioning/VerifyAuthenticatedAccess.ps1
- docs/access-provisioning/LocalAcceptanceHttp.ps1

Web:

- src/modules/hr/schemas/access-setup.ts
- src/modules/hr/components/approvals/EmployeeAccessSetup.tsx
- src/modules/hr/components/approvals/AdditionalRoleAssignment.tsx
- src/modules/hr/README.md

No unrelated existing edit was reset or rewritten. Root separately owns current shared wiring/migrations/permission catalogue and attendance web verification fixes.

## Verification evidence

- TEMP actual Zod schema check before implementation: failed `assignment schema accepts omitted required reason` (expected missing-behavior failure). After implementation: passed missing/blank/control/overlength/valid normalized additional reason and initial required/valid reason payload checks. No permanent automated web test file/project was added.
- TEMP real AccessManagementService + existing-pattern synthetic fixture before implementation: failed identical assignment retry with `access_version_conflict` (expected missing replay failure).
- Updated existing docs/access-provisioning/VerifyDelegation.cs after implementation: **354 checks passed**, exit 0. Includes original reason/evidence/delegation/scoping guards plus identical initial assignment, role creation and permission grant retry, changed payload conflicts, actor/action key separation, expired receipts, removed current authority, protected targets and inactive target employment/roles. Collaborators simulate persistence/file IO; this is not proof of SQL rollback, contention or real encrypted storage.
- TEMP real NotificationTemplates.TryGet before implementation failed missing new-device alert; after implementation passed safe new-device template.
- A01 scoped ESLint and Prettier checks passed. Initial full npm verify exposed two A01 lint issues, corrected, plus an out-of-scope existing attendance Date.now render impurity. Subsequent build exposed existing Zod SafeParseReturnType and possibly-undefined MIME parsing errors in attendance files. Root coordinates these separately.
- PowerShell Parser syntax checks passed for modified VerifyAuthenticatedAccess.ps1 and LocalAcceptanceHttp.ps1. No authenticated HTTP script execution.
- Scoped git diff --check passed (only line-ending conversion notices).
- Final repository-wide npm run verify: passed exit 0 (full lint, full Prettier check, TypeScript compile and Vite production build). Vite reports the existing >500 kB chunk warning. Existing VerifyWebBoundary.cs Release check could not run: current API compilation has out-of-scope CS0246 errors in SiteReportsController.cs:47/53 for SiteReportQueueListRequest/SiteReportQueueSiteResponse. Root informed; no repository-wide backend Release success is claimed.

Commands use TEMP/cosmix-a01-verification/*-bin isolated OutDir to avoid running-host output locks; no permanent test project is present.

## Auth audit and remaining gates

Existing source already implements password-reset/change session/challenge revocation, refresh rotation/reuse revocation, logout/session/device revocation, purpose/device-bound OTP and due employment validation. EmployeeLifecycleProcessor revokes sessions/proofs and changes security state; authentication independently checks effective history so worker delay cannot extend access. New-device event persistence already occurred in the successful OTP transaction; only its missing display template was added.

These source findings and standalone checks are **not real SMS/MFA/recovery/device/employment acceptance**. Approved provider selection/operating configuration, cryptographic keys/private storage, secure deployment and genuine approved-target tests remain required. Actual first-owner provisioning remains deferred; no default user/password was introduced. No real employee or account acceptance was attempted.

Receipt expiry is a replay window, not automatic deletion. Retention/orphan reconciliation/cleanup needs the approved retention policy; no cleanup job was enabled. SQL contention, interrupted commits, rollback after private-result IO and genuine authorized browser replay/reauthorization still require separately authorized isolated target acceptance. Role/credential/channel/revocation consumer APIs outside the three specified receipt writes retain their existing contracts.

Root integration note: IsEmployeeActiveForAccessAsync(Guid employeeId, DateTimeOffset now, CancellationToken cancellationToken) checks Employee.Status=Active plus unambiguous due status history; it does not select channel, require JoinedOn or establish onboarding/approval authority. A06 consumers must retain their own account/channel/session/grant checks. No new root-owned wiring/schema is required by A01.