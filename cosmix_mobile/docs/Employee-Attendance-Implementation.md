# Employee attendance implementation

## Approved slice

Implement the owner's requested login -> dashboard -> camera/face/liveness Check In -> Check Out using the existing backend. The 20 October instruction to start implementation supersedes the architecture-only scope restriction in the original foundation. Existing folder placement and security rules remain binding. No commit, push, release deployment or permanent test suite is included.

The architectural approach retains React Native CLI and TypeScript. Alternatives of replacing the foundation with Expo or adding client-only demo authentication are rejected because they do not serve the requested integration. Work proceeds inline on the existing `codex/mobile-foundation` branch, preserving the pre-existing `.editorconfig` change. This is already a separate mobile project; no repository/worktree reset is needed.

## Boundaries

- `src/screens`: phone login, OTP, live dashboard and attendance operation screens.
- `src/components`: reusable accessible controls, feedback and layout; `src/theme`: design tokens.
- `src/navigation`: typed authenticated/anonymous navigation. No customer routes or speculative supervisor tools.
- `src/auth`: session lifecycle. Access token in memory; refresh and installation secrets in OS Keychain/Keystore. Serialize refresh and never replay an ambiguous refresh token.
- `src/services/api`: fixed HTTPS backend URL, bounded native transport with redirects disabled, typed DTO validation and safe ProblemDetails handling. No CompreFace credentials or direct model calls.
- `src/services/attendance`: pure operation coordinator plus camera/location adapters. Backend owns all identity, permissions, enrollment, liveness, face matching, geofence, schedule and punch acceptance decisions.
- `src/services/storage`: protected session and uncertain-punch records. Do not persist OTPs or raw face images.
- `src/types`, `src/utils`: explicit contracts and pure validation helpers.

## Flow and correctness

1. Registered phone -> OTP request/resend honoring server cooldown -> verification -> protected session -> current account/permissions. A 202 is not proof of SMS delivery.
2. Dashboard reads the actual attendance state and recent raw receipts; unknown/unavailable values remain explicit. No calculated pay, fabricated hours or mock profile.
3. Check In preflight -> fresh server challenge -> native front-camera capture -> orientation/metadata normalization -> bounded encrypted capture upload -> server verification. Require current `Passed` AND `canSubmitCheckIn`; only then acquire fresh accurate location and submit the exact bound punch key/proof.
4. Check Out uses current state and fresh location; no camera or face proof.
5. HTTP 200 alone is not attendance success: inspect receipt `outcome`. Keep uncertain punch key/body in secure storage and offer the exact same retry; never generate a new submission to hide a timeout. Prevent concurrent attendance submissions.
6. Recheck current permissions on entry/foreground, clear account-scoped state on sign-out, hide background snapshots, and do not log tokens, phone numbers, image payloads or provider internals.

## Deployment and verification

- Android emulator connects to a dedicated normal development instance of the existing API on host loopback port 7241 through `10.0.2.2`. Use a proper SAN certificate signed by the already verified local CA and debug-only Android CA trust. No TLS bypass or firewall weakening.
- Existing 7240 and CompreFace/SQL configuration remain in place. No SMS is sent while no provider exists. No authentication gate is disabled to make a demo succeed.
- Implementation order: contracts/validation and native dependencies; transport and secure sessions; attendance coordinator; UI/navigation; native permissions/TLS/emulator setup; verification and review.
- Meaningful synthetic regression checks run from an external temporary harness, respecting the foundation's no-test-suite rule. Cover validation, refresh concurrency/ambiguity/logout races, face-failure gating, exact proof/key binding, uncertain punch retry and receipt outcomes.
- Run `npm run verify`, Metro bundling and Android debug compilation. Exercise emulator launch, HTTPS connectivity and login failure behavior. Native camera, GPS, secure storage and authenticated attendance must only be claimed when actually exercised.
- Full employee OTP/face acceptance is externally dependent on SMS provider configuration, eligible employee/device permissions, real enrollment, roster/geofence configuration and consented physical-device tests. The owner confirmed SMS is absent; document that limit without substituting a fake login.

## Progress

- Exploration: foundation and authoritative backend contracts read; emulator and SDK found; no SMS provider configured.
- Product direction saved in `Product-Direction.md`.
- Implemented the shared four-screen flow, native adapters, OS-protected session/pending storage and permission-aware navigation.
- Meaningful external regression harness:29/29 pass after RED/GREEN checks for face gating, exact retry, refresh/logout races, iOS repeat authorization, ProblemDetails and business-calendar boundaries. These are synthetic transport/OS-port checks, not real biometrics.
- Fresh reviewer found iOS repeated authorization hang and UTC history date exclusion. Both were reproduced and fixed; focused re-review confirmed29/29 and no remaining Important/Critical findings.
- `npm run verify` passes. Metro Android bundling passes. Current source API publishes in Release successfully to an isolated external artifacts directory.
- The earlier dedicated API preview at localhost7241 used a CA-signed certificate with SAN for 10.0.2.2. Normal Windows TLS validation returned readiness 200, unauthenticated current-account 401 and disabled mobile authentication 503 `authentication_unavailable`. That dedicated preview was stopped when the owner requested code only; the unrelated 7240 service was preserved.
- Android debug x86_64 compilation succeeded, including the Metro-host correction and removal of unused storage/download permissions. The final earlier-slice APK was preserved outside Git. Installation/launch acceptance of that final APK was not completed before the owner stopped execution; real employee SMS/device/enrollment and physical camera/print/replay acceptance remain pending.
- Subsequent self-service changes are documented in Employee-Self-Service.md. No build, app run, tests, lint or typecheck were performed for that expansion. Earlier passing evidence must not be attributed to the new code.
