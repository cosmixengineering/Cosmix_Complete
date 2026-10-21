# Fresh Attendance Testing Preparation Handoff

13 October 2026, Asia/Karachi. User approved local preparation only after declining
SQL write approval. No SQL connection, instance start/create, database change,
credential/key/fixture write, real provider, permanent setting or Git write occurred.
This is test infrastructure preparation, not completion of Attendance or ERP integration.

## Later Approved Local Preparation - 13 October

The fresh invocation now uses `.NET 10 WebApplication.CreateEmptyBuilder`, with explicit API
discovery, Development and Kestrel. It does not load source appsettings, user secrets, inherited
environment business values or command-line providers. Only the exact private configuration is
loaded afterward. A fresh flag with absent/disabled private testing fails closed, not normal startup.
Standalone runtime checks now pass **24** (superseding 19 below), and guard checks were rerun:
**194**, including **173** unsafe synthetic snapshots rejected. Release build is 0 warnings/errors.
No host, SQL, persistent key or private root was created. The missing-builder and disabled-profile
regressions failed before their respective fixes and passed afterward.

Read-only inventory still lists only `CosmixUat_d893653346` and `MSSQLLocalDB`. The proposed new
instance, its managed system directory, private root and sanitized artifact root were absent.
An executable staged setup is being prepared separately in `Fresh-Executor-Review.md`.
No SQL approval is inferred from code preparation or offline guard results. Its exact packet must
be reviewed and approved separately before any write. Clone/integration work remains paused.

## Implemented And Checked Offline

The existing API development workflow now supports a separate explicit
`HumaAttendanceIsolatedV1` profile. It pins the proposed instance, explicit pipe and
server pair, owner/operator, catalog, settings/private paths and physical MDF/LDF
identity. It rejects default/old/remote sources, alternate connections, credentials,
attach-file fallback, enabled face providers and external SMS. Legacy mode remains
separate; unknown or disabled profile requests fail closed.

Actual runtime verification rejects a mismatched DbContext connection before SQL
opening. On a future approved target it will inspect server/owner/operator/files,
read-only/indirect-dependency state and the exact 21-migration chain. Those positive
SQL reads and actual seeding have not been executed. The profile never interprets
connection failure as absence or runs automatic migrations. Existing bootstrap
transaction code checks/locks every business table for emptiness, then the fresh
callback rechecks target identity before retaining credentials and reusing the
existing three synthetic authentication fixture bodies. No new seed was run.

The fresh owned-host profile removes EmployeeLifecycle, AttendanceOutbox,
ZoneRetention and MissingCheckoutReminder pollers. Local OTP dispatch and the
framework Data Protection key initialization service remain explicitly accounted
for. Token keys bind to the fresh private directory with current-user Windows
DPAPI protection. Existing directory ACLs are checked; no directory/key was created.
Normal registrations without the fresh profile remain unchanged.

The packet helper has **no execution mode**: it inventories instance names/path
absence, checks ancestor links, hashes migration inputs and returns explicit-file
creation SQL/commands for review. Its creation SQL needs approved server/owner
parameters on new-instance `master`; it is not invoked. Cleanup is stop-and-retain,
not deletion. `ReadyForSqlExecution` deliberately remains false.

## Executed Results

| Check | Result |
| --- | --- |
| Final Release solution build | 0 warnings / 0 errors |
| Fresh config/identity guard | 194 checks, including 173 rejected unsafe synthetic snapshots |
| Actual runtime/host/keyring registration | 19 checks; wrong DbContext bindings rejected with zero connection attempts; no host or key writes |
| Read-only packet source/collision checks | 30 checks, synthetic inventories and actual script AST |
| Generated full migration chain | 11 checks, exact from-zero 21 migrations, zero connection attempts |
| Existing actual DI composition | 14 checks passed; synthetic storage, no SQL/private writes |

Runtime tests first failed with `Fresh owned-host isolation hook is missing` after
fixing a harness namespace typo. Packet tests first failed with the missing helper;
full-chain verification first failed with the missing separate reviewed artifact.
The host test initially omitted .NET's Data Protection hosted initializer, then
explicitly allowed that keyring service while still rejecting all unrelated workers.
This is configuration/registration verification, not actual DPAPI persistence/restart
acceptance. Standalone file apps were used; no test project was created.

Full-chain artifact:
`docs/sql/20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence.fresh-isolated.sql`

SHA256: `E9FB2FBBF59C96C72C7C6F1506652EFA431545B0BD93C90987AD94822D63AB03`.
It was generated offline and not applied. Migrations 1-20 remain untouched; migration
21 was already locally prepared and is still unapplied here.

## Changed Files In This Approved Patch

- `src/Cosmix.Api/Development/FreshLocalTestingGuard.cs` (new pure target guard).
- `src/Cosmix.Api/Development/FreshLocalTestingDatabase.cs` (new runtime metadata guard).
- `src/Cosmix.Api/Development/LocalTesting.cs` (fresh profile and isolated-host hook).
- `src/Cosmix.Api/Development/LocalTestingSetup.cs` (explicit fresh empty-database handoff).
- `src/Cosmix.Api/Program.cs` (local profile-only host integration).
- `docs/attendance-completion/VerifyFreshLocalTestingGuard.cs`.
- `docs/attendance-completion/VerifyFreshLocalTestingRuntime.cs`.
- `docs/attendance-completion/VerifyFreshMigrationScript.cs`.
- `docs/attendance-completion/Prepare-Fresh-SQL-Packet.ps1`.
- `docs/attendance-completion/VerifyFreshSqlPacket.ps1`.
- The new separate generated SQL artifact above and dated preparation/integration notes.

No Domain/Application/Infrastructure business workflow, permission catalogue,
snapshot, migration source, package, React or React Native change was made by this
patch. Older local changes remain intact. Source-laptop/old-target runners were not
edited or executed.

## Exact Remaining Work

Before SQL approval: prepare/review the executable staged provisioner, fresh
private settings/secret-generation procedure, final binary/tool hashes, owned host
manifest and bounded fixture budgets. The approved instance/private/artifact paths
are still absent. Stage-specific pipe/server/physical-file checks cannot be tested
live before creation; they must run after explicit approval and before further writes.
Do not silently use the old target or replace approval with a manifest boolean.

Then, only after approval, real MFA/CSRF/mobile/scopes/revocation, SQL persistence,
concurrent HTTP, encrypted storage/restart/failure acceptance can run. Full Attendance
also needs the remaining financial/legal/extension workflows and deferred real
biometric acceptance. No salary release or production activation is implemented.

The integration document's baseline/source manifest is prepared, not a module port.
Review Contact scope/contract/ownership first, then the first CRM Contact trial,
Inventory and Projects. See [continuation order](../integration/Continuation-Order-20261013.md)
and [source manifest](../integration/CRM-Contact-Source-Baseline-20261013.md).
