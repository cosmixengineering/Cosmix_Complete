# Remaining HR live acceptance scope - 20 October 2026

## Status and boundaries

**P1 access provisioning and the bounded compatibility fix are user-approved.**
Business fixtures below remain proposals, not executed business acceptance.
P1 provisioning has now executed: 52 HTTP checks passed; a runner audit
correlation assumption failed and was independently reconciled by SELECT-only
verification of all 51 new audit events and 157 protected SQL hashes.
[Exact results and limits](HR-Permission-P1-Verification-20261020.md#retry-execution-and-post-run-reconciliation).
The user requested an exact synthetic fixture/permission scope before further
live benefit/recovery/vehicle/outstation tests. No migration, real face, external
OTP or legal activation is included. Local test MFA must remain enabled.

Read-only inspection actually passed using
`Inspect-HrLivePrerequisites20261020.ps1`:

- `Software-PC1\LOCALDB#9F3001F7 / Cosmix_Testing`, ONLINE, process 25768.
- Owner/operator `COSMIXRND\Huma.Khan`; family
  `D9C34627-18A2-41CF-A804-821E39516303`.
- Existing `C:\Users\Huma.Khan\Cosmix_Testing.mdf` / `_log.ldf`.
- Exactly 25 migrations, matching the complete checked-in migration list.
- Three existing active synthetic web accounts; no credentials displayed.
- No current listener on port 7481.

Actual source counts: Policies 0, PolicyVersions 0, EmployeeAssignments 0,
LeaveRequests 0, LeaveLedgerEntries 0, LeaveRecoveryCommitments 0, VehicleTrips 0,
VehicleMileageEntries 0, VehicleServiceRecords 0, FuelClaims 0,
OutstationDeployments 0, DeploymentHomeBreakRequests 0, AllowanceClaims 0,
AllowanceClaimSubmissions 0. The one vehicle and one custody from V1 remain
untouched and will not be silently reused.

## Exact missing permission catalog entries

1. `attendance.leaves.benefits.review`
2. `attendance.vehicle.policy.manage`
3. `attendance.deployments.read`
4. `attendance.deployments.request`
5. `attendance.deployments.decide`

At the initial preflight none existed in the database catalog or in the three
synthetic accounts' effective grants. The approved P1 run has now created all
five and the exact assignments below, expiring 21 October at 16:09 Pakistan time.
All five are supported in the application catalog.
The normal `POST /api/v1/access/roles/{id}/permissions` path uses
`GetOrAddPermissionAsync` to add supported missing catalog entries. A migration,
direct SQL permission insert or fixed-role edit is **not** required for catalog
provisioning.

Existing leave configure/review/self, policy configure/publish/applicability,
KPI and ordinary vehicle management permissions were read-only verified on the
existing synthetic accounts. Do not grant them again indiscriminately.

## Approved access batch P1

The user approved this exact scope on 20 October and later approved one fresh
retry after a startup-only DI failure. The retry provisioned these exact objects;
its audit-verifier failure was reconciled read-only, without repeating writes.
The original P1 and Retry01 evidence directories are retained.

Use the existing synthetic SuperAdmin to create/configure/assign exactly:

| New object | Exact proposed scope |
| --- | --- |
| Custom role `TEST-HR-P1-20261020-ADMIN` | Four codes: benefit review, vehicle policy manage, deployment read, deployment decide |
| Custom role `TEST-HR-P1-20261020-REQUESTER` | Two codes: deployment read and deployment request |
| Admin role assignment | Existing synthetic `admin@example.invalid`; All scope; start at the captured execution time, expire exactly 24 hours later |
| Requester role assignment | Existing synthetic `hr@example.invalid`; Employee scope (kind 2) targeting only its bound employee `35d19922-70a8-4c80-8578-e1029360e9fc`; same 24-hour bound |

Total intended additions: **five permission catalog rows, two custom roles,
six role-permission links, two role assignments and two scope rows**. The grants
must be configured before either custom role is assigned, because assigned roles
are immutable. Exact source account/role rowversions are obtained through normal
APIs. Fixed ADMIN/SUPERADMIN/HR role definitions and all their existing grants stay
unchanged. No access-management permission is delegated.

Two expected existing-row changes must be explicitly authorized: assigning a role
increments the synthetic Admin and HR accounts' security version/rowversion and
invalidates their older sessions. It is not safe to promise those two account rows
remain byte-for-byte identical. Passwords, employee identities, channel settings
and existing assignments must not change. SuperAdmin does not self-assign anything.

Use at most 100 HTTP requests and at most five new local-MFA sessions across the
three synthetic accounts. Allow related authentication/rate/audit/private-evidence
and idempotency records only. Maximum six successful access-management commands:
create each role, grant each role's permissions, assign each role. Exact-key
retries must create no additional role/assignment. Scope is not approved for a
directory-wide administrative test or cleanup.

Original private root (startup failure; no SQL changes):
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020P1\`.
Separately approved executed retry:
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020P1Retry01\`.
Owned HTTPS host only `https://localhost:7481`, same reviewed local named-pipe
exception. Root absence, target identity, credential bindings, request budget,
new marker absence, retained SQL/file snapshots and all allowed writes must be
checked before first mutation. Existing C01-C04/V1/R1 evidence is preserved.
No reset, overwrite, fallback database or automatic rerun after partial execution.

## Authorization blocker and approved fix

The latest benefit implementation already supports genuine fixed Admin membership
plus its separate explicit custom benefit-review grant. Before this continuation,
vehicle policy operations and outstation decisions did not support this arrangement:

- `VehiclePolicyPublication` sets `requireSystemRole: true`.
- Deployment, home-break and claim decisions do the same and retain an
  ADMIN/SUPERADMIN reviewer role in their evidence.
- `AttendanceOperation.MatchingGrantsAsync` currently requires the new permission
  and fixed system-role authority on the **same assignment**.
- Normal access APIs intentionally refuse editing a fixed role.

Therefore adding the proposed custom roles alone must still produce a denial for
those vehicle/outstation operations. Do not call this successful full provisioning.

The user approved the bounded source fix:
preserve existing fixed reviewer eligibility, independently validate that current
fixed authority plus the explicitly delegated permission/scope, and retain
canonical reviewer evidence. Recheck both authority and grant after awaited IO,
on replay and before commit. Reject a custom role merely named Admin, revoked/
expired fixed authority, missing MFA, scope mismatch and self-review. Do not
change the default behavior of unrelated `AttendanceOperation` callers or
weaken fixed-role immutability. The implementation opts only vehicle policy and
deployment decisions into `AttendanceOperation`'s independent administrator check;
the existing same-assignment default remains unchanged.

Frozen Release build15 completed with zero warnings/errors. The initial delegated
policy regression failed with `access_denied` against old build11. Further
adversarial tests exposed timing defects in build12/13; these were fixed rather
than excluding the scenarios. The final delegated-administrator verifier passed
471/471 checks on build15, and the P1 host composition verifier passed 266/266.
Affected vehicle, deployment, outstation, benefit and recovery service regressions
also passed after the timing fix. These are not SQL acceptance results.
See [current verification evidence](HR-Permission-P1-Verification-20261020.md)
for exact builds, counts and live execution status.

## Business-fixture batches

Detailed candidate fixture designs are separate:

- [Benefit/recovery fixture scope](Benefit-Recovery-Live-Fixture-Scope-20261020.md).
- [Vehicle/outstation fixture scope](Vehicle-Outstation-Live-Fixture-Scope-20261020.md).

These are not covered by P1. Each needs a final finite source manifest with actual
IDs, exact policy JSON, dated applicability, row counts, protected-reference
purposes and expected outcomes, reviewed before seed/HTTP writes. An illustrative
count or a verifier's in-memory fixture is not an executable SQL manifest.

The two candidate documents include older alternative access-role proposals.
Do not execute those additional grants: this approved P1 table supersedes their
access-provisioning portions. Their business-fixture portions remain unapproved.

Historical baseline data may be explicitly synthetic, but must not be reported
as an HTTP-tested hiring/KPI/correction/legacy-import workflow. Do not fabricate
face verification, use arbitrary database `Verified=true` flags, bypass source
provenance, freeze live authentication time or shorten the required KPI year.

Vehicle policy selection is database-wide by effective date, not per test
employee. A proposed dated synthetic vehicle policy affects every vehicle
calculation in that date interval. Its window and persistence must be specifically
approved; a TEST prefix does not isolate it. Current/future company rules must not
be replaced. The public vehicle publisher also intentionally requires a future
effective date; same-day historical testing cannot be solved by weakening it.

## Test evidence required after approved writes

- Real password/local MFA/CSRF, current scope and independent review.
- Real separate-connection competing HTTP requests, exact-key replay,
  changed payload, stale rowversion and duplicate-source rejection.
- Exact business/audit/receipt/outbox SQL state and encrypted private evidence.
- Scope and source changes fail closed; original approvals/claims stay immutable.
- No automatic pay, recovery deduction, legal activation or real provider call.
- Logout/revocation, host exit and final protection checks.

No live business fixture test was executed by this scope. The separately approved
P1 permissions were provisioned and verified as recorded above. Neither the
earlier R1 report test nor P1 authorizes the additional business source fixtures
or globally dated vehicle policies.
