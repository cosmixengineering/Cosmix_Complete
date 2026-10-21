# Shifts and Individual Rosters — historical baseline and current status

**Later site-only continuation:** [Site scheduling verification](group-rosters/Verification.md) supersedes runtime resolver and individual-write route expectations below. On 9 October the user approved all 13 migrations and synthetic SQL/private acceptance only on fresh local `Cosmix_Testing`; no development or production database was modified. Actual authenticated scheduling/geofence run, persisted evidence, restart replay, transfer/calendar boundaries, revoked-site replay and constraint checks passed. The main HTTP count reached 97; the supplemental boundary run/replay reached 58. This earlier record remains evidence for the preserved individual service baseline; retired individual write routes do not resume operation.

## Historical baseline — 8 October 2026

All following unexecuted/next-step statements describe the earlier phase and are
superseded where the current site-only verification records later execution.

Executed on 8 October 2026 in `C:\Users\Affan.Ahmed\Desktop\cosmix-backend`, on local `develop` at `70adaf7eeae4857b739c4b80cae6d8833ad163d2` plus uncommitted feature changes. These results describe the current files, not the old checkpoint's handoff. No remote state was inspected or changed.

## Executed results

Follow-up access review: [Access provisioning verification](access-provisioning/Verification.md)
records the new delegation/web-boundary checks and fresh relevant scheduling/geofence reruns.
On 9 October the user confirmed fixed Admin/SuperAdmin web role administration, with protected administrator targets controlled only by SuperAdmin. That later implementation supersedes the historical first-grant limitation below. The user then separately approved and ran isolated SQL/private-storage acceptance; current counts and remaining deployment/mobile/provider limits are recorded in the linked continuation documents.

| Verification | Actual result | Boundary |
|---|---|---|
| `dotnet build Cosmix.Backend.sln --configuration Release --no-restore` | Passed, zero warnings/errors | Compilation is not behavioral acceptance |
| `shifts-rosters/VerifySchedule.cs` | 41 checks passed | Real date/window helpers and resolver with synthetic repositories |
| `shifts-rosters/VerifyOperations.cs` | 111 checks passed | Real scheduling authorization/idempotency orchestration with fake persistence/private storage |
| `shifts-rosters/VerifyServices.cs` | 140 checks passed | Real shift/individual roster services and operation wrapper with fake repositories |
| `shifts-rosters/VerifyInfrastructure.cs` | 52 checks passed | Real SQL Server EF model, existing snapshot, append-only guards and DI; zero database connections/file operations |
| `shifts-rosters/VerifyHttp.ps1` | 34 checks passed across all 12 scheduling operations | Running Release API, OpenAPI contract and anonymous 401s; authentication disabled |
| `site-geofences/VerifyHttp.ps1` | 14 checks passed across all 6 existing geofence operations | Regression check against the same updated Release API |
| `git diff --check` | Passed | Whitespace check of tracked changes |
| Independent source review | No actionable defect found after fixes | Read-only review; not a substitute for production acceptance |

There are **378 scheduling checks**, plus **14 geofence HTTP regression checks** in this run. Counts are assertions within standalone harnesses, not end-to-end scenarios. No test project or package was added. The prior geofence business verification remains documented separately in [Site-Geofences-Verification.md](Site-Geofences-Verification.md).

## Reproduced defects and corrections

1. Resolver checks originally accepted a roster after its occupied next date moved to another/missing HR site, and allowed a null-site roster to adopt a later site. The first boundary run had 33 passing and four failing checks. Strict nullable site comparison and validation of the actual occupied next date fixed these cases. Exact-midnight endings and rest days exclude the next date. Four additional occupied-window/calendar-limit checks bring the final suite to 41.
2. Effective-roster authorization previously covered only the start date even if an overnight result exposed another occupied date outside the actor's scope. `SchedulingOperation.ReadAsync` now supports a post-resolution resource check inside the same transaction, with refreshed grants before audit/return. The operation harness initially reproduced the missing hook at compilation; final operation and service runs verify the runtime authorization behavior.
3. Ending/replacing an assignment could disclose its original history to an actor authorized only over the later changed range. The service regression initially expected 403 but received success. End/replace now authorize from the original assignment start across the future timeline, including retries. Final service checks verify denial and allowed alternatives.

## Coverage and practical limits

Executed checks include same-day/overnight boundaries, applicable weekdays, duration/break validation, immutable schedule publication, future-date rules, interval/duty overlaps, stale versions, inactive employee/eligibility rejection delegation, locked-period checks, full returned-history scope, self identity, permission revocation, identical retries, altered payload/key conflicts, expiry and purpose mismatch. Model verification confirms existing SQL date/time/rowversion mappings, unique indexes, constraints, restrictive relationships and all four SaveChanges overloads rejecting ShiftVersion edit/deletion even when automatic detection is disabled. Scheduling registrations and the existing geofence registrations each resolve once.

The fake service repositories exercise how the services use eligibility and lock results. They do **not** prove real SQL queries correctly handle every employment approval, suspension, HR transfer, team scope or month-lock history. EF model checks do **not** execute database constraints or competing transactions.

The HTTP scripts start a temporary hidden localhost process with authentication disabled, a deliberately unusable synthetic database connection and no real user data. They verify every operation is registered, documented and protected against anonymous access. Scheduling also verifies seven required Idempotency-Key headers, both shift 201 Location headers and the required business-date-only My Shift contract. Each script stops its own host in `finally`. No successful authenticated request is claimed.

## Historical 8 October deployment gaps (superseded by current evidence)

- Approve and implement initial provisioning of the five scheduling permissions. Definitions alone do not grant access. Existing immutable bootstrap/system-role and delegation rules were preserved; no role was silently expanded.
- Run authenticated web/MFA/CSRF and mobile/self flows with approved synthetic fixtures, including different employee/site/team scopes and revoked access.
- Verify real SQL Server transactions, eligibility/scoped queries, competing HTTP writes, rowversion conflicts, database constraints, rollback and atomic audit/idempotency receipts.
- Verify the existing encrypted private-file store and frozen response replay on disk with approved private storage/key configuration. Harness private storage is synthetic. The production store forbids repository-contained private paths, so no outside-repository fixture/key directory was created under this local-only authorization.
- Confirm management-owned group assignment precedence, temporary-change approval authority, holiday applicability and retrospective/locked-period authority before implementing those workflows. The current slice supports individual scheduling and fails closed on applicable unsupported group/temporary records.

## Reproduction

Run from the repository root with the existing cached .NET 10 dependencies. The local restore source below prevents package downloads. Generated caches/logs go under ignored `artifacts`.

```powershell
$env:TEMP = Join-Path $PWD 'artifacts/site-geofences-temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
dotnet build Cosmix.Backend.sln --configuration Release --no-restore

dotnet run --file docs/shifts-rosters/VerifySchedule.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false
dotnet run --file docs/shifts-rosters/VerifyOperations.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false
dotnet run --file docs/shifts-rosters/VerifyServices.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false
dotnet run --file docs/shifts-rosters/VerifyInfrastructure.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Infrastructure --property:NuGetAudit=false

./docs/shifts-rosters/VerifyHttp.ps1
./docs/site-geofences/VerifyHttp.ps1
git diff --check
```

HTTP scripts require PowerShell 7 and permission to bind/connect to loopback. Run builds sequentially because the file harnesses reference the same solution assemblies.

## Change boundaries and next work

Scheduling owns its controllers, Application contracts/services, module repositories and feature documentation. Shared edits are additive Application/Infrastructure registration calls, five permission definitions through the catalog, and shift Location-header OpenAPI metadata. Domain changes add controlled Shift name/status and assignment-shortening methods. The previously implemented geofence changes remain present.

There are no DbContext, migration, model-snapshot, package, bootstrap permission or policy feature changes. No database migration/data write, branch switch/creation, fetch/pull/push, commit, merge or PR was performed. The retained scheduling checkpoint was used only as locally available source material.

The immediate next step is approved access provisioning and authenticated database acceptance. The remaining attendance sequence is: settle outstanding roster rules; implement approved face enrollment/live Check In verification; implement append-only punches consuming roster and geofence configuration; build work sessions/daily summaries; then integrate confirmed policy calculations, overtime, corrections and month locks. Zones, site reports, notifications and attendance reports remain separate work. Leave, allowances, vehicle/fuel, advances/loans and payroll handoff remain separate HR work; this scheduling slice does not claim completion of those SRS modules. The separate attendance-policy work is not integrated into this checkout.
