# Shared authentication and access management

Current policy is the **Approved Authentication Policy Update - 7 October 2026** at the end
of this document, including the PM's Admin-set initial password decision. Earlier statements
about privileged mobile blocking or web policy not being chosen are historical, not current rules.

## Purpose and review status

This design defines the shared security module used by all Cosmix business modules.
It is a design for PM review, not a statement that authentication is implemented.
The current agreed implementation priority is employee mobile phone and SMS OTP
login, with provider selection deferred. Web credential provisioning remains a
separate delivery item and must not block independent mobile work.

The reviewed checkout is `develop` at `34e8e077283039a830cff7258bb37c2514f73d57`.
The local `Cosmix_Development` database already contains the four existing
migrations, ending at `20261006105213_CorrectReviewedModelLifecycles`. It has 117
mapped tables, not implemented authentication APIs. Older repository notes that
say no database exists describe the earlier offline review, not this local state.

Runtime update, 7 October 2026: the original SSMS database was confirmed and upgraded through
`AddSharedMobileAuthentication` after backup. An intervening missing-database observation
was from another running LocalDB instance under the same alias. Current execution evidence
is in `../../Shared-Authentication-Verification.md`; the design baseline above is historical.

## Confirmed requirements

- Every approved, eligible employee can use the mobile app, including HR,
  managers and administrators. Mobile is employee self-service and attendance.
- Mobile sign-in uses an explicitly approved phone number and an SMS code, not
  a password. The company has not chosen its SMS provider.
- One UserAccount and EmployeeId are shared by web, mobile and future modules.
- Web entitlement is separately administered. Roles and designation do not
  automatically activate web access.
- Admin assigns supported permissions and effective resource scopes through
  roles. Role names never grant implicit access to sensitive employee data.
- HR submission and Admin approval remain prerequisites for ordinary accounts.
  Approval, employment eligibility, phone verification and sign-in are distinct.
- Face verification belongs to attendance Check In, not login or Check Out.
- There is no WhatsApp integration, public registration or automatic approval.
- No new test projects, unrelated UI, shared passwords, real employee seeds,
  automatic startup migrations, commits or pushes are authorized by this task.

## Approach and alternatives

Recommended: extend the existing Authentication module in the four-project
backend, with an Application delivery contract and Infrastructure SMS adapters.
Cosmix owns its approved account binding, challenge lifecycle and session/access
rules. Provider-specific HTTP details do not enter Domain or controllers.

A provider-managed verification service is an alternative. It reduces local
code-secret handling but requires the selected provider's challenge and verification
contract before its implementation can be completed.

Replacing all accounts with an external identity platform is another alternative,
but adds deployment, migration and integration decisions not presently approved.
It is not part of this design.

There is no universal configuration that supports every SMS vendor. A provider
that implements the documented gateway contract can be connected by configuration.
Other vendors need a small named adapter, with their success/error, retry and
delivery-receipt semantics verified. No arbitrary URL from a login request is used.

## Account and onboarding boundaries

HR's approved employee submission includes the selected normalized login number.
Personal phone, company phone and SIM custody remain contact/employment records;
changing any of them does not silently change the login credential.

The authentication provisioning use case consumes an authoritative current
employee approval and authorized role/scope assignment. It is idempotent and
creates mobile eligibility without marking the phone verified. A repeated approval
integration call does not create a second account or reset existing credentials.

An eligible account with unverified phone binding can request its first challenge.
Successful verification may activate mobile authentication only after eligibility
is re-evaluated. An already active web account verifies mobile access on the same
account. Mobile activation never grants web entitlement or sets a dummy password.

HR approval itself still owns employment activation. Joining/effective dates and
current suspension/termination history are checked at authentication and refresh.
No face enrollment prevents Check In but does not itself prevent account login.

The existing password-based invitation workflow is preserved for the separate
web path. Mobile activation must not require entering the Invited state. Existing
invitation replacement and audit evidence remain valid.

## Persistence changes

Reuse existing UserAccount, UserSession, RegisteredDevice, RefreshToken, Role,
Permission, UserRoleAssignment, UserAccessScope and AuditEvent models.

Add explicit channel eligibility and authentication-method state without changing
the stable account/employee identifiers. Revise the active-account SQL constraint
to support a real verified mobile credential or the applicable web credential;
do not simply remove credential validation. Preserve enum numeric values already
stored in the database and update relevant checks when adding states.

Add an account-bound login-phone history with canonical international number,
binding version, approval/assignment evidence, verification time and revocation
history. A unique active binding prevents one login number resolving to multiple
accounts. Do not enforce this restriction on ordinary HR contact numbers.

Add purpose-specific mobile login challenges rather than reusing password-reset
or MFA challenges. Persist opaque challenge identity, binding/security version,
device context, issue/expiry, attempts, consumption/revocation and delivery state.
Use concurrency tokens and database enforcement for single-use state transitions.

Durable rate-limit counters and an authentication delivery outbox are introduced
only where the existing shared infrastructure lacks those capabilities. Coordinate
these contracts with the notifications/job owner. No second DbContext is introduced.

## SMS lifecycle and configuration

1. Validate and normalize the phone with a maintained parser. Evaluate provider
   readiness independently of whether the number is registered.
2. Apply request, destination, account, IP and service-wide abuse limits. Requests
   for unknown/ineligible identities return the same public response shape as
   accepted requests without sending SMS or exposing account identifiers.
3. Create a purpose-bound challenge and durable delivery work atomically with
   appropriate audit evidence. Ordinary logs and notifications contain no code.
4. Dispatch only after commit. Do not hold a SQL transaction across provider HTTP.
   Use bounded retries and message identity; a resend supersedes the old challenge.
5. Distinguish queued, provider-accepted, delivery-confirmed, failed and unknown
   outcomes. A provider HTTP success alone is not proof of handset delivery.
6. Verify proof, eligibility and current binding again; atomically consume it and
   persist activation/session/audit state. Replays and concurrent attempts must not
   create additional successful sessions from the same challenge.

For locally generated codes, use cryptographic randomness and a keyed verifier
with its key outside SQL/Git. Plain hashing of a short numeric code is not adequate.
If durable delivery needs the raw code temporarily, keep it in a dedicated
encrypted outbox payload with separately managed keys, strict access and deletion
on expiry/terminal handling. Never return codes through public APIs or write them
to console logs. Do not invent custom cryptographic primitives.

Configuration includes named provider, HTTPS endpoint and allowed destination,
sender/template identifier, secret references, request timeout, delivery retry
rules and receipt validation. Prefer typed named adapters; avoid a configurable
arbitrary HTTP scripting engine. Provider credentials are server-only.

Required security-policy settings include code length/lifetime, resend delay,
attempt and sending budgets, lockout/backoff, session/access-token lifetime,
challenge retention and key references. Production enablement requires explicit
validated settings; example values are not silently treated as approved policy.

Unconfigured delivery returns a consistent service-unavailable result without
queuing fake successes or issuing tokens. It must not prevent unrelated service
startup, documentation or authorized provider-independent verification.

## Sessions and access enforcement

Use established .NET identity/token libraries and preserve the trusted API token
validation boundary. Do not expose a public token generator or add a new custom
authentication protocol. Token issuance must be linked to persisted completed
authentication and a current session, not merely to a syntactically correct code.

Enforce account, employment, channel entitlement, device, session and security
version on protected requests and refresh, not only at login. Revocation and
permission removal must not leave stale cached grants effective indefinitely.

Rotate refresh tokens atomically, retain digests for reuse detection, revoke the
family on confirmed reuse and persist that revocation before returning failure.
Protect concurrent refresh without issuing two valid successors. Store mobile
tokens in OS secure storage; do not add browser token storage to localStorage.

Data access must pair each permission with scopes from that same role assignment.
Apply effective memberships and recorded termination when resolving populations.
Scope filtering precedes pagination, counts and exports. Self-service grants are
not department-wide grants even for an employee with an HR designation.

Server-derived session context and authentication assurance constrain capabilities.
A client-supplied `clientType`, role, EmployeeId or device identifier cannot grant
web access, trusted-device status or privileged-operation access.

## Privileged authentication and credential recovery

SRS AUTH-004 remains mandatory; a single SMS code is not MFA. The exact privileged
authentication method is not chosen by the SMS-channel confirmation. Existing
MFA-required accounts must fail closed or receive a restricted continuation until
the approved MFA flow is available, never an unrestricted OTP-only session.

Password-based web authentication and its delivery/setup method are a separately
reviewed integration. Mobile-only accounts cannot enable it through password reset.
The first-administrator bootstrap must be explicitly approved; no fake approval,
anonymous bootstrap endpoint or hardcoded administrator bypass is introduced.

Lost-phone/number replacement requires a controlled identity-recovery decision.
It is not ordinary profile editing. The new binding needs verification; old
challenges and affected sessions are revoked atomically. Recycled phone numbers
never transfer historical accounts to another employee. Until the recovery policy
is approved, no unauthenticated automated number-reassignment endpoint is enabled.

## Proposed client contracts

Routes follow `/api/v1`. Existing web routes are not repurposed as mobile routes.

| Route | Purpose |
| --- | --- |
| POST /auth/mobile/otp/request | Start a generic public challenge response |
| POST /auth/mobile/otp/resend | Supersede an eligible challenge within abuse limits |
| POST /auth/mobile/otp/verify | Complete authentication or restricted MFA continuation |
| POST /auth/refresh | Rotate an eligible session's refresh proof |
| POST /auth/logout | Revoke current session |
| POST /auth/logout-all | Revoke the authenticated account's sessions |
| GET /me | Minimal approved current-user data |
| GET /me/permissions | Effective capabilities for UI hints, never authorization proof |
| GET /me/devices and DELETE /me/devices/{id} | List/revoke owned devices |
| GET /me/sessions and DELETE /me/sessions/{id} | List/revoke owned sessions |
| /access/roles and /access/permissions | Authorized supported permission/role management |
| /access/accounts/{id} | Authorized channel/access assignment and revocation |

Define bounded DTO fields and exact HTTP behavior in the implementation plan.
Challenge responses use opaque references and consistent expiry/resend metadata;
they never include OTPs, account status or employee identifiers before authentication.
Authentication responses are non-cacheable. Respect existing ProblemDetails,
correlation IDs, HTTPS, CORS and OpenAPI conventions.

## Delivery scope and sequencing

First delivery: account/channel and phone-binding support, challenge lifecycle,
provider boundary, mobile contracts, sessions, current-user access and auditing.
Follow with authorized role/scope administration and onboarding integration.
Provider availability and privileged MFA remain explicit enablement gates, not
reasons to fabricate success or call the entire authentication project complete.

The backend produces React/React Native integration documentation, not client UI.
It does not implement HR CRUD, face providers, attendance processing or payroll.
SMS vendor changes must be isolated from account and authorization rules.

## Migration and verification

Existing migrations are already applied locally and must not be rewritten.
Coordinate new shared mappings and migration ownership with Developer 2. Prepare
new reviewed migrations after model changes; this feature does not automatically
authorize applying new migrations to an arbitrary database or resetting the current
one. Do not add startup migrations or fabricate verified existing phone data.

Verification must include normalization collisions, approval/channel eligibility,
first and subsequent login, resend ordering, expiry, concurrent proof consumption,
provider timeout/outage, rate-limit bypass, changed phone binding, device revocation,
refresh replay/races, cross-user/scope denial and web/MFA downgrade attempts.

Use temporary focused checks without adding test projects. Synthetic adapters may
be injected only by isolated checks, never selected as a successful runtime provider.
Run Release builds and distinguish offline checks from separately authorized real
SQL/provider/device integration. No real employee data or persistent demo accounts.

Acceptance of the provider-independent implementation requires honest unavailable
behavior and passing independent checks. Real SMS end-to-end acceptance additionally
requires the selected provider, configured secrets and authorized delivery testing.
# Approved Authentication Policy Update - 7 October 2026

The PM explicitly requested implementation of these clarified rules. They supersede the
earlier proposed authenticator-app MFA and the blanket MFA block on employee mobile login:

- Every web sign-in uses email and password, followed by SMS OTP to the approved registered
  phone. Correct password alone issues no session. This also applies to Admin and SuperAdmin.
- Employee mobile sign-in is SMS OTP only, including HR and managers. Mobile sessions are
  self-service only, do not assert web MFA, and cannot invoke management endpoints.
- Admin and SuperAdmin are protected, unique system roles used by the CEO/director and are
  web-only. Other roles remain permission-based and administrator-created. Protected roles
  must not become an authorization bypass into confidential HR data.
- Reuse the existing account, repositories, encrypted SMS outbox, session and permission
  services. Keep changes local. No new frameworks, identity system, frontend or repo test projects.
- Web credentials are verified with the maintained ASP.NET Core password hasher. Browser
  tokens stay in Secure HttpOnly cookies; unsafe cookie requests require antiforgery validation.
- Keep the existing OTP table name for migration compatibility; explicit purposes separate
  mobile login and password-verified web login. Preserve all applied migrations.
- The PM subsequently confirmed Admin sets the initial staff web email/password during the
  approval/access handoff, including HR/managers. Reuse the approved account and assigned role;
  store an Identity V3 hash, never return the password, and still require SMS at login. This is
  initial setup only, not a silent password reset, phone verification or first-admin bootstrap.
- SMS provider selection/live delivery remain external prerequisites. Password/phone recovery
  and initial administrator identity must not be faked to demonstrate login.

Implementation and verification status is recorded in Shared-Authentication-Verification.md.
The historical design above is retained for context, not as a conflicting current login policy.
