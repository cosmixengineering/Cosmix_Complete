# Fresh SQL Executor Review

## Superseding execution status - 13 October 2026

The setup-only execution was later approved, including the separately confirmed Windows 5.1
packet ordering hash. It created the new private folders and instance, then stopped at metadata
parsing of SqlLocalDB's `np:` pipe prefix. No new catalog, migration, auth account or key was
created there. The partial instance and private stage records remain preserved; no reset/delete
or automatic resume occurred. The narrow parser fix passed 220 offline Windows 5.1 checks.

The user subsequently directed testing to the existing synthetic database. Its verified-backup
upgrade and real auth/shift HTTP results are in [latest verification](Verification.md).
Migration 19's then-unapplied filtered-index defect was corrected and that existing target is at
21. These source changes obsolete the old fresh packet/artifact, and the new-instance absence
precondition no longer holds. Do not execute the earlier setup command or hashes as a retry.
Current Office Roster model/snapshot drift additionally blocks further guarded live acceptance.
The older preparation statements below are retained historical records, not current write state.

## Final Local Preparation - 13 October 2026

This section supersedes the initial scaffold description below. The staged **setup-only executor
is implemented and checked offline; SQL execution is NOT approved or run**. No hash grants consent.
Root independently ran `VerifyFreshSqlSetupExecutor.ps1`: **214 offline checks**, exit 0.
Actual default REVIEW-ONLY execution succeeded: **21 migrations, 916 input hashes, zero writes**.
Its packet hash is now superseded by this documentation change. A new packet is required for approval.

The exact proposed NEW instance is `(localdb)\CosmixAttendanceHuma20261013A01` (version 17.0),
with catalog `Cosmix_Testing` and owner/operator `COSMIXRND\Huma.Khan`. This is not the existing
database with that name. Read-only inventory listed only `CosmixUat_d893653346` and `MSSQLLocalDB`.
The proposed private root, managed instance directory and artifact root were absent. No existing
SQL connection was opened. Target absence/ancestor links are rechecked before any approved write.

Private root: `C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01`.
Explicit MDF/LDF: `sql\Cosmix_Testing.mdf`, `sql\Cosmix_Testing_log.ldf` under that root.
Settings: `config\LocalTesting.json`. Fresh raw keys/credentials/encrypted files/stage results remain
inside this root. LocalDB also creates its previously absent managed directory:
`C:\Users\Huma.Khan\AppData\Local\Microsoft\Microsoft SQL Server Local DB\Instances\CosmixAttendanceHuma20261013A01`.
Reserved sanitized artifact root remains absent and unused by setup-only execution:
`C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\artifacts\attendance-huma-20261013-a01`.

### Exact Proposed Write Stages

1. Recheck inputs/absence. Create new private parents/root/subdirectories using protected
   current-user/SYSTEM/Administrators ACLs. Existing parents are not reconfigured. CreateNew stage records.
2. Create/start ONLY the new instance. Capture its explicit pipe, actual server and operator.
   Connect only to new-instance master; verify and record LocalDB system-file paths.
3. Execute guarded CREATE with actual server/owner parameters and explicit MDF/LDF; verify owner,
   server/files, read-only state, no synonyms/triggers/external dependencies and empty catalog.
4. Apply reviewed full 21-migration SQL using exact new pipe/catalog/integrated authentication;
   reject directives, target switches and hash drift. Verify ordered history/product versions,
   exact table inventory and zero business rows.
5. Generate five independent fresh crypto values; CreateNew private key files/settings, never
   copy source secrets. Use the empty-builder configuration-only path and cleared child environment.
6. Invoke ONLY existing `setup-local-testing`, then verify exact row/private-object budgets and
   synthetic identities. No API host, Attendance fixture or provider starts.
7. Retain all state/results on success or failure. No resume, reset, deletion, downgrade or cleanup.

Exactly three synthetic `.invalid` accounts (existing TEST ONLY identities) are proposed.
Post-seed budget: Employees/Submissions/OnboardingDecisions 3 each; UserAccounts/Roles 3 each;
Permissions 39; RolePermissions 92; UserRoleAssignments/UserAccessScopes/LoginPhoneBindings 3 each;
AuditEvents 1; **all other business tables zero**. Private seed evidence: one bootstrap snapshot,
two encrypted submission files and one CreateNew credentials file. Data Protection keys during
setup: zero. No Finance grants, posting/settlement evidence or new legal authority are seeded.
Later Attendance fixtures/live HTTP testing require a separate reviewed bounded manifest.

Execution requires explicit human consent, `-Execute`, exact approved packet hash, explicit scope
statement and 64-bit Windows PowerShell 5.1. Every mutation adapter requires the owned context.
Reviewed source/Release binaries/tools are hashed and read-locked during execution. Child arguments
are bounded and inherited environment is cleared. Only the executor's own timed-out child may be
terminated. No production setting/connection string is changed. Stage failures retain bounded records.

Full migration SQL SHA256 remains
`E9FB2FBBF59C96C72C7C6F1506652EFA431545B0BD93C90987AD94822D63AB03`.
Root additionally reran fresh runtime **24**, guards **194**, full-chain **11**, composition **14**,
and Release build **0 warnings / 0 errors**. No SQL/private/key/credential/fixture writes occurred.
Offline checks and a successful review packet do not verify actual SQL, ACL durability, seed counts,
restart/encryption or authenticated HTTP. Source/binary markers are not behavioral correctness proof.
Full Attendance acceptance, legal certification and real biometric acceptance remain unachieved.

## Initial Scaffold (Superseded)

13 October 2026. Attendance only; ERP integration remains paused.
Status: **NOT RUN; incomplete executor, fail closed before every write.**
Local code preparation is authorized. SQL, instance, fixture, secret/key, host and
persistent environment operations still require later explicit human approval.
This document supplements the existing proposal; that shared file was not edited.

## Interface And Current Barrier

`Invoke-Fresh-SQL-Setup.ps1` defaults to REVIEW-ONLY. Running it without flags would
use the existing read-only packet helper (LocalDB name inventory only), validate
the pinned full-chain SQL, inventory source/assemblies/installed tools and emit
an in-memory review packet plus SHA256. It does not save a manifest or settings.
Neither that review entrypoint nor execution mode was run during this preparation.
Dot sourcing imports helpers only, even if execution parameters are present.

The future execution interface requires `-Execute -ApprovedPacketSha256 <exact
uppercase 64-character hash>`; `-AfterSeed` accepts only `SetupOnly`. The current
driver validates the proposed approval hash, packet drift, fixed checkout/operator
and read-only absence inputs, then always throws:

```text
Fresh setup refused: ConfigurationProviderIsolation; values suppressed.
```

No flag, hash or manifest boolean bypasses that barrier. A matching SHA256 is a
drift guard, not human approval. Every proposed mutation stage is explicitly
`Implemented = false`; there is no mocked SQL acceptance or placeholder success.
The new script contains no process-start, SQL-open, private-write or RNG adapter.
Returning a review packet is not setup success. Failure records are bounded
stage-only exceptions with dependency details/values suppressed; no private run
record exists because private creation is blocked. Failed provisioned state
handling remains part of the unimplemented manifest adapter.

## Reviewed Target And Operations

- Checkout: `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend`.
- Operator/owner: `COSMIXRND\Huma.Khan`.
- Only NEW instance: `CosmixAttendanceHuma20261013A01`, version `17.0`.
- Only catalog: `Cosmix_Testing`, with explicit MDF/LDF under the proposal's
  `Huma20261013A01\sql` directory. New-instance `master` is the sole management
  connection; no old instance, default alias, external database or attach fallback.
- LocalDB also writes its managed system directory:
  `%LOCALAPPDATA%\Microsoft\Microsoft SQL Server Local DB\Instances\CosmixAttendanceHuma20261013A01`.
  This includes instance master/model/tempdb data/log files and LocalDB-owned
  instance metadata/logs. Capture exact physical system file paths from ONLY the
  newly created instance's metadata, and retain them in its private manifest.
  Check managed-directory absence/reparse ancestors before creation. The current
  packet helper does not check this extra directory; the creation adapter must.
  No existing instance or its system files may be changed.
- ONLY AFTER future creation, capture `SqlLocalDB info CosmixAttendanceHuma20261013A01`;
  validate name, owner, Running state and explicit named pipe. The pure helper
  validates supplied metadata fields but does not parse command output or invoke
  this command. Localized/ambiguous output must refuse in the future parser.
- New-pipe `master` metadata query must parameterize expected operator and read
  actual `SERVERPROPERTY('ServerName')`; verify pipe/server ID pairing before
  parameterized guarded CREATE using the existing packet helper's exact SQL.
- Apply the full reviewed from-zero SQL with installed `sqlcmd`, exact new pipe,
  `-d Cosmix_Testing -E -C -b -X1 -r1 -t 60 -i <reviewed SQL>`. The pure builder
  returns an argument array, never shell text. Disable directives/environment
  substitution; reject target switches. The SQL text screening is an extra
  conservative guard, not a SQL parser or permission to execute changed SQL.
- Recheck physical files, owner/operator/server, exact ordered migration history,
  schema/model, indirect dependencies and all business-table emptiness before seed.
  Seeder reuses the existing fresh-profile branch and its transaction guard.
- Fresh private config/raw keys/credentials/manifest must use CreateNew exclusively
  and non-inheriting ACLs limited to this user, SYSTEM and Administrators. Reject
  collision/link/ACL mismatches; never overwrite, copy source secrets or repair
  an existing keyring. Raw file/snapshot keys and explicit OTP/JWT values must be
  independently freshly generated only after approval. Data Protection uses
  `keys\data-protection`, current-user DPAPI and the fresh profile binding.
- Child environment only: explicit allowlist, cleared inherited values, isolated
  configuration inputs, bounded suppressed output, disposal/restoration in finally.
  Existing setup-local-testing is a command that returns before host construction;
  no default server launch follows seeding. Retain database/files/manifest/results;
  no automatic resume, reset, drop, delete, stop/reconfigure of any old instance.

## Input And Budget Limits

Review packet hashes cover migration sources/snapshot, C# sources/project files,
lockfiles, global.json/Directory.Build.props, packet/driver/verifier/review files,
Release DLLs/deps/runtimeconfig files and installed sqlcmd/SqlLocalDB/dotnet EXEs.
The full SQL SHA256 remains pinned to
`E9FB2FBBF59C96C72C7C6F1506652EFA431545B0BD93C90987AD94822D63AB03`.
Inputs are read only; appsettings/private settings/user secrets are not read/copied.
Hashes detect drift but do not prove binary/source correspondence, runtime/native
dependency identity or tool compatibility. These are unresolved execution gates.
Review packet SHA is generated on demand, not a stable approval token supplied here;
any shared-source/binary change requires a fresh packet and human review.

The in-memory config builder requires five explicit, distinct canonical 32-byte
base64 values. It selects `HumaAttendanceIsolatedV1`, one exact pipe/catalog
connection, local SMS, disabled face sections, fresh file paths and explicit JWT/OTP
crypto values. It generates no randomness, passwords or files. Synthetic tests
exercise serialization only: this does not prove config completeness, entropy,
provider exclusion or acceptance by the actual API configuration/DI runtime.
FilesKey/SnapshotKey inputs are validated but their bytes are not emitted in JSON.
The existing seed body generates the three authentication passwords in future.

The intended seed budget is exactly `superadmin@example.invalid`,
`admin@example.invalid`, `hr@example.invalid`, with the existing TEST ONLY
names/phones/approval/bootstrap body. Zero Attendance business fixtures, API calls,
server sessions, OTP deliveries, face services or Finance integration are included.
Those three accounts create additional employees, permissions/roles, scope/phone
bindings, approvals, audit and encrypted bootstrap/file evidence. Exact per-table
and private-object counts still need a bounded reviewed manifest; account count
alone is insufficient. No actual fixture was created.

## Exact Unresolved Stages

The first hard barrier is **ConfigurationProviderIsolation**: prove the default
WebApplication configuration path cannot inherit user-secret, environment or
provider settings. No existing API/configuration guard was weakened or edited.
All remaining stage adapters also remain unimplemented: private ACL/manifest,
LocalDB creation/metadata parser, parameterized ADO identity/catalog creation,
sqlcmd migration, independent schema/empty-data checks, fresh material generation,
and bounded child seeding/postchecks/retention. These must be implemented, checked
offline and reviewed before any SQL approval request. Positive SQL/private ACL/
DPAPI/seed/process acceptance can occur only after later authorized creation.

## Executed Offline Checks

The verifier first failed RED with `Fresh setup executor is missing (expected
initial RED)`. Its initial reruns exposed two verifier PowerShell mistakes
(unparenthesized array multiplication and OrderedDictionary.Clone), corrected
within the owned verifier. Final GREEN: **105 standalone executor checks** and
**30 existing packet checks**, both exit code 0. These results are offline guards
and artifact checks only, not a successful setup or positive SQL acceptance.

Run the standalone verifier with:

```powershell
& .\docs\attendance-completion\VerifyFreshSqlSetupExecutor.ps1
```

It imports the driver, uses deterministic synthetic values, parses actual AST and
reads the actual SQL/source artifact only. It tests malformed/mismatched approval
inputs, operator/checkout/collision/pipe/server/catalog refusal, SQL directives,
exact sqlcmd argument routing, source/hash drift, full ordered SQL migration chain,
explicit synthetic config/crypto validation, dot-source inertness and absence of
mutation/RNG adapters. It never invokes the driver entrypoint, inventory, execution
mode, SQL, host, key generation or private writes. The existing packet verifier is
also an offline synthetic/AST check. No build, test project, Git, network,
dependency installation or persistent environment write is involved.
