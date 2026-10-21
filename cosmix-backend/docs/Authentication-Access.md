# Authentication and Access Management

This slice implements dynamic, server-side permission reads and bounded access administration.
As of 7 October, web sign-in requires email/password followed by registered-number SMS OTP,
including Admin/SuperAdmin. Current persisted SMS proof is required for management operations;
an MFA claim or a mobile token cannot unlock these routes. Authorized initial Admin-set password
provisioning, password/phone recovery and local first-administrator initialization are implemented.
Actual owner creation is deferred by the PM; live SMS awaits provider selection and configuration.
There is no public signup or seeded account. See [Shared Authentication](Shared-Authentication.md).

Confirmed module access rule (9 October 2026): current fixed Admin/SuperAdmin users create
roles, select supported module permissions and assign roles to employees through the existing
web APIs. They require current web MFA and their existing All-scoped access-management rights,
but need not personally own the business permissions being configured. Custom role names or
management grants do not confer fixed-administrator authority. This resolves scheduling's
first-grant dead end without changing the frozen bootstrap list or granting anything automatically.
There is no separate local provisioning command. See the [confirmed web provisioning flow](access-provisioning/Provisioning-Design.md).
The separately approved isolated `Cosmix_Testing` web acceptance on 9 October passed 65
checks, including the first catalog grants before Admin owned scheduling permissions,
SuperAdmin-only changes to protected accounts, genuine MFA/CSRF and scope-preserving synthetic
assignments. Four synthetic grants remain for scheduling/replay tests. This is not real-account
provisioning or proof of the entire SQL acceptance suite; see the
[current access verification](access-provisioning/Verification.md).

After account activation, the initial onboarding assignment/scope IDs stay historical.
An authorized replacement role can restore current mobile eligibility after the original
assignment expires or is revoked; callers must not rewrite the initial pair on an active account.
Assignment/revocation still invalidates the account security version. Current effective
permissions, scope, employment and MFA checks apply on fresh login and protected requests.

## Integration Contracts

- `Cosmix.Application.Authentication.Access.IAccountAccessReader.GetGrantsAsync(Guid accountId,
  DateTimeOffset now, CancellationToken)` returns `IReadOnlyList<PermissionGrant>`.
- `PermissionGrant` contains `Permission`, `Sensitive`, `RoleAssignmentId`, `ScopeKind`, and `TargetId`.
- `IAccessManagementService` implements the use cases through `IAccessManagementRepository`.
- `Cosmix.Infrastructure.Authentication.Access.AccessManagementExtensions.AddAccessManagement(IServiceCollection)`
  registers the reader, repository, service, and default `TimeProvider`.
  The composition root must also register `ICurrentAuthentication`, the existing `CosmixDbContext`,
  and the directly registered, fully bound `MobileAuthenticationOptions` instance. This repository
  uses that same settings object, not a separately defaulted `IOptions<MobileAuthenticationOptions>`.
- `MobileAuthenticationOptions.BusinessTimeZoneId` must be explicitly configured with a valid
  system timezone. Missing/invalid timezone denies administrator validation, even if mobile login
  is disabled. The access slice does not invent a company timezone.
- `ICurrentAuthentication.Require()` must return an account validated from the stored session,
  including its real client and MFA state. HTTP request fields and ordinary JWT permission claims
  are not permission decisions.

The raw reader intentionally does not filter by session client: each consuming service applies
the allowed channel surface. It joins role permissions and scopes through the same
assignment. Assignments require an active role, inclusive effective start, exclusive effective
end, and no effective revocation. Role permissions require `RevokedAt == null` and
`GrantedAt <= now`. Unknown catalog codes never authorize a request.

`AccessPermissionEvaluator.ForSession(actor, grants)` produces session-appropriate UI hints.
Mobile receives only non-sensitive supported self-service grants that originally cover its own
employee: Self, its own Employee scope, or All. Each is capped to Self with the authenticated
employee ID. Department/team/site/project scopes are not assumed to cover that employee.
Web self-service grants are also capped to the actor's own employee.

`AccessPermissionEvaluator.Allows(actor, grants, permission, AccessResource)` checks an individual
server-resolved resource. The matching permission and matching scope must occur in the same
grant; do not flatten permissions and independently union all scope IDs. Resolve membership and
resource IDs in the protected feature's query, enforce scope before returning or paging data,
and never construct `AccessResource` solely from client-supplied membership IDs. This helper
does not implement employee/team/site membership resolution or automatically scope EF queries.

For an unscoped collection such as the existing department reference list, use
`AccessPermissionEvaluator.HasAllScope(actor, grants, "hr.organization.read")`. This verifies
an All grant for that exact permission and enforces the catalog, client and MFA restrictions.
An unrelated role's All scope does not satisfy it. Role names never bypass these checks.

## Supported Catalog

Role labels never grant access by themselves. Current permission and matching server-resolved
resource scope govern each consumer, including reads, writes, evidence/downloads and receipt replay.

| Consumer | Boundary |
| --- | --- |
| Employee | Own permitted self-service fields and records; the authenticated employee ID comes from the session. Mobile grants are capped to Self. |
| Supervisor | Only explicitly assigned team/site/employee resources supported by the consuming API. Supervisor status does not grant unrelated sites, salary, bank, medical or HR administration. |
| HR | Explicit HR permission plus that operation's required scope. Sensitive identity, compensation, bank/medical and document evidence remain separate permissions. |
| Accounts | Exact authorized financial handoff/input sources; an Accounts label grants neither HR secrets nor raw punch/policy mutation. |
| Fixed Admin/SuperAdmin | Current stored fixed-role web MFA and access-management grants. Protected administrator changes require SuperAdmin; the identity alone grants no arbitrary sensitive business access. |
| Auditor | Explicit scoped read/export grants supported by the consumer; no mutation/approval authority follows from an auditor label. |

The narrow scope evaluator is a reusable authorization primitive. A service requiring All scope
does not become a scoped supervisor/auditor service merely because the assignment has Team/Site scope.
Missing consumer APIs and unconfigured business permissions remain unavailable.

| Permission | Classification |
| --- | --- |
| `auth.self.read` | Own account; mobile and web |
| `auth.self.sessions.manage` | Own sessions/devices; mobile and web |
| `hr.organization.read` | Web management; genuine MFA |
| `access.roles.read` | Sensitive web management; genuine MFA |
| `access.roles.create` | Sensitive web management; genuine MFA |
| `access.roles.permissions.manage` | Sensitive web management; genuine MFA |
| `access.accounts.read` | Sensitive web management; genuine MFA |
| `access.accounts.assign` | Sensitive web management; genuine MFA |
| `access.accounts.mobile.manage` | Sensitive web management; genuine MFA |
| `access.accounts.web.manage` | Sensitive web management; genuine MFA |
| `attendance.shifts.read/manage` | Web scheduling management; genuine MFA; shift APIs require All |
| `attendance.rosters.read/manage` | Web roster management; genuine MFA; server-resolved dated employee scopes |
| `attendance.rosters.self` | Own employee's My Shift only; mobile and web |

Permission codes are case-sensitive, fixed in application code, and cannot be created by a
request. Role-permission writes create a missing catalog database row only from that fixed
definition. Stored sensitivity can strengthen the catalog requirement, never weaken it.

The slash-separated scheduling rows above abbreviate four distinct catalog codes.
Scopes belong to a role assignment and apply to all its permissions: use separate roles
for Shift-All, Roster-scoped and MyShift-Self access when their scopes differ. Adding My Shift
to a previously assigned employee role is not supported; use a separately reviewed role and
explicit assignment without changing an active account's historical initial-access pair.
Geofences reuse `hr.sites.read/manage` and `auth.self.read`, which already have a bootstrap
path. The same confirmed fixed-administrator web configuration rule applies; resource-specific
site administration and self-only guards still apply at use time.

## HTTP Contracts

All routes require authentication and return non-cacheable responses. Request validation
attributes live on positional-record constructor parameters, as required by MVC; direct service
validation reads the same parameter attributes. Successful operations
return `200` with the DTO described below. Errors use ProblemDetails with a stable `code`.
IDs are GUIDs; dates are ISO-8601 offsets; versions are base64 SQL Server eight-byte rowversions.
Enums use the API's existing numeric serialization. No DTO exposes passwords, phone numbers,
MFA secrets, devices' secrets, or employee personal/financial records.

| Method and path | Additional authority | Response |
| --- | --- | --- |
| `GET /api/v1/me/permissions` | Valid current account | Array of session-filtered `PermissionGrant` |
| `GET /api/v1/access/permissions` | `access.roles.read` | Fixed catalog definitions |
| `GET /api/v1/access/roles?page=1&pageSize=25` | `access.roles.read` | Paged roles with permission codes and version |
| `POST /api/v1/access/roles` | `access.roles.create` | Newly created empty role and version |
| `POST /api/v1/access/roles/{roleId}/permissions` | `access.roles.permissions.manage` plus delegation | Role with updated permission codes/version |
| `GET /api/v1/access/accounts/{accountId}` | `access.accounts.read` | Account access metadata/version |
| `GET /api/v1/access/accounts/{accountId}/assignments?page=1&pageSize=25` | `access.accounts.read` | Paged assignment history and scopes |
| `POST /api/v1/access/accounts/{accountId}/assignments` | `access.accounts.assign` plus delegation | Assignment and updated account version |
| `POST /api/v1/access/accounts/{accountId}/assignments/{assignmentId}/revoke` | `access.accounts.assign` and `access.roles.permissions.manage` | Account access metadata/version |
| `PUT /api/v1/access/accounts/{accountId}/mobile-access` | `access.accounts.mobile.manage`; enabling also requires delegation | Account access metadata/version |
| `PUT /api/v1/access/accounts/{accountId}/web-access` | `access.accounts.web.manage`; enabling also requires delegation | Account access metadata/version |
| `POST /api/v1/access/accounts/{accountId}/web-credentials` | Current fixed Admin/SuperAdmin, `access.accounts.web.manage`, `access.roles.permissions.manage` and target delegation | Account access metadata/version only |
| `POST /api/v1/access/accounts/{accountId}/web-credentials/reset-initial-password` | Same fixed administrator rules, private identity review, no previous login | Account access metadata/version only |
| `GET /api/v1/access/accounts/{accountId}/device-recovery?page=1&pageSize=25` | Fixed administrator, mobile management, role-permission management and delegation | Account version and bounded device history/versions |
| `POST /api/v1/access/accounts/{accountId}/device-recovery/{deviceId}` | Same reviewed-device authority; another eligible staff account only | New unverified device ID/version; fresh OTP required |

Every `/access` endpoint requires a current web session with genuine MFA and **All scope**
for its endpoint permission. Scoped administration/list filtering is deliberately not exposed.
The per-resource evaluator remains available for future protected feature queries.
List responses have `items`, `totalCount`, `page`, `pageSize`; page size is 1-100.
Role lists include unrevoked role permissions, including scheduled future grants; this is role
configuration metadata, not proof of the current account's effective authorization.

Admin and SuperAdmin have persisted `SystemRoleKind` identities (Admin=1, SuperAdmin=2),
with a filtered unique index. Custom role creation rejects reserved code/name variants;
system roles cannot be deactivated or have persisted grants edited through these APIs.
Assigning/revoking a system role or administering a system-role account additionally requires
a current SuperAdmin with the normal permission/delegation checks. System-role assignment
disables mobile and invalidates existing sessions. It does not create a password or verify a phone.
Role DTOs expose `systemRole` and `isSystemRole`; clients should use these, not role-name guesses.
No system role/account is automatically seeded or inferred from an old custom role's name.

### Retry contract (13 October 2026)

The three POST writes for role creation, permission grants and assignments now require
`Idempotency-Key: <8-100 ASCII letters/digits/underscore/dot/colon/hyphen>` alongside the usual
CSRF protection. Keys belong to the current actor and action. Keep the exact body, target ID,
versions, dates and key for an uncertain response; a newly generated date/version/reason is a
new payload and cannot reuse the key. Identical authorized retries within 24 hours return the
original DTO and original rowversions. Read current state before the next distinct action.
A receipt is historical acknowledgement; replay does not restore or extend an assignment.

Current persisted web MFA, current fixed administrator and same-assignment All-scoped access
rights are rechecked inside the serializable authentication transaction before a receipt is
read. Assignment replay additionally checks current target account and effective employment,
active role, supported permissions, existing scope targets and SuperAdmin authority for a
protected target. Revoked actor access, effective suspension/termination or now-protected
account can deny replay. Existing history remains available through separately authorized reads.

Changed payload/target or differently cased key conflicts with `409 idempotency_key_reused`;
expired/incomplete/unreadable receipt metadata conflicts with `409 idempotency_result_unavailable`.
Missing/malformed keys return 400; direct service malformed keys use `invalid_idempotency_key`.
Role, permission/assignment, account security version, protected audit and receipt commit in one
transaction using the existing `IdempotencyReceipt` table/index. Response snapshots are encrypted
private objects, referenced by the receipt; receipt storage failure rolls back the transaction.
Private result IO still requires configured keys/storage. No new migration or default receipt
cleanup policy is supplied. Revoke/channel/credential writes retain their existing contracts.

### Request Bodies

Create role: `{ "code": "EMPLOYEE_SELF", "name": "Employee self service" }`.
Code must match `[A-Za-z0-9][A-Za-z0-9_.-]{0,99}` and is stored uppercase; name is at most
150 characters. A duplicate code returns `409`. Creation has no prior rowversion to compare.

Grant role permissions with an explicit reason:

```json
{ "permissions": ["auth.self.read"], "expectedRoleVersion": "<version>", "reason": "Employee needs self-service access for assigned duties" }
```

`reason` is required for permission grants and role assignments: 1–1000 characters,
non-whitespace and no control characters. It is normalized by trimming and retained only
in protected change evidence, not ordinary audit/log text or public response DTOs.
One to ten unique catalog codes are accepted. This operation only adds new permissions.
A role that has **ever** been assigned, even through an expired or revoked assignment, is
immutable. Use a new reviewed role and explicit assignment/revocation. Duplicate unrevoked
permissions and assigned-role mutation return `409`; unknown codes return `400`.

Assign role:

```json
{
  "roleId": "<role-guid>",
  "scopes": [{ "kind": 1, "targetId": null }],
  "effectiveFrom": "2026-10-06T12:00:00+00:00",
  "effectiveTo": null,
  "expectedAccountVersion": "<account-version>",
  "expectedRoleVersion": "<role-version>",
  "configureAsInitialAccess": false,
  "reason": "Employee needs these module permissions for assigned duties"
}
```

Scope kinds are Self=1, Employee=2, Department=3, Team=4, Site=5, Project=6, All=7.
Self and All reject a client target. All cannot be combined with other scopes. Other kinds
require an existing target GUID. One to fifty unique scopes are supported. The permission
response resolves Self to the account's employee ID, while assignment DTOs use `targetId: null`
for Self to match the write contract. `effectiveTo`, when present, must follow both the start
and the current time. The role must be active and have unrevoked permissions.
An assignment predating account creation returns `400 assignment_precedes_account`.
Initial-access changes before the account's recorded state time return
`409 account_state_not_yet_effective`.

A PendingAccess account automatically receives its initial-access pair only if the new
assignment is effective now and contains exactly one explicit Self scope. Otherwise assignment
is rejected. This uses `ConfigureInitialAccess` and changes the account to ReadyForInvitation;
actual OTP eligibility still requires the separate approved phone/channel provisioning and
all mobile authentication checks. Assigning a role does not provision or verify a phone.

For ReadyForInvitation, `configureAsInitialAccess: true` explicitly replaces the pair only
with a current assignment containing exactly one Self scope and no outstanding invitation.
Without that flag, additional assignments preserve the existing pair. Invited/active accounts
cannot switch their initial pair with this flag; invitation recovery requires a separate
guarded workflow. Additional assignments to active accounts preserve their initial pair.

Revoke assignment: `{ "reason": "<1-1000 characters>", "expectedAccountVersion": "<version>",
"expectedAssignmentVersion": "<version>" }`. The assignment must belong to the route's account.
Revoking an initial assignment intentionally makes authentication that requires it fail closed;
it does not silently select a substitute.

Set either channel: `{ "enabled": false, "expectedAccountVersion": "<version>" }`.
The two commands are separate. Enabling mobile validates the employee's approved mobile
request/submission through the domain method. Enabling web requires an already active account
with provisioned web credentials. Neither command creates credentials or bypasses onboarding.
An account lacking web credentials returns `409 web_credential_provisioning_required`.
Other expected domain channel eligibility guards return `409 channel_access_ineligible`.

Initial web credentials: `{ "email": "<staff-email>", "password": "<admin-selected-password>",
"expectedAccountVersion": "<version>" }`. This is only first setup on an approved account with
assigned access, not password reset or approval bypass. Mobile-only active accounts retain
mobile entitlement. The registered SMS number comes from the approved submission, not this
body. An existing password is never overwritten. Explicit password-policy configuration is
required; see Shared Authentication for guards, errors and handoff order. Browser writes use CSRF.

The separately approved initial-password reset and revoked-device reenrollment commands are
documented in Shared Authentication. They require a private identity-review reference, current
versions and a different administrator. Neither grants access by itself or restores old sessions.
The current-permission service accepts an `AuthRequestContext` so its sensitive read records the
actor's real role(s), employee scope, initiating session, IP and correlation ID.

### Delegation and Concurrency

Actors cannot assign/revoke their own roles or change their own channel entitlements.
Access administration requires the endpoint's All-scoped permission on a currently effective,
active fixed Admin/SuperAdmin assignment. Role permission configuration, role assignment and
channel enablement additionally require All-scoped `access.roles.permissions.manage` on that
same authorizing assignment. Personal business permissions and their scopes are not prerequisites
for configuring an employee's role. A custom role cannot acquire administration authority by
copying permission codes or using an administrator's name. Unknown historical permission codes
still reject assignment/channel enablement; role permissions and current/future account rights
must remain in the catalog. All target scopes are explicitly validated.

Assigning management/sensitive rights calls `RequireMfa()` on the target account when needed.
Assignment/revocation/channel changes invalidate the account security version. Removing a
privileged role never automatically removes the MFA requirement. Actual first-admin creation
uses the separate local owner-controlled command; role names/catalog rows do not establish an administrator.

The repository rejects existing transactions and unsaved changes, clears previously tracked
middleware reads, then opens a serializable SQL transaction. It takes the same transaction-owned
exclusive `cosmix:authentication` application lock as OTP/session writes. It revalidates the
stored web session, account/security version, genuine consumed MFA proof and active credential,
employee approval, joining date in the configured business timezone, and effective employment
history. Ambiguous latest status records with identical effective timestamps deny access.
No external HTTP/SMS operation runs inside this transaction.

Role changes and assignments force a rowversion-guarded role update, so role child changes
participate in concurrency. Account and assignment writes compare their expected rowversions.
Each use case stages its audit metadata and saves once before committing. Sensitive management
reads also save audit events. The audit includes the authorizing role code, actor/session,
target, time, correlation ID, IP when available, and employee scope where relevant. Immutable
assignment, scope and permission rows preserve the concrete change evidence.
Initial-access selection has its own audit event; channel event types record enabled/disabled.

Permission grants and role assignments additionally stage a purpose-bound encrypted snapshot
before business/account mutations and link it from the committed audit. The snapshot records
the normalized reason, current actor/fixed assignment, expected prior versions/state and
intended role/permission/recipient/scope/interval change. No private reference is returned in
the client DTO. Missing/unavailable private storage fails closed before staging those mutations.
An orphaned encrypted object may remain after a later SQL failure; storage and SQL are not one
atomic store. Protected storage is resolved lazily only when authorized evidence/result writes or receipt reads
are needed. Current-permission/catalog reads continue without requiring storage configuration;
creation/grant/assignment writes fail closed if their protected evidence or result cannot be stored.

The web UI can preview catalog, roles, assignments and current account/role versions through
existing GETs, then submit an explicit reasoned write. There is no separate local command or
server-issued preview digest. The receipt contract above preserves the exact response after a lost
reply, while expected versions still protect every new distinct write.

Stale versions, uniqueness/foreign-key races and SQL deadlocks return `409`. The shared lock's
10-second timeout returns `503 access_temporarily_busy`. Reread current state before deciding
to submit a distinct action; use the exact saved body/key for an uncertain creation/grant/assignment retry.
Invalid input returns `400`; authentication failure returns `401`; insufficient authority or
ineligible administrator state returns `403`; authorized missing-resource lookups return `404`.

## Verification and Remaining Work

The original access-slice verification used an isolated Release build and temporary C# checks
outside the repository, without creating a test project, database, seed user or production provider.
The checks cover
the real assignment/permission/scope query, SQL Server query translation, allow/deny scope and
mobile cases, stale versions, delegation, immutable assigned roles, mandatory target MFA,
initial-access transitions, channel controls, effective employment history, timezone rollover,
and transaction rejection/clearing of previously tracked state. Actual MVC/HTTP checks with
isolated synthetic authentication cover every request DTO, model-binding errors, mobile
administration denial, and the real self-permissions route. A connection interceptor stops
the transaction-boundary checks before any SQL connection opens.

Offline checks do not establish live SQL lock contention, rollback/concurrency behavior,
multi-replica operation, migrations, HTTP/JWT web integration, or MFA provider behavior. Those
require separately authorized integration verification. The shared exclusive lock prioritizes
correctness and requires production throughput review. Later integrated SQL/HTTP checks and
credential/recovery/owner-setup implementations are recorded in Shared-Authentication-Verification.md.

The approved 9 October access run now establishes real SQL-backed web login/current-session
checks and access administration on synthetic fixtures. It verified missing-CSRF denial,
required reason/unknown-code rejection, stale-version conflicts, immutable fixed/assigned
roles, protected-account authority, session invalidation after assignment and exact All/Site/Self
permission projection. Its local OTP viewer supplies genuinely issued browser-bound challenges
through the test SMS adapter; real carrier delivery is still unverified. SQL rollback/concurrency,
restart replay and revocation acceptance are separate checks and were not inferred from these
65 assertions. Private evidence retains safe results only; live clients are never serialized.

Public signup, automatic seeds, invitation-recovery endpoints, mutable assigned-role editing and MFA
downgrade are not exposed. Protected feature owners must implement scoped resource queries and their own
sensitive-data audit handling before exposing those features.
