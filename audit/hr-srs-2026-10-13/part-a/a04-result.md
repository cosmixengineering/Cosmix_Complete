# A04 employee lifecycle implementation result

Implemented in `C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend` on 13 October 2026.
The approved plan's code work is present. Retirement/suspension release policy activation, new-episode
rehire, applied SQL migration, real SMS/device/biometric acceptance and physical biometric disposal remain open.

## Behavior delivered

- Extended Active/Suspended -> Retired and Suspended -> Active through the existing HR request and independent
  fixed Admin decision routes. New transitions require configured approval version, effective window and
  their explicit enable flag. Defaults remain disabled; baseline suspension/resignation/termination remains.
- Schema-2 private request snapshots and immutable status history retain the approved lifecycle policy
  version. The reviewer rejects requests if the configured version changed. Schema-1 baseline requests remain
  readable and cannot supply unapproved new transition evidence.
- Added an applied-status marker and ordered reconciliation so a delayed Active -> Suspended -> Active cycle
  cannot skip the suspension merely because the final status equals the old cached status.
- Effective transitions disable both channels and revoke sessions, open OTP challenges, registered devices
  and unrevoked role assignments. Reasons carry the approved status-change ID; dated records remain.
- Release only prepares an account whose suspension came from this lifecycle workflow, with channels disabled.
  Independent Disabled/Suspended account restrictions remain in force. No old grants/devices/sessions restore.
- Logically revoke active face enrollments during both offboarding and release, including leftovers from an
  older implementation. Existing `FaceEnrollment.Revoke` is used; encrypted template references/evidence stay.
- Added fixed-Admin, dedicated-permission archive/unarchive with reason, employee version, idempotency and
  append-only actor/role/state/time history. Only approved ended employment is eligible. Self/protected-owner
  actions fail, including protected-owner checks before receipt replay. Unarchive never changes employment.
- Employee directory filters archived rows before counting/pagination: exclude by default, `includeArchived`,
  or `archivedOnly` (precedence). Profile/summary/lifecycle metadata includes archive state.
- Terminal rehire explicitly returns `employee_rehire_requires_approved_employment_episode_policy` (503).
  Existing onboarding is not reused as a fabricated new employment episode.

## Changed files owned by A04

- `src/Cosmix.Domain/HR/Employees/Employee.cs` — lifecycle/marker/archive hunks only; A03 phones preserved.
- `src/Cosmix.Domain/HR/Employees/EmployeeStatusChange.cs`
- `src/Cosmix.Domain/HR/Employees/EmployeeArchiveChange.cs` (new)
- `src/Cosmix.Domain/Authentication/UserAccount.cs` — approved targeted lifecycle release method only.
- `src/Cosmix.Application/HR/Employees/EmployeeLifecycleContracts.cs`
- `src/Cosmix.Application/HR/Employees/EmployeeLifecyclePolicy.cs` (new)
- `src/Cosmix.Application/HR/Employees/EmployeeLifecycleService.cs`
- `src/Cosmix.Application/HR/Employees/EmployeeLifecycleProcessor.cs`
- `src/Cosmix.Application/HR/Employees/EmployeeHiringContracts.cs` — archive query/summary hunks only.
- `src/Cosmix.Application/HR/Employees/EmployeeHiringService.cs` — summary archive flag hunk only.
- `src/Cosmix.Infrastructure/Repositories/HR/EmployeeLifecycleRepository.cs`
- `src/Cosmix.Infrastructure/Repositories/HR/EmployeeHiringRepository.cs` — list filter/summary hunks only.
- `src/Cosmix.Infrastructure/Database/Configurations/HR/EmployeeConfiguration.cs`
- `src/Cosmix.Infrastructure/Database/Configurations/HR/EmployeeStatusChangeConfiguration.cs`
- `src/Cosmix.Infrastructure/Database/Configurations/HR/EmployeeArchiveChangeConfiguration.cs` (new)
- `src/Cosmix.Api/Controllers/HR/EmployeeLifecycleController.cs`
- `docs/Employee-Lifecycle.md` (new)

The existing lifecycle worker consumes the new processor without changing its polling configuration.
Root owns shared DI, DbContext, permission catalog, live authentication/access marker guards and coordinated
migration/snapshot/SQL. No Attendance domain, provider, storage/disposal implementation or runtime settings changed.

## Executed verification

Temporary console harnesses compile real Domain/Application code; synthetic entities and test-only
repository/file/clock adapters replace persistence/provider IO. No permanent test project was added.

Initial red Domain run exited 1: `FAIL approved retirement transition: This employment status transition is not supported.`
Retirement policy evidence and its rejection without policy evidence are covered by the final Domain checks.

The processor regression red run compiles the original `git show HEAD:src/Cosmix.Application/HR/Employees/EmployeeLifecycleProcessor.cs`
under a test-only `LegacyEmployeeLifecycleProcessor` name alongside current Domain/Application dependencies.
It exits 1 with five intended failures: enabled old channels; skipped delayed cycle; release disabled the
account; overwritten independent suspension; protected owner offboarding. New policy/archive tests already
pass in that comparison because only the processor is the original baseline. This is documented negative
regression evidence, not a claim that the entire baseline contained the new policy/archive code.

Final checks (fresh after the last A04 production edits):

```powershell
dotnet run --project "$env:TEMP\cosmix-a04-lifecycle-check\Check.csproj" --configuration Release --artifacts-path "$env:TEMP\cosmix-a04-lifecycle-check\artifacts"
# exit 0; 7 domain checks passed
dotnet run --project "$env:TEMP\cosmix-a04-processor-check\Check.csproj" --configuration Release --artifacts-path "$env:TEMP\cosmix-a04-processor-check\artifacts" -- --baseline
# exit 1; five intended original-processor regressions
dotnet run --project "$env:TEMP\cosmix-a04-processor-check\Check.csproj" --configuration Release --artifacts-path "$env:TEMP\cosmix-a04-processor-check\artifacts"
# exit 0; 15 policy/processor/archive-service checks passed
```

Output: [domain green](a04-domain-green.txt), [processor red](a04-processor-red.txt),
[processor/service green](a04-processor-green.txt). Release Application compilation succeeded through the
temporary project reference without warnings/errors. Root coordinates the full Release solution build and
runtime EF model/snapshot/directory-query verification; those results are not asserted by this subtask.

## Integration and open decisions

- Bind `EmployeeLifecycleOptions.SectionName` (`EmployeeLifecycle`) and register `EmployeeLifecyclePolicy`.
  Add dedicated catalog `EmployeeLifecyclePermissions.Archive`; existing roles receive no automatic grants.
- Add nullable same-employee composite applied-marker FK `(LastAppliedStatusChangeId, Id)` -> status changes
  `(Id, EmployeeId)` with alternate key and restrictive deletion. Root's reviewed migration backfills only
  uniquely timed, fully approved due evidence matching cached employee status; invalid legacy records stay
  unmarked for review. New archive state/date checks and journal mapping are in the A04 configurations.
- Live authentication/access must deny a due Active release until its applied marker matches, otherwise
  worker delay could leave previous access effective. Root owns these A01 shared checks.
- Management must approve retirement/release policy values and dates before activation. Their source
  implementation is available, but no policy was enabled by this work.
- Rehire requires an approved new employment episode model/authority/effective boundaries. It remains
  explicitly unavailable, rather than pretending the previous approval covers another employment.
- Face use is logically retired here. Part B05 owns approved retention, durable physical disposal, backup
  treatment and provider acceptance; no ciphertext deletion or physical retirement claim is made.
- Full HTTP/SQL race/restart acceptance, genuine devices/face/SMS, directory query SQL verification and
  coordinated migration application remain separate acceptance. No SQL/provider/account/runtime changes,
  commit or push occurred in A04.
