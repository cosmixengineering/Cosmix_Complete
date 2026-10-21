# HR permission P1 continuation - 20 October 2026

## Authorized boundary

The user approved the exact P1 access scope in
[HR live fixture scope](HR-Live-Fixture-Scope-20261020.md), plus the bounded
vehicle/outstation fixed-administrator and delegated-permission compatibility
fix. No migration, business fixture, real face/SMS call or legal activation.
The original fixed roles remain immutable. Business fixture proposals are not
executed acceptance.

## Product change

`AttendanceOperation` has an explicit opt-in `requireAdministrator` mode.
Current fixed ADMIN/SUPERADMIN membership is checked independently of the
permission-owning assignment. The verified fixed role supplies audit/decision
identity. Both authority and permission remain necessary before new work,
after protected IO and during receipt replay. Existing same-assignment
`requireSystemRole` behavior remains the default for its existing callers.

Only vehicle policy administration and deployment/home-break/claim decisions
opt in. Scope checks, MFA, independent review, current source/rowversion,
effective-dated policy rules and immutable evidence remain unchanged.
Benefit review retains its separately implemented fixed-Admin-only design.

## Executed offline evidence

Release build12: zero warnings and errors.
Output: `D:/CosmixVerification/hr-backend-20261020/hr-p1-build12`.

The new genuine-Admin/delegated-vehicle-permission test first failed against
build11 with `access_denied`; after the implementation it passes.

| Suite | Result |
| --- | --- |
| Vehicle policy publication, authorization and replay | 74/74 |
| Deployment dated-policy boundaries | 52/52 |
| Attendance operation authority timing | 54/54 |
| Benefit workflow and authority | 1387/1387 |
| Recovery work claims | 132/132 |
| Recovery replay | 51/51 |
| Historical vehicle dated attribution | 173/173 |
| Changed-amount fuel reconciliation | 42/42 |
| Remaining vehicle historical boundaries | 97/97 |
| Outstation continuity/allowance boundaries | 71/71 |

These use real application services with synthetic repository/storage ports;
they are not authenticated SQL acceptance.

An additional adversarial authority verifier then found eight failing timing
scenarios on build12 (324/340 checks): expiry or session loss during the final
authorization reads could still permit commit or replay. Build12 is therefore
**not** the final accepted P1 binary. The implementation now rechecks the session,
fixed-assignment interval and retained grant effective dates after those awaited
reads. Build13 narrowed the remaining failures to final session timing. The
final implementation validates the retained persisted web session against the
fresh clock after awaited authorization reads, including account/session IDs,
client, device absence, security version, expiry and revocation.

Final frozen Release **build15** completed with zero warnings/errors:
`D:/CosmixVerification/hr-backend-20261020/hr-p1-build15`.
The dedicated delegated-administrator verifier passed **471/471 checks across
106 scenarios on this exact build**, including session/grant/fixed-role expiry,
receipt replay, current authority, MFA, scope, spoofing and legacy compatibility.
Its log is
`D:/CosmixVerification/part-c-parent-20261013/managed-6af277409fe84d9eb1b5f477dd5d1a52/VerifyDelegatedAdministratorOperation.log`.

The P1 API composition verifier passed **266/266** on build15. It checks exact
configuration/target/schema restrictions, command and login budgets, business
route denial, and authentication/CSRF/authorization ordering. Its log is
`D:/CosmixVerification/part-c-parent-20261013/managed-8a640918845347aab8368a1ab33c1a0a/VerifyHrPermissionP1Composition.log`.
This does not itself execute a host or write private files.

Vehicle policy 74/74, deployment policy 52/52, outstation 71/71, old operation
authority timing 54/54, benefits 1387/1387, recovery claims 132/132 and recovery
replay 51/51 were also rerun successfully on build14 after the final authority
fix. Logs:
`D:/CosmixVerification/part-c-parent-20261013/managed-04dcbd6ed31844e4befaf3f598ad1ac9`.

Evidence directories under `D:/CosmixVerification/part-c-parent-20261013/`:

- `managed-2d74ab28c0bb4a66b8320223d24065e9`: expected old-build regression failure.
- `managed-5a0a76874f2549b689afa2bc3286ea60`: first four passing suites.
- `managed-15b3505fda0645f7b62b69feba94d0cf`: recovery/dated vehicle passed;
  reconciliation then stopped because the required fixture assembly variables
  were not supplied. This was a harness precondition failure.
- `managed-f288d8c8f1664eb99d4899167fb8d512`: reconciliation and vehicle
  boundaries passed after process-only fixture path/hash binding; the old
  outstation test adapter lacked the new authority repository methods.
- `managed-4d7c7688188a4476939215a5337649ad`: outstation passed after updating
  that synthetic adapter. No product authority bypass was added to make it pass.

## Read-only SQL

`Inspect-HrLivePrerequisites20261020.ps1` passed again:
`Software-PC1\LOCALDB#9F3001F7`, PID 25768, `Cosmix_Testing` ONLINE,
owner `COSMIXRND\Huma.Khan`, expected family/files and all 25 migration IDs.
The five P1 permissions were still absent, the three existing synthetic
accounts present, and localhost:7481 available. No SQL writes from this check.

The P1 inspector initially rejected identical account GUIDs because SQL JSON
used uppercase formatting. Typed GUID comparisons and canonical formatting
fixed that guard without accepting different identities; 104 offline PowerShell
checks passed at that checkpoint.

Subsequent full inspections stopped on SQL post-login timeouts before writes.
A separate read-only diagnostic connected to the exact approved pipe and
confirmed the same server, database, ONLINE state, family and PID 25768. It also
confirmed zero P1 roles and zero thread-pool waiters. The cause of the transient
timeouts is not established. No database restart or configuration change was
performed. P1-local bounded connection reuse now avoids a new physical login
for every snapshot SELECT: maximum one pooled connection, 30-second connection
timeout, 20-second command timeout and no connection retry or fallback. Frozen
R1/V1 readers and application settings were not changed. Existing verified phone
bindings and server-provided OTP cooldowns are checked without weakening MFA.
The final PowerShell offline guard suite passed **167/167**.

## Live execution

The complete read-only inspection passed, and Execute independently matched:

`1A12C3CC000A2E14F61AAF341DB11995CE1E330500A797FFDE7C937FF47DE367`.

The approved run then **failed during host startup, before its first HTTP
request**. The dependency injection container could not construct
`HrPermissionP1Boundary`, whose constructor was internal. The older composition
tests used reflection and did not exercise this container resolution. This is
a test-host defect, not a successful permission or business acceptance result.

Actual evidence in the private P1 results directory:

- `failure.json`: zero HTTP requests, zero logins, no acceptance checks executed.
- `host-final.json`: owned process **34132 exited**; no database stop or cleanup.
- `post-host-preservation.json`: **157 protected SQL snapshots and 77 prior
  files unchanged**; partial-write bounds passed.
- Before/after counts identical: accounts 3; migrations 25; sessions 129;
  refresh tokens 190; challenges 135; deliveries 135; rate buckets 89;
  audit events 2601.
- No new roles, permission entries, role-permission links, assignments, scopes
  or idempotency receipts. No HR business, migration or external provider work.

Fresh P1 keys/settings/startup logs were created only inside the approved private
P1 directory and are retained. Do not reset, overwrite or adopt that directory
as a fresh run.

The user subsequently approved **one same-scope fresh retry** under
`C:\Users\Huma.Khan\AppData\Local\Cosmix\HrAcceptance\Huma20261020P1Retry01`.
The target, six access commands, account identities, 24-hour assignment duration,
100-request/five-login limits and exclusions remain unchanged. The original P1
directory is now part of the protected prior-file snapshot, not a reset target.
169 offline guard checks passed, including old-root rejection and approved
retry-root acceptance.

The constructor is now public on the still-internal boundary class so the
registered singleton can be resolved. The actual-container regression failed
on build15 (**272/273**) and passed on build16 and the retry-root **build17
(273/273)**. It registers configuration and resolves the boundary through the
container, and verifies OTP repository/factory activation without SQL or OTP
dispatch. Build17 completed with zero warnings/errors.

Final composition log:
`D:/CosmixVerification/part-c-parent-20261013/managed-f975672ae88743ae9269b6bc9f3444bb/VerifyHrPermissionP1Composition.log`.
Expected-red log:
`D:/CosmixVerification/part-c-parent-20261013/managed-a5d1f844fb064fc98248bfc1610c5d80/VerifyHrPermissionP1Composition.log`.

The retry read-only inspector passed with unchanged approved SQL identity,
25 migrations, absent retry directory and available localhost:7481:
`44C6513F86133A5B987808C734AAC221F548414FB1CB629FDFD5B1C53BD6DB30`.
## Retry execution and post-run reconciliation

Retry01 executed **65 HTTP requests, five actual local-MFA logins and 52 passing
HTTP checks**. This includes six successful access commands and six immediate
same-key replays; Admin/HR old-session invalidation; exact fresh permission and
scope checks; three missing-CSRF logout denials, successful logouts and retained
cookie rejections.

The runner then exited nonzero at its final audit assertion. It incorrectly
required every new audit correlation to start with its client-supplied `HR-P1-`
header. The API deliberately uses `HttpContext.TraceIdentifier`; OTP dispatch
uses its delivery ID. No production correlation behavior is being weakened to
satisfy this incorrect test assumption. The original `failure.json` is retained.

Independent SELECT-only checks confirmed these exact additions:

| Record | Added |
| --- | ---: |
| Custom roles | 2 |
| Permission catalog entries | 5 |
| Role-permission links | 6 |
| Role assignments / scope rows | 2 / 2 |
| Completed idempotency receipts | 6 |
| Sessions / refresh tokens / challenges / deliveries | 5 each |
| Rate buckets | 16 |
| Audit events | 51 |

The assignment window is exactly
`2026-10-20T11:09:40.0589427Z` through `2026-10-21T11:09:40.0589427Z`
(21 October 16:09 Pakistan expiry). Fixed roles/passwords remain unchanged.
The owned retry host **18788 exited**. Post-host checks recorded all **157
protected SQL snapshots** and prior private files unchanged. No migration,
business fixture, real SMS/face, legal activation or payment was executed.

`Reconcile-HrPermissionP1.ps1` is a separate SELECT-only reconciliation, not a
repeat provisioning run. It revalidates target and counts, reconstructs the
retained ID sets only when their recorded populations/hashes match, reproduces
the original correlation assertion failure, and checks every audit actor,
target, event cardinality and authentication proof chain.

**Executed reconciliation: PASS, exit code 0.** The strict audit helper's
39 offline positive/negative checks passed first. Live SELECT-only verification
then matched **51 audit events in 32 HTTP correlation groups plus five
delivery-bound correlations**, with exact role/permission/assignment/receipt
and session counts. All **157 original protected SQL hashes were freshly
recomputed and matched**, including retained access history, business tables,
account population and unrelated existing rate state. Prior private-file hashes
also matched. No HTTP request or SQL write was repeated during reconciliation.
The original failed runner result and its evidence remain unchanged.

The narrow conclusion is **P1 access provisioning and its bounded authenticated
checks are verified, with the original runner's audit-contract defect reconciled
separately**. This is not a clean original runner exit, not concurrent HR business
acceptance, and not benefit/recovery/vehicle/outstation workflow acceptance.

## Current remaining work

Business source fixtures remain unapproved/unexecuted by this access-only scope.
The existing proposals enumerate the missing initial employment and dated policy
sources for recovery/outstation, and the protected historical sources needed for
annual KPI benefits and legacy vehicle corrections. No fabricated face/KPI
verification or guessed business policy was added. See
[business fixture boundaries](HR-Live-Fixture-Scope-20261020.md#business-fixture-batches).
Do not silently extend the 24-hour assignments if preparation outlasts them.

## Changed files owned by this continuation

- Application compatibility: `Attendance/Common/AttendanceOperation.cs`,
  `HR/VehicleFuel/VehiclePolicyPublication.cs`,
  `HR/Allowances/DeploymentService.cs`, `HR/Allowances/DeploymentClaims.cs`.
- API test containment: `Program.cs`, `Development/LocalTesting.cs`,
  `Development/HrPermissionP1Guard.cs`, `HrPermissionP1Database.cs`,
  `HrPermissionP1Host.cs`, `HrPermissionP1Boundary.cs`.
- Standalone checks: `VerifyVehiclePolicyPublication.cs`,
  `VerifyDeploymentPolicyBoundaries.cs`, `VerifyOutstationAllowances.cs`,
  `VerifyDelegatedAdministratorOperation.cs`,
  `VerifyHrPermissionP1Composition.cs`, `Verify-HrPermissionP1.ps1`,
  `Verify-HrPermissionP1Audit.ps1`.
- Local acceptance support: `Inspect-HrLivePrerequisites20261020.ps1`,
  `HrPermissionP1Profile.ps1`, `Invoke-HrPermissionP1.ps1`,
  `HrPermissionP1AuditVerifier.ps1`, `Reconcile-HrPermissionP1.ps1`,
  this verification record and the linked fixture-scope records.

Application paths above are relative to `src/Cosmix.Application`, API paths to
`src/Cosmix.Api`, and standalone/support paths to `docs/hr-srs-remediation`.
Other developers' changes remain preserved. No migration, Git commit or remote
synchronization was performed.
