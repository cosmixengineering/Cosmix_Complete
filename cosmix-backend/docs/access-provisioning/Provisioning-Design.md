# Module access provisioning — confirmed web administration

Confirmed by the user on **9 October 2026**: fixed **Admin and SuperAdmin** users create
roles in the web UI, explicitly select the supported module permissions for each role,
and assign those roles to selected employees. There is no separate local provisioning
command, automatic grant, or business-permission ownership prerequisite. This supersedes
the earlier owner-command proposal. Implementing this rule does not authorize changing
any existing real account or running a database fixture.

## Supported flow

Use the existing authenticated access routes with a current password/SMS-MFA web session
and browser CSRF protection:

1. `GET /api/v1/access/permissions` returns the supported permission catalog.
2. `POST /api/v1/access/roles` creates an explicitly named empty custom role.
3. `POST /api/v1/access/roles/{roleId}/permissions` adds the selected catalog codes to the
   unassigned role using its current expected rowversion and required reason. Each request accepts 1–10 codes.
   Additional batches are possible before the role has ever been assigned.
4. `POST /api/v1/access/accounts/{accountId}/assignments` assigns the selected role
   with explicit scopes, start/end, current account/role rowversions and required reason.
5. Existing assignment revocation and channel routes retain their distinct purposes.
   Enabling a channel does not provision credentials, approve employment or grant a role.

The backend supplies these APIs to the separate web client; this backend change does not
claim that a new client screen was implemented. See [the HTTP contract](../Authentication-Access.md).

## Authority and safety boundaries

- Every access-administration operation first revalidates the stored web session, consumed
  SMS proof, account security version, credentials and employment inside the existing
  serializable authentication transaction and shared lock.
- The operation's existing All-scoped access-management permission must belong to a current,
  active, genuine fixed Admin/SuperAdmin assignment. The service verifies persisted system
  role identity through `IsCurrentSystemRoleAssignmentAsync`; a custom role named Admin,
  custom management permission, inactive role, expired/scheduled/revoked fixed assignment,
  mobile token or MFA claim alone cannot satisfy this authority.
- Role permission grants, assignments and channel enablement also require
  `access.roles.permissions.manage` on that same authorizing assignment. Administrators
  configure supported business permissions without needing to personally own each one.
  Configuring another role does not give the actor those business permissions or scopes.
- Only codes in `AccessPermissionCatalog` may be configured or activated, including when
  assigning a historical role or enabling a channel with current/future rights. Unknown
  codes fail closed. No free-form module names become executable permissions.
- Fixed roles and roles that have ever been assigned remain immutable. Create a replacement
  custom role and explicitly assign/revoke it. Reserved fixed-role names/codes are rejected.
- Actors cannot assign/revoke their own roles or change their own channel entitlements.
  Only current SuperAdmin authority may change an account with a current/scheduled fixed
  role or grant/revoke a fixed role. System-role lifecycle checks and web-only rules remain.
- Scopes are explicit and validated against existing targets. Self always resolves to the
  recipient employee, All cannot be mixed with narrower scopes, and each resource service
  retains its own server-side scope checks. Granting a permission does not bypass a feature's
  fixed-role, self-only, lifecycle or other business constraints.
- Assignments/revocations/channel changes invalidate the recipient security version. Sensitive
  or management access requires target MFA; removing rights does not silently downgrade MFA.
  Account/role/assignment rowversions and atomic audit persistence remain mandatory.
- Permission grants and role assignments require a nonblank 1–1000 character reason without
  control characters. They stage a protected, purpose-bound snapshot of the actor/fixed authority,
  expected prior state and intended role/permission/employee/scope/date change; the committed audit
  retains its reference. Private storage failure prevents these business mutations. Storage is
  resolved lazily for authorized evidence writes; existing read-only access routes do not need it.

`GET /api/v1/me/permissions` retains its ordinary current-account and self-service behavior.
It is not restricted to fixed administrators. Catalog definitions and UI permission hints
are not authorization decisions; the server rechecks current authority on every operation.

## Scheduling roles and scopes

The fixed bootstrap permission list remains unchanged. The confirmed web authority resolves
the five scheduling permissions' first-grant dead end through explicit custom-role configuration.
There is no bootstrap mutation, startup grant, new identity, local command or schema change
for this bounded access-service correction.

| Role purpose | Explicit permissions | Assignment scope |
|---|---|---|
| Shift configuration | Selected `attendance.shifts.read` / `attendance.shifts.manage` | All; shift reference data is global |
| Site roster administration | Selected `attendance.rosters.read` / `attendance.rosters.manage` | Explicit permitted site/employee population scope, or approved All |
| My Shift | `attendance.rosters.self` | Self for the selected eligible account |

Use separate roles where scopes differ, because an assignment's scopes apply to every
permission in that role. My Shift never grants another employee's data, mobile entitlement,
credentials, site administration or `auth.self.read`. Existing initial-access history remains
unchanged on active accounts. Geofences continue using `hr.sites.read/manage` and `auth.self.read`.

## Concurrency, audit and verification limits

Existing GET catalog/role/account/assignment reads provide the web UI's review data and current
versions. The UI submits the selected target, permissions, scope, dates and reason explicitly.
There is no separate local command or server-issued preview digest. Role creation, permission grants and assignments now require `Idempotency-Key`. The exact
body/target/key is replayable for 24 hours after current fixed web MFA and permission checks;
assignment replay also rechecks target employment, active role, scope targets and protected-account
SuperAdmin authority. The response preserves original IDs/versions and never reapplies access.
A different payload with that key conflicts. New keys still require current expected versions.
See [the complete retry contract](../Authentication-Access.md#retry-contract-13-october-2026).
No automatic retry refreshes the caller's versions or dates.
The audit identifies the genuine fixed assignment's role code, actor/session and concrete target.
Grant/assignment audit references retain the protected reason and change evidence; public DTOs
contain no private-storage references. A failed SQL transaction can leave an unlinked encrypted
object, which must be retained for reconciliation rather than described as a cross-store rollback.

[VerifyDelegation.cs](VerifyDelegation.cs) exercises the real application service/evaluator
and domain access entities with explicit synthetic in-memory collaborators. It covers first
catalog grants, both fixed authorities, custom-role denial, current authority rechecks,
unknown codes, scope validation, immutable roles, protected targets, self denial, expected
versions and security invalidation. These are not SQL, authenticated browser, real MFA,
CSRF, rollback, lock-contention or provider acceptance results.

Actual account changes, isolated SQL writes and new encrypted private fixtures still require
fresh approval under [the acceptance plan](Isolated-Acceptance-Plan.md) and
[readiness checklist](Readiness-Checklist.md). Site-roster and temporary-amendment schema
readiness remain separate blockers. Actual owner creation and production provider/policy
choices are not implied by this confirmed role-administration rule.
