# Self-hosted face PoC

Selected Development PoC storage now additionally requires explicit approved
`FaceStorageRetention` use boundaries and a separate private `FaceStorageRetentionJournal`.
Missing options fail closed, without falling back to the raw template vault. Stored and use-release
preparation records are encrypted; expiry/retirement denies later release while retaining evidence.
Durable retirement intents, committed-source validation, retry/recovery and an opt-in
retirement worker are now implemented for revoked/replaced enrollment and consumed capture
evidence. Retirement closes future use and retains ciphertext; physical deletion still has
an unavailable adapter and pending disposal policy. See [configuration readiness](Face-Configuration-Readiness.md)
for the current code and setup boundary; older lifecycle/execution records describe earlier slices.

This adapter implements the user-selected PoC: Exadel CompreFace image detection and stateless
1:1 image verification over HTTP, plus local C# Microsoft.ML.OnnxRuntime inference for reviewed
Silent Face Anti-Spoof ONNX exports. There is no Python service, cloud API, remote face collection,
automatic enrollment write, fabricated model, or successful unconfigured fallback.

**No ONNX model artifact or artifact-specific license/provenance review was supplied.** Therefore
the liveness service and the complete provider remain unavailable with the current empty model
configuration. An implemented inference path and passing transport tests do not establish biometric
accuracy, live-person acceptance, licensing of an eventual export, or production readiness.

## Integration signatures

Namespace: `Cosmix.Infrastructure.FaceVerification`.

```csharp
new CompreFaceClient(CompreFaceOptions options); // IDisposable, shared private transport
Task<CompreFaceDetectionResult> DetectAsync(byte[] image, CancellationToken ct = default);
Task<CompreFaceVerificationResult> VerifyAsync(byte[] source, byte[] target, CancellationToken ct = default);

new SilentFaceLivenessService(SilentFaceOptions options); // IDisposable, loads verified bytes once
Task<SilentFaceLivenessResult> VerifyAsync(byte[] image, FaceBounds bounds, CancellationToken ct = default);
bool IsConfigured;
string ContractDigest;
string ReasonCode;

new SelfHostedFaceVerificationProvider(SelfHostedFaceOptions options, CompreFaceClient client,
    SilentFaceLivenessService liveness, IFaceCaptureEvidenceReader evidence); // scoped
```

`FaceBounds(int XMin, int YMin, int XMax, int YMax)` uses CompreFace DTO extents: width/height
are max-minus-min. Bounds must be positive, nonnegative at the origin and inside the original
decoded image. Detection returns `Bounds` and `Probability`. Verification returns `SourceBounds`,
`SourceProbability`, `TargetBounds`, `TargetProbability`, and `Similarity`. Every score must be a
finite JSON number in [0, 1]; strings, missing values and out-of-range values are failures.

Root owns packages, DI, Application contracts/services, routes and evidence storage. Use the root's
Application `IFaceCaptureEvidenceReader.ReadAsync(string token, FaceCaptureBinding binding,
string operationKey, CancellationToken ct)`. The adapter reads `ActorAccountId` and `ActorSessionId`
from the server-populated input fields, never from the mobile payload. Enrollment binds account,
session, employee and device with null attempt/challenge and uses `input.IdempotencyKey`, the
server journal key `enrollment:{receiptId:N}`. Verification binds the same identities plus attempt
and SHA-256 of the challenge, and uses `verification:{input.AttemptId:N}`. The reader must validate
current authorization, expiry and the protected token and atomically claim the capture; this adapter
cannot independently establish those properties. Returned image buffers are exclusively owned by
the adapter and zeroed after use. The reader must not return shared mutable cached buffers.

Register `IFaceVerificationProvider` as **scoped**, because its reader uses scoped attendance services.
The client and liveness service can be shared singleton instances with host-managed disposal.
Root selects this adapter only in Development with `SelfHostedFace.Enabled`; disabled/production
selection uses the existing unavailable provider without constructing the client. Explicit invalid
PoC client configuration fails closed; missing/invalid model configuration remains unavailable.
Do not wrap its HTTP transport in retry, redirect or body-logging middleware.
The adapter also requires `PocApproved`, an approval reference and explicit thresholds. Existing
policy confirmation and dedicated encrypted template storage remain additional Application gates.

Enrollment runs Detect + liveness and returns a separate copy of the **original JPEG/PNG reference
image** in `Template`. It is not an embedding or irreversibly transformed faceprint. Existing
FaceService stores it through the dedicated encrypted template vault. The CompreFace comparison
uses a fresh source capture and this decrypted target reference image. No subjects, image IDs or
face-collection endpoints are called. CompreFace necessarily processes both images in memory;
operators must disable biometric request logging/retention at the private service and its proxy.
The adapter itself writes no images, SQL rows or audit bodies. Root handles capture retention.

The upload token authenticates server bindings; it does not attest a physical camera acquisition.
Passive image classification cannot by itself establish resistance to prerecorded/re-encoded
digital frames. Test direct frame injection and camera capture on real approved devices before
production acceptance. Durable use retirement is implemented; physical capture/reference
disposal, orphan cleanup, backup/key retention and real deployment acceptance remain pending.
Do not enable real employee uploads on the offline evidence alone.

Enrollment infrastructure/configuration failures throw a sanitized `AuthenticationFailure` with
status 503 so existing FaceService journals **ProviderUnavailable**. Caller cancellation propagates.
Invalid binding, reader denial and an actual liveness rejection return `Success=false`. Verification
outages return `Available=false`; a valid liveness or match rejection is available but not passed.
No remote error text is retained in these results. Provider version is a deterministic SHA-256 of
the reference-image contract, deployment version, model/export metadata and decision thresholds;
changing these requires compatible re-enrollment under the existing Application rules.

## Exact packages

Root installs and locks these; this work does not edit csproj or lockfiles.

| Package | Version | Use |
| --- | --- | --- |
| Microsoft.ML.OnnxRuntime | 1.24.3 | Local CPU inference and managed tensors |
| OpenCvSharp4 | 4.13.0.20260627 | BGR decoding, upstream-compatible linear resize and crop |
| OpenCvSharp4.runtime.win | 4.13.0.20260627 | Windows native OpenCV runtime |
| OpenCvSharp4.official.runtime.linux-x64 | 4.13.0.20260627 | Alternative native runtime for Linux x64 deployment |

Both platform runtime pins were verified against the official NuGet package index. Select the
runtime for the deployment platform; package availability does not prove native loading on an
unexecuted platform. These owned files do not require Hosting.Abstractions; root's integration may
use its separately selected `Microsoft.Extensions.Hosting.Abstractions` 10.0.12.

Primary package sources: [ONNX Runtime 1.24.3](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/1.24.3),
[OpenCvSharp4](https://www.nuget.org/packages/OpenCvSharp4/4.13.0.20260627),
[Windows runtime](https://www.nuget.org/packages/OpenCvSharp4.runtime.win/4.13.0.20260627),
[Linux runtime](https://www.nuget.org/packages/OpenCvSharp4.official.runtime.linux-x64/4.13.0.20260627).

## Configuration and transport

Option sections are `SelfHostedFace`, `SelfHostedFace:CompreFace`, and `SelfHostedFace:SilentFace`.
Start disabled, with no model list and no invented thresholds. Supply server-controlled options
through root configuration; none of these options belong in request DTOs.

Use the owned [one-time configuration validator](Face-Configuration-Readiness.md) before requesting
controlled deployment acceptance. It enumerates missing setting names, checks the actual private
storage/transport/model contracts and optionally loads local pinned ONNX bytes and native codecs.
It never selects/registers the provider or calls CompreFace. Its `Passed` result is a local setup
check; `LocalArtifacts=not_checked` explicitly means no ONNX graph/hash/native loading was checked.

CompreFace requires a service **origin** `BaseUri` (absolute URI with path `/`, no credentials,
query or fragment), separate private detection/verification API keys, a pinned `DeploymentVersion`,
and explicit `MinimumDetectionProbability`. Timeout defaults to 10 seconds per HTTP request
(configurable 1-30); response cap defaults to 65,536 bytes (configurable 256-1,048,576).
`AllowLoopbackHttpForPoc` is the sole plaintext exception and requires a literal loopback URI.
HTTPS retains normal certificate/hostname validation. No certificate bypass exists.

The production constructor owns SocketsHttpHandler: redirects/cookies/proxy/decompression disabled,
HTTP/1.1 selected explicitly, bounded headers/connections and no application retry pipeline.
At connection time DNS resolution must return only RFC1918 IPv4 or IPv6 ULA addresses; loopback
requires explicit opt-in. Public, link-local/metadata, multicast and unspecified destinations fail.
IPv4-mapped IPv6 is checked as IPv4. The socket connects directly to one validated resolved address,
preventing a second unchecked DNS lookup and avoiding fallback connection attempts. Host and port
must equal the configured origin. The client never consumes a URI supplied in an image, token,
response or mobile request, and never follows `Location`.

Actual endpoints are POST `/api/v1/detection/detect` (multipart `file`) and POST
`/api/v1/verification/verify` (multipart `source_image`, `target_image`) with the appropriate
`x-api-key`. Every request sets `limit=0&det_prob_threshold=0&face_plugins=&status=false` to request
the entire detected set. The reviewed probability threshold is enforced locally, after requiring
exactly one result, so a second low-confidence returned face cannot be filtered away locally.
The detector's intrinsic limitations still require empirical evaluation.

VerifyAsync pre-detects **both** images, then requires one verification result, one source face
and one complete target match. Response bounds must agree with pre-detection. There is no
first-face, best-face or top-score selection. Malformed/truncated/oversized/non-JSON responses,
duplicate properties, unexpected fields, wrong coordinates, conflicting/error fields, status other
than 200 and non-finite/nonnumeric scores fail closed. Optional plugins/status are disabled and the
accepted response shape is correspondingly narrow. HTTP response size is enforced while streaming,
including unknown content length. Deadlines cover headers and body; caller cancellation propagates.
There are three HTTP requests in client VerifyAsync, and four in provider verification including
the face detection passed to liveness. Per-request deadlines are finite; this is not one combined
network deadline. Remote protocol/status failures currently map to a canonical 503.

The image boundary caps each original input at 1 MiB, 4096 pixels per dimension and 4,194,304 total
pixels before native decode. It accepts only 8-bit opaque non-interlaced RGB PNG and 8-bit three-channel
baseline/progressive JPEG. APNG, transparency and orientation-bearing EXIF/APP1 are rejected. Root's
capture service may impose smaller limits. Capture must supply an orientation-free image before
token protection; stripping/rotating client evidence after it is bound is outside this adapter.
Liveness performs the real bounded OpenCV decode and verifies dimensions and three-channel type.

## Model review contract

Every `SilentFaceModel` needs all of the following, supplied through private server configuration:

- Fully qualified local `.onnx` Path and exact 64-hex SHA-256. UNC/network paths, alternate data
  streams and reparse points are rejected. Model files are limited to 32 MiB. The session is built
  from the exact bytes hashed, rather than reopening a path after verification. External-data exports
  are not supplied or supported by this one-file byte-loader contract.
- Exact upstream SourceRevision, ExportReference, LicenseReviewReference and SemanticsReviewReference.
  These are review references, not substitutes for inspecting the licensed artifact and export graph.
- Explicit input/output names, static `[1,3,height,width]` float input, static `[1,classCount]` float
  output, dimensions, class count and live class index. Dynamic/wrong/extra inputs or outputs fail.
- InputContract exactly `float32-NCHW-BGR-0_255-OpenCV_INTER_LINEAR-CompreFaceExtent`.
- CropKind `UpstreamScaledBox` with an explicit finite CropScale (1-10), or reviewed `WholeImage`
  with no scale. No model name is parsed to guess scale or dimensions.
- OutputKind explicitly `Logits` or `Probabilities`, and MinimumLiveProbability in (0,1].
  Up to four distinct hash-pinned models can be configured; **each** must independently pass.

The upstream inference path reads OpenCV BGR pixels and applies its custom `ToTensor` implementation.
The actual numpy and byte-image code casts to float without division by 255; its old 0-1 docstring
does not describe the executed code. This adapter follows the executed float BGR 0-255 path with
NCHW layout. It does not add normalization or swap channels. If a particular reviewed export embeds
or expects other preprocessing, it is incompatible with this input contract.

Scaled crop adapts upstream `CropImage._get_new_box`: scale is capped by image dimensions, expansion
is shifted back inside borders, coordinates are truncated and the final crop includes the bottom/right
endpoint before OpenCV INTER_LINEAR resize. CompreFace width/height are max-minus-min, while the
upstream Silent Face detector constructs its own box with a `+1` convention. The explicit adapter
contract uses CompreFace extents; detector/crop compatibility and parity for the eventual exported
model must be reviewed and tested. No numerical or biometric parity is claimed from the source alone.

For logits, stable softmax is applied exactly once. Probability outputs must be individually in
[0,1] and sum to one within 0.0001; they are not softmaxed again. The configured live class must
be the unique winning class and meet its reviewed threshold. Ties fail. The returned overall live
probability is the minimum across all configured models. This intentionally differs from upstream's
two-model summed prediction; do not import upstream demo thresholds without evaluation of this rule.

CPU sessions use one inference/inter-op thread, no profiling and fatal-only native logging. A
single inference gate bounds concurrency. A 1-30 second deadline includes waiting for that gate;
RunOptions.Terminate signals cancellation and inference is awaited to completion before releasing
the gate/buffers. Native decode and cancellation are cooperative, not a process-kill guarantee.
Encoded buffers, managed tensors and primary decoded/resized mats are cleared after use; native
library allocator/workspace copies are outside managed zeroization control. Single-frame spoof
classification plus a protected capture token does not itself attest physical camera provenance or
prove resistance to video replay, injected frames, masks or unseen attacks. Those need real PoC acceptance.

## Official source record

Read on 12 October 2026. These revisions pin the implementation references, not an installed provider
image digest or the absent model export.

- CompreFace revision `ddf32da8245e2e6688c3ab6f60587fd3e31538c2`:
  [REST API](https://github.com/exadel-inc/CompreFace/blob/ddf32da8245e2e6688c3ab6f60587fd3e31538c2/docs/Rest-API-description.md),
  [verification controller and full response wrapper](https://github.com/exadel-inc/CompreFace/blob/ddf32da8245e2e6688c3ab6f60587fd3e31538c2/java/api/src/main/java/com/exadel/frs/core/trainservice/controller/VerifyController.java),
  [source count and target matches](https://github.com/exadel-inc/CompreFace/blob/ddf32da8245e2e6688c3ab6f60587fd3e31538c2/java/api/src/main/java/com/exadel/frs/core/trainservice/service/FaceVerificationProcessServiceImpl.java),
  [DTO extents](https://github.com/exadel-inc/CompreFace/blob/ddf32da8245e2e6688c3ab6f60587fd3e31538c2/embedding-calculator/src/services/dto/bounding_box.py).
- Silent Face revision `b6d5f04ad78778917853b25c778acef6d5626d15`:
  [actual ToTensor functional code](https://github.com/minivision-ai/Silent-Face-Anti-Spoofing/blob/b6d5f04ad78778917853b25c778acef6d5626d15/src/data_io/functional.py),
  [inference and softmax](https://github.com/minivision-ai/Silent-Face-Anti-Spoofing/blob/b6d5f04ad78778917853b25c778acef6d5626d15/src/anti_spoof_predict.py),
  [crop source](https://github.com/minivision-ai/Silent-Face-Anti-Spoofing/blob/b6d5f04ad78778917853b25c778acef6d5626d15/src/generate_patches.py),
  [demo live class and ensemble](https://github.com/minivision-ai/Silent-Face-Anti-Spoofing/blob/b6d5f04ad78778917853b25c778acef6d5626d15/test.py),
  [upstream repository license](https://github.com/minivision-ai/Silent-Face-Anti-Spoofing/blob/b6d5f04ad78778917853b25c778acef6d5626d15/LICENSE).
- [ONNX Runtime C# inference API](https://onnxruntime.ai/docs/api/csharp/api/Microsoft.ML.OnnxRuntime.InferenceSession.html)
  and [RunOptions cancellation](https://onnxruntime.ai/docs/api/csharp/api/Microsoft.ML.OnnxRuntime.RunOptions.html).

Crop adaptation attribution: Copyright 2020 Minivision, adapted for CompreFace extents and C#.
The original source is licensed under Apache License 2.0; retain its copyright/license and applicable
notices when distributing the adaptation. The license is available at
[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0). This repository-level source license
does not constitute approval of an absent ONNX artifact, weights, training data or export distribution.

## Verification and remaining acceptance

`VerifySelfHostedFaceProvider.cs` follows the existing .NET file-based harness pattern. It invokes
the real client with an internal HttpMessageHandler transport double through reflection; public
production construction never selects a double. Generated flat-color PNGs contain no face and never
leave the process. Model-output/crop/tensor helpers are checked directly with synthetic arrays; no
model file is created. The outage regression overrides only the provider's readiness gate through
harness reflection to exercise its actual enrollment catch path with throwing evidence readers,
without inventing an ONNX model or a successful liveness inference.

The existing offline runner builds to a unique TEMP output and uses already installed dependencies:

```powershell
pwsh -NoProfile -File docs/attendance-capture/RunFaceSidecarChecks.ps1
```

Transport checks cover official multipart fields, separate keys, complete detection of both inputs,
no largest-face truncation, full response parsing, duplicate/error/malformed response rejection,
score ranges, coordinates, repeated-bound consistency, unknown-length response limits, timeout,
caller cancellation and no application retry. Local checks cover actual OpenCV RGB PNG encoding,
raw BGR float tensor values, crop shifting/endpoints, stable softmax, explicit probability handling,
class ties, malformed probabilities and unavailable models. Enrollment regressions cover sanitized
503 outage propagation, exact actor/session/operation binding and invalid-binding rejection.

These are **transport and semantic checks, not biometric acceptance**. Real ONNX loading/inference,
hash mismatch with actual licensed artifacts, graph/export parity, real service TLS/destination
behavior, accuracy/threshold calibration, spoofs, multiple faces in real photos, end-to-end protected
capture/device/session/challenge/claim behavior, retention and deployment-native loading require
root's separate controlled acceptance. This task performs no real biometric submission, SQL write,
migration, fixture insertion, package edit, registration edit or Git operation.

Historical execution: [Face-Sidecar-Verification-20261013.md](Face-Sidecar-Verification-20261013.md)
records 122 provider assertions and 353 total offline assertions against the later code, superseding
the initial 112-check result and its pending PdfPig/outage-regression note. The same historical
record's claim that automatic retirement is absent is superseded by the implemented durable
retirement flow. Fresh checks for this configuration pass are recorded in
[Face-Configuration-Readiness.md](Face-Configuration-Readiness.md). None establishes real biometrics.
