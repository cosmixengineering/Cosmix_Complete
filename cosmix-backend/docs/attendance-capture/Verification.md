# Attendance capture verification — 9 October 2026

These checks belong to the current local implementation. No GitHub operation, database write,
real biometric, external face provider call, SMS send, real account change or test project was used.
The parent task coordinates the combined migration, final Release build and registered HTTP checks.
Migration generation is not schema deployment or SQL acceptance.

| Standalone harness | Observed result | Meaning |
|---|---:|---|
| `VerifyCaptureRules.cs` | 12 passed | Pure acceptance/review gates: offline, missing face, sequence, lock, geofence, schedule and face-free checkout |
| `VerifyCaptureModels.cs` | 30 passed | Real domain proof binding, expiry/reuse, liveness+match, revocation, verified-device gate, immutable session revisions and optional factual policy |
| `VerifyCapturePersistenceModel.cs` | 30 passed | EF metadata, historical pin FKs, optional policy FKs, append-only/face transition guards, atomic-consumption tracked guard, actual SQL translation of legacy and bounded enrollment history, audit-only failure-finalizer mutation guards; no connection opened |
| `VerifyCaptureService.cs` | 82 passed | Real capture/face services and shared authorization/replay boundary with synthetic repositories and private IO; enrollment failures, legacy replay, scoped outcome history and protected continuation regressions |
| `VerifyFaceReadiness.cs` | 10 passed | Canonical policy/provider/vault vs active compatible enrollment readiness; no provider/store IO |
| `VerifyEnrollmentJournal.cs` | 19 passed | Real Application journal with serialized synthetic IO; terminal arbitration, retry, proof and audit metadata binding |

The parent task executed the enrollment audit/readiness continuation in its coordinated verification
slot. Release build passed with **0 warnings and 0 errors**. Its latest HTTP harness passed **316 checks**
across 48 Attendance and 58 existing employee operations, including enrollment-history and correction-review contracts.
Those HTTP checks establish registrations/contracts and anonymous denial, not authenticated SQL acceptance.

- `VerifyFaceReadiness.cs`: canonical configuration vs matching-enrollment readiness; untrusted provider
  reason suppression; unavailable/malformed provider/vault and revoked enrollment cases.
- `VerifyEnrollmentJournal.cs`: real Application journal with serialized synthetic IO; one start/terminal,
  protected proof, forged-finalizer denial, failed/success race winners, late failure after success,
  independent identities for two operators using one client key, current revocation audit context,
  and receipt-expiry versus retained audit semantics.
- Expanded `VerifyCaptureService.cs`: real service rejection/outage/storage/readiness failures and retry,
  revoked-session finalization, in-flight duplicate exclusion, secret suppression and actual legacy
  generic success-receipt shape compatibility. Provider calls fail the fixture if held inside the
  authorization transaction.
- Expanded `VerifyCapturePersistenceModel.cs`: actual EF tracked-state checks prevent business
  insertion/modification in the audit-only finalizer before a save. No SQL connection is opened.

This feature's six standalone runners total **183 assertions**. The controlled journal race uses an
in-memory serializing transaction double; it proves application terminal arbitration logic, not SQL
isolation or crash durability. No real provider call, authentication fixture or database write ran.

## Enrollment history verified in the coordinated run

The subsequent `enrollment-attempts` management GET is implemented with the existing FaceRead
permission/current web MFA and original/current scopes. New real-service cases have been added to
`VerifyCaptureService.cs`: sanitized success/failure/pending history, retained history after retry
expiry, mobile/MFA/revocation denial, current-site transfer denial, 100 inaccessible candidates with
empty-page continuation, changed-filter/account cursor denial, invalid bounds and corrupted journal
provenance. `VerifyCapturePersistenceModel.cs` exercises the actual SQL Server query translator
for employee/start/time filtering, bounded order and timestamp/GUID continuation. These expanded
harnesses passed at the counts above. Shared encrypted-cursor checks separately passed 44 assertions.
No new schema/provider/permission or enrollment-history database fixture was introduced.

The service harness exercises successful factual pairing using an explicitly synthetic preverified
proof fixture; this does **not** verify liveness, a provider or production authentication. It covers
server employee/device selection, immutable pinning, repeated/changed-key replay, absent/revoked
permissions/current session, competing logical Check In, Check Out while provider unavailable,
face expiry during repository work, enrollment/provider-version mismatch, out-of-boundary and offline
evidence, site transfers, locked periods, web denial, page bounds, exact location precision, and
absence of the proof secret from capture audit snapshots.

It also counts synthetic enrollment-provider invocations: a completed enrollment retry uses the
protected receipt without another provider/template operation; changed keys/payloads and stale
expected enrollment are rejected before remote work. Dedicated template-store absence fails before
the provider. The stale pending-verification regression first failed by reaching a forbidden template
read, then passed after adding version validation to preflight while retaining the final recheck.

Representative invocation (local dependency cache only):

```powershell
dotnet run --file docs/attendance-capture/VerifyCaptureService.cs --configuration Release --property:NuGetAudit=false --property:RestoreSources=C:\Users\Affan.Ahmed\Desktop\cosmix-backend\src\Cosmix.Application
dotnet run --file docs/attendance-capture/VerifyCapturePersistenceModel.cs --configuration Release --property:NuGetAudit=false --property:RestoreSources=C:\Users\Affan.Ahmed\Desktop\cosmix-backend\src\Cosmix.Infrastructure
```

The sandbox initially denied the run-file compiler's temporary cache. Reviewed compilation escalation
ran the synthetic checks; no data-access permission was inferred. Each harness has its own restore
metadata path. One early model run encountered the file-based restore lock-path conflict and was
corrected before the recorded successful result. A fixture constructor compile error was also fixed;
neither failed run was counted as passing.

## Still outstanding

- Approved provider/runtime/enrollment/liveness/matching/retry rules, real provider acceptance and
  biometric authority/medical exception decisions.
- Dedicated template storage/key provisioning, real encrypted template acceptance, approved
  retention/offboarding/deletion lifecycle, operational enrollment-failure reconciliation
  and real SQL acceptance of the new persisted outcome journal.
- Expanded SQL schema/fixtures and authenticated mobile HTTP acceptance under a separately approved
  isolated plan. Existing scheduling/access test approval does not automatically authorize new face,
  punch or session writes.
- Actual SQL competing punches, transaction rollback, fresh-host replay and current-session lifecycle
  transitions; in-memory transaction proxies cannot establish database locking/rollback guarantees.
- Confirmed duplicate time tolerance, excessive-duration/impossible-travel limits, periodic missing
  checkout detection, approved exception/correction process, offline revalidation and financial policy.
- Throughput/load and operational provider/storage failure acceptance. The global auth transaction is
  explicitly conservative; no production volume claim is made.

The default provider and policy fail closed. The APIs can record authorized mobile evidence after
schema deployment, but no real successful Check In should be enabled before the blockers above are
resolved. Models, registered routes, fixture proofs and a successful build are not biometric acceptance.
