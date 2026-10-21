# Part B05: Durable Biometric Retirement

Status: local durable-intent implementation, owned module registration and standalone verification
are implemented. B05 is **not complete**.
Physical disposal, approved retention/hold interpretation, backup/key authority, root activation and
deployment acceptance remain pending. No production cleanup is accepted by this document.

## Scope and durable sources

No EF entities, scalar model properties, configurations, DbContext, migration, permissions,
shared DI/Program, HR source or Domain.FaceEnrollment changes belong to this slice. A23's model
freeze is preserved. No SQL execution, host launch, provider, credentials, real biometric data,
physical evidence deletion or Git operation is performed.

`FaceRetirementService` stages encrypted internal intents indexed by the existing append-only
Audit table. Revoke/replacement hooks in `FaceService` stage only metadata inside the existing
authorization transaction. `SaveChangesAsync` there is still precommit; no vault retirement runs
in either callback. Rollback can leave unindexed encrypted metadata, never a committed intent
or a storage retirement disabling a still-valid enrollment.

`FaceCaptureEvidenceReader` stages an intent in the capture-claim transaction and reauthorized
read/replay boundary. That intent initially awaits completion. It binds the complete envelope,
token digest (never token), original account/session/employee/device/challenge, capture ID and
server-owned enrollment/verification operation ID. The processor requires the committed exact
claim receipt and terminal proof before doing anything to storage. Verification completion uses
the existing completed attempt; enrollment success/failure uses its existing start/terminal
journal. Receipt/token/use expiry never substitutes for terminal proof or disposal authority.

A04's existing logical revoke rows remain the durable offboarding source. Recovery matches the
original `hr.employee.lifecycle.effective` audit, exact status-change ID from the revocation reason,
employee, existing system actor and revocation time. It copies that original audit identity and
role; no personnel role or HR mutation is introduced. Legacy direct-revoke/replacement recovery
uses the original revoke audit or protected successful enrollment terminal naming the prior and
successor enrollment. Missing/ambiguous original audit requires reconciliation, not invented authority.

## Processing and observability

`FaceRetirementRepository` queries existing Audit, receipts and enrollment/attempt rows. Reads
of business terminal sources are untracked and outside business transactions. Recovery/outcomes
use an audit-only transaction through the existing authentication transaction lock; its guard
permits only append-only B05 Audit records, and checks before save and commit.

The processor validates canonical purpose, complete reference, source employee, immutable committed
revocation time and exact successor for replacement. It rejects an object still referenced by an
active enrollment. Capture receipt digest validation repeats the original server binding; a different
session, challenge, capture, token digest or operation cannot borrow a terminal result. Retry expiry
does not erase the source. The processor rechecks the committed source after storage IO.

`IFaceEvidenceRetirement` exposes the existing use-closing operation without widening common
`IFaceTemplateStore`/FaceContracts. `RetentionControlledFaceTemplateStore` implements it and keeps
its existing object lock, strict encrypted journal and approved use gate. Successful retirement
preserves ciphertext, metadata and keys. Concurrent/restarted processing replays that same exact
operation. A crash after retirement and before outcome commit is recovered by repeating idempotent
retirement and appending the acknowledgement. Failure/cancellation never acknowledges physical disposal.

Protected outcomes distinguish awaiting terminal, policy/hold/boundary/adapter pending, retry,
scope rejection and disposal acknowledgement. The existing Audit index is the durable observable
status, bound to its source intent. Unchanged status is not appended repeatedly; outcome timestamps
have an unambiguous order even under a static test clock. Logs contain only aggregate counts and
fixed messages, never exception details, identities, private locators, tokens or key material.

The unregistered `FaceRetirementWorker` scans bounded pages, creates a fresh scope each iteration,
resets on failures/restarts and begins another sweep at the end. Intent and revoked-row recovery
have independent keyset cursors. Missing original authority audit is explicitly counted as recovery
pending; failed recovery is counted separately and does not prevent already-staged intents from
processing. Both counters are logged without private data. Poll/batch defaults are technical
scheduling controls, not retention values. The worker defaults to disabled.

## Disposal Gate

`FaceDisposalRunner` defaults to disabled and returns explicit policy-pending status. The default
`PendingFaceDisposalPolicy` never grants destruction; `UnavailableFacePhysicalDisposal` cannot delete.
Enabling the runner alone is insufficient. A future approved policy must name this exact intent/object,
the independently approved disposal boundary, current hold authority, backup treatment and key treatment.
Blank/substituted authority fails closed. A future physical adapter must atomically revalidate current
holds and object identity, durably reconcile live ciphertext/backup/key outcomes, and be idempotent under
the intent ID. There is **no production physical adapter or key/backup implementation** in B05.

Shared keys cannot be destroyed per enrollment merely because this intent is terminal. Unclaimed
uploads, uncertain precommit allocations, legacy claims without newly staged intents, malformed
journal tails and absent original audits remain retained pending explicit reconciliation. No automatic
repair deletes evidence or adopts untracked objects. Existing CompreFace/ONNX Development gates and
device/challenge/proof controls remain in their existing owners.

## Exact Root DI Handoff

Root authorized B05 to add `FaceRetirementRegistration.AddFaceRetirementInfrastructure` and call it
from the B-owned Infrastructure `Repositories/Attendance/AttendanceCaptureRegistration`. That call
is now implemented. No core DI/Program/catalog/model/snapshot is edited. The existing scoped
`IPrivateFileStore`, `CosmixDbContext`,
`IAccessManagementRepository`, singleton `TimeProvider` and selected `IFaceTemplateStore` are reused.

```csharp
// Parent-owned Application/Attendance/Punches/AttendanceCaptureRegistration:
services.AddScoped<FaceRetirementService>();
services.AddScoped<FaceRetirementProcessor>();
services.AddScoped<FaceDisposalRunner>();
// Optional API registration after root review, still disabled until explicitly configured:
services.AddHostedService<FaceRetirementWorker>();
```

The implemented Infrastructure extension registers the scoped `IFaceRetirementRepository`, singleton
`IFaceEvidenceRetirement` alias to the selected decorated store, `PendingFaceDisposalPolicy`,
`UnavailableFacePhysicalDisposal`, and disabled `FaceDisposalOptions`/`FaceRetirementWorkerOptions`.
For an unselected/undecorated store the retirement binding is explicitly unavailable; it does not
construct a second vault, fall back to raw biometric bytes or enable the provider. The singleton alias
and selected decorated store are the same instance. Root supplies the three Application registrations
above and owns the optional API hosted-service registration and final combined composition check.
The parent-owned Application module now contains all three registrations (read-only confirmed by B05).

`FaceService` and `FaceCaptureEvidenceReader` now require `FaceRetirementService`. Supply that service
even while the worker is disabled: authorized business transactions still persist durable intent metadata.
The existing Application registrations for both consumers remain unchanged. Outside the selected,
approved retention-decorated PoC, `IFaceEvidenceRetirement` resolves to the unavailable adapter and
the processor records retry status; it cannot fall back to unguarded evidence access.

Do not configure an enabled real disposal policy/provider from this handoff. If root decides to bind
worker settings, validate batch 1..100 and poll 1..3600 seconds; leave `FaceDisposal.Enabled=false`.
The approved-use options continue to have no fabricated approval/duration defaults.

## Verification Record

- Initial standalone RED compiled and failed with the missing durable retirement consumer assertion.
- The expanded runner hit CS9113 on temporary unread constructor skeletons; this was a compilation
  failure, not behavioral RED. Both skeletons were replaced atomically with complete implementations;
  temporary suppression was removed. Release Application build then passed with zero warnings/errors.
- The compiled expanded runner also observed the explicit unimplemented staging failure before GREEN.
- Existing real capture-service suite passed 127 checks, including actual revoke/replacement/capture
  intent hooks and preserved authority/device/challenge/replay checks. Its persistence is synthetic.
- A separate compiled runtime RED rejected the absent recovery-pending status. GREEN now reports
  missing original authority and failed recovery separately, with independent recovery pagination.
- `VerifyFaceRetirement.cs`: **56** checks passed, using actual service/processor/use gate and actual
  source-validation rules with serialized synthetic transactions. Covers rollback preserving storage
  use, committed-only retirement, reference/scope/session/terminal substitution, expiry without
  completion authority, failure/restart, outcome-save failure after storage retirement, idempotent
  acknowledgement, cancellation, A04 recovery, required/duplicate evidence fields and disposal gates.
- `VerifyFaceRetirementAdapter.cs`: **21** checks passed. Actual SQL Server translation of both
  keyset queries, restart filters, dirty-context/audit-only guards and actual parent Application plus
  owned Infrastructure registrations. Selected Development store and retirement interface are the
  same instance; Production has no undecorated retirement capability; worker/disposal defaults are
  disabled/unapproved. This opens no SQL connection and launches no API host.
- `VerifyFaceRetirementFiles.cs`: **18** checks passed with real existing AES-GCM vault/journal and
  private ACL checks in a unique owned TEMP root. Two independent child processes exercised an
  in-progress read versus retirement lock, then restart denial/idempotence. A real empty index
  reservation simulated an interrupted append and preserved ciphertext/metadata while denying
  release/retirement acknowledgement. Reference/purpose substitution and vault/journal key isolation
  also passed. Synthetic fixtures are retained: **no file deletion** is performed, even as test cleanup.
  This is not a power-loss, network-filesystem, malicious rollback or production backup test.
- The file harness initially used a `with` expression on a class (compile failure, not RED); corrected
  before execution. Its initial final key-isolation assertion targeted the journal for a biometric
  vault setting. Source inspection confirmed that guard belongs to the existing vault/use boundary;
  the corrected harness verifies biometric and metadata key isolation at their actual boundaries.
  No production guard was changed or suppressed.
- Fresh existing regression runs all passed: capture service **127**, enrollment journal **19**,
  capture protection/real MVC JSON filter **33**, lifecycle **66**, offline provider transport/semantic
  checks **122**. No model inference, network, SQL execution, real biometric or device acceptance.
- Owned Infrastructure Release build after module registration passed with zero warnings/errors.
  Final full `Cosmix.Backend.sln` Release build also passed with **zero warnings and zero errors**
  (68.50 seconds). No host or SQL execution was required.
- `VerifyCaptureService.cs` is handed back to the parent for required B01 `IOfficeReader`/
  `ICaptureValidationEvaluator` constructor and fixture updates. B05 retirement fixture changes
  are complete and the last B05-run checkpoint is **127 passing checks**. B05 will not edit that
  file again. Later parent B01 changes require the parent's fresh combined regression/composition run.

Commands (standalone console programs, no test projects):

```powershell
dotnet run --file docs/attendance-capture/VerifyFaceRetirement.cs --no-cache -c Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false --property:UseSharedCompilation=false
dotnet run --file docs/attendance-capture/VerifyFaceRetirementAdapter.cs --no-cache -c Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false --property:UseSharedCompilation=false
dotnet run --file docs/attendance-capture/VerifyFaceRetirementFiles.cs --no-cache -c Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false --property:UseSharedCompilation=false
dotnet run --file docs/attendance-capture/VerifyCaptureService.cs --no-cache -c Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false --property:UseSharedCompilation=false
```

## Remaining Acceptance Limits

The API worker file is available but unregistered by this slice, and default settings disable it.
Root owns worker activation and complete combined composition acceptance. Database atomicity,
cross-host worker races and committed SQL restart behavior have not been exercised here; query
translation and synthetic transaction tests are not SQL acceptance. The original protected Audit
and receipt stores must be provisioned and retained together with biometric lifecycle metadata.
Legacy source audits or malformed original replacement evidence may require explicit reconciliation;
recovery failures are surfaced while already-staged intent processing continues. Unclaimed captures
and uncertain allocations have no terminal proof and remain retained.

Approved destruction timing/holds, live-object deletion, backup disposition, shared/historical key
treatment and a durable accepted physical adapter are outstanding. No policy durations, approvals,
paths or real keys are supplied. Production cleanup and B05.T2 are **not accepted**. Existing module
use denial remains distinct from physical disposal. Provider/model/device acceptance stays separate.
