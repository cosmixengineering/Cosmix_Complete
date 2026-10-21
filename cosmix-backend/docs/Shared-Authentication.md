# Shared Authentication: Implementation and Handover

Updated: 7 October 2026. Baseline: `34e8e077`. Branch: `codex/shared-mobile-authentication`.

This is real backend implementation, not a UI/mockup. Web/mobile login, password recovery/change,
controlled phone/device recovery and the local first-SuperAdmin setup command are implemented. The original
development database is migrated through `20261007090713_AddAccountRecoveryAndOwnerInitialization`.
This is **not yet a deployable complete rollout**: actual owner setup, selected SMS provider,
approved operating settings and live acceptance remain external prerequisites. No frontend work is in this task.
The PM explicitly deferred actual owner creation until approved identity details are available.

## Confirmed Change

The CEO's mobile phone/SMS OTP decision supersedes mobile-password wording in SRS v1.2. It affects AUTH-001/002/003/005/006/007/008, ROLE-001/004, MOB-006/007/008 and HR approval integration. The original SRS document is preserved.

- One employee and one account across all modules and both clients.
- Eligible staff, including HR and managers, use phone + SMS OTP for mobile self-service. Fixed Admin and SuperAdmin accounts are web-only.
- Mobile is self-service. It never gains web/management access from a client flag.
- Web access is separately administered. Mobile activation creates no password.
- Web login for every account, including Admin/SuperAdmin, requires email + password, then SMS OTP to its currently approved registered number. Password alone issues no session.
- Mobile SMS OTP remains one factor and never satisfies web MFA. HR/manager accounts may use mobile self-service even when their web account requires MFA; management grants are not exposed through mobile.
- Admin and SuperAdmin are protected system roles, not editable custom-role names. Other roles use the supported permission catalog.
- Face verification belongs to attendance Check In, not login.

## Implemented Components

| Layer | Responsibilities |
|---|---|
| Domain/Authentication/Mobile | Historical phone binding, single-use bounded OTP, encrypted-delivery lifecycle, durable rate buckets |
| Domain/Authentication | Separate channel entitlements, protected system roles, passwordless mobile activation and consumed web-SMS session proof; existing invitation/recovery/password history preserved |
| Application/Authentication | OTP request/resend/verify, refresh/replay, current session validation, logout, device/session management, audited employee provisioning contract, dispatch orchestration |
| Application/Authentication/Access | Supported permission catalog, paired permission/scope evaluation, controlled role/assignment/channel services |
| Infrastructure/Authentication | SQL repositories/transaction locking, phone parser, HMAC/AES-GCM adapter and optional HTTPS SMS gateway adapter |
| Application/Authentication/Web | Email/password verification, purpose-bound web SMS challenge, browser-bound session/refresh and current-state validation |
| Api | Versioned controllers, mobile bearer transport, secure web cookies/CSRF, current server-state validation, safe errors and background dispatch |

No new DbContext, frontend, attendance workflow, real account, password seed or public signup/bootstrap was added.

## HR Approval Handoff

1. HR explicitly selects the registered login phone. Normalize it with `IPhoneNumberNormalizer.Normalize` before calling `Employee.SelectLoginPhone(number, true)` for staff mobile access, or `SelectLoginPhone(number, false)` for a web-only account. An approved phone does not itself grant either channel. Contact/emergency numbers are not inferred or made unique.
2. `SubmitForApproval` captures `MobileAccessRequested` and `ApprovedLoginPhoneNumber` in the immutable submission. Existing submissions default to no mobile enrollment, not retrospectively verified access.
3. The existing approval domain flow approves the submission; this does not verify the phone or start a session.
4. The real HR approval service owner must call `IEmployeeMobileProvisioner.ProvisionAsync` **after the approval transaction commits**, under a current authorized MFA-authenticated web administrator. It owns a separate idempotent transaction. There is no fake HR approval endpoint.
5. Provisioning creates/reuses the same account and an unverified binding. Its internal `employee:<id>` login identifier is not a web credential. Retrying never re-enables a later disabled account or silently changes its phone.
   First mobile enrollment of an existing account also requires All-scope role-permission management
   and delegation authority for its existing/scheduled permissions, like the channel-enablement API.
   Existing privileged permissions preserve the MFA requirement. An idempotent retry does not
   reassign rights, re-enable the channel or verify the phone.
6. Authorized Admin assigns supported access and initial scope. Assignment and channel entitlement remain distinct. No login is allowed before valid initial access is configured.
7. Successful mobile OTP verification rechecks current approval, employment/joining eligibility, binding, account version, non-system account status and own self-service access. Only then does passwordless activation, device registration and mobile session creation occur atomically. Admin/SuperAdmin are rejected by mobile provisioning, sign-in and current-session validation.

The initial assignment/scope pair remains immutable onboarding evidence after activation.
First activation must use that explicitly configured pair. Subsequent mobile logins require a
currently effective assignment and its own scope for the same account/employee; revoking or
expiring the original role must not permanently lock out an employee who has authorized
replacement access. The normal audited assignment/revocation API invalidates old session
versions. This does not permit scope borrowing or bypass current permission/MFA checks.

8. When the Admin also grants software/web access, the Admin supplies the email and first password through the initial-credential command below. This reuses the approved account and assigned access; it does not register another identity or send a login token. A later real HR approval screen can coordinate this handoff after approval succeeds.

The actual HR CRUD/approval application service is not present in this repository yet. These callable authentication contracts are its integration points, not a claim that employee onboarding APIs are finished. Run the implemented local first-SuperAdmin setup with the owner's real approved details before exercising this workflow end-to-end. The mobile provisioner deliberately cannot initialize a system-role account.

## Admin Sets Initial Web Credentials

PM confirmation on 7 October: the Admin sets the staff member's initial web password, including HR/managers. The system does not require employee self-setup or silently turn it into a temporary-password flow.

`POST /api/v1/access/accounts/{accountId}/web-credentials` takes `{email,password,expectedAccountVersion}` over HTTPS with the authenticated browser's CSRF token. It requires a current fixed Admin/SuperAdmin assignment, a valid web password/SMS session, All-scoped `access.accounts.web.manage` and `access.roles.permissions.manage`, and delegation authority for every existing/scheduled target permission. A system-role target additionally requires SuperAdmin. Self-provisioning is denied; this is not a first-admin setup route.

The account must already have genuine approved onboarding, configured access, eligible employment and an explicitly approved registered phone. Initial setup works for ReadyForInvitation or already-active mobile-only accounts, without inventing an invitation. An outstanding invitation, existing password, blocked account, stale version, duplicate login email or phone-recovery state is rejected. No silent phone rebinding or existing-password overwrite is allowed.

The command stores only an Identity V3 password hash, enables web access, updates the security version and writes an attributed audit in one transaction. It preserves mobile entitlement and initial-access history. It creates/reuses an unverified approved phone binding but never marks a phone verified or creates a session. Response contains account access metadata/version, never the password/hash/email/phone. Admin must communicate the password through an approved private channel; no password SMS/email dispatch was added. Proxies/APM must also avoid logging credential request bodies.

Explicitly configure `WebAuthentication:MinimumPasswordLength` (12-128) before provisioning; zero fails closed with `web_password_policy_not_configured`. This is an engineering boundary, not a chosen company policy. Passwords are accepted up to 1024 characters, are not trimmed, and cannot be all whitespace. Login can remain disabled while initial credentials are configured. Password reset/change uses the separate audited recovery routes below.

## Web HTTP Contract

### Reviewed Initial-Password Reset

PM approved this exception on 7 October: before **any successful login**, another authorized
Admin may reset a forgotten Admin-set initial password after reviewing identity.
`POST /api/v1/access/accounts/{accountId}/web-credentials/reset-initial-password` takes
`newPassword`, `reason`, `approvalReference` and `expectedAccountVersion`. Retain the full reason
and reviewed evidence in the private record referenced by `approvalReference`; ordinary audit
stores its reference, not identity evidence or a password.

The same fixed-administrator, web SMS MFA, All-scoped web-management/delegation and protected-target
SuperAdmin rules apply. Self-reset is denied. Current approved access, employment, joining date,
phone binding and rowversion are checked. Any persisted session history (including expired/revoked
sessions), verified phone history, disabled channel, block, invitation or pending phone recovery
denies this exception. Accounts that already logged in use the normal SMS recovery below.

One transaction hashes the replacement, invalidates the security version and outstanding proofs,
and writes attributed audit evidence. It does not change login email, registered phone, roles,
mobile access or original approval. No login session is created; email/password plus fresh SMS
OTP remain required. This is not a general administrator password-overwrite endpoint.

### Browser Sign-In

The approved sequence is email/password, registered-number SMS OTP, then authenticated web session. There is no authenticator-app step. Existing historical MFA entities are retained; they do not replace the new SMS proof during web-session validation.

| Method and route | Contract |
|---|---|
| GET `/api/v1/auth/web/csrf` | HTTPS; creates a browser-context cookie and returns `{requestToken,headerName}` |
| POST `/api/v1/auth/web/login` | `{email,password}` with browser cookies and `X-CSRF-TOKEN`; generic 202 OTP challenge, no login cookies |
| POST `/api/v1/auth/web/otp/resend` | `{challengeId,email,password}` with the same browser context; password is checked again |
| POST `/api/v1/auth/web/otp/verify` | `{challengeId,code}`; only both verified factors create the web session |
| POST `/api/v1/auth/web/refresh` | No token body; reads protected refresh/browser cookies and rotates the refresh secret |
| POST `/api/v1/auth/web/logout` | Current authenticated session; revokes it and clears web cookies |
| POST `/api/v1/auth/web/logout-all` | Own-session management authority; revokes account sessions and clears web cookies |

Verify/refresh success returns only `{sessionId,accessTokenExpiresAt,refreshTokenExpiresAt,nextStep}`. Tokens are sent in `__Host-Cosmix.Access` and `__Host-Cosmix.Refresh` cookies: Secure, HttpOnly, SameSite=Strict, path `/`, no Domain. The separate browser-context cookie binds proofs/refresh to that browser. Never put these tokens in localStorage or return them to React as JSON.

Use HTTPS for both frontend and API with a same-site hostname scheme (for local development, `https://localhost:5173` and `https://localhost:7240`). Use `credentials: "include"` and the exact configured CORS origin. HTTP frontend to HTTPS API is cross-site for these cookies; allowing an HTTP CORS origin does not make browser login work. Do not weaken cookies to work around this. Trusted frontend certificates and frontend HTTPS configuration are separate setup tasks.

Fetch a fresh CSRF request token before login, again after successful sign-in, and before refresh when the access cookie has expired. ASP.NET antiforgery tokens are identity-bound. All unsafe web-auth routes and all unsafe requests bearing access/refresh cookies require the token. Login/OTP routes do not exempt themselves. Missing/invalid tokens return 400 `invalid_csrf_token`.

Invalid credentials, unknown accounts and ineligible/throttled requests get the same 202 challenge shape and no SMS. A 202 is not proof of delivery. Web and mobile challenges, resend invalidation, sessions and refresh paths are purpose-bound; neither can be exchanged for the other. The registered destination comes from approved server data, never a login request. Browser passwords must not be persisted by application JavaScript.

Passwords use Microsoft's Identity password hasher with the explicit `Microsoft.AspNetCore.Identity.PasswordHasherV3` format marker. Existing unknown hash formats fail closed; do not label a hash with a format it does not use. Only the authorized initial-provisioning command creates credentials; it is not a reset endpoint.

## Mobile HTTP Contract

All responses containing authentication/current-account information use no-store caching.

| Method and route | Contract |
|---|---|
| POST `/api/v1/auth/mobile/otp/request` | `{phoneNumber, deviceSecret, platform, deviceName}`; platform `android` or `ios`; explicit international phone |
| POST `/api/v1/auth/mobile/otp/resend` | `{challengeId, request:{phoneNumber,deviceSecret,platform,deviceName}}`; same initiating device context |
| POST `/api/v1/auth/mobile/otp/verify` | `{challengeId,code,deviceSecret}`; six ASCII digits |
| POST `/api/v1/auth/refresh` | `{refreshToken,deviceSecret}`; rotated response, no clientType override |
| POST `/api/v1/auth/logout` | Current bearer session, 204 |
| POST `/api/v1/auth/logout-all` | Revoke all current account sessions, 204 |
| GET `/api/v1/me` | Safe account/employee identifiers, display name and channel flags; no HR-confidential profile |
| GET `/api/v1/me/permissions` | Current supported grants restricted to this session's allowed surface |
| GET `/api/v1/me/devices?page=1&pageSize=100` | Own devices, including revoked history, bounded pages, no device secrets |
| DELETE `/api/v1/me/devices/{id}` | Own device plus its sessions/challenges revoked |
| GET `/api/v1/me/sessions?page=1&pageSize=100` | Own active sessions, bounded pages |
| DELETE `/api/v1/me/sessions/{id}` | Own session revoked; no cross-account selection |

`/me` requires `auth.self.read`. Device/session listing and management, including logout-all,
require `auth.self.sessions.manage` with an effective own-employee scope. Current-session logout
remains available to every authenticated session as a basic security action. `/me/permissions`
allows an authenticated client to discover its current allowed capabilities.

Device listing keeps its array response. `page` is 1-1,000,000 and `pageSize` is 1-100;
omitting them returns the newest 100 devices. Results sort by registration time descending,
then ID ascending, with account filtering before pagination. Request successive pages until
the returned array is shorter than `pageSize`. Concurrent registrations can move page boundaries;
refresh/deduplicate the client list by ID. Device revocation performs an ownership-scoped lookup
by ID, independent of whichever listing page the client has loaded.

Active sessions use the same page limits and array contract, ordered by creation time descending,
then ID ascending. Filtering by account and active state happens in SQL before pagination;
sessions beyond the first 100 remain accessible. Logout audit records each affected session ID,
current actor role(s), employee scope and initiating session/device. Current-permission reads are
audited with the actual current role(s); they are not a substitute for future HR sensitive-read audits.

OpenAPI distinguishes mobile bearer authorization from browser-only cookie/CSRF authorization.
The browser owns HttpOnly cookies; a bearer token does not authorize a web management route.
Refresh requires refresh/browser cookies and CSRF, not an unexpired access cookie.

Request/resend return HTTP 202 with `{challengeId,expiresAt,resendAvailableAt,nextStep:"enter_otp"}`. This acknowledges a generic request, **not confirmed SMS delivery or an existing employee**. Unknown/ineligible/throttled numbers receive the same shape; no account IDs or codes. Too-early resend returns a generic non-delivering response, so clients must honor the cooldown.

Verification/refresh success returns `{accessToken,accessTokenExpiresAt,refreshToken,refreshTokenExpiresAt,sessionId,tokenType:"Bearer",nextStep:"authenticated"}`. Tokens are issued only after the database transaction commits successfully; a token constructed before commit is not returned on rollback.

Errors use ProblemDetails: `invalid_authentication` (401), `temporarily_limited` (429), `authentication_unavailable` (503), or `device_recovery_required` (403). Input-validation errors are 400. No OTP, destination, provider body or secret appears in these errors.

React Native must store refresh/device secrets in OS-protected Keychain/Keystore storage, not AsyncStorage. Generate a random installation secret with at least 128 bits of entropy (32-256 character transport contract); it is context binding, not hardware attestation or MFA. Access tokens should be kept in memory where practical. Serialize refresh requests; do not automatically retry a consumed refresh token after an uncertain network response.

Screens/states for the client: phone entry, OTP entry, resend countdown, generic incorrect/expired code, unavailable provider, session/device revoked and contact support for recovery. No mobile password/reset UI.

## Configuration

Both `MobileAuthentication:Enabled` and `WebAuthentication:Enabled` ship as false. Existing health/documentation/reference APIs can start without a selected SMS provider. Login returns 503; no fake provider is installed. The existing `MobileAuthentication` settings also own shared SMS/crypto/session infrastructure; web requires that infrastructure enabled, but individual accounts still require their own channel entitlement.

Enabling web additionally requires explicit `WebAuthentication` controls: `MaximumPasswordFailures` (1-20), `PasswordLockoutSeconds` (60-86400), `PasswordAttemptsPerAccount` and `PasswordAttemptsPerEmail` (at least the failure limit and at most 100), `PasswordAttemptsPerIp` (at least both account/email limits), and `PasswordAttemptsGlobal` (at least the IP limit). Password budgets use the shared rate window. No company values are silently chosen.

Set secrets with API user secrets for local development, or a production secret manager/environment. Never commit keys, phone data or bearer tokens. Every replica must use the same approved keys, database and configuration.

Required explicit `MobileAuthentication` settings when enabled:

| Setting | Validation boundary; management must choose the actual value |
|---|---|
| `OtpLifetimeSeconds` | 60-600 |
| `ResendCooldownSeconds` | At least 30, less than OTP lifetime |
| `MaximumOtpAttempts` | 1-10 |
| `RateWindowSeconds` | 60-86400 |
| `RequestsPerPhone`, `RequestsPerIp`, `RequestsGlobal` | Positive, nested limits; phone limit at most 20 per window |
| `VerificationAttemptsPerPhone`, `VerificationAttemptsPerIp`, `VerificationAttemptsGlobal` | Positive nested limits, phone limit at least per-challenge attempts |
| `AccessTokenLifetimeSeconds` | 60-900 |
| `RefreshTokenLifetimeSeconds`, `SessionLifetimeSeconds` | Refresh at least access lifetime; absolute session at least refresh, at most 30 days |
| `MinimumRequestDurationMilliseconds` | 100-3000; timing padding is mitigation, not proof of constant timing |
| `BusinessTimeZoneId` | Explicit valid server time-zone ID for joining-date eligibility |

These bounds are engineering guardrails, not claims of approved company policy. All actual values remain unset in Git.

`MobileAuthentication:Crypto`:
- `KeyVersion`: 1-32 ASCII letters/digits/hyphens/underscores.
- `VerifierKeyBase64`: cryptographically random 32-128 bytes.
- `EncryptionKeyBase64`: a different random 32-byte key.
- `MaxDeliveryLifetimeSeconds`: 1-3600, at least `OtpLifetimeSeconds`.

`Jwt`: existing `Issuer`, `Audience`, plus a separate private random `SigningKey` of at least 32 bytes. Never reuse SMS/OTP encryption secrets as signing keys.

`MobileAuthentication:Sms`:
- `Provider=GenericHttp` only for a reviewed operator-owned compatible gateway.
- `Endpoint`: fixed HTTPS URL, no userinfo/query/fragment.
- `BearerToken`: secret outside Git.
- `TimeoutSeconds`: explicit 1-60 seconds.

The gateway must accept POST JSON `{recipient,message,idempotencyKey}`, use bearer authentication, deduplicate idempotency keys and translate to the selected SMS vendor. Only HTTP 200/202 mean accepted. Its logs/proxy/APM must suppress credential and message bodies. This is **not** a universal vendor configuration: a different vendor protocol needs a reviewed adapter. No service was purchased or contacted.

## Security and Failure Semantics

- Numeric codes use CSPRNG generation, keyed HMAC verifiers and constant-time verification. Delivery payloads use AES-GCM with expiry. No ordinary plaintext OTP table, notification or log.
- OTP consumption, activation, device/session/token persistence and audit share one transaction. Wrong-attempt evidence and refresh-reuse revocation commit before their failure response.
- SQL Server serializable transactions and a shared `sp_getapplock` protect authentication writes across replicas. This intentionally serializes a small critical section; perform realistic load testing before production. No SMS HTTP call runs inside that database transaction.
- Durable per-number/account, IP and global budgets prevent resend from resetting the account attempt allowance. Windows are fixed-duration buckets, not a mathematically rolling-window limiter.
- Delivery is claimed and committed before HTTP. Accepted means submitted, never delivered. Failed/unknown/expired delivery is terminal and its encrypted payload is cleared. Interrupted leases are never blindly resent. There is no unauthenticated callback route.
- While workers run, expiry cleanup clears pending encrypted payloads. If workers are stopped or the whole feature disabled, physical cleanup stops; expiry still prevents decryption/use. Operations must drain/expire the queue before disabling workers and include encrypted backups in the eventual retention policy.
- Every protected request checks persisted session/account version, employment/approval, channel entitlement and current role/scope eligibility. Mobile additionally checks phone/device state; web checks its consumed SMS proof, approved current verified phone and browser binding. Web tokens sent as bearer headers and mobile tokens sent as web cookies are rejected. No cached permission claims authorize access.
- Changing roles/channel entitlements/security version invalidates sessions; new login rechecks rights. Disabling web leaves mobile entitlement intact but requires reauthentication when the shared version changes.
- Refresh secrets are high-entropy and only digests are stored. Reuse revokes the family. Absolute session expiry cannot be extended by rotation.
- A revoked installation cannot silently reuse its registration. New-device notifications are durably queued in-app; notification UI/delivery belongs to its module and is not marked delivered here.
- Permission and scope stay paired to the same assignment. System Admin has no role-name bypass into HR-confidential data. New resource modules must use scoped server queries, not merely a list of menu permissions.
- Incoming forwarded IP headers are not trusted. Configure only known reverse proxies before deployment; otherwise budgets group by the direct peer IP. Edge request limiting, TLS, database/service least privilege and monitoring remain deployment responsibilities.
- Rotate keys through a documented operational procedure. Current helper configuration is loaded at startup. Verifier-key/version changes invalidate outstanding proofs, refresh/device digests and rate keys; coordinate across all replicas and explicitly revoke sessions as needed. No transparent multi-key rotation is claimed.

## Explicitly Pending

- Approved SMS vendor/contract, sender identity, gateway deployment and real delivery acceptance.
- Run the first-SuperAdmin command with the real owner's authorization, private evidence/key storage and privately entered password. No real account has been created by this implementation task.
- Real HR approval application integration and initial access assignment ownership.
- Broader SQL concurrency, real-browser/mobile/SMS acceptance, operating limits and retention remain gates. Both web and recovery migrations were applied/verified against the original development database. SMS carries SIM-swap/interception risks and is not phishing-resistant MFA; this implements the approved login policy, not a zero-risk guarantee.

## Password Recovery and Change

- `POST /api/v1/auth/web/password/forgot`: email; generic 202 challenge metadata. Only an active,
  web-enabled account with a currently approved, already verified phone can receive a reset SMS.
- `POST /api/v1/auth/web/password/resend`: email and previous challenge ID; same protected browser,
  cooldown and shared delivery/verification budgets. Failed-attempt history is not reset by resend.
- `POST /api/v1/auth/web/password/reset`: challenge ID, SMS code, new password. Purpose, browser,
  account version and current eligibility are rechecked; proof consumption, hash replacement,
  security-version change, session/proof revocation and audit are atomic. Returns 204, no session.
- `POST /api/v1/auth/web/password/change`: current password and new password, with an authenticated
  SMS-backed web session. Revokes existing sessions and clears browser authentication cookies.
- All browser routes require the existing CSRF/browser-cookie contract. An active password lock,
  suspension or employment block is not bypassed. Password policy must be explicitly configured.

## Controlled Phone Recovery

PM approved identity-reviewed recovery on 7 October. A profile edit or possession of an arbitrary
SIM cannot rebind a login account. Existing HR submission snapshots remain immutable.

1. Another fixed Admin/SuperAdmin reviews identity evidence outside this endpoint, then posts to
   `POST /api/v1/access/accounts/{accountId}/phone-recovery`: `newPhoneNumber`, `reason`,
   `approvalReference`, explicit `expiresAt`, and `expectedAccountVersion` (base64 SQL rowversion).
   It requires current SMS-authenticated web authority, All-scope channel/role-permission management
   and delegation for the target's permissions. Only another SuperAdmin may recover protected-role targets.
2. One transaction records `auth.LoginPhoneChanges`, revokes the old binding, sessions and open
   challenges, invalidates the account version and installs an unverified replacement binding.
   Normal login is denied while recovery is pending. Approval expiry is explicitly supplied, with
   an engineering maximum of 24 hours; no company expiry default is invented.
3. The employee requests an OTP using the approved **new** number:
   `POST /api/v1/auth/mobile/phone-recovery/request` (`phoneNumber`, `deviceSecret`, `platform`,
   optional `previousChallengeId`) or `/api/v1/auth/web/phone-recovery/request` (`phoneNumber`,
   optional `previousChallengeId`, protected browser cookie/CSRF). Public responses do not disclose eligibility.
4. `POST /api/v1/auth/mobile/phone-recovery/verify` accepts challenge ID, code, same device secret
   and platform; the web equivalent accepts challenge ID/code plus browser cookie/CSRF. Current
   approval, approver authority, account version, destination binding and attempt budgets are rechecked.
   Completion verifies only that approved number, revokes previous access again and returns 204.
   It never issues a session, changes a password, enables a channel or grants a role. Login follows separately.
5. Expired/superseded recovery does not restore a possibly stolen SIM. Another reviewed approval
   replaces the unfinished attempt with explicit reason/evidence; history is retained. There is no
   public cancellation that re-enables the old number. Normal phone uniqueness prevents concurrent reuse.

## Reviewed Device Reenrollment

PM approved this recovery on 7 October. A revoked installation cannot restore itself or reuse
its old sessions. Another current fixed Admin/SuperAdmin with All-scoped mobile-management and
role-permission/delegation authority first reviews identity and the account's device history.

- `GET /api/v1/access/accounts/{accountId}/device-recovery?page=1&pageSize=25` returns bounded
  device history plus account/device rowversions, without the installation secret/hash.
- `POST /api/v1/access/accounts/{accountId}/device-recovery/{deviceId}` takes
  `approvalReference`, `expectedAccountVersion` and `expectedDeviceVersion`.
- Only the current revoked record of an eligible mobile staff account can be replaced.
  Self-approval and fixed Admin/SuperAdmin mobile targets are denied; stale versions and
  duplicate approvals conflict. The evidence reference identifies a retained private review record.
- Approval preserves the old revoked device, creates a new **unverified** row for that installation,
  invalidates account proofs/sessions and audits reviewer, employee, previous/new device and session.
- The employee must request and verify a **fresh normal mobile OTP** using the same installation
  secret. Approval alone creates no session. Only successful fresh proof verifies the replacement.
  Old OTPs, refresh tokens and revoked history remain unusable.

No new table, device attestation claim, hardware identifier or silent reinstatement was added.

## First SuperAdmin Setup

`bootstrap` here means **one-time first-SuperAdmin initialization**, not a CSS/UI framework.
Actual execution is deferred by PM instruction; no real or fabricated CEO account has been created.
`dotnet run --project src/Cosmix.Api --configuration Release -- bootstrap-superadmin --help`
prints the local command contract without opening SQL or starting an HTTP server. Run without
`--help` in a private interactive terminal for actual initialization; redirected input/output and
password arguments are refused. The actual SQL server/database must be explicitly confirmed.

Provide real CEO/director identity and retained approval evidence, explicit owner authorization
for self-initialization/fixed-role grants, and a separately protected backed-up 32-byte encryption
key. The command reads private input paths outside Git/public web storage and checks their ACLs.
Password is entered twice without echo, then hashed using the maintained Identity V3 hasher.
No password, OTP or raw identity is written to Git/logs by this command.

It requires the reviewed current schema and an empty business database, serializes using the
authentication lock, and refuses reruns. Owner initialization has explicit external provenance,
not invented HR submitter/approver IDs. The private encrypted snapshot preserves actual owner
authorization and its original timestamp. Existing HR workflows still require real user actors.
Only one owner-initialization submission is permitted by a unique filtered database index.

Fixed Admin and SuperAdmin definitions are created with explicitly enumerated supported permissions;
only SuperAdmin is assigned to the owner. The shared identity is web-only, phone remains unverified,
and first login still requires email/password plus genuine SMS OTP. No session is seeded. The
command is not registered in the normal HTTP host. If this sole SuperAdmin loses the phone, the
approved second-SuperAdmin recovery rule cannot be bypassed: appoint an authorized second SuperAdmin
through normal access administration before relying on in-app privileged recovery.

See [Access Management](Authentication-Access.md) for administration routes and delegation constraints. See [Verification](Shared-Authentication-Verification.md) for actual evidence and migration status.

Implementation references: [EF Core transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions), [EF concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency), [ASP.NET Core JWT validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).
