# Self-hosted Face configuration readiness

## Latest status - 20 October 2026

The 13 October preparation narrative below is historical. The inspected local
installation now has a CompreFace deployment, two hash-pinned ONNX model files
and enabled face configuration. `PocApproved` is no longer a field in the current
`SelfHostedFaceOptions`; use the actual versioned contracts, not the older table,
when preparing a new deployment. Enrollment/retention authority and real-device
acceptance are still distinct from configuration readiness.

The optional `-CheckProviderConnection` mode first requires strict file,
configuration and local-artifact validation, then performs only a bounded
`GET /healthz` with the actual adapter's private-destination/TLS transport.
It does not send API keys, images, enrollment requests or SQL writes.
The configuration file is held read-only without write/delete sharing across
validation and option binding. The default mode performs no network probe.
The JSON report explicitly identifies `RealPersonAcceptance=not_run`.

`docs/attendance-capture/Prepare-FacePilotConfiguration.ps1` can export existing
flat face/vault settings into a **new owner-only** local diagnostic directory.
It refuses existing targets, linked paths, untrusted source-file ACLs and output
larger than the private reader's 65,536-byte limit before creating the directory.
It neither invents approvals/keys nor changes the API's selected configuration.
The synthetic preparation verifier passed 34/34 checks on 20 October.

The final strict check passed on 20 October against `hr-pilot-build09`:
zero issues/disabled sections, `LocalArtifacts=loaded` and
`ProviderConnection=reachable_with_adapter_tls_validation`.
The initial local-artifact failure was a package-free runner native lookup issue,
not missing ONNX models. Its exact-name codec resolver uses only the staged
build DLL; no system PATH or product configuration was changed.
`RealPersonAcceptance` remains `not_run`. A health response is not credential,
matching, liveness/spoof-resistance or mobile acceptance.

See [the current pilot report](../hr-srs-remediation/HR-Mobile-Pilot-Readiness-20261020.md)
for fresh executed checks, remaining failures and the live/device boundary.

## Historical preparation - 13 October 2026

This is preparation for the existing Development-only CompreFace + SilentFace ONNX/C# PoC.
The actual reviewed ONNX artifact and private CompreFace deployment are still unavailable.
Configuration preparation does not approve production activation or real employee uploads.
No model, threshold, approval, key or provider credential is supplied by this change.

## One-time local validation

From the backend root, use PowerShell 7 and the installed .NET 10 SDK:

```powershell
pwsh -NoProfile -File src/Cosmix.Infrastructure/FaceVerification/Test-FaceConfiguration.ps1
pwsh -NoProfile -File src/Cosmix.Infrastructure/FaceVerification/Test-FaceConfiguration.ps1 -ConfigurationFile $facePrivateConfigurationFile
pwsh -NoProfile -File src/Cosmix.Infrastructure/FaceVerification/Test-FaceConfiguration.ps1 -ConfigurationFile $facePrivateConfigurationFile -CheckLocalArtifacts
```

Set `$facePrivateConfigurationFile` to the absolute path of the separately provisioned private
JSON file containing the sections below. The first command checks empty defaults and is expected
to exit 2 with missing prerequisites. No ambient appsettings, environment variables, credentials
or other secret store is read. Supply the complete candidate configuration, including `PrivateFiles`,
so isolation can be checked against the ordinary document/audit vault. This file is not automatically
installed into the API's configuration; the composition owner must later provision matching inputs
through the existing host configuration after the required approvals.

The candidate file must already exist, be 1-65,536 bytes, outside repositories/application binaries/
`wwwroot`, on a local non-linked path with private ACLs. On Windows, the existing private-path
checker permits the executing identity, SYSTEM and Administrators; other Allow entries or an absent
DACL fail. Run under the intended service identity to check its actual access. Never put this JSON,
key files or model artifacts in Git. Parse/binding/file exceptions are reduced to fixed reason codes;
the report never returns option values, paths, model hashes, credentials or reference images.

The script builds Release with `--no-restore -m:1 -p:UseSharedCompilation=false` and a unique TEMP
output. Its small TEMP runner references those assemblies; its package-free restore uses only its
TEMP directory. No package update or API startup occurs. To reuse a freshly built full solution's
output, pass `-BuildDirectory $faceReleaseBuildDirectory`. This Windows runner copies the already
installed `runtimes/win-x64/native` DLLs; other deployment platforms need their own native acceptance.

Exit codes: 0 means the requested local check passed; 2 means prerequisites failed; 3 means the
runner/build failed. JSON `Issues` contains only fixed setting names and canonical codes.
`DisabledSections` lists six independent activation switches; setup validation checks the supplied
inputs while leaving their configured values intact. A disabled section is not an error in this
preparation check. `PocApproved` and `AuthorityConfirmed` are still required approvals and never
overridden. Local storage constructors inspect existing paths/ACLs/keys and create no evidence,
directories, journal entries or locks. CompreFace option validation constructs no HTTP transport.

`LocalArtifacts=not_checked` means no model bytes, hash, ONNX graph or native codec were loaded.
`-CheckLocalArtifacts` loads exact hash-pinned bytes and verifies the existing ONNX metadata and
codec construction through `SilentFaceLivenessService`, then disposes the local instance.
`loaded_for_poc` means those loading checks passed; it does not mean inference, preprocessing parity,
accuracy, liveness, private HTTP/TLS or real-device acceptance passed. `failed` is a closed failure.
The complete provider is never created, registered or activated by this command. Keep
`SelfHostedFace.Enabled=false` while preparing inputs.

## Exact operator-supplied inputs

JSON section names below are the actual server option contracts; colons denote nesting. Numeric
values must come from their cited approvals/calibration. Allowed ranges are validation limits,
not recommended decision thresholds or lifetimes.

| Section | Required inputs and bounds |
| --- | --- |
| `SelfHostedFace` | `PocApproved`, `ApprovalReference` (nonblank, no controls, <=500 chars), `MinimumSimilarity` (finite, >0 and <=1); `Enabled` stays false during preparation |
| `SelfHostedFace:CompreFace` | `BaseUri` (absolute origin, path `/`, no userinfo/query/fragment), separate `DetectionApiKey` and `VerificationApiKey` (nonblank printable ASCII, <=256 chars), `DeploymentVersion` (nonblank/no controls, <=100 chars), `MinimumDetectionProbability` (finite, >0 and <=1) |
| `SelfHostedFace:SilentFace` | `Models` (one to four distinct hash-pinned reviewed exports); `Enabled` is independent of parent selection |
| `FaceVerificationPolicy` | `AuthorityConfirmed`, `ApprovalReference`, `RetentionPolicyReference`, `ChallengeLifetimeSeconds` (1-3600), `MaximumAttemptsPerPunch` (1-100) |
| `FaceCapture` | `ApprovalReference` (nonblank, <=1000 chars), `LifetimeSeconds` (1-300); `Enabled` is independent |
| `FaceStorageRetention` | `AuthorityConfirmed`, `ApprovalReference` (nonblank/no controls, <=1000 chars), positive explicit `CaptureUseLifetimeSeconds` and `EnrollmentUseLifetimeSeconds`; `Enabled` is independent |
| `FaceTemplateStorage` | `Storage` private-vault inputs below; `Enabled` is independent |
| `FaceStorageRetentionJournal` | `IndexDirectory`, `Storage` private-vault inputs below; `Enabled` is independent |
| `PrivateFiles` | The existing ordinary private-vault inputs below, required for isolation comparison |

CompreFace transport limits have existing non-biometric defaults: `TimeoutSeconds=10` (allowed
1-30) and `MaximumResponseBytes=65536` (allowed 256-1048576). SilentFace `TimeoutSeconds=10`
(allowed 1-30). These defaults do not set any match/liveness probability. HTTPS uses normal
certificate/hostname validation; `AllowLoopbackHttpForPoc=true` is the explicit exception for a
literal loopback HTTP origin. At runtime DNS must resolve entirely to RFC1918/IPv6 ULA addresses
(or explicitly allowed loopback); public/link-local/metadata destinations fail. This command
does not resolve DNS, open sockets, check a TLS certificate or check that the two keys work.

Each `SelfHostedFace:SilentFace:Models:<index>` requires:

| Field | Accepted contract |
| --- | --- |
| `Path`, `Sha256` | Fully qualified local `.onnx`, no UNC/reparse points/Windows alternate streams; exact 64-hex artifact SHA-256; one self-contained file, 1-33,554,432 bytes when loaded |
| `SourceRevision` | Exact reviewed 40-hex upstream revision |
| `ExportReference`, `LicenseReviewReference`, `SemanticsReviewReference` | Actual nonblank review references, no controls, <=500 chars each; references alone do not review the artifact |
| `InputName`, `OutputName` | Explicit nonblank names, no controls, <=100 chars; graph has exactly one static float32 input `[1,3,InputHeight,InputWidth]` and one static float32 output `[1,ClassCount]` |
| `InputWidth`, `InputHeight` | Explicit integers 16-512 |
| `InputContract` | Exactly `float32-NCHW-BGR-0_255-OpenCV_INTER_LINEAR-CompreFaceExtent` |
| `CropKind`, `CropScale` | `UpstreamScaledBox` with explicit finite scale 1-10, or reviewed `WholeImage` with no scale |
| `OutputKind` | Explicit `Logits` or `Probabilities` |
| `ClassCount`, `LiveClassIndex` | Class count 2-16 and explicit zero-based live-class index in range |
| `MinimumLiveProbability` | Explicit calibrated finite probability >0 and <=1; each configured model must independently pass |

All four object/index directories (ordinary objects, biometric objects, journal metadata and
journal index) must already exist and be mutually nonoverlapping. Each `Storage`/`PrivateFiles`
requires `Directory`, stable `KeyReference` (1-80 ASCII letters/digits/`.`/`_`/`-`) and `KeyFile`
(existing private 32-byte binary AES key). Optional `DecryptionKeyFiles` maps up to 16 retained
references to existing private keys. Provision every key outside all four roots. Biometric and
ordinary keyrings need distinct references, paths and key material, including retained keys;
different filenames with equal material fail. Journal metadata may share the biometric keyring.
`MaxSizeBytes` defaults to 10 MiB, with allowed storage limits 1-64 MiB; the journal requires at
least 8192 bytes. Set the biometric cap to support the approved capture/reference sizes (the
capture contract allows up to 512 KiB, adapter inputs up to 1 MiB). Do not generate keys here as
part of a validation run. Durability/write access and recovery require separate acceptance.

Existing durable retirement stages server-owned intents inside business transactions, validates
committed sources outside them, closes use through the encrypted journal and retries/reconciles
later. `FaceRetirementWorker` is separately opt-in: `Enabled`, `BatchSize` (1-100; existing default
100), `PollSeconds` (1-3600; existing default 30). This configuration validator neither starts nor
certifies that worker. `FaceDisposal.Enabled` does not supply an approved disposal policy/adapter;
physical deletion remains unavailable. RetireAsync preserves ciphertext and audit evidence.

## Verification and acceptance boundary

Existing offline checks can be run from the backend root:

```powershell
pwsh -NoProfile -File docs/attendance-capture/RunFaceSidecarChecks.ps1
```

Fresh execution on 13 October 2026:

| Check | Result |
| --- | --- |
| Full solution Release, no restore, serial build, TEMP output | Passed; 0 warnings, 0 errors |
| New TEMP configuration probe | Baseline RED: missing validator, exit 1; GREEN: 49 assertions, exit 0 |
| Existing provider / lifecycle / protection / readiness / enrollment / capture harnesses | 122 / 66 / 33 / 10 / 19 / 145 assertions; all exit 0 (395 total) |
| Validator command with empty defaults | Exit 2; missing prerequisite names; all six switches disabled |
| Validator command with complete synthetic disabled inputs | Exit 0; `LocalArtifacts=not_checked`; no activation |
| Validator command with invalid synthetic local graph/pin | Exit 2; closed load failure |
| Validator command with malformed private JSON | Exit 2; sanitized file failure |
| Command output sanitization | Passed for all four cases; no synthetic secrets/model paths |

Configuration probes cover missing fields, private/public/loopback origin constraints, finite
thresholds, binding/parser failures, duplicate JSON keys, absent model paths, actual native-loader
rejection of invalid bytes and pin mismatch, disabled selection, real TEMP private ACL/keyring
isolation, same-material keys in different files, overlapping roots and absence of storage writes.
The only `.onnx`-suffixed test file contains intentionally invalid text bytes; it is not a model
export or weights and cannot perform inference. Test approvals, thresholds and keys are synthetic
and confined to TEMP; none are deployment inputs. Existing harness copies remove only `#:` build
directives and preserve assertions. The current capture harness has 145 assertions, superseding
the earlier historical 103-count record. No real provider HTTP, SQL or usable ONNX inference ran.

Machine-local reproduction of the new configuration probe from the backend root:

```powershell
$faceChecks = 'C:/Users/Huma.Khan/AppData/Local/Temp/Cosmix-Face-Configuration-3f1ff71795be4f508cd65e29571d8ee0'
dotnet build Cosmix.Backend.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false "-p:OutDir=$faceChecks/ReleaseBuild/" -v:minimal
dotnet run --project "$faceChecks/Checks.csproj" -c Release --no-restore "-p:BuildDirectory=$faceChecks/ReleaseBuild" -p:HarnessFile=Probe.cs -v:minimal
```

The TEMP project has no package/project references, and its initial restore used only its own local
directory. The new probe remains TEMP as required; no permanent test project was added. The same
command with `-p:HarnessFile=VerifySelfHostedFaceProvider.cs` (or each other existing harness name
in `RunFaceSidecarChecks.ps1`) ran the six preserved offline harnesses against these fresh assemblies.
Two implementation compilation failures (unavailable JSON extension, then `IConfigurationRoot`
disposal typing) were corrected in the owned helper before the successful Release build; no
dependencies or shared registrations were changed.

The public API continues to return canonical configuration-only readiness. Its FaceReadiness
gate combines confirmed policy, actual provider capability, private storage and compatible active
enrollment. Liveness plus 1:1 matching remains Check In-only; Check Out requires no face.
Capture/proof binding, expiry/replay/single-use claims, authorization rechecks after I/O and
private encrypted evidence remain enforced by the existing Application services. This local
report is never used as provider capability or proof authority.

Residual prerequisites: actual licensed/reviewed self-contained ONNX export and weights/data
rights; hash/graph/export/preprocessing/crop/output parity; calibrated ensemble/decision thresholds;
private pinned CompreFace deployment and working separate keys; real DNS/TLS/destination/logging
acceptance; consented reference/capture images and approved physical devices; injection/video replay/
spoof/multiple-face/lighting/error-rate testing; authenticated SQL/device/session/challenge/race
acceptance; service-identity ACLs, disk durability and cross-process/crash recovery; approved privacy,
medical alternatives, retention/holds, physical disposal/orphans and backup/key retention. Model
loading alone supplies none of these approvals. No production activation approval is inferred.
