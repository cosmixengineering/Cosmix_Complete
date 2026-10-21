# A2 SQL and HTTP Environment Preflight

Observed 2026-10-15 at approximately 13:35 Asia/Karachi. Read-only sidecar for the approved first A+B repair batch. **A2 is not accepted: no usable host or currently verified synthetic SQL target.** Reuse remains the intended approach, conditional on the checks below; the batch approval is not being reopened.

## Scope and Result

- Read repository AGENTS, A2/B1/B2, architecture/ownership guidance, current guard/runner source, directory/file metadata and allowlisted non-secret manifest/configuration fields.
- Ran only read-only LocalDB `info`, process/listener inventory and file inspection/hash comparisons. No SQL connection, HTTP request, OTP, host/instance start, build, migration, seed, key generation, configuration change, provider call or Git operation.
- Only this report was written. Parent owns all shared code, scripts, manifests and configuration.
- Can continue code/static and parent-owned standalone B1/B2 verification. No authenticated HTTP or live migration/fixture verification was performed by this preflight.

## Current Observations

| Item | Observed state and implication |
| --- | --- |
| Windows operator | `COSMIXRND\Huma.Khan`; matches retained ownership. |
| LocalDB | `MSSQLLocalDB`, `CosmixUat_d893653346`, and `CosmixAttendanceHuma20261013A01` all **Stopped**, no current pipe; version `17.0.4025.3`. Do not connect by alias: that can start an instance. |
| API host | No `dotnet.exe` or `Cosmix.Api.exe` process observed. No listeners on inspected candidate ports, including 7240 and 7481. One unrelated/unidentified `sqlservr.exe` PID 1732 exists; executable path unavailable. This is not proof of an approved target and was not contacted. |
| Current source migrations | 24 designer-backed migration IDs, ending `20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims`. Full ordered IDs match the retained original B24 result. This is a file comparison, not live SQL/model equality. |
| Retained original result | Reports 24 migrations, 5 accounts, 14 employees; retained IDs/source facts preserved; production not accessed; original idempotent rerun not performed. Recorded 2026-10-13, not freshly verified. |
| Retained clone result | `Cosmix_PartA_Acceptance_20261013`, 24 migrations, preservation and idempotent repeat recorded. It is not an interchangeable acceptance target: current LocalTesting requires `Cosmix_Testing`. |
| Fresh A01 attempt | Directory and instance now exist, unlike the earlier preparation document. Receipts show private-root completion, instance creation completion, then `NewInstanceMetadata` failure. `config` and `sql` currently contain no files. Retain this partial attempt; do not initialize/resume/adopt it. |

The strongest retained schema evidence is under:

`C:\Users\Huma.Khan\AppData\Local\Cosmix\PartAContinuation\Database20261013`

| Relative evidence path | Safe retained fields |
| --- | --- |
| `target-binding.json` | Names `CosmixUat_d893653346`, catalog `Cosmix_Testing`, pipe suffix `D3CA674F`, operator above; selection says existing isolated testing database. |
| `CosmixUat_d893653346-inventory.json` | Later **UnselectedReadOnlyInventory**: same instance name, pipe suffix `673B4355`, 12 migrations, 5 accounts, 14 employees. |
| `known-backup-header.json` | Historical server `Software-PC1\LOCALDB#842DD4FB`, catalog `Cosmix_Testing`, family `d9c34627-18a2-41cf-a804-821e39516303`, copy-only/checksummed backup. |
| `original-upgrade-f90875ab8719494484783114825f0331/result.json` | Source is catalog name only; 3 known fixture matches among 5 accounts/14 employees. |
| `b24-original-86e3870eda664c97bf82adf5575f3e0c/result.json` | Original B24 result described above. SHA256 `E878966944E0760CB81665E219490597EA72420A85D5BF981549E0CBCBCD1913`. |
| `b24-clone-6c709a6d194f4bbc8aaac6814220c643/result.json` | Clone result described above. SHA256 `DBFA36B6682048427CC1F24873385E14DAA05227BA10B53A55ED67BE2EF1BFF4`. |

**Target identity remains unresolved.** The early selected binding and later unselected inventory are not a current binding to the upgraded original. B24 receipts name a catalog and family, not a live instance/physical-file pair. Restores can share family identity. Parent must reconcile which existing instance owns the selected original, its MDF/LDF paths and present migration history; do not pick whichever `Cosmix_Testing` answers. No pending migration range can be asserted from these stopped instances.

## Existing Tooling and Guard Compatibility

| Source | Current applicability |
| --- | --- |
| `docs/attendance-completion/Huma-Acceptance-Preflight.ps1` and `Huma-Acceptance-Common.ps1` | Good patterns: exact checkout/operator, explicit pipe/server/owner, manifest/artifact SHA256, path/reparse/ACL checks, indirect SQL dependency checks, complete history/schema/count/rowversion comparison. Hard-pinned to pipe `842DD4FB`, old artifact/run roots, baseline 12, exactly 3 accounts/11 employees/8 drafts. No finalized Huma manifest found in their required docs directory; only the incompatible example JSON exists. Do not run against the 5/14 candidate or change expected counts merely to pass. |
| `Huma-Acceptance-Auth.ps1` | Checks upgrade receipt, private configuration/runtime hashes, PID/start time/exact command and exclusive loopback port, retained key metadata and credentials; uses real MFA/CSRF. Its same old context/state guards and upgrade receipt format block current reuse. Its command matcher requires an assembly plus `--urls`; current isolated `CreateBuilder` requires zero command arguments. A new reviewed adapter must reconcile that invocation contract. |
| `src/Cosmix.Api/Development/LocalTesting.cs:20` | Existing profile `HumaExistingAttendance20261013B01` uses empty configuration builder and exact private settings path; still pins `842DD4FB` and historical server. `VerifyDatabaseAsync` requires complete migrations/model and exactly the three known account/employee triples (lines 146-172). The retained 5-account database was already documented as rejected. |
| Existing private `LocalTesting/Attendance-Host-20261013-B01.json` | Allowlisted fields confirm old pipe, integrated auth, `Cosmix_Testing`, localhost:7481, local SMS adapter and disabled AttendanceJobs. No external SMS endpoint/token configured in those checked fields. Not a current owned-process receipt. No secret values printed. |
| `docs/access-provisioning/RunAuthenticatedAcceptance.ps1`, `StartAcceptanceHost.ps1`, `StopAcceptanceHost.ps1` | Hard-coded `C:\Users\Affan.Ahmed\...\AccessAcceptance\20261009` and port 7249. Access runner provisions roles/scopes and writes evidence; starter launches a host. Reference code only, not runnable for this target. |
| `RunExistingAttendanceWeb.ps1` | Huma private root, port 7481, real login/CSRF/logout checks; B01/B02 receipt names already exist. It does not independently establish current SQL/process identity. Do not erase receipts or rerun it. |
| `RunExistingAttendanceSql.cs` | Old pipe/server plus migration-12/21/22 modes, including write-capable modes. Not a current 24-migration preflight. |
| `scripts/Start-LocalTesting.ps1`, `LocalTestingSetup.cs` | Build/start/setup paths can create/migrate/seed or generate credentials. Do not execute for reuse. |
| `FreshLocalTestingGuard.cs`, `FreshLocalTestingDatabase.cs`, `Invoke-Fresh-SQL-Setup.ps1` | Separate fresh profile with physical-file/provider checks; executor has create/start/schema/key/seed stages. The partial A01 attempt is not the retained original and must not be retried here. |
| `docs/access-provisioning/LocalAcceptanceHttp.ps1` | Reusable helper after independent target/host validation. Actual password challenge, pre-MFA 401, local OTP proof, identity-bound CSRF refresh and current permissions. Returns live clients: never print/serialize them. Its local-mode probe alone does not identify the database instance. |

## Worker and Actor Gates

`LocalTesting.RemoveUnboundedWorkers` (line 132) removes EmployeeLifecycle, AttendanceOutbox, ZoneRetention and MissingCheckoutReminder only. `Program.cs:45` still registers **DocumentExpiryReminderWorker**, which has no enable option, waits 15 seconds and can `SaveChangesAsync` notification rows (`DocumentExpiryReminderWorker.cs:20,133`). Startup would therefore exceed an auth/read-only test budget. Parent must exclude it explicitly from the bounded acceptance host and verify actual registrations. Do not invent a disable setting it does not consume.

Keep FaceRetirementWorker explicitly disabled and all face/storage-retirement/provider paths off. OTP dispatch is needed for real login and writes delivery state; inspect pending delivery metadata before enabling it so unrelated queued work is not processed. Data Protection initialization/rotation may write keys despite retaining a directory; key persistence must be checked and any required key write explicitly scoped before hosting.

Only the three original synthetic aliases are established by fixture code and retained match counts: SuperAdmin and Admin are web-only fixed roles; HR has web plus mobile eligibility. All three original assignments use **All** scope. HR's bootstrap permission list contains employee/financial/medical/bank reads but not `hr.reports.read` or `hr.reports.export`. Later grants may differ; current effective permissions are unknown. Private `Test-Accounts.json` exists, owned by the current operator with only that operator SID in its inspected ACL; contents/passwords were not read.

Two additional accounts and eleven employee records beyond the three known fixtures lack a complete synthetic classification in the evidence inspected. Older 11-employee statements and historical role receipts cannot authorize this 14-employee population. Do not log in as extras, query HR details, alter grants or infer report scope from bootstrap code. Current Self/Team/Site/Department/read-only-export-denied/organization-only actors are **not verified available**. The local OTP adapter and HTTP helper allow only the original three aliases/phones.

## Exact Reuse Proposal

1. **Resolve retained original, no replacement provision.** Parent reconciles the receipts to one existing named instance, `Cosmix_Testing`, owner/operator, family and exact two physical files. Confirm provenance for every 5-account/14-employee record and allowed report source data. If that classification remains ambiguous, stop writes and positive data reads; do not broaden the fixture guard. Existing general reuse approval remains conditional on this verification.
2. **Operational access.** This sidecar forbids starts. Once the selected existing instance is unambiguous, parent needs authority to start that instance and later one owned HTTPS loopback host. Do not create an instance or connect using an auto-start alias. Resolve the new explicit pipe after the authorized start; historical pipes are not usable configuration. Inspect SQL metadata first. Zero migrations/seeds are proposed; if history differs, return the exact pending range for separate review/backup instead of applying it.
3. **Parent-owned bounded profile.** Pin current binary/source/helper hashes, original instance/server/owner/files, all allowed synthetic IDs and exact effective permission assignments; preserve existing keys and original settings. Use a separate reviewed process configuration and owned PID/start/port receipt. Keep current authentication, exact actor validation and business-data boundaries; do not relax the legacy guard globally. Exclude all non-OTP business workers including DocumentExpiryReminderWorker. Review key initialization and queued OTP side effects before startup. This profile/manifest does not currently exist.
4. **First HTTP slice, no fixture/grant edits.** At most three login challenges, three local OTP deliveries/verifications, three sessions and three logouts, one per known alias; no resend/retry on failure. Allow only associated auth/session/audit records and explicitly audited protected reads/exports. Use `https://localhost:7481` only if free and owned by the reviewed host, with trusted certificate. Keep live clients and response bodies private. Limit business date to `2026-10-13`, employee/site/department IDs to the private allowlist, report page 1/pageSize 10, exports to the same filter. No business writes, new employees/sites/policies, raw punches, corrections, settlements, external calls or keys are proposed in this slice.
5. **B1/B2 checks after repaired build is frozen.** Anonymous/pre-MFA denial, real web MFA, missing-CSRF denial, current permissions, permitted scoped tracking GET where a valid existing grant exists, all 14 report permission decisions and distinct export checks; at most one tracking read and one retired-route denial per authorized alias, and at most one preview plus CSV and XLSX request per report/alias. Verify zero punch/receipt/success-audit changes on retired tracking POST; denial/security audits may occur. If a response exposes an unexpected employee ID, stop without printing its body. Logout and replay retained cookies to prove persisted revocation. SQL proofs are aggregate deltas/allowlist-membership checks, not exported personnel rows.
6. **Remaining B coverage is separately bounded.** True positive/negative Self/Team/Site/Department/All intersections need confirmed populated scoped fixtures; report-read-without-export and organization-only actors need exact existing grants or a proposed role/assignment budget. Mobile management denial needs a real authorized mobile device/session workflow, not a forged token. Role revocation/replay requires a specified disposable assignment and authorized mutation. Missing rows cannot count as a positive scope test. Do not change a shared fixed role or issue new credentials just to complete this matrix. Tracking correction positive writes and report value correctness remain outside the first slice.

The request budget above is a proposed maximum, not permission to run with an unresolved target. A protected GET/export can create audit state; HTTP tests are not SQL read-only. Parent must bind these expected effects to the final run manifest before execution.

## Concrete Commands and SQL

These commands can repeat the non-mutating checks now; they do not connect or start SQL. No existing write-capable runner needs to execute for this step.

```powershell
Set-Location 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend'
sqllocaldb info MSSQLLocalDB
sqllocaldb info CosmixUat_d893653346
sqllocaldb info CosmixAttendanceHuma20261013A01
Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='Cosmix.Api.exe'" |
    Select-Object ProcessId, Name, ExecutablePath, CreationDate
Get-NetTCPConnection -State Listen |
    Where-Object LocalPort -eq 7481 |
    Select-Object LocalAddress, LocalPort, OwningProcess
```

After parent has resolved and started only the authorized existing target, execute these SELECTs through a single explicitly pinned integrated-auth connection to `Cosmix_Testing`, not a LocalDB alias or fallback. Validate identity/files before proceeding to history. No such connection was made here.

```sql
SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')) AS ServerName,
       DB_NAME() AS DatabaseName, ORIGINAL_LOGIN() AS OperatorName,
       SUSER_SNAME(d.owner_sid) AS OwnerName, d.state_desc, d.is_read_only,
       r.family_guid,
       HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION') AS CanViewDefinition
FROM sys.databases AS d
JOIN sys.database_recovery_status AS r ON r.database_id = d.database_id
WHERE d.database_id = DB_ID();
SELECT type_desc, physical_name FROM sys.database_files ORDER BY file_id;
SELECT COUNT_BIG(*) AS SynonymCount FROM sys.synonyms;
SELECT COUNT_BIG(*) AS ExternalDependencyCount
FROM sys.sql_expression_dependencies
WHERE referenced_server_name IS NOT NULL OR referenced_database_name IS NOT NULL;
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT s.name AS SchemaName, t.name AS TableName, SUM(p.rows) AS MetadataRows
FROM sys.tables AS t JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY s.name, t.name ORDER BY s.name, t.name;
```

Metadata row counts are approximate and do not establish provenance. Detailed actor/scope inventory must be restricted to the subsequently approved synthetic allowlist. An unavailable identity/family/metadata field is a failed guard, not a reason to skip it.

The exact existing helper for the later reviewed HTTP harness is `LocalAcceptanceHttp.ps1`: `New-LocalAcceptanceSession -BaseUri 'https://localhost:7481/' -Account $approvedPrivateAccount`, then `Invoke-LocalAcceptanceRequest -Client $client -Method GET -Path '/api/v1/me/permissions'`. Capture both return values in memory; do not invoke now. Module routes are `GET /api/v1/hr/employees/{id}/tracking`, retired `POST .../tracking/record-checkin`, `GET /api/v1/hr/reports/{reportType}` and `POST /api/v1/hr/reports/{reportType}/export`. Parent must finalize expected statuses/body schemas from the repaired controllers before sending requests. There is currently **no valid existing end-to-end command** that safely passes the retained target guards; claiming one would conceal the blockers.

## Required Access or Decision

- Resolve the selected original's current instance/physical files and confirm synthetic ownership of the entire retained 5/14 population. Receipts alone do not resolve the conflicting target binding. If any business data is real or ambiguous, reuse for these HTTP tests is blocked; no writes.
- Parent must supply/review the bounded host/profile manifest and fix its target, actor, worker and process-invocation bindings. These are implementation prerequisites, not a request to reapprove the A+B repairs.
- Instance/host startup, local auth/session/audit writes and any unavoidable Data Protection writes are outside this sidecar. Parent must apply only the operational scope actually authorized; key creation, extra grants/fixtures or ambiguous-data access are not inferred from general reuse approval.
- Until these gates pass: source/standalone testing can continue; live HTTP, mobile/scoped-positive tests and A2 acceptance remain pending. No production/provider/financial readiness claim.

## Reproducibility

Observed source SHA256 values (parent should recapture after concurrent repairs):

| File | SHA256 |
| --- | --- |
| `src/Cosmix.Api/Development/LocalTesting.cs` | `92FFCEED15B7A7C744DAEAF28BB5A4947217C76C8DB7710364CB8B21121FDB9D` |
| `src/Cosmix.Api/Program.cs` | `1C1025C84D8D6FF53F078DB858EE250467E47BEC8A0DD7A39D350B1F3709BF17` |
| `src/Cosmix.Api/BackgroundJobs/DocumentExpiryReminderWorker.cs` | `70E88609652A96BE737287FAEE4F66917DFC049EDF73DA8044A1A1E1F8A2742B` |
| `docs/attendance-completion/Huma-Acceptance-Common.ps1` | `785BA2700EC895750864A84AE65FFA207BD0B37F9D1BFB05E4BEE93656E2973C` |
| `docs/access-provisioning/LocalAcceptanceHttp.ps1` | `898546EAD2479964114004839D9E18FBF678F0550818C7402290942497BFA359` |
