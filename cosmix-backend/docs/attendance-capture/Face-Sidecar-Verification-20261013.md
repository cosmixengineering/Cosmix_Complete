# Face Sidecar Verification - 13 October 2026

Completed the bounded existing-checks path. No production-source defect was reproduced
by these checks and no production source was edited. This is offline verification of
the existing CompreFace + Silent Face ONNX/C# PoC, not real-face acceptance.

## Executed Results

The successful command, from the backend root, was:

```powershell
& './docs/attendance-capture/RunFaceSidecarChecks.ps1'
```

The reviewed local run completed with exit code 0. Its Release solution build used
`--no-restore -m:1 -p:UseSharedCompilation=false` and a unique TEMP `OutDir`.
Build result: **0 warnings, 0 errors**, elapsed **00:01:01.97**.

| Existing harness | Passed assertions | Exit code |
| --- | ---: | ---: |
| VerifySelfHostedFaceProvider.cs | 122 | 0 |
| VerifyFaceStorageLifecycle.cs | 66 | 0 |
| VerifyFaceCaptureProtection.cs | 33 | 0 |
| VerifyFaceReadiness.cs | 10 | 0 |
| VerifyEnrollmentJournal.cs | 19 | 0 |
| VerifyCaptureService.cs | 103 | 0 |
| Total | 353 | |

The runner generated a package-free TEMP console project referencing the freshly built
assemblies. It copied each original harness, removing only `#:` file-based build
directives. Assertions and fixtures were unchanged. Its restore used only the local
TEMP directory as a source, local TEMP package/cache paths and `NuGetAudit=false`.
Each run used Release, `--no-restore`, its own intermediate/output directory, and the
already installed Windows native runtime binaries. It did not start the API.

Successful TEMP root:

```text
C:/Users/Huma.Khan/AppData/Local/Temp/Cosmix-Face-Sidecar-424f26879f9c448880ae9d1cb9cb28c4
```

The provider checks used an internal offline HTTP handler and non-face flat-color PNGs.
They exercised transport parsing, binding, cancellation, unavailable-model behavior and
tensor/crop/output helpers. They did not load model weights or perform model inference.
Lifecycle/journal checks used synthetic persistence. Protection used ephemeral keys.
Capture service checks used existing synthetic repositories and preverified fixtures;
their passed outcomes do not establish a live person's identity or camera provenance.

## Setup Failures

Before the successful run, sandbox attempts failed on cross-command TEMP writes,
TEMP NuGet metadata writes, or generated global-using/file-list writes. Those attempts
executed no harness assertions. Separate Release solution builds nevertheless passed.
Reviewed escalation was used for the local offline runner after these write denials.

The first reviewed runner build passed, but restore failed with MSB5029 because its
native-runtime path property was omitted. MSBuild rejected the resulting empty wildcard.
Passing `FaceRuntimeDirectory` to restore corrected only the runner setup. The complete
successful run above followed that correction. Neither setup failure is a biometric
failure, a production-source regression, or a passing test result.

`dotnet --info` returned useful SDK/runtime metadata but failed its Service Control
Manager workload inspection with access denied. `dotnet --version` succeeded and returned
`10.0.401`; installed host/runtime metadata reported `10.0.12`, Windows x64.

## Read-Only Real-Face Prerequisites

- Docker executable was found at `C:/Program Files/Docker/Docker/resources/bin/docker.exe`.
  `docker version --format '{{json .Client.Version}} {{json .Server.Version}}'` reported
  client **29.7.2**, then failed to connect to `dockerDesktopLinuxEngine` because its
  named pipe was absent. `docker ps -a --format '{{.Names}}|{{.Image}}|{{.Status}}'` and
  `docker image ls --format '{{.Repository}}:{{.Tag}}|{{.ID}}'` also failed to connect.
  A running CompreFace container and installed provider image/digest could not be verified.
  No Docker service was started and no container logs, environment or credentials were read.
- `rg --files --hidden -g '*.onnx' -g '!**/.git/**' -g '!**/node_modules/**'`
  returned no matches in this backend tree (exit 1). The supplied face docs record no
  reviewed model export. Private artifacts outside this tree and runtime configuration
  were not inspected; this is not a claim that no model exists anywhere on the machine.
- Existing Infrastructure assets contain `Microsoft.ML.OnnxRuntime/1.24.3`, its managed
  package at the same version, and OpenCvSharp4/runtime.win **4.13.0.20260627**.
  Existing Windows native output contains `onnxruntime.dll` (14,203,936 bytes),
  `onnxruntime_providers_shared.dll` (22,048 bytes) and `OpenCvSharpExtern.dll`
  (68,575,744 bytes). ONNX native product metadata is `1.24.20260304.5.3a728b7`.
  Installed binaries and passing OpenCV checks do not prove reviewed ONNX inference.
- No consented enrollment image, capture input or approved real-device test was supplied
  to this slice. No real biometric input was read, generated or submitted.

## Remaining Acceptance Dependencies

1. Supply the actual reviewed local single-file ONNX export and its exact SHA-256,
   upstream revision, provenance, export reference, artifact/weights/license review and
   semantics review. Verify its static float input/output names and shapes, BGR 0-255
   NCHW preprocessing, CompreFace crop parity, live-class index, logits/probabilities,
   ensemble rule and calibrated thresholds. Repository source licenses and synthetic
   outputs do not approve weights, training-data rights or export redistribution.
   The existing loader does not support an external-data export.
2. Make the approved private CompreFace deployment available separately. Accept its pinned
   deployment/image identity, private destination/TLS behavior and request logging controls.
   Detection/verification credentials belong in secure provisioning; none were inspected
   or requested by this slice. Actual provider HTTP and ONNX loading/inference remain unrun.
3. Supply explicitly consented enrollment/capture input and approved physical devices for
   the controlled real-face test. Device/camera capture, frame injection, replay, spoofing,
   multiple-face handling, lighting and false-accept/false-reject behavior remain untested.
4. Accept the existing authenticated enrollment/check-in flows against the isolated SQL
   setup with real session/device/challenge and one-use proof binding. Current tests use
   synthetic transactions; they do not prove SQL isolation or real authentication.
5. Provision approved policy/use durations, separate private vault/keyrings and encrypted
   lifecycle journal; accept ACLs, encryption, disk durability and cross-process recovery.
   The implemented gate denies expired/retired/untracked byte release. Automatic retirement
   after committed revoke/replacement/capture completion and physical deletion/orphan cleanup
   are still absent. Approved disposal, backup and key-retention boundaries remain required.

Production activation was not performed. The documented Development-only PoC boundary
was preserved. Check Out continues to require no face proof.

## Changed Repository Paths

- `docs/attendance-capture/RunFaceSidecarChecks.ps1`: standalone offline runner.
- `docs/attendance-capture/Face-Sidecar-Verification-20261013.md`: this execution record.

No shared registrations, API source, DbContext, migrations, financial source, runtime
configuration, secrets, database or Git synchronization were changed by this slice.
Existing unrelated modified/untracked work was preserved.
