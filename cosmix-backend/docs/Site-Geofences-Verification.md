# Site Geofences verification — updated 9 October 2026

## Approved isolated SQL and authenticated acceptance

On 9 October the user explicitly approved a fresh isolated `Cosmix_Testing`
database, synthetic fixtures and private storage. All 13 migrations were applied
only to that new local testing database, with a verified checksummed backup before
acceptance fixtures. No development or production database was modified. Detailed
evidence remains under `%USERPROFILE%\AppData\Local\Cosmix\AccessAcceptance\20261009`,
outside the repository.

The combined [scheduling/geofence run](group-rosters/Verification.md) passed 62
authenticated HTTP checks. Its geofence cases exercised actual fixed Admin creation
of a future version, HR denial, correct effective-boundary read and stale-version
409. The separate 83-check SQL/private verifier matched the geofence receipt's
original organization journal to the accepted HTTP result. After a real host
restart and fresh MFA login, all 13 original scheduling/geofence operations replayed
their frozen results: 26 additional checks, cumulative 88. These counts include
scheduling; they are not 62 or 83 distinct geofence scenarios.

The later supplemental scheduling run passed 42 transfer/holiday checks and 16
replay checks after fresh login against the same host (58 cumulative). Revoking only the original Site A roster grant
invalidated the prior Admin session (401); fresh MFA then produced all nine expected
403 scheduling replay denials, bringing the main run to 97 checks. These are roster
authority results, not additional geofence revocation cases. The actual isolated
database constraint audit found no violations, no disabled/untrusted foreign keys,
and all 13 migrations recorded. A separate nine-check competing shift-update run
returned exactly one 200 and one 409 for different keys sharing one rowversion,
then confirmed only the winner persisted with unchanged schedule history. These
are scheduling concurrency results, not a geofence contention test.

The web flow used genuine password challenge, browser-bound OTP proof, persisted
sessions and authenticated CSRF; OTP delivery used the guarded local adapter.
The acceptance helper required two corrections for byte-array JSON responses and
retained request headers. A narrowly verified scheduling checkpoint resumed the
same fixture without recreating data. Production code was unchanged in this live
acceptance continuation. See the [runner notes](group-rosters/Isolated-Acceptance-Runner.md).

Real mobile/Bearer and carrier behavior, deployment provider setup, and operational
radius/accuracy approval remain separate work. This run makes no face verification,
punch, fake-location, attendance/payroll or production-deployment claim. Geofence
approval remains the authorized administrator's external-approval attestation; the
synthetic fixture reference does not demonstrate an electronic approval chain.

## Historical baseline — 8 October 2026

The following preserves the earlier verification phase. Its statements that no SQL
or authenticated acceptance had run were superseded by the isolated results above.

Local feature files were added to the Desktop `cosmix-backend` checkout on its existing `develop` branch. No commit, branch, merge, push, pull, fetch, PR or GitHub operation was performed for this implementation. No database/schema changes or real employee fixtures were used. The solution still contains its four application projects; no test project or package was added.

## Executed checks

| Check | Result | Scope |
|---|---|---|
| `dotnet build Cosmix.Backend.sln --configuration Release --no-restore` | Passed, 0 warnings/errors | All four application projects |
| `VerifyBoundary.cs` | 32 passed | Pure geometry, coordinate/accuracy validation, inclusive radius/accuracy, no accuracy-radius expansion, antimeridian/poles/antipodal stability |
| `VerifyService.cs` | 55 passed | Real service plus OrganizationOperation, in-memory collaborators: version dates, future selection, historical reads after deactivation, expected versions, locked periods, evidence/precision validation, audit/receipt staging, identical/changed retries, revoked-grant replay, fixed-admin authority and self identity/site status |
| `VerifyModel.cs` | 21 passed | Real SQL Server EF model matches snapshot, existing precision/date/unique/FK constraints, sync/async append-only guards, DI resolution; zero database connection attempts |
| `VerifyHttp.ps1` | 14 passed | All six routes in generated OpenAPI with security/description metadata; all six reject anonymous requests with 401; 201 Location metadata; self endpoint accepts no identity/date parameters |

The boundary and service harnesses were written before their implementations and initially failed because the implementation types were absent. The standalone service harness uses runtime proxy collaborators and explicitly disables native AOT; no production AOT/build setting was changed. Each harness uses its own disabled lock-file target to avoid file-based app lock conflicts.

HTTP checks ran against a short-lived hidden local host using the built Release DLL, authentication providers disabled and a synthetic unusable database address. The host was stopped in `finally`. The initial sandboxed run started the host but could not access its loopback socket; the isolated check passed with the required local socket access. No external provider or authenticated business request was exercised.

An independent source review found no blocking defect in authority, self scoping, effective dates, immutable storage or replay handling. Its minor finding—missing OpenAPI Location header metadata—was fixed and covered by the HTTP check.

## Reproduce

From the repository root in PowerShell, first build as above. Then use the .NET 10 standalone file runners (no test projects):

```powershell
$env:TEMP = Join-Path $PWD 'artifacts/site-geofences-temp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Path $env:TEMP -Force | Out-Null
dotnet run --file docs/site-geofences/VerifyBoundary.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Domain --property:NuGetAudit=false
dotnet run --file docs/site-geofences/VerifyService.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Application --property:NuGetAudit=false
dotnet run --file docs/site-geofences/VerifyModel.cs --configuration Release --property:RestoreSources=$PWD/src/Cosmix.Infrastructure --property:NuGetAudit=false
./docs/site-geofences/VerifyHttp.ps1
```

Dependencies must already be restored/cached for the local-only restore sources. TEMP/TMP overrides apply only to this shell; artifacts are ignored by Git. OpenAPI output and temporary host logs go under `artifacts/site-geofences-http`. The HTTP script requires local socket/process access and stops only the process it starts.

## Historical 8 October acceptance gaps (superseded by current evidence)

The follow-up [access verification](access-provisioning/Verification.md) rechecks the real
service and registered HTTP contract alongside scheduling. Geofences retain their existing
bootstrap-supported permissions; the new scheduling first-grant gap does not apply to them.
The concrete [isolated acceptance plan](access-provisioning/Isolated-Acceptance-Plan.md) is
proposed only and requires the user's fresh approval before any fixture/database write.

- No live SQL insert/rollback or actual concurrent authenticated HTTP writes were tested. Atomic version/audit/receipt persistence and row/index contention require an agreed isolated SQL fixture and real session setup. Service checks establish the business decisions with in-memory collaborators, not SQL behavior.
- Actual successful web MFA/cookie/CSRF and mobile Bearer flows, access revocation during a request, deployed Site permissions and approved radius/accuracy inputs remain operational acceptance.
- Management approval is an authorized administrator's external-approval attestation/reference. No electronic approval-chain verification is claimed.
- Boundary configuration begins strictly after today's business date, including the first version. Retrospective edits, replacing/cancelling scheduled versions, fake location detection, faces, punches, ATT-007 review events, zones and attendance/payroll calculations are not implemented by this feature.
