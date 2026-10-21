# Part A continuation: read-only local inventory

Observed 13 October 2026 in the nested `Cosmix_Erp_software/cosmix-backend` checkout. This inventory did not connect SQL, start any host/service/instance, apply SQL, read credentials/key bytes/employee payloads, send OTP or carrier messages, edit configuration, or provision external services.

## Current local state

| Item | Evidence and limit |
|---|---|
| Tooling | `dotnet`, `sqlcmd`, SqlLocalDB and Docker CLI exist. .NET 10 SDK, Release assemblies and restore manifests exist; file presence is not current build success. |
| LocalDB instances | `MSSQLLocalDB`, `CosmixUat_d893653346`, and `CosmixAttendanceHuma20261013A01` exist. Each reports Stopped; owners match the current Windows operator. No instance was started. |
| Other SQL service | `MSSQLSERVER` is Running, with SQL process 7952 and loopback port 1434. This inventory does not identify that service as the approved testing target. |
| API | No listener on the retained owned API port 7481. Existing dotnet processes are not assumed to be owned acceptance hosts. |
| Docker/face | Docker service is stopped; `docker ps` could not reach the engine. No configured CompreFace endpoint/API keys or reviewed SilentFace ONNX models were observed in repository configuration, current user-secrets, known testing settings or relevant current process environment. |
| Scanner | ClamAV binaries are installed privately under `LocalAppData/Cosmix/Scanner/clamav-1.5.4.win.x64` although absent from PATH. Known settings and clamd configuration bind loopback port **13310**. No process or listener was observed there. Main/daily/bytecode signature files exist and have 8 October file timestamps; freshness and detection are unverified. |

## Target and private configuration

The known `LocalTesting/settings.json` parses successfully and selects exact `Cosmix_Testing` with integrated Windows authentication and a local named pipe. Local testing, web authentication and mobile authentication switches are true. SMS explicitly uses `LocalTesting`, with no external endpoint/token. Scanner configuration is enabled and loopback. The former documented SQL pipe is currently absent; it must not be treated as reachable or silently replaced with an alias.

Current user-secrets instead select local `Cosmix_Development`. They are not acceptance settings. The repository base/development configuration has no live database connection or provider configuration. No production deployment target was identified by the inspected sources.

The retained LocalTesting root has a protected ACL and no reparse-point attribute. Settings, synthetic-account file and the two separate 32-byte file/snapshot keys exist; known file ACLs permit no unexpected principal. Key bytes and account contents were not read. Metadata reports three key files, one snapshot and 109 encrypted object files; this is not object correctness or restore evidence.

`LocalTesting/Attendance-Host-20261013-B01.json` is a **private configuration overlay**, not process-ownership metadata. It must not be printed. The source host preparer refuses overwrite and creates a keyring; it must not be blindly rerun. The source-laptop `StartAcceptanceHost.ps1` hard-codes another user's paths and overwrites its host record, so is unsuitable for this continuation.

The newer `AttendanceAcceptance/Huma20261013A01` root is a retained **partial fresh setup**. Its configuration, SQL and results directories are empty; keys contain only the empty Data Protection directory. Four stage records show private-root completion, new-instance start/completion, then `NewInstanceMetadata` failure. Current metadata agrees with the superseding Fresh Executor Review: no fresh catalog/settings/account/key success is established. Do not reinitialize, reset or adopt this partial target.

## Retained evidence

The latest Attendance verification and private bounded result records agree that the original synthetic testing target previously reached **Office migration 22**, with three synthetic authentication accounts and eleven retained employees. The B05 result has 22 history entries, retained-row preservation true and new Office tables empty. This inventory did not refresh those facts from SQL.

B02 records synthetic actors, real web MFA and three actors. B04's independent shift proof records two shifts, two schedule versions, four committed receipts, four business audits, eight protected reads and wrong-purpose denial. These are retained bounded evidence, not fresh Part A23 acceptance.

The B06 Office attempt has retained original/continuation intent, resources and role files, with no completed result. Latest documentation says it stopped at bounded local OTP delivery failure. Reconcile retained writes and authentication state before continuing; do not rerun it as a fresh operation.

B01 and B05 COPY_ONLY/CHECKSUM `.bak` files exist, approximately 18.5 MB and 19.3 MB. Historical HEADERONLY/VERIFYONLY checks are recorded. No actual restore drill or coordinated database/private-object/key backup was found in the inspected evidence. VERIFYONLY is not the true restore required by the A23 upgrade runbook.

## Safe next read-only operations

These commands do not select or start a replacement target:

```powershell
SqlLocalDB info MSSQLLocalDB
SqlLocalDB info CosmixUat_d893653346
SqlLocalDB info CosmixAttendanceHuma20261013A01
.\scripts\Test-AcceptanceReadiness.ps1 -AsJson
netstat -ano -p tcp | Select-String -Pattern ':(13310|7481)\s'
```

After root establishes the exact currently running pipe/server/owner/catalog binding, the following SELECT-only query can refresh safe identity/history/count evidence. `$verifiedPipe` must come from the selected target's metadata; it is not permission to guess an alias or connection:

```powershell
sqlcmd -S $verifiedPipe -d Cosmix_Testing -E -C -b -l 5 -Q "SET NOCOUNT ON; SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName, ORIGINAL_LOGIN() AS OperatorName, DB_NAME() AS DatabaseName; SELECT SUSER_SNAME(owner_sid) AS DatabaseOwner,is_read_only FROM sys.databases WHERE database_id=DB_ID(); SELECT MigrationId,ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId; SELECT COUNT_BIG(*) AS AccountCount FROM auth.UserAccounts; SELECT COUNT_BIG(*) AS EmployeeCount FROM hr.Employees;"
```

The current `RunExistingAttendanceSql.cs` accepts only source chains of 21/22 migrations and pins the former pipe. It cannot verify/apply A23 or a later coordinated B migration unchanged. Older manifests/runners also have machine-specific hashes and fixture assumptions. Root must prepare a current continuation runner and exact pending range after coordination with B; do not run an unbounded update-to-latest.

## Remaining concrete inputs and work

- Root-owned target selection and live identity/baseline refresh, exact current pending migration range and coordinated schema window. The retained historical synthetic target is available as evidence; no production target is configured by the inspected sources.
- Reconciliation of the partial B06 writes and any due lifecycle/access cleanup before host startup. A new owned-process record must bind actual process creation time, binary/configuration hashes and localhost URI.
- A current combined backup of SQL, encrypted objects and all decryption dependencies, plus a true restore into separately identified isolated physical files/private paths and proof that restored evidence decrypts. Preserve originals and partial setup.
- Actual scanner clean/standard antimalware/quarantine/failure acceptance using the installed local binary, bounded private samples and current signatures. Presence of binaries or signatures does not pass this gate.
- Actual Part A HTTP/SQL/retry/concurrency/current-revocation/private-file acceptance against the selected target. Existing local web MFA evidence does not establish every recovery or mobile/device flow; the local OTP viewer supports WebLogin only.
- For carrier acceptance, an approved provider/policy, privately supplied endpoint/credential and an approved real test destination. Inspected configuration contains none. The fictional local-testing phones cannot establish carrier delivery.
- For real biometric acceptance, running reviewed deployment and reviewed ONNX artifact/contract plus actual capture/device tests. These prerequisites remain absent in the inspected setup; earlier user deferral remains recorded.
- Any required management policy/authority decisions must use genuine approved values; do not seed invented production policy or real ownership evidence to clear acceptance gates.

The inventory executed only metadata reads and the offline readiness script. It did not establish behavioral or production acceptance.
