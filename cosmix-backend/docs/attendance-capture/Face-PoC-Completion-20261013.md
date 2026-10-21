# Face PoC Completion Slice - 13 October 2026

## Result and scope

This bounded slice hardens the existing Development CompreFace 1:1 plus Silent Face
ONNX/C# OnnxRuntime PoC. It does not activate a provider or approve real biometric uploads.
No Python service, cloud API, ONNX artifact, calibrated threshold or runtime success substitute
was added. No actual biometric provider was called. No API was started by this slice.

Read AGENTS.md, README.md, docs/Architecture.md, docs/SRS-Module-Map.md,
Self-Hosted-Face-PoC.md, Face-Storage-Lifecycle.md, the folder READMEs, all face
provider/contracts/services, the encryption/journal contracts and existing offline checks.
The inspected workspace already contains unrelated modified and untracked work.

Exactly these repository files were edited by this slice:

- src/Cosmix.Application/Attendance/FaceVerification/RetentionControlledFaceTemplateStore.cs
- src/Cosmix.Infrastructure/FaceVerification/EncryptedFaceStorageRetentionJournal.cs
- src/Cosmix.Infrastructure/FaceVerification/CompreFaceClient.cs
- docs/attendance-capture/Face-PoC-Completion-20261013.md

FaceService, DI, contexts, schema, migrations, project files, lockfiles, APIs, authentication,
SQL and runtime configuration were not edited. Existing evidence, keys and user data were
not deleted or rewritten. No permanent test project, commit or push was created.

## Reproduced failures and implemented guards

The dedicated TEMP regression runner compiled and failed at all five expected runtime
assertions before changing production code. It aggregates failures rather than stopping
at the first assertion.

| Regression | Before | After |
| --- | --- | --- |
| Clock rolls from t+5 to t+4 during release-record persistence | Plaintext returned despite a newly persisted future release timestamp | Canonical 503; decrypted buffer zeroed |
| Retained enrollment boundary is t+30 under a configured t+20 use duration | Bytes released at t+25 | Canonical 503 before vault decryption |
| Actual Infrastructure journal receives release preparation at exact use expiry | Record appended | Canonical 503 before metadata/index allocation |
| Caller cancellation occurs at final HTTP body EOF | Parsed detection returned | Caller cancellation propagated |
| Final HTTP body read returns after the one-second test deadline | Parsed detection returned | Sanitized face_provider_timeout 503 |

RetentionControlledFaceTemplateStore now captures one preparation timestamp, validates use
at that timestamp, and compares the final release timestamp against it. Its previous final
check only examined events read before the new append. A rollback during append can no
longer bypass the new timestamp. Existing failure handling zeroes decrypted plaintext.

The gate also rejects retained boundaries longer than the currently configured approved
capture/enrollment duration and rejects UseReleasePrepared events at or beyond their
retained boundary. This validates existing records; it never rewrites or adopts them.
Shorter historical boundaries remain shorter. Reducing a configured duration or changing
its approval reference can make old metadata unavailable; it does not silently extend,
migrate or reconcile old authority. Synthetic test durations are not deployment policy.

EncryptedFaceStorageRetentionJournal enforces the same exclusive preparation boundary
during append and replay validation. Retirement after use expiry is still a permissible
terminal event; expiry is not deletion authority.

CompreFaceClient explicitly checks its linked cancellation/deadline token after response
headers, before and after each body read (including EOF), and after strict JSON parsing.
A body implementation that completes without honoring cancellation cannot return parsed
success after that cancellation/deadline. Existing response caps, zeroization, strict JSON,
private transport, TLS validation, multipart fields and no-retry behavior are preserved.
These checks are cooperative guards, not a hard process-kill guarantee.

## Executed verification

All harnesses ran against freshly built Release assemblies, without project references
that could trigger repository restore or edits. The TEMP project references the compiled
assemblies and existing native DLLs. Its only restore used its own local TEMP directory
as RestoreSources, with NuGetAudit disabled and no package references.

| Check | Result |
| --- | --- |
| Dedicated new regressions | RED: 0 passed / 5 expected failures; GREEN: 5 passed / 0 failures |
| Existing VerifyFaceStorageLifecycle.cs copied to TEMP | 66 passed |
| Existing VerifySelfHostedFaceProvider.cs copied to TEMP | 122 passed |
| Existing VerifyCaptureService.cs copied to TEMP | 103 passed |
| Full solution Release --no-restore with TEMP output | Passed, zero warnings/errors |
| git diff --check | Exit 0; Git also printed unrelated pre-existing line-ending notices |

The existing lifecycle checks exercise replacement/revocation retirement, exact expiry,
later-use denial, retained objects, recreated journal state, malformed/partial metadata,
cancellation, zeroization and concurrent release/retirement. They use synthetic persistence;
they do not establish actual disk durability, ACLs or cross-process crash recovery.

Adapter checks use offline HTTP handlers and non-face flat-color PNGs. They cover malformed,
duplicate, oversized and inconsistent responses, deadlines/cancellation, one-face response
requirements, missing model availability, explicit tensor/crop/output semantics and invalid
probabilities. No ONNX model inference or biometric accuracy was tested. Existing capture
checks exercise real Application logic with synthetic repositories/provider results only
inside the harness, including enrollment replay, revoked-enrollment capture denial,
single-operation claims, completed challenges and Check Out without a face requirement.
They do not establish live authentication, SQL transactions or physical camera provenance.

TEMP-only files:

- C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{0fa33fc3-8279-447e-b2b0-4800a95c2f04}/AC/Temp/Cosmix-Face-PoC-20261013-1423/Checks.csproj
- C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{0fa33fc3-8279-447e-b2b0-4800a95c2f04}/AC/Temp/Cosmix-Face-PoC-20261013-1423/Regression.cs
- C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{0fa33fc3-8279-447e-b2b0-4800a95c2f04}/AC/Temp/Cosmix-Face-PoC-20261013-1423/Lifecycle.cs
- C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{0fa33fc3-8279-447e-b2b0-4800a95c2f04}/AC/Temp/Cosmix-Face-PoC-20261013-1423/Provider.cs
- C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{0fa33fc3-8279-447e-b2b0-4800a95c2f04}/AC/Temp/Cosmix-Face-PoC-20261013-1423/Capture.cs

The three existing harness copies differ only by removal of their four file-based
project/property directives. No assertions were removed or relaxed. The TEMP project
adds the existing Windows native DLLs at its output root because plain assembly references
do not carry the original native-runtime dependency resolution.

Two preliminary regression attempts were fixture/setup failures, not RED evidence:
the initial PNG was rejected as outside the RGB contract; the first native-codec attempt
could not resolve OpenCvSharpExtern. After correcting only TEMP setup, all five intended
assertions failed against the original code before production edits.

The initial parallel baseline build returned exit 1 with zero reported errors; serial
MSBuild (-m:1) passed before the edits. The first post-edit normal-output solution build
compiled Application/Infrastructure but failed copying their DLLs into API output
(MSB3027/MSB3021, existing process file locks). That process was left running. A complete
serial build with TEMP OutDir then passed for all four projects. No API/SQL initialization
was needed for the successful build or any check.

Reproduction commands from the backend root (TEMP files remain machine-local):

```powershell
$faceChecks = 'C:/Users/Huma.Khan/AppData/Local/Packages/sandbox.{0fa33fc3-8279-447e-b2b0-4800a95c2f04}/AC/Temp/Cosmix-Face-PoC-20261013-1423'
dotnet build Cosmix.Backend.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false "-p:OutDir=$faceChecks/ReleaseBuild/" -v:minimal
dotnet run --project "$faceChecks/Checks.csproj" -c Release --no-restore -p:HarnessFile=Regression.cs -v:minimal
dotnet run --project "$faceChecks/Checks.csproj" -c Release --no-restore -p:HarnessFile=Lifecycle.cs -v:minimal
dotnet run --project "$faceChecks/Checks.csproj" -c Release --no-restore -p:HarnessFile=Provider.cs -v:minimal
dotnet run --project "$faceChecks/Checks.csproj" -c Release --no-restore -p:HarnessFile=Capture.cs -v:minimal
```

## Exact remaining dependencies

1. Physical deletion: IFaceTemplateStore and IPrivateFileStore expose only IsConfigured
   (face store), WriteAsync and ReadAsync. EncryptedFaceTemplateStore delegates to
   EncryptedPrivateFileStore; neither provides an authorized deletion primitive.
   RetireAsync denies future byte release and preserves evidence; it cannot attest deletion.
   A separately reviewed vault disposal contract and explicit authority must distinguish
   disposable captures, enrollment references and uncertain orphan allocations from retained
   audit/attendance evidence, bind the exact object/version, persist disposition results,
   and define backup/key retention. This slice does not invent that authority or erase data.

2. Automatic retirement: FaceService replacement/revocation and completed captures do not
   call storage RetireAsync. The existing IFaceTemplateStore registration exposes no retirement
   operation through that interface. Correct integration requires durable, server-owned
   committed disposition intent with exact reference/purpose/reason/operation binding plus
   retry/reconciliation after crashes. SaveChangesAsync inside AttendanceOperation is not
   proof that its outer transaction committed. Adding precommit retirement could irrevocably
   deny a valid enrollment after rollback. No FaceService/shared composition change was made.
   Existing active-enrollment/challenge authorization checks continue to deny revoked or
   replaced proofs; that does not mean their underlying ciphertext is automatically retired.

3. Model artifact: none was supplied. Exact weights/export SHA-256, provenance, artifact-specific
   licensing, graph metadata, BGR/NCHW preprocessing, CompreFace crop parity, output semantics,
   live class, ensemble decision and thresholds still need review against the actual artifact.
   Existing documented upstream source references and synthetic semantic checks cannot approve
   an absent export. No model was downloaded or fabricated and no threshold was inferred.

4. Real service/devices/deployment: private CompreFace TLS/destination behavior, genuine ONNX
   loading/inference, actual enrolled devices, camera injection/replay/spoof accuracy,
   protected authenticated SQL flows, storage ACL/durability/retention/disposal acceptance
   and operator logging controls remain unexecuted. Explicit production approval and
   operational provisioning remain necessary; the existing Development-only selection was
   not changed. No real-device success, production readiness or completion of all seven HR
   gaps is claimed.

Check Out continues to require no face verification.

