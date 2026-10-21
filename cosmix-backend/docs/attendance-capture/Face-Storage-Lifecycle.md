# Face Storage Lifecycle

13 October B05 continuation: durable Audit-backed revoke/replacement/capture intent hooks,
committed-source processor and disabled disposal gate now exist. Module Infrastructure wiring
reuses the selected store. This supersedes the automatic-retirement deferral below, within the
explicit activation/testing limits in [B05 handoff](PartB05-Durable-Retirement-20261013.md).
Approved physical disposal, holds, backup/key treatment and production cleanup remain pending.

## Latest root integration and executed checks

Root applied the selected Development PoC decorator/journal registration described below.
There is no unguarded fallback for a selected PoC with missing retention configuration.
Combined lifecycle verification passed **66** checks; actual service-composition verification
passed **14** in Development/Production with ephemeral protection and synthetic storage.
Release solution build passed with zero warnings/errors. These are not real disk, SQL, model,
CompreFace or device tests. Automatic committed-operation retirement and physical deletion remain
deferred; no policy duration, approval reference, persistent key or private path was provisioned.

Independent review found omitted biometric/ordinary keyrings in the path-isolation checks.
The constructor now validates all three active/historical rings against all four storage/index
roots. Pure synthetic path regressions and the combined lifecycle suite passed. Final full record:
[second-pass verification](../attendance-completion/Verification.md).

The earlier worker-specific execution/integration statements below retain their original stage.

Status: Application control and encrypted Infrastructure journal implemented in owned new files;
final journal GREEN execution is queued for root's build slot. Production activation remains blocked.
No retention/deletion policy is approved by
this document. No camera freshness, model acceptance or legal compliance is claimed.

## Inspected behavior

- `FaceService.RevokeAsync` commits inactive enrollment and audit; it does not remove the old
  reference image. Replacement also revokes the prior enrollment in the finalization transaction.
- `FaceService.EnrollAsync` writes an encrypted reference image before the SQL transaction commits.
  Failed/uncertain finalization can leave an encrypted object with no committed enrollment reference.
- `FaceCaptureService.Store` writes the encrypted upload before its protected token/receipt commits.
  Protection, audit or transaction failure can leave an encrypted upload.
- `FaceCaptureEvidenceReader` claims a capture for one bound operation. Same-operation retry is
  allowed while authorization, binding, challenge state and token lifetime remain valid. It checks
  completed/consumed challenges but does not dispose of the encrypted upload.
- `IFaceTemplateStore` deliberately exposes only configuration, write and read. There is no approved
  physical deletion primitive. Current PoC templates are original images, not embeddings. Token
  expiry and the enrollment journal's 24-hour retry lifetime are not deletion authority.
- Existing encrypted storage preserves incomplete objects on uncertain outcomes. Legal documents,
  raw punches, protected audit snapshots and general private evidence are outside this slice.

## Authorized safe control

`RetentionControlledFaceTemplateStore` implements `IFaceTemplateStore` in Application, backed by
`EncryptedFaceStorageRetentionJournal` in Infrastructure. They preserve every evidence object and
its encryption key and restrict biometric byte release using an explicitly approved boundary.

`FaceStorageRetentionOptions` requires enabled/confirmed authority, its approval reference and
separately approved capture/reference use durations. Neither duration has a default. These are
server options, never a request payload. Snapshot options on construction. Token lifetime remains
an independent restriction, and this layer does not authorize a session or bypass capture checks.

An object must have an exact immutable Stored record before its reference is returned. Records bind
the complete `PrivateFileReference`, canonical face purpose, sequence, UTC use boundary and approval.
Untracked legacy objects deny use and remain encrypted for explicit reconciliation. Never silently
adopt them or infer their creation time from filesystem timestamps.

Under one cross-process object lock, read validates journal continuity and binding, checks expiry
and retirement, decrypts through the existing template store, rechecks expiry/cancellation, persists
a UseReleasePrepared record and then returns exclusively owned bytes. Failure after decryption zeroes
the buffer. Persistence may finish after cancellation/expiry; the final recheck still withholds bytes.
The audit describes a prepared release, not successful delivery, inference or biometric acceptance.

`RetireAsync(reference, purpose, reason, operationKey, ct)` records EnrollmentRevoked,
EnrollmentReplaced or CaptureCompleted using the configured authority. It irrevocably closes use
under the same lock. Repeating disposition is idempotent. It does not unlink files, destroy keys,
remove raw attendance, change business state or claim an already released provider buffer was recalled.
Actor/session/resource audit remains the responsibility of the existing authorized operation.

## Infrastructure Persistence

The journal uses the existing `EncryptedPrivateFileStore` for encrypted lifecycle metadata, separately provisioned
private metadata/index directories, and immutable sequence entries that point to authenticated metadata.
Validate local private paths/ACLs with existing Infrastructure helpers. Require disjoint directories
from face evidence and ordinary documents. No package, shared store, schema or migration changes.

Reserve an index entry exclusively before writing encrypted metadata. Fill, flush to disk and verify
the entry before returning. A partial reserved entry, a missing encrypted record, sequence gap,
corrupt/authentication-failed metadata or substituted object/purpose must deny further use. Never
clean up an uncertain evidence or metadata outcome automatically. Exclusive filesystem locking must
serialize separate instances/processes. Interrupted registration withholds the object reference;
the retained orphan requires operator reconciliation. This is fail-closed recovery, not automatic deletion.
Host/filesystem durability and malicious index rollback need separate deployment acceptance; an
immutable local index is not an external rollback-resistant audit authority.

The production index uses a bounded ten-second exclusive-file acquisition, immutable five-digit
sequence names, strict bounded JSON, duplicate/unknown-field rejection, flushed reservation/pointer
read-back and readonly completed entries. Every lifecycle event field is explicitly required in JSON;
absent timestamps/kinds cannot be interpreted as C# defaults. There is a technical maximum of 10,000 records per object;
reaching it denies further use, without deleting history or inventing a retention policy. The metadata
store requires capacity for at least 8,192 plaintext bytes. Metadata/index/face/ordinary directories
must be pairwise disjoint. Metadata keys are independent of ordinary document keys, using the existing
biometric isolation check; they may belong to the dedicated biometric keyring. Key files must remain
outside all four object/index directories. Provision keys/directories/ACLs explicitly; no constructor
creates or adopts a deployment root.

## Root Integration

These are proposed existing-file changes, not edits made by this slice:

1. Root wires the decorated `IFaceTemplateStore` for both `FaceService` and capture services after
   explicit approval and storage provisioning. The undecorated vault must not be exposed to biometric
   consumers. No route, permission, catalog, Program or shared DI edit belongs to this slice.
2. Automatic revoke/replacement retirement is explicitly deferred. It requires a durable committed
   disposition intent and replay strategy not specified here. A future integration may pass the retained
   exact reference/purpose and a server-owned operation key only after a committed revoke/replacement.
   Do not call retirement after `repository.SaveChangesAsync` inside the callback: the outer authorization
   transaction still owns commit. No outbox, migration, automatic retirement or fresh worker is introduced.
3. Capture completion retirement uses a committed terminal proof. Do not retire immediately on the
   provider's read: same-operation retry and uncertain provider/finalization outcomes remain possible.
   Enrollment consumption needs its terminal journal outcome; verification needs its completed attempt.
4. Failed storage writes/finalization preserve encrypted evidence. Cleanup remains unavailable until
   an approved distinction between retained evidence and disposable allocation plus a compatible
   single-operation disposition contract exists. Retention controls can ship independently of deletion.

### Exact Constructors And Options

```csharp
// Cosmix.Application.Attendance.FaceVerification
new RetentionControlledFaceTemplateStore(
    IFaceTemplateStore vault, IFaceStorageRetentionJournal journal,
    FaceStorageRetentionOptions options, TimeProvider clock);
// FaceStorageRetentionOptions.SectionName == "FaceStorageRetention"

// Cosmix.Infrastructure.FaceVerification
new EncryptedFaceStorageRetentionJournal(
    FaceStorageRetentionJournalOptions options,
    FaceTemplateStorageOptions biometric, PrivateFileStorageOptions ordinary);
// FaceStorageRetentionJournalOptions.SectionName == "FaceStorageRetentionJournal"
```

### Precise Registration Replacement For Root

Replace the existing three-line `TryAddSingleton<IFaceTemplateStore>` registration in
`src/Cosmix.Infrastructure/Repositories/Attendance/AttendanceCaptureRegistration.cs` with this block.
Existing usings already include both namespaces, Configuration, Hosting and DependencyInjection.
No additional registration or constructor change to `FaceService`, `FaceCaptureService`,
`FaceCaptureEvidenceReader` or `AttendanceCaptureService` is needed for the use gate.

```csharp
services.TryAddSingleton<IFaceTemplateStore>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var biometric = configuration.GetSection(FaceTemplateStorageOptions.SectionName)
        .Get<FaceTemplateStorageOptions>() ?? new();
    var ordinary = provider.GetRequiredService<PrivateFileStorageOptions>();
    var vault = new EncryptedFaceTemplateStore(biometric, ordinary);
    var selectedPoc = provider.GetService<IHostEnvironment>()?.IsDevelopment() == true &&
        (configuration.GetSection(SelfHostedFaceOptions.SectionName)
            .Get<SelfHostedFaceOptions>() ?? new()).Enabled;
    if (!selectedPoc) return vault;

    var options = configuration.GetSection(FaceStorageRetentionOptions.SectionName)
        .Get<FaceStorageRetentionOptions>() ?? new();
    var journalOptions = configuration.GetSection(FaceStorageRetentionJournalOptions.SectionName)
        .Get<FaceStorageRetentionJournalOptions>() ?? new();
    var journal = new EncryptedFaceStorageRetentionJournal(journalOptions, biometric, ordinary);
    return new RetentionControlledFaceTemplateStore(vault, journal, options,
        provider.GetRequiredService<TimeProvider>());
});
```

For a selected Development PoC, this always returns the decorator, even when its configuration is
absent or invalid. There is no fallback to unguarded biometric use. In that case `IsConfigured` is
false; existing readiness checks block capture upload/enrollment and template reads fail closed.
The PoC/provider production gate remains unchanged. Once approved options/private storage are
provisioned, existing upload/reference writes receive immutable lifecycle records and every existing
biometric read flows through the use boundary and audit preparation. SQL/actor/resource checks remain
in their current services. A completed or consumed attempt already fails `RequireAttempt` before the
reader releases bytes; same-operation retries remain usable only inside both capture-token and
independently approved storage-use boundaries. This slice does not retire captures on first read.

If root wants the existing enrollment failure journal to classify a retention IO failure as template
storage unavailable, add `or "face_retention_unavailable"` to the existing `AuthenticationFailure`
code pattern in `FaceService.EnrollAsync`'s catch. This is a classification-only patch, with no side
effect or precommit retirement. Current generic failure journaling still fails closed without it.

### Disabled Configuration For Root

Add these sections to the root-selected server configuration, preserving existing values and gates.
The empty/null values are intentional; actual durations and approval references must come from the
approved server boundary. Nothing here approves real uploads or production activation.

```json
"FaceStorageRetention": {
  "Enabled": false,
  "AuthorityConfirmed": false,
  "ApprovalReference": "",
  "CaptureUseLifetimeSeconds": null,
  "EnrollmentUseLifetimeSeconds": null
},
"FaceStorageRetentionJournal": {
  "Enabled": false,
  "IndexDirectory": "",
  "Storage": {
    "Directory": "",
    "KeyFile": "",
    "KeyReference": "",
    "MaxSizeBytes": 8192,
    "DecryptionKeyFiles": {}
  }
}
```

## TDD and Verification

`VerifyFaceStorageLifecycle.cs` exercises the Application control, the actual Infrastructure journal
through its internal persistence seam, and their combined path. Evidence, encrypted-store IO and index
IO use narrowly scoped in-memory doubles. The normal production constructor always selects the existing
AES-GCM store/private filesystem index; it cannot select those doubles through server configuration.
No file/key creation, database connection, model, provider call or real employee data is used.

The harness specifies approved capture/reference boundaries, exact expiry, retirement/retry,
retained ciphertext, restart against retained state, untracked objects, purpose/reference substitution,
configuration denial, audit outage, orphan preservation, expiry during decryption, zeroization,
cancellation and concurrent byte-release/retirement ordering. Journal cases include metadata round-trip,
recreated adapter state, lock requirement, complete reference substitution, sequence rejection, metadata
authentication failure, interruption after reservation and after metadata-before-pointer, denied use after
restart, and zeroed metadata/plaintext buffers. These test journal arbitration, not actual disk crashes.
Real filesystem crypto/ACL/durability/cross-process acceptance remains outstanding. This workspace-only
run does not create an external private-storage fixture; production helpers reject repository roots.

Requested coordinated command, run only after root grants the slot:

```powershell
dotnet run --file docs/attendance-capture/VerifyFaceStorageLifecycle.cs --no-cache -c Release --property:RestoreSources="$PWD/src/Cosmix.Infrastructure" --property:NuGetAudit=false --property:UseSharedCompilation=false
```

Observed execution in the granted slot:

- Initial RED compiled and failed at runtime with the missing retention-control assertion.
- Application GREEN passed 37 checks in Release with `--no-cache`.
- Expanded journal runner initially had two `Task<T>` inference errors in its harness callbacks;
  these were fixed. That compilation failure was not counted as RED or passing.
- Journal RED compiled with `--no-cache` and failed at the expected missing-journal runtime assertion.
- Slot released with no active test process for financial-agent RED. Journal implementation completed
  without another build while that slot is in use. Final combined GREEN awaits root's next queue slot.
- Source review added required-field schema checks and seven malformed-record cases to the final
  runner. Final combined source (including the journal) has not been built/run by this slice.

Root registration/configuration patches above remain proposals in this document. They have not been
applied by this slice. Until root wires the decorator, the current host still uses its original store;
the implemented control is not claimed as active in the application. Root owns final combined Release
build and activation decisions.
