# Fresh Attendance SQL Acceptance Proposal

Prepared 13 October 2026, Asia/Karachi. Status: proposed, not provisioned or approved.
After review, the user explicitly withheld SQL write approval pending verified
isolation and exact operation review. The fresh adapter/operation packet is still
incomplete; this setup is **not yet ready for write approval**. The user subsequently
approved local code-only preparation, not SQL/instance/fixture/key writes. The
guarded backend profile, read-only packet helper and full-chain SQL are prepared;
executable provisioning/private configuration/secret generation and a final
manifest still require review. See [local preparation](Fresh-Preparation-Handoff-20261013.md).
The existing `Cosmix_Testing` and its private files remain untouched. This is a
setup proposal, not an executable initializer or evidence that acceptance passed.

## Exact Proposed Target

| Binding | Proposed value |
| --- | --- |
| Checkout | `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend` |
| New LocalDB instance | `CosmixAttendanceHuma20261013A01` |
| Human-readable SQL target | `(localdb)\CosmixAttendanceHuma20261013A01` |
| Catalog inside that new instance | `Cosmix_Testing` |
| Windows operator and database owner | `COSMIXRND\Huma.Khan` |
| Installed LocalDB version observed read-only | SQL Server 2025, `17.0.4025.3` |
| Private root | `C:\Users\Huma.Khan\AppData\Local\Cosmix\AttendanceAcceptance\Huma20261013A01\` |
| MDF | Private root + `sql\Cosmix_Testing.mdf` |
| LDF | Private root + `sql\Cosmix_Testing_log.ldf` |
| Private settings and credentials | Private root + `config\LocalTesting.json` and `Test-Accounts.json` |
| Private keys and encrypted evidence | Private root + `keys\`, `snapshots\`, `files\`, `metadata\`, `receipts\`, `exports\` |
| Private manifest and detailed run records | Private root + `manifest\` and `results\` |
| Sanitized artifact destination | `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\artifacts\attendance-huma-20261013-a01\` |

The identical catalog name does not mean the identical database: the new instance
owns a separate catalog and explicitly separate physical files. Never connect to
the default `MSSQLLocalDB` alias as a fallback. Never use the previously observed
`LOCALDB#842DD4FB` pipe, `CosmixUat_d893653346`, or an existing private keyring.

Read-only inventory lists `CosmixUat_d893653346` and `MSSQLLocalDB`. The proposed
instance, private root and artifact root were absent. Their absence must be checked
again immediately before provisioning; a collision is a stop condition, not reuse
permission. No existing database was connected for this proposal.

The new instance's pipe and actual `SERVERPROPERTY('ServerName')` do not exist yet.
After approved creation, resolve its pipe from that instance only, record it, and
independently verify server identity and operator before creating the catalog.
Private runtime settings must use that exact `np:\\.\pipe\LOCALDB#...\tsql\query`.
Existing `LocalTesting.Configure` pins the catalog and accepts explicit local
pipes; it does not accept the proposed named-instance alias directly.

## Planned Writes After Approval

1. Before provisioning, prepare and review the exact fresh adapter, database
   creation commands, full-chain SQL, initial fixture manifest, host configuration
   and stop/retention procedure. Pin their hashes and executing assemblies. None
   may be inferred from the old target's scripts. Obtain explicit SQL approval.
2. Recheck absence, then create only the new instance and approved private/artifact roots. Private ACLs
   allow only this Windows user, SYSTEM and local Administrators. Reject reparse
   points and pre-existing files; do not repair or inherit an unrelated keyring.
3. Verify the new instance's actual pipe/server/operator. Catalog creation uses
   `master` on **this new instance only**; include that management connection in
   approval. Bind explicit MDF/LDF paths inside the new private root. Do not rely on LocalDB
   default file locations, which could collide with an existing same-named MDF.
4. Apply the already reviewed full chain only to the new verified target, starting
   with an empty catalog, not migration 12 or 17. Last local
   migration is `20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence`.
   Recheck the exact chain before execution; changed inputs require review.
5. Generate new synthetic passwords, private encryption/Data Protection keys and
   a laptop-specific manifest. Reuse the existing synthetic authentication fixture
   workflow for exactly three approved `.invalid` accounts, never source secrets.
6. Build synthetic attendance fixtures through supported workflows with explicit
   evidence and write budgets: employment/compensation, sites/shifts/jurisdictions,
   permission scopes, configured test policies, leave and financial source records.
   Authentication-only bootstrap records are not salary or genuine hiring evidence.
7. Run bounded authenticated HTTP and independent SQL/storage checks. Preserve
   auditable test outputs; report pending dependencies rather than inventing data.

No production/development data, real employees, external SMS, paid face APIs,
salary payments or automatic legal activation are included. Real CompreFace,
reviewed ONNX inference and device/spoof tests are explicitly deferred by the user.
Monitoring, outbox/reminder/retention workers and provider paths require explicit
reviewed host settings. Do not assume every registered worker has an enable gate:
`EmployeeLifecycleWorker` has no such gate, and enabled authentication dispatches
OTP records. Account for those synthetic writes and owned workers in the manifest,
or isolate them through a reviewed test-host configuration. Explicitly bind Data
Protection persistence to this new private keyring; a proposed `keys` folder alone
does not configure it. The fresh backend profile now removes the four non-OTP
business polling workers and binds Data Protection to its own current-user
DPAPI-protected directory. The framework Data Protection startup service also
remains; budget its private key initialization only after later approval.
Registration was checked offline; no host was started and no key was generated.
The private configuration/manifest and executable provisioner remain unprepared.

The existing database is not upgraded and needs no backup for these writes. A
later upgrade of this disposable target requires its own verified backup before
applying any additional pending range. No automatic drop/reset/cleanup is planned.
Default end-of-run handling stops only the identity-verified owned host, closes
owned connections, restores temporary process environment and retains the new
database/evidence. No destructive cleanup is approved. Any later deletion needs
separately explicit scope review, exact instance/catalog/physical-file verification
and manifest-owned paths; never a wildcard, computed unverified path or cleanup of
an existing instance.

## Required Tooling Adaptation

`Huma-Acceptance-Common.ps1` deliberately permits only the old existing-instance
profile, old pipe/private roots and 11 employees including eight drafts. Keep its
guards intact. Its 13-21 incremental SQL is not a fresh-target initializer.
`Huma-Attendance-Acceptance.ps1` describes 15 cases; it is a contract, not a runner.

The legacy `LocalTestingSetup.RunAsync` branch preserves its existing-database
validation. Its new explicit fresh-profile branch never calls `MigrateAsync` or
uses connection failure as absence: it requires the reviewed pre-created target,
metadata/file identity and complete migration history. Existing bootstrap code
locks/checks every business table for emptiness; the fresh callback revalidates the
target before retaining credentials and using the original synthetic fixture body.
The legacy guard was not removed. This branch is prepared, not executed against SQL.

The fresh adapter must bind approval, instance/pipe/server/owner, zero-business-row
preflight, explicit physical files, exact migration/model hashes, fixture budget,
private ACLs and owned host identity. Keep read-only inspection separate from every
write phase. A partly provisioned run stops for inspection; it never overwrites
credentials, adopts another database, regenerates keys, or silently resumes.

Do not edit existing appsettings, user secrets, launch profiles, machine/user
environment or existing connection strings. Pass fresh settings only to the owned
process; restore its temporary environment in `finally`. This does not weaken the
legacy local-testing or old Huma guards. The approved local patch adds a separate
explicit profile to the existing API development workflow; no appsettings, user
secret or permanent environment was changed.

## Read Only Review Results

| Requirement | Result |
| --- | --- |
| Proposed instance/catalog absent | Instance absent in LocalDB metadata inventory; therefore no catalog exists inside it. No query/start against an existing instance. |
| Proposed private/files/artifact paths absent | Root, MDF, LDF and artifact paths absent. Reparse/ACL/collision checks must repeat before creation. |
| Migration source inventory | 21 parsed Up methods; 800 builder calls, 11 raw SQL calls, zero InsertData/UpdateData/DeleteData calls. Raw forward SQL reviewed as current-catalog guards/constraints, not external target routing. |
| Latest model/migration | 16 actual offline model/snapshot/incremental SQL checks passed with zero connection attempts; not full-chain SQL execution. |
| Fresh backend binding | 194 pure guard checks and 19 actual runtime/registration checks passed; wrong actual DbContext bindings fail before any connection attempt. Positive SQL metadata/seed/host acceptance is not executed. |
| Existing databases/configuration/Git preserved | No SQL connection, instance start/create, migration application, seed, real provider, credential/key write, permanent configuration or Git write occurred. Only approved local source/tooling preparation occurred. |
| Exact creation/migration/cleanup packet | **Incomplete:** read-only packet and from-zero SQL prepared/hash-pinned; executable provisioning/configuration/secret generation and final binary/fixture manifest still required. Cleanup is stop-and-retain only. |

An independent pre-implementation reviewer identified the missing fresh seed
handoff, exact target guards, executable artifacts and keyring/worker configuration.
The approved local preparation addresses backend binding/registration and prepares
SQL, but is not positive SQL or full setup acceptance. Do not mark this checklist
passed because paths are absent, guards pass offline or the build succeeds.

## Acceptance And Stop Conditions

Actual acceptance must verify MFA plus CSRF, mobile/self identity, denied/current/
historical scopes and revocation, concurrent requests on independent connections,
same-key replay/conflicting payload/stale versions, atomic audit/receipts/outbox,
encrypted restart/replay and controlled storage failures, overnight/transfer/leave/
OT/policy timelines, and locked-period restrictions.

Unsupported positive paths must stay explicitly pending: real face acceptance,
legal activation without formal authority/clearance, financial credits without
confirmed posting, and successful month finalization while dependencies are
unavailable. Synthetic collaborator checks are not authenticated SQL acceptance.

Stop on any identity, path, hash, history, schema or fixture-budget mismatch; any
unexpected business row; or a configured non-local provider. Preserve failed-run
evidence. Authorization for this proposal must be explicit before any SQL,
instance, fixture, credential or key writes.

References: [latest verification](Verification.md),
[existing local guard](../../src/Cosmix.Api/Development/LocalTesting.cs),
[existing fixture initializer](../../src/Cosmix.Api/Development/LocalTestingSetup.cs),
[old-target guard](Huma-Acceptance-Common.ps1),
[prepared case contract](Huma-Attendance-Acceptance.ps1).
