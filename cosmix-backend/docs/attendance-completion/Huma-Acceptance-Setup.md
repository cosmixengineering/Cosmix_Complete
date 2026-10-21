# Huma Laptop Guarded Acceptance Setup

Prepared tooling only, 12 October 2026. No SQL, API request, initializer, build,
test project, Git command or private-file content read was performed while
preparing these files. Root owns shared files, manifests, migrations, private
setup, SQL review and any later execution. These scripts are not approval to run.

## Exact Target And Unresolved Approval

The intended target is the **existing** `Cosmix_Testing`, not a replacement.
The user supplied these prior read-only observations; this preparation did not
reconnect to confirm them:

| Binding | Required value |
| --- | --- |
| Checkout | `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend` |
| Actual SQL server name | `Software-PC1\LOCALDB#842DD4FB` |
| Explicit source pipe | `np:\\.\pipe\LOCALDB#842DD4FB\tsql\query` |
| SQL database owner / Windows operator | `COSMIXRND\Huma.Khan` |
| Applied history | Exactly 12 rows; latest `20261008074438_AddEmployeeChangeJournal` |
| Accounts | Exactly three known synthetic accounts, with exact IDs still to be bound |
| Employees | 11 total, eight in `EmployeeStatus.Draft` (`Status=0`) |
| Existing private root | `C:\Users\Huma.Khan\AppData\Local\Cosmix\LocalTesting` |
| Proposed private artifact root | `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\artifacts\attendance-huma-20261012-a01` |

The alias reported stopped while a running process and the explicit pipe were
observed. Do not infer absence of the database, initialize it, start an alias,
or fall back to `(localdb)\MSSQLLocalDB`. A changed pipe requires a new reviewed
target binding and a tooling update by its owner, not a guessed endpoint.

**All writes remain blocked.** Eight additional employees/drafts have unconfirmed
ownership. A human must confirm that each exact record is synthetic and approved
for this existing-database acceptance, with an approval reference for every ID.
Counts and known account names alone do not establish that ownership. If any
record is real, unknown or outside the approved scope, stop; this profile cannot
silently remove, reseed, isolate or overwrite it.

At source inspection, this checkout contained **20** migration designer/source
pairs, ending at `20261012135725_AddAttendancePayrollInputsAndCompanySettings`.
The proposed final count of 21 is **unconfirmed**. Root must finalize the chain,
name the exact final migration/count, review the complete ordered history and
ProductVersion values, and generate/review only the pending range after migration
12. The scripts compare the manifest to the current local chain and refuse a
count/ID mismatch. Neither 20 nor 21 is automatically approved by this document.

## Files And Modes

| File | Behavior when root later invokes it |
| --- | --- |
| `Huma-Acceptance-Common.ps1` | Dot-source function library; no requests at load time |
| `Huma-Acceptance-Preflight.ps1` | `Inspect` is offline (default). `Inventory` and `Preflight` run SELECT-only SQL inspection; no filesystem evidence writes |
| `Huma-Acceptance-Upgrade.ps1` | `Inspect` validates reviewed target/SQL offline (default). `Execute` requires separate action approval, verified backup and exact baseline |
| `Huma-Acceptance-Auth.ps1` | `Inspect` is offline (default). `Execute` tests real password challenge, pre-MFA denial, local OTP proof, authenticated CSRF and current permissions for the three supplied synthetic accounts |

Every entry point requires explicit `ManifestPath`, `ApprovedManifestSha256` and
`SourceServer`. The common library is hash checked before dot-sourcing. No script
starts/stops a server or API host, runs an initializer, builds projects, generates
SQL, resets credentials, rotates keys, grants roles or performs attendance writes.
The authentication phase does write ordinary login/OTP/session/security/audit
state through the real API; it therefore needs its own human write authorization.
The scripts have no automatic migration or login continuation mode.

Inventory returns only server/owner bindings, ordered history, account and
employee IDs, state/rowversions, synthetic-role flags, all user-table counts,
schema fingerprint and lifecycle mismatch count. It does not return names,
phones, passwords, protected HR payloads, key bytes or connection-string secrets.
The JSON can contain sensitive identifiers; root must retain it privately.
Inventory observations do not approve writes. Preflight compares the exact
inventory and schema to the approved manifest and rejects any mismatch.
Identity inspection also refuses existing synonyms or recorded cross-database/
server expression dependencies, so indirect targets must be reconciled by root.

Preflight uses a serializable read transaction, rolled back to release its locks.
It can block other users briefly and requires metadata visibility. Root must
schedule it appropriately. No catalog, identity or data checks use an alias.

## Manifest Contract For Root

Root creates the actual **non-secret** JSON manifest in
`docs\attendance-completion`, reviews it and supplies its exact SHA256 separately.
This document deliberately creates no manifest, SQL or private artifacts. An
example of the unresolved values follows; empty arrays/hashes and false approvals
are intentionally unusable for execution. Fill them only from reviewed evidence.

```json
{
  "SchemaVersion": 1,
  "Profile": "Huma-existing-localdb-v1",
  "Mode": "ExistingDatabase",
  "RunId": "HUMA20261012REPLACE01",
  "Database": "Cosmix_Testing",
  "SourceServer": "np:\\\\.\\pipe\\LOCALDB#842DD4FB\\tsql\\query",
  "ActualServerName": "Software-PC1\\LOCALDB#842DD4FB",
  "Owner": "COSMIXRND\\Huma.Khan",
  "ExistingPrivateRoot": "C:\\Users\\Huma.Khan\\AppData\\Local\\Cosmix\\LocalTesting",
  "ArtifactRoot": "C:\\Users\\Huma.Khan\\Downloads\\Cosmix_Erp_software\\cosmix-backend\\artifacts\\attendance-huma-20261012-a01",
  "WorkerEnabled": false,
  "FreshFixtures": false,
  "RotateKeys": false,
  "TargetMigrationCount": 0,
  "TargetMigration": null,
  "MigrationScript": "docs/attendance-completion/ROOT_REVIEWED_PENDING_RANGE.sql",
  "ArtifactHashes": {},
  "BaselineHistory": [],
  "TargetHistory": [],
  "AllowedAccounts": [],
  "AllowedEmployees": [],
  "BaselineTableCounts": [],
  "TargetTableCounts": [],
  "BaselineSchemaSha256": "",
  "TargetSchemaSha256": "",
  "OwnershipApprovals": [],
  "ApprovalReference": "",
  "Approvals": {
    "Upgrade": false,
    "Auth": false,
    "ExistingDataOwnership": false,
    "PrivateArtifactSetup": false,
    "RetainedKeyring": false,
    "ExclusiveSqlWindow": false,
    "OwnedGuardedHost": false
  },
  "SqlReview": {
    "FromMigration": "20261008074438_AddEmployeeChangeJournal",
    "ToMigration": null,
    "BatchCount": 0,
    "SingleDatabase": false,
    "NoFixtureOrCredentialChanges": false,
    "NoGoInsideStringsOrComments": false,
    "TransactionBoundariesReviewed": false
  },
  "BaseUri": null,
  "UpgradeRunId": null,
  "UpgradeManifestSha256": "",
  "SchemaReceiptSha256": "",
  "HostReceiptSha256": "",
  "HostSettingsSha256": "",
  "AccountsSha256": "",
  "RetainedKeyFiles": []
}
```

`ArtifactHashes` is a map from repository-relative forward-slash paths to
uppercase SHA256 values. It must include these exact files, all migration `.cs`
and `.Designer.cs` pairs in the complete target chain, the model snapshot, and
the reviewed pending SQL artifact:

- The four `Huma-Acceptance-*.ps1` files and this `Huma-Acceptance-Setup.md`.
- `docs/access-provisioning/LocalAcceptanceHttp.ps1`.
- `src/Cosmix.Api/Development/LocalTesting.cs`.
- `src/Cosmix.Api/Development/LocalTestingSetup.cs`.
- For auth execution, `Cosmix.Api.dll`, `Cosmix.Domain.dll`,
  `Cosmix.Application.dll` and `Cosmix.Infrastructure.dll` under
  `src/Cosmix.Api/bin/Release/net10.0/`, plus any other runtime inputs root needs
  to pin for the exact host. No build is performed by this tooling.

Supply arrays in the exact order/property order emitted by inventory; JSON
comparison is deliberately exact. History rows have `MigrationId, ProductVersion`.
`AllowedAccounts` rows have `AccountId, EmployeeId, RowVersion, SyntheticRole`.
`AllowedEmployees` rows have `EmployeeId, Status, OnboardingStatus, RowVersion`.
`*TableCounts` rows have `Table, RowCount`, ordered by schema and table. Expect
exactly 11 employees/eight draft-status employees and all three synthetic triples;
inventory also exposes the actual `hr.EmployeeHiringDrafts` row count separately
within the complete table inventory. Do not assume that table's count from status.

The schema fingerprint covers sorted SQL catalog objects/definitions, columns,
indexes and foreign keys. Baseline fingerprint comes from approved inventory.
Root must provide a reviewed target fingerprint/table-count expectation from its
separately authorized migration rehearsal or equivalent evidence; this tooling
does not create another database to derive it. Do not fill target expectations
from a failed run merely to make the guard pass. Any partial schema not represented
by the 12 baseline history rows fails the baseline fingerprint comparison.

`OwnershipApprovals` contains exactly 11 unique rows with `EmployeeId`,
`SyntheticConfirmed: true` and a non-secret `ApprovalReference` explaining who
approved ownership. The reference fields cannot contain secret evidence. Root
must retain actual approval evidence separately and treat boolean fields as a
record of that human decision, not as a mechanism for granting permission.

## Private Artifact And Key Setup Still Pending

Root must explicitly approve and provision the exact proposed artifact directory
before either write phase, with ACLs limited to Huma's SID, SYSTEM and local
Administrators. The scripts verify ACL ownership/allow rules and reject linked
paths. They never provision or repair that root or alter existing private ACLs.
They create only new stage markers, run evidence and the verified backup beneath
the approved root after guards pass. Root must exclude the whole private artifact
root, manifests containing private identifiers, backups, logs and runtime secrets
from source control and sharing; this tooling makes no `.gitignore` changes.

Preserve **all** contents of the existing LocalTesting private root, including
settings, `Test-Accounts.json`, snapshots, files and keys. Do not read/display or
copy those secrets during this preparation. The optional future auth execution
reads private credentials into memory and verifies their file hash; it never
prints or serializes credentials, OTPs, cookies, bearer material or CSRF tokens.
Root must run it without transcript/debug tracing of HTTP payloads or live clients.

Old protected files and database references depend on the existing keyring.
`EncryptedPrivateFileStore` resolves each immutable key reference through the
active `PrivateFiles.KeyReference/KeyFile` or `DecryptionKeyFiles` mapping. The
bootstrap snapshot store also binds ciphertext to its original key/reference.
Preserve the old key bytes, reference mappings, ciphertext and necessary settings.
Generating a replacement key at the same path/reference makes old ciphertext
unreadable. Key presence/length and an approval receipt are not proof of decryption;
root must review the actual dependency mapping and its recovery plan privately.

Fresh keys/fixtures need a **separate explicit mode, target and human approval**,
including a plan for old decryption dependencies. These scripts support only
`ExistingDatabase` and reject fresh fixtures/key rotation. They do not offer a
flag to turn fresh mode on. No source-laptop private paths or secrets are reused.

Root may later approve a separate `runtime-settings.json` under the private
artifact root if the existing settings do not pin the observed pipe. Preserve the
original settings; root must review the derived local configuration and all old
key mappings. Neither these scripts nor the original initializer should generate
or copy settings/keys for this existing-data task.

## SQL Review And Failure Handling

Root reviews **every byte/batch** of the pending upgrade SQL: from the exact 12-row
baseline through the finalized target only, normal EF transaction boundaries,
no fixture/credential/role changes, and references confined to this database.
The script accepts ordinary standalone `GO` separators without counts. Root must
confirm there is no `GO` inside multiline strings/comments. The SQL is executed
as batches through one explicitly targeted integrated-auth SQL connection, never
through a shell or sqlcmd interpreter. Rejection checks block common target
switching/directives; they are not a SQL sandbox or proof that arbitrary SQL is
safe. Nested dynamic SQL, synonyms, triggers, functions and all indirect targets
also require root review. Do not weaken guards to accept an unreviewed script.

Root must keep all other writers/API hosts stopped throughout the SQL window and
record `ExclusiveSqlWindow` approval. Read-only inventory uses locks, but there
is no global exclusion of an unrelated sysadmin connection. Baseline is checked
before and after backup, and source/tooling/SQL/backup hashes are rechecked before
execution. Server/database/owner identity is checked before each batch. The script
performs `BACKUP DATABASE ... WITH COPY_ONLY,CHECKSUM`, checks that the single
backup header is the intended database/copy-only/checksummed backup, then executes
`RESTORE VERIFYONLY ... WITH FILE=1,CHECKSUM`. Only after a private verified-backup
receipt is durable may the reviewed migration batches begin. A backup failure
prevents migration execution. No automatic restore is performed.

The artifact root contains one durable `upgrade-attempt.json` and one
`auth-attempt.json`, created exclusively before each phase. Changing `RunId` does
not permit a repeat in the same root. Do not delete markers, change artifact root
or change fingerprints to rerun. After any stop, preserve the backup, markers,
batch intents, receipts and database state. Closing a SQL connection rolls back
only its uncommitted transaction; already committed migration batches remain.
Root must reconcile actual full history, schema and data and obtain a distinct
reviewed recovery plan/tooling if needed. A failed operation can have taken effect;
completed-batch counts and absence of a receipt are not proof of rollback.

## Later Authentication Host Binding

`LocalTesting.Configure` requires Development, private settings, integrated auth,
`Cosmix_Testing`, local pipe/allowed LocalDB alias and only the local SMS adapter.
`VerifyDatabaseAsync` rejects pending/model changes and requires the exact three
synthetic account/employee identity triples. Its account check does not approve
the eight additional employees. `LocalTestingSetup` returns without resetting an
already valid existing database; when it cannot connect, it can create/migrate and
seed fixtures. Therefore **do not invoke setup/initialize** for this acceptance.
Original source-laptop runners/manifests are references only, not runnable adapters.

Root must separately review/start the owned HTTPS localhost host after upgrade,
without bypassing either LocalTesting guard. It must bind only loopback and have
the Development settings pinned to the observed pipe. The normal OTP delivery
worker is necessary for the real login flow. Attendance jobs/zones, zone retention
and zone-location storage must be explicitly disabled; missing-checkout reminders
must be disabled or made inactive by disabled attendance delivery. The lifecycle
worker remains registered and has no disable switch: the SQL guard rejects every
status-change/current-status mismatch before hosting/login, including future ones.
Any lifecycle concern requires separate root review, not a fake worker-disable flag.

Root creates a **non-secret, private, hash-approved** `owned-host.json` in the
artifact root with all fields checked by `Huma-Acceptance-Auth.ps1`:

- `ProcessId`, `StartedAtUtc`, `BaseUri`, `SourceServer`, `Database`,
  `ActualServerName`, `Owner`, `SettingsPath`, `SettingsSha256`, `ApiAssemblySha256`.
- `Environment: "Development"`, `LocalTestingEnabled: true`,
  `SmsProvider: "LocalTesting"`, `ExternalSmsConfigured: false`.
- `AttendanceJobsEnabled`, `AttendanceZonesEnabled`, `ZoneRetentionEnabled`,
  `ZoneLocationStorageEnabled`, `MissingCheckoutRemindersEnabled`: all false.
- `EffectiveConfigurationReviewed: true`, `RetainedKeyringReviewed: true`.

`SettingsPath` must be the retained `settings.json` or the approved private
`runtime-settings.json`. A hash and receipt cannot independently prove the
process's effective environment/configuration; root must verify that binding.
The script checks process name/start time/command and exclusive loopback port
ownership before sending credentials. The reviewed command must use the quoted
absolute API assembly path and `--urls` followed by the manifest's exact HTTPS
localhost root URL without a trailing slash. There is no certificate-bypass flag;
root must establish the trusted localhost certificate separately.

For auth, root finalizes a **new** exact manifest/hash after upgrade and host
review. `UpgradeRunId`, `UpgradeManifestSha256` and `SchemaReceiptSha256` bind the
previous completed schema receipt; this avoids circular hashes. `HostReceiptSha256`,
`HostSettingsSha256` and `AccountsSha256` bind later private inputs. The target
inventory must still match before any login. If hosting changes state, stop and
review it, not merely replace expected values. `RetainedKeyFiles` supplies each
non-secret `{ "Reference": "original-reference", "Path": "original-key-path" }`;
auth checks only metadata/ACL/32-byte length, never the key contents.

The existing `LocalAcceptanceHttp.ps1` helper performs actual password login,
requires 401 before MFA, reads the challenge/browser-bound local OTP, verifies OTP,
refreshes the CSRF token after authentication and checks persisted permissions.
The Huma auth script emits only safe status receipts, does not return live clients,
and stops on the first failure. It does not claim attendance/business acceptance.

## Root-Only Future Invocation Examples

These are examples for later review/run, **not commands executed in preparation**.
Replace the manifest path/hash with the exact human-reviewed document. Default
`Inspect` modes never connect. A supplied hash binds bytes; it is not human consent.

```powershell
$humaManifest = 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\docs\attendance-completion\Huma-reviewed-manifest.json'
$humaSha = 'REPLACE_WITH_EXACT_REVIEWED_64_HEX_SHA256'
$humaPipe = 'np:\\.\pipe\LOCALDB#842DD4FB\tsql\query'
$humaArgs = @{ ManifestPath=$humaManifest; ApprovedManifestSha256=$humaSha; SourceServer=$humaPipe }

# Offline file/target review (the finalized manifest must supply required hashes).
& .\docs\attendance-completion\Huma-Acceptance-Preflight.ps1 @humaArgs -Mode Inspect
# Separately authorized read-only inspection; keep resulting IDs privately.
& .\docs\attendance-completion\Huma-Acceptance-Preflight.ps1 @humaArgs -Mode Inventory
& .\docs\attendance-completion\Huma-Acceptance-Preflight.ps1 @humaArgs -Mode Preflight -Stage Baseline

# Only after all existing-data, target, SQL, artifact and keyring approvals.
& .\docs\attendance-completion\Huma-Acceptance-Upgrade.ps1 @humaArgs -Mode Inspect
& .\docs\attendance-completion\Huma-Acceptance-Upgrade.ps1 @humaArgs -Mode Execute -ActionApproval UpgradeExistingCosmixTesting

# Use the separately finalized auth manifest/hash and reviewed owned host later.
& .\docs\attendance-completion\Huma-Acceptance-Auth.ps1 @humaArgs -Mode Inspect
& .\docs\attendance-completion\Huma-Acceptance-Auth.ps1 @humaArgs -Mode Execute -ActionApproval RealSyntheticWebMfa
```

Before execution root must resolve: ownership of the eight extra records; exact
three account IDs and all 11 employee IDs/state; full 12-row history; finalized
target count/ID (21 remains unconfirmed); reviewed pending SQL/hash/batch scope;
baseline/target schema and data expectations; exact private artifact setup/ACLs;
retained settings/keyring/decryption dependencies; exclusive SQL window; and,
separately for auth, runtime hashes, real guarded host/certificate and private
credential bindings. Any missing approval or unexpected history/data means stop.
