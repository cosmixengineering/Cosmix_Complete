# Scheduling/geofence access verification — 9 October 2026

The user confirmed that fixed Admin/SuperAdmin users configure module permissions through
roles in the existing web UI flow, then explicitly assign those roles to employees.
Only SuperAdmin may change protected Admin/SuperAdmin accounts/access. This supersedes
8 October's proposed local owner-command design. No separate command, automatic grant,
bootstrap expansion or real account change is part of this correction.

## Current supported-path status

| Feature | Conclusion |
|---|---|
| Role catalog/configuration/assignment | Current fixed Admin/SuperAdmin plus the existing All-scoped access-management permissions can configure supported catalog codes through existing web APIs without owning each business permission. |
| Shifts and site rosters | Scheduling's first-grant authority dead end is resolved and exercised through the approved SQL-backed web flow. Explicit custom-role assignments remain required. Full scheduling/replay/concurrency acceptance is tracked separately. |
| My Shift | Explicit `attendance.rosters.self` assignment is required. Neither `auth.self.read`, login nor mobile enablement grants it automatically. Self-service is capped to the authenticated employee. |
| Geofence administration/history | Existing `hr.sites.read/manage` rights and resource-specific fixed Admin/SuperAdmin requirements remain. Custom role names cannot establish fixed authority. |
| Own site attendance configuration | Existing server-derived employee identity and `auth.self.read` remain; no client employee/site/date selector is accepted. |

The [confirmed provisioning flow](Provisioning-Design.md) documents role creation, catalog
permission addition, explicit assignment scopes/intervals and the fixed-role protection rules.
The backend APIs support the separate web client; no new frontend screen was built here.

## Current executed evidence

| Check | Result | Evidence boundary |
|---|---|---|
| Updated `VerifyDelegation.cs`, before service edit | Failed as intended: current fixed Admin's first `attendance.shifts.read` grant returned `access_denied` | Actual old service's per-business-permission ownership predicate |
| Updated `VerifyDelegation.cs`, after authority edit | 283 passed, exit 0 | Actual application service/evaluator/domain entities; explicit synthetic in-memory collaborators |
| Required grant reason regression, before reason/evidence edit | Failed as intended: missing-reason grant succeeded instead of returning `invalid_access_reason` | Actual prior grant contract accepted an unexplained change |
| `VerifyDelegation.cs`, including reason/evidence changes | 317 passed, exit 0 | Required reason/control/length checks, private evidence content/purpose/linking, and failure before business staging, using in-memory file IO |
| Storage-independent read regression, before lazy storage edit | Failed as intended: unavailable eager storage prevented service construction | Write-only dependency must not block existing reads |
| `VerifyDelegation.cs`, including lazy storage | 322 passed, exit 0 | Catalog/current-permission reads avoid unavailable storage, authorization precedes its creation, and authorized evidence writes fail closed |
| Final `VerifyDelegation.cs`, including required JSON reasons | **326 passed**, exit 0 | Both DTOs reject JSON omitting `reason` and preserve explicitly supplied reasons; the C# default does not weaken JSON presence requirements |
| `RunAuthenticatedAcceptance.ps1 -Phase Access` / `VerifyAuthenticatedAccess.ps1` | **65 passed** against the approved isolated `Cosmix_Testing` host | Real password challenge, browser-bound OTP verification, stored sessions, authenticated CSRF, SQL rowversions, fixed authority, protected targets and scoped synthetic assignments |
| `VerifyEncryptedStorage.cs`, write then fresh-process read | **13 passed** (5 + 8) | Actual encrypted disk round-trip and wrong-purpose/key/version/digest, tamper and missing-key denials on dedicated synthetic objects |
| `VerifySqlRollback.cs` | **9 passed** | Real current Admin/MFA authorization; injected scheduling failures before protected writes and after persisted journal save; independent SQL context finds no business/audit/receipt rows; two encrypted orphan objects retained and privately indexed |
| Main authenticated scheduling run | **62 passed** | Actual HTTP/SQL scheduling with the explicit synthetic grants |
| Independent scheduling SQL/private evidence verification | **83 passed** | Persisted rows/receipts and protected evidence checked independently, including 12 decrypted audit objects |
| Main scheduling replay after owned host restart | **26 additional assertions** across 13 operations; cumulative **88** | Fresh process/login and retained encrypted receipts; this does not imply access-write idempotency |
| Transfer-boundary supplemental grant/run/replay | **9 grant checks + 42 run checks + 16 replay assertions passed**; boundary cumulative **58** | Actual HTTP HR transfer effective at +20 days, overnight conflict rejection, exact-midnight adjacency and holiday pinning; replay used a fresh login on the same API host, not a host restart |
| Run-scoped revocation and denied replay | Original Admin Site-A roster assignment revoked by real SuperAdmin; prior Admin session **401**; **9 replay requests returned 403** after fresh MFA; main cumulative **97** | Current scope is rechecked before old receipts can be returned; other synthetic grants/history remain retained |
| Competing shift metadata writes with distinct idempotency keys | **9 passed** | Exactly one HTTP 200 and one HTTP 409; subsequent GET confirmed the winner and immutable schedules stayed unchanged |
| Isolated SQL constraints | **No DBCC constraint violations**, all foreign keys enabled/trusted, **13 migrations applied** | Actual approved `Cosmix_Testing` target only |
| Final solution Release build | **Passed, zero warnings and errors** | Final application code compiles; behavioral evidence remains the separate checks above |
| `Test-AcceptanceReadiness.ps1` | PowerShell parsing, actual offline JSON run, fixed output contract and synthetic in-memory connection parser passed | Fixed file/configuration metadata only; no SQL, tool/server/provider execution or private writes |

The current harness covers both fixed authorities' first catalog grants, no automatic actor
business rights, custom-role-name denial, current authority rechecks, missing/non-All operation
grants, auxiliary permission on the same authorizing assignment, catalog allowlisting,
immutable fixed/ever-assigned roles, reserved names, explicit scope validation, stale versions,
self-assignment denial, assignment audits, target MFA/security invalidation and ordinary
web/mobile self-permission reads. Admin denial and SuperAdmin allowance are exercised for
protected assignment/revocation/channel changes; SuperAdmin still cannot edit own channels.

The 326 count is assertions, not 326 HTTP or SQL workflows. The in-memory repository does
not simulate transaction rollback, real concurrent writes, SQL locks or authentication proof.
Its current-session/fixed-role results are explicit collaborators. The separately authorized
65-check HTTP run below exercises the actual SQL predicates and persisted-session behavior;
it does not turn the in-memory assertions into SQL checks.

### Approved isolated access acceptance

On 9 October the user explicitly approved fresh `Cosmix_Testing` migration, synthetic fixtures,
isolated private keys/files and actual acceptance. The owned HTTPS host was
`https://localhost:7249`; evidence is under
`%USERPROFILE%\AppData\Local\Cosmix\AccessAcceptance\20261009`. The private
`access-results.json` retains the 65 named checks and safe role/assignment metadata; it contains
no live clients, passwords, OTPs, cookies or CSRF tokens. The helper checks the guarded local
database-mode endpoint before sending synthetic credentials, verifies that a password challenge
alone leaves `/me/permissions` unauthorized, and refreshes antiforgery after real OTP login.
OTP delivery uses the existing local test adapter, not a real SMS carrier.

The run proved that Admin initially lacked scheduling permissions yet could create four
explicit custom roles and configure supported permission codes. Admin could not change the
other protected SuperAdmin account; SuperAdmin explicitly assigned Shift-All and Roster-Site
to protected Admin. Admin then assigned HR MyShift-Self and RosterRead-Site. Previous Admin
and HR sessions were rejected after assignment; fresh MFA sessions returned the exact All,
Site and server-capped Self grants. HR could not administer access. Missing CSRF, omitted or
control-character reasons, unknown codes, stale writes, fixed-role mutation and assigned-role
mutation were rejected as expected. This closes the first-grant/authenticated-access blocker.

The original Admin Site-A roster assignment has now been explicitly revoked by genuine
SuperAdmin after replay; its immutable history and all other synthetic grants are retained.
The prior Admin session returned 401. A fresh MFA login still received 403 on nine old
scoped receipt replays, proving current permission loss is not bypassed by a cached result.
The check count is
assertions, not 65 distinct workflows. The later isolated storage, scheduling SQL rollback,
independent evidence and main restart-replay results are recorded separately in the table above.
Transfer-boundary replay and scoped revocation also passed. These results do not establish
production throughput, every concurrency scenario or production/provider readiness.

The final solution Release build passed with zero warnings/errors. The owned acceptance host
(PID 2876) was stopped after the run; private fixtures, evidence, backup and intentionally
retained grants remain. Broader scheduling/geofence regression results retain their own
verification records and are not inferred from this harness or existing DLLs.

## Historical evidence, not rerun by this access correction

On 8 October the previous access follow-up recorded 235 delegation checks under the former
business-permission ownership rule and 70 web-boundary checks, plus scheduling/geofence
component and anonymous HTTP regressions. The former first-grant-denial expectation is
superseded by the confirmed rule and updated harness above. Historical build/count totals
must not be presented as a fresh run against the current code.

`VerifyWebBoundary.cs` uses real ASP.NET antiforgery with ephemeral in-memory DataProtection,
configured JWT transport/validation callbacks and CurrentAuthentication. It checks token
pairing, cookie/bearer separation and typed server identity; these are not actual signed-token
login, MVC integration or database session-validation results. Anonymous HTTP scripts start
only their own isolated host with authentication providers disabled and unusable synthetic SQL;
their route metadata and anonymous rejections do not establish authenticated success.

## UI review, reason and retry semantics

Existing GET catalog/role-list/account/assignment reads expose the selected codes, scopes,
intervals and expected versions needed for a normal reviewable web form. Explicit writes and
current authority checks prevent automatic provisioning; rowversions reject stale submissions.
The user-confirmed flow is normal web administration: review these reads, then explicitly
submit the selected role/target/scope/interval and required reason with current versions.

- There is no separate local provisioning command or server-issued preview token/digest.
  Separate read and write requests remain independently authorized; expected versions bind
  grant/assignment writes to the reviewed role/account state.
- Permission addition and role assignment now require a nonblank 1–1000 character reason
  without control characters. Protected evidence binds the normalized reason, current fixed
  actor authority, expected prior versions/state and intended concrete permissions, recipient,
  scopes and dates. Existing audit events retain its private reference. Public responses do
  not expose the reference or reason. Role creation/channel DTOs remain unchanged; revocation
  already required a reason.
- Access writes have no idempotency-key or receipt/frozen-result replay contract. Repeating
  a committed grant/assignment with old expected versions conflicts; duplicate role codes or
  permissions conflict. After a lost response, the client must reread current state and reconcile,
  rather than blindly retry with refreshed versions. Those protections are not replay acceptance.

The earlier unapproved owner-command/digest proposal is superseded by this confirmed web flow.
Required reasons/protected evidence and conservative retry protection are implemented for the
bounded grant/assignment flow; frozen receipt replay is not claimed. The additive Reason fields
default empty for C# source compatibility but reject omission at runtime, so web clients must
send them. Protected storage is resolved lazily for evidence writes after authorization;
current-permission/catalog reads continue when write storage is unavailable. Storage failure
is tested before business/account/audit staging. This is not proof of SQL rollback;
after a later failure an unlinked encrypted evidence object can remain for reconciliation.

## Remaining boundaries

The original metadata-only [readiness inventory](Readiness-Checklist.md) did not find the old
LocalTesting directory; sandbox `LOCALAPPDATA` was redirected. It did not establish database
absence. The later approved setup separately verified absence, initialized fresh `Cosmix_Testing`,
applied 13 migrations and retained a verified backup and isolated private files before fixtures.
That explicit approval supersedes the earlier pending-approval status for this exact local run.

The [isolated acceptance plan](Isolated-Acceptance-Plan.md) retains the exact target and scope.
No real employee/account, production provider, Git operation, automatic permission grant or
access DbContext/model/snapshot/package/test-project change is part of this correction.
The 13 migrations are applied only to the fresh isolated `Cosmix_Testing` target for this run;
production and the original development database were not upgraded. Main host-restart replay,
transfer-boundary replay with fresh login on the same host, bounded scheduling SQL rollback,
scoped revocation/denied replay and competing metadata writes have now passed.
Actual mobile behavior, real SMS/scanner delivery,
production policy/owner setup, throughput and any unexecuted concurrency scenarios retain
their separate prerequisites; this is not completion of the whole backend.

## Reproduction

Run solution-referencing file harnesses sequentially with cached .NET 10 dependencies:

```powershell
$env:TEMP = Join-Path $PWD 'artifacts/site-geofences-temp'
$env:TMP = $env:TEMP

dotnet run --file docs/access-provisioning/VerifyDelegation.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false --property:UseSharedCompilation=false -tl:off
.\scripts\Test-AcceptanceReadiness.ps1 -AsJson
```

No database initialization, migration application, fixture setup or provisioning execution
is part of these commands. The repository Release build remains a separate required check.
