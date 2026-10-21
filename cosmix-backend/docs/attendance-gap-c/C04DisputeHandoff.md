# C04 Employee KPI Disputes

Implemented directly in the existing checkout. This handoff covers only the new dispute/self-read files below. The parent owns the KPI engine/source/service/final reader/contracts, core DI, permission and HTTP catalogs, and full Release integration. This task compiled and ran only the standalone checks against the parent's frozen Release in its explicitly granted managed slot. No product rebuild, restore, SQL, provider, host, keys, credentials, Git, worktree, branch, commit, PR or automation operation was performed by this task.

## Files

- `src/Cosmix.Application/Attendance/Kpi/KpiDisputeContracts.cs`
- `src/Cosmix.Application/Attendance/Kpi/KpiDisputeRules.cs`
- `src/Cosmix.Application/Attendance/Kpi/KpiDisputeAuthority.cs`
- `src/Cosmix.Application/Attendance/Kpi/KpiDisputeEvidenceReader.cs`
- `src/Cosmix.Application/Attendance/Kpi/KpiDisputeService.cs`
- `src/Cosmix.Application/Attendance/Kpi/KpiDisputeRegistration.cs`
- `src/Cosmix.Infrastructure/Repositories/Attendance/KpiDisputeRepository.cs`
- `src/Cosmix.Infrastructure/Repositories/Attendance/KpiDisputeRepositoryRegistration.cs`
- `src/Cosmix.Api/Controllers/Attendance/MyKpiController.cs`
- `src/Cosmix.Api/Controllers/Attendance/KpiDisputesController.cs`
- `docs/attendance-gap-c/C04DisputeVerify.cs`
- `docs/attendance-gap-c/C04DisputeRepositoryVerify.cs`
- `docs/attendance-gap-c/C04DisputeHandoff.md`

All implementation files are frozen after the parent-reported CS0411 corrections. The three list/detail `AttendanceOperation.ReadAsync` calls now specify their DTO type explicitly. The frozen service SHA-256 is `35EA72C8D801FCB021059D8F04EB6582B81FA324C0A6E355F3468BC8E64E7612`.

## Frozen Source Hashes

All eight runtime files below matched these SHA-256 values before and after the granted managed run. No runtime file or verifier source changed during or after that run. Metadata inspection of the copied Application, Infrastructure and API assemblies found the expected dispute types and both controllers without loading or starting the API.

| File | SHA-256 |
| --- | --- |
| `src/Cosmix.Application/Attendance/Kpi/KpiDisputeContracts.cs` | `0E5A6D219CF8676EACC465A05B2FEE5DA7FFD93035A1746A110C4D2FFF079C6F` |
| `src/Cosmix.Application/Attendance/Kpi/KpiDisputeRules.cs` | `56D6DDFDDE18372FCD0C38560422F459FB0F133B0C33150757B5967C52459BF1` |
| `src/Cosmix.Application/Attendance/Kpi/KpiDisputeAuthority.cs` | `105EC20D1C3D1FBCCCC7E807DD6434170E9F213C3FB17E8492D55339C70DB0BE` |
| `src/Cosmix.Application/Attendance/Kpi/KpiDisputeEvidenceReader.cs` | `D9B86BAF8EB3E1D726236718ADD8F2E5259A3CCBC93C671E98158F2E2FF0CD8F` |
| `src/Cosmix.Application/Attendance/Kpi/KpiDisputeService.cs` | `35EA72C8D801FCB021059D8F04EB6582B81FA324C0A6E355F3468BC8E64E7612` |
| `src/Cosmix.Infrastructure/Repositories/Attendance/KpiDisputeRepository.cs` | `65EAD6D6497A9836932EC17F80A91CFE9DB9508959E7FC96E0E07A240A332B5C` |
| `src/Cosmix.Api/Controllers/Attendance/MyKpiController.cs` | `334FED6AA44458B2BEE0AE2A6CDA23F7EC559F9791318C3F1F179EFD2D648D0B` |
| `src/Cosmix.Api/Controllers/Attendance/KpiDisputesController.cs` | `D72D413EFFADEB51472B12DAE0B283B989933FCD6DE9E2EAB44F89E61AE7B474` |

## Parent Integration

Latest parent integration read: the live catalog contains `KpiDisputePermissionDefs.Definitions` once, `KpiRegistration.AddAttendanceKpiApplication` registers the service/evidence/authority helpers, and `KpiRepositoryRegistration.AddAttendanceKpiRepository` registers the dispute repository. No additional hooks are needed. The exact codes are `attendance.kpi.self` (non-sensitive self service) and `attendance.kpi.disputes.decide` (sensitive web management). There is no parent `Self` constant or shadow permission evaluator.

The disjoint extensions below remain available but are unused because the parent already wired these registrations directly. Do not invoke them in addition to the live parent hooks:

```csharp
services.AddAttendanceKpiDisputeApplication();
services.AddAttendanceKpiDisputeRepository();
```

`KpiDisputeRegistration.AddAttendanceKpiDisputeApplication` registers scoped `KpiDisputeService`, `KpiDisputeAuthority` and `KpiDisputeEvidenceReader`. `KpiDisputeRepositoryRegistration.AddAttendanceKpiDisputeRepository` binds scoped `IKpiDisputeRepository` to `KpiDisputeRepository`. The existing scoped `AttendanceOperation`, `IFinalizedKpiReader`, `IKpiSourceReader`, `IAttendanceSummaryRepository`, `INotificationOutbox`, private store, authorization/session readers and clock are reused. No EF registration, entity, table, migration, generic approval route or new worker is needed.

The source inventory found no separate HTTP operation catalog. These controllers use the existing authentication and AttendanceOperation permission boundary; actual HTTP authorization remains unverified. All routes require authentication and no-store responses:

| Method | Route | Authority |
| --- | --- | --- |
| GET | `/api/v1/me/attendance/kpi?yearFrom=...` | Self/mobile/device |
| POST | `/api/v1/me/attendance/kpi/disputes` | Self/mobile/device; Idempotency-Key |
| GET | `/api/v1/me/attendance/kpi/disputes?yearFrom=...` | Self/mobile/device |
| GET | `/api/v1/me/attendance/kpi/disputes/{id}` | Self/mobile/device |
| GET | `/api/v1/hr/employees/{employeeId}/attendance/kpi/disputes?yearFrom=...` | Independent reviewer |
| GET | `/api/v1/hr/employees/{employeeId}/attendance/kpi/disputes/{id}` | Independent reviewer |
| POST | `/api/v1/hr/employees/{employeeId}/attendance/kpi/disputes/{id}/response` | Independent reviewer; version/key |
| POST | `/api/v1/hr/employees/{employeeId}/attendance/kpi/disputes/{id}/amendment` | Independent reviewer; version/key |

## Retained Workflow

Self employee identity comes only from the authenticated actor. Every self action uses `AttendanceOperation` with `self: true, requireDevice: true`, plus a persisted session/device and timed permission recheck. The finalized reader runs inside the existing authorized transaction. The self score DTO preserves exact and configured display values, annual dates, policy/finalization/source identities, finalization date and revision. It omits actors, scopes, private references, raw manifests and review identity.

A dispute freezes the actual finalized score and current manifest-backed annual source in encrypted private storage. A new existing `ApprovalRequest` and a protected submission `AuditEvent` retain that immutable draft. Its request type includes the annual start and exact finalization ID; its subject is the server-derived employee. Reason is typed and a printable statement/reason is mandatory. Replaying a key reuses the existing attendance receipt, while a different key cannot create a duplicate active dispute for that final version. Returned resubmissions require the latest returned request/version and append a new linked draft. Old drafts and responses remain readable.

Optional evidence is limited to eight registered Standard/Other employee documents. The service verifies employee scope, current availability, clean scan/signature, human verification, validity date, exact revision/rowversion, private purpose, content length and SHA-256. Documents are rechecked after protected IO and at review; arbitrary IDs, medical/sensitive documents, absent content and withdrawn verification fail closed. No upload, download or scanner shortcut is introduced.

Independent review requires current web MFA and `attendance.kpi.disputes.decide`. The requester account and beneficiary employee are denied on detail, decision and replay; requester records are excluded from reviewer pagination. Authorization covers the complete current annual membership/source scopes and all original retained scopes, including supplemental evidence scopes for related employees where present. Saved authority retains actual timed permission intervals and the full checked scopes. Unknown timing, lost scope, expired authority and changed evidence block conclusions.

The service reuses the parent's actual saved-day manifest verification and full annual calculation proof. The standalone fixture explicitly sets `Composition = KpiCompositionStrategy.Additive`; runtime composition remains entirely policy driven. Missing policy, partial annual evidence, unresolved source inputs or missing final evidence never become a zero/default score.

Each response retains an explicit reason, the observed finalized score, actual verified source/document proof and independent authority. `ReviewedNoChange` additionally requires actual source/calculation/policy/exact/display equality with the disputed final. `CorrectionRequired` records the need for correction without performing it. Final/source drift blocks these conclusions; `Returned` can instead retain proof of the observed changed source/final version. Drift during protected review/notification IO and missing policy/document proof still block every response.

The generic approval state represents this single response stage only: Approved maps to ReviewedNoChange, Rejected to CorrectionRequired, and Returned to Returned. These states do not approve/reject/rewrite the KPI score. The public queue still shows CorrectionRequired until a verified amendment is linked. If the final itself has been superseded, a fresh dispute selects the new final; same-final returned resubmission uses the explicit previous dispute link. Existing old-final drafts remain in the annual history.

Response stages the existing generic `NotificationTemplates.Decision` through `INotificationOutbox`, addressed to the draft's actual requester account. The event identity is the immutable `ApprovalDecision.Id`; the resource is the dispute request ID. Staging uses the same transaction/token and precedes the atomic save. No arbitrary statement, score or reviewer reason is put in the notification. Receipt replay creates no second decision/outbox intent. Actual delivery remains the existing worker's separate responsibility.

CorrectionRequired is fulfilled only by linking an already independently finalized direct successor from the existing KPI prepare/rescore/review APIs. Linkage requires the exact previous final ID, next revision, different parent request/decision, a finalization at or after the dispute response, and current manifest/source equality with that successor. It appends a protected audit link and reauthorizes before replay. It does not calculate, create, silently adopt or modify a score, amount, benefit, disciplinary outcome or financial record.

## Bounds and Verification

The repository requires the existing transaction, scopes by employee/year/requester in SQL before pagination, materializes results, and caps annual candidates at 129 (128 retained requests plus overflow detection). Page size is 1-50, page number 1-1000, decision/submission/link audit lookups take two for uniqueness detection, and private document lookups take at most nine. Reviewer list authorization covers all retained candidate scopes before SQL count/page reads.

Executed: 15 read-only source checks passed, including safe DTO shape, absence of client identity selectors, all four bound-device self boundaries, independent replay guards, notification binding, parent manifest reuse, actual document content checks, bounded scoped pagination, direct successor guards and absence of score/financial mutations. These checks are not compilation or behavioral verification.

Executed: `C04DisputeVerify.cs` passed 73 checks using actual AttendanceOperation, FinalizedKpiReader, dispute service/evidence and NotificationOutbox with synthetic in-memory adapters. It covers exact/display preservation, recursive safe JSON, unknown identity selector rejection using Web JSON options, own and independent reads, duplicate/replay, stale versions, source drift and returned recovery, missing policy/final/storage, mobile device/session/account/security-version binding and revoked-device replay, web self denial, expired/unknown-timing grants, original/current/related-source scope denial, pagination before denial, registered/withdrawn/untrusted documents, immutable returned history, generic requester notification, source/authority/private-store races during protected notification/proof IO, CorrectionRequired and direct-successor linkage/replay. It simulates transaction rollback but does not prove SQL isolation, encrypted-storage behavior, actual HTTP/device access or delivery.

Executed: `C04DisputeRepositoryVerify.cs` passed 32 offline checks using the actual KpiDisputeRepository and existing EF model. It asserts all read/write/save paths reject an absent authorized transaction, inspects the actual private annual query with an offline synthetic transaction marker, verifies employee/year/requester/exclusion predicates and translated ordered bounds, rejects invalid scopes/pages/documents, and checks existing concurrency/append-only models. Connection and command interceptors forbid all SQL IO; query translation is local only. Both attempt counters remained zero. This does not execute PageAsync against a database or prove SQL transaction/isolation behavior.

Latest actual result: **105 passed (73 behavior + 32 offline repository), zero failures and zero SQL connection/command attempts.** The parent granted the managed slot after C05 returned. Both suites compiled with warnings as errors and exited zero using `VerifyManaged.ps1` and the fresh parent Release at `D:/CosmixVerification/part-c-parent-20261013/c-current/bin/Cosmix.Api/release`. The runner checked test-source and Release-input hashes before returning successfully. All eight frozen runtime source hashes matched before execution; no source correction was needed. The slot was returned after both processes completed. The behavior verifier also passed the actual parent permission-catalog registration checks. SQL, authenticated web/mobile/device acceptance, real document scanner/storage, actual notification delivery and production acceptance remain unverified.

Exact execution output: `D:/CosmixVerification/part-c-parent-20261013/managed-41bb032995144ff581c3220daa7d4dd8`.

| Evidence | Count / SHA-256 |
| --- | --- |
| `C04DisputeVerify.log` | 73 checks; `18FA53948496E8726B9756A5701250A4A90DE89A891D0C8AB24E8C9DA9B65059` |
| `C04DisputeRepositoryVerify.log` | 32 checks; `5D5749850A3D4F745B152B8AD3E22BECB1BE95A466571BB88248E6372538AFE3` |
| `inputs.json` | Frozen dependency inventory; `C7D2D9DB54138B512D5677F889405ABD8024E2CA4DBCE60BB51A936E9CCA3B3B` |
| `Cosmix.Api.dll` | `0229568F28DAB812DD812A9B7D839D6FBE51272C552CC7C210ED0CC626F8E06D` |
| `Cosmix.Application.dll` | `64EE5ED6D2A66799EDBB03A1BFE2774E95ABEAA27B74684F8EDBEAC049AD7810` |
| `Cosmix.Domain.dll` | `20BC5CD377FB934B03B64315DE04E6BA2D0F3569B7A76916BF7A565FF59BA60B` |
| `Cosmix.Infrastructure.dll` | `0272DDB4950CF359A97ED9F4E43A8720CF85F3E5B5F00C392809C87E86835F01` |

All 64 entries in the recorded Release-input inventory were rechecked after the slot returned and still matched their hashes. This read-only check started no compiler, product build or host.

Successful compiler invocations emitted no output, so `Tee-Object` did not create compile-log files. Compilation success is evidenced by the runner's zero exits and subsequent successful execution, not by nonexistent logs. Test source SHA-256 values were `0031EAA4C5365D3864B75F3329BB0F508887DA53A20139E85E52FDC9779E6CCF` (behavior) and `9500C45C557F94B7F31F67BA1EA8773356EE0693A3108DC770967AA62592A284` (repository).

## Parent Run Recipe

Run only after assigning the managed serial slot. Use the parent's managed compiler runner with an already-built frozen Release; do not use `dotnet run --file`, rebuild production or restore projects. Process TEMP/cache/home stay under a fresh D: dispute GUID. The parent runner creates a separate fresh D: managed-artifact GUID. Capacity-guard breakpoints check C: reserve immediately before its file-writing and compiler/runtime invocation lines without editing the parent runner. Each C# source is compiled as its own standalone application against copied, hash-checked Release dependencies and installed reference packs:

```powershell
$ErrorActionPreference = 'Stop'
if ((Get-PSDrive -Name C).Free -lt 350MB) { throw 'C reserve below 350MB' }
$c04VerificationRoot = Join-Path 'D:/CosmixVerification/c04-disputes-unique' ([guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $c04VerificationRoot
$c04Repo = 'C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend'
$c04Runner = Join-Path $c04Repo 'docs/attendance-gap-c/VerifyManaged.ps1'
$c04Guard = { if ((Get-PSDrive -Name C).Free -lt 350MB) { throw 'C reserve below 350MB' } }
$c04Line = 0
$c04WriteLines = @(Get-Content -LiteralPath $c04Runner | ForEach-Object {
    $c04Line++
    if ($_ -match 'CreateDirectory|FileMode\]::CreateNew|\$file\.Write|\[IO\.File\]::Copy|& \$dotnet|Tee-Object') { $c04Line }
})
$c04Breakpoints = Set-PSBreakpoint -Script $c04Runner -Line $c04WriteLines -Action $c04Guard
$c04Variables = @('TEMP','TMP','TMPDIR','DOTNET_CLI_HOME','NUGET_HTTP_CACHE_PATH','NUGET_SCRATCH','NUGET_PACKAGES',
    'DOTNET_CLI_TELEMETRY_OPTOUT','DOTNET_SKIP_FIRST_TIME_EXPERIENCE',
    'DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE','DOTNET_GENERATE_ASPNET_CERTIFICATE','MSBUILDDISABLENODEREUSE',
    'DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER')
$c04PriorEnvironment = @{}
foreach ($c04Variable in $c04Variables) {
    $c04PriorEnvironment[$c04Variable] = [Environment]::GetEnvironmentVariable($c04Variable, 'Process')
}
try {
    $env:TEMP = $c04VerificationRoot; $env:TMP = $c04VerificationRoot; $env:TMPDIR = $c04VerificationRoot
    $env:DOTNET_CLI_HOME = $c04VerificationRoot
    $env:NUGET_HTTP_CACHE_PATH = Join-Path $c04VerificationRoot 'nuget-http'
    $env:NUGET_SCRATCH = Join-Path $c04VerificationRoot 'nuget-scratch'
    $env:NUGET_PACKAGES = Join-Path $c04VerificationRoot 'nuget-packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'; $env:MSBUILDDISABLENODEREUSE = '1'
    $env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
    if ((Get-PSDrive -Name C).Free -lt 350MB) { throw 'C reserve below 350MB' }
    & $c04Runner -ReleaseDirectory 'D:/CosmixVerification/part-c-parent-20261013/c-current/bin/Cosmix.Api/release' `
        -Sources @('docs/attendance-gap-c/C04DisputeVerify.cs','docs/attendance-gap-c/C04DisputeRepositoryVerify.cs')
    if ($LASTEXITCODE -ne 0) { throw 'C04 managed standalone verification failed' }
}
finally {
    $c04Breakpoints | Remove-PSBreakpoint
    foreach ($c04Variable in $c04Variables) {
        [Environment]::SetEnvironmentVariable($c04Variable, $c04PriorEnvironment[$c04Variable], 'Process')
    }
}
```

This task's D: artifact/backup root is `D:/CosmixVerification/c04-disputes-unique/8be992eea85d4d82bcccd35764537bab`. Exact owned-file backups were hash checked before edits, except one response-projection edit where KpiDisputeRules.cs was omitted from the immediate backup set. Its pre-edit text was recovered on D: by reversing that single added line, and the recovered bytes were compared with the reverse hunk; SHA-256 is `A510F2DACFD8FE8DB918ADA469C76339F67BFF53B287B9CD946D9DCE4CB2CD6F`. This recovered copy is explicitly not a before-edit backup. No source was truncated, deleted or restored during this task.

The granted managed run used process TEMP/home/cache under `D:/CosmixVerification/c04-disputes-unique/f14d9cad90824612802a7c851589b69e`. The execution-documentation edit has an exact hash-verified prior backup at `backup-9e5e18006c524430aba53a33a3884c57/C04DisputeHandoff.md` under that root, SHA-256 `A5FB935E7832DB2390ABEB8A3EBA66B1ECCB564E8D7112428F076FFFEC973E6C`.

The latest count/source-hash documentation update has an exact prior backup at `D:/CosmixVerification/c04-disputes-unique/db26174b035c41aeaeb9e812e31351f1/C04DisputeHandoff.before-latest-counts.md`, SHA-256 `C8C97CF72DF1FAD11DBDBEFBF04D07EF3FF0AA64C4F46F1D7BF6291139F03C2D`. C: reserve was checked before each backup and edit write.
