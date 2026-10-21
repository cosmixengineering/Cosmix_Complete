# HR and mobile pilot readiness - 20 October 2026

## Decision

**Not full production acceptance, and not "only SMS remains".**
The backend has implemented workflows and bounded passing acceptance results.
This record distinguishes those results from unexecuted business/device tests.
No migration, existing business-data mutation, real employee enrollment, payment,
or legal activation was performed in this continuation.

## Fresh code fix and verification

Historical vehicle submission/approval receipt replay incorrectly rejected an
earlier approved service after a valid successor consumed further distance.
Replay now proves the earlier record belongs to the full current retained chain.
Original proof, source quantities, independent reviewer, current authority,
competing-root rejection and fresh-write validation remain enforced.

Release build: **0 warnings, 0 errors**.

Frozen output:
`D:/CosmixVerification/hr-backend-20261020/hr-pilot-build09/bin/Cosmix.Api/release`.

| Standalone suite | Passing checks |
| --- | ---: |
| Vehicle remaining historical boundaries, including successor replay | 109/109 |
| Fuel reconciliation and immutable legacy evidence | 42/42 |
| Extra/KPI benefit workflow | 1,387/1,387 |
| Recovery work claims | 132/132 |
| Benefit KPI eligibility | 79/79 |
| Total | **1,749/1,749** |

Retained logs and frozen-input hashes:
`D:/CosmixVerification/part-c-parent-20261013/managed-1c9c0448ca5342fb8b8c07be10738801/`.

The preceding invocation stopped because the reviewed fixture assembly/hash
environment bindings were missing. The corrected invocation used process-only
bindings and restored them afterwards. No failed invocation is counted as a pass.
The fixture SHA256 was
`D604753ED9E704AFA615BE3EA00C74EE68813FD01E4D4DF68E563A70E5035668`.
Product assemblies came from build09, not the retained fixture's older build.

These are real application-service checks with synthetic collaborators. They
are **not authenticated SQL, SQL race or real-device acceptance**. Independent
read-only review found no actionable issue in the vehicle replay change.

## Existing bounded live evidence

- C04: 28 checks / 35 HTTP requests; authenticated department concurrency and
  permissions, not blanket HR acceptance.
- O1Continue01: 38 HTTP checks / 45 requests; one synthetic deployment, one
  PKR 200 claim and independent Admin approvals. SELECT-only reconciliation
  verified persistence after the runner's final audit-query timeout.
- V1: 27 checks / 36 requests; vehicle/custody persistence subsequently checked,
  but the original run stopped before logout and is **not an overall pass**.

See [O1 evidence](HR-Outstation-O1-20261020.md),
[C04 evidence](HR-Business-C04-20261020.md),
[V1 evidence](HR-Vehicle-V1-20261020.md), and
[remaining business matrix](HR-Business-Acceptance-Matrix-20261020.md).
Their original evidence and failures remain retained.

## Face pilot

The inspected local configuration already selects the real self-hosted
CompreFace adapter and ONNX liveness service. Two pinned model files exist.
The existing API on port 7240 responded to health and advertised nine face
routes. This does **not** prove that a real person's matching/liveness passed.
The running API predates build09; this continuation did not replace it.

The strict configuration-file check rejected the UserSecrets parent-folder ACL.
No ACL guard was relaxed and the existing secret store was not changed.
A new owner-only diagnostic copy exported 73 existing face/vault settings to:
`C:/Users/Huma.Khan/AppData/Local/Cosmix/FaceVerification/pilot-20261020/face-pilot.json`.
No keys or approvals were generated, and no host was switched to this file.
The preparation helper passed **34/34 synthetic local-file checks**, including
refusal of existing targets and oversized output before directory creation.

Fresh strict configuration-only validation passed with no issues and no disabled
sections. A missing-file negative probe returned the canonical sanitized
`private_configuration_file_invalid` failure without contacting the provider.
The first local-artifact probe failed `local_artifact_load_failed`; its diagnostic
result is retained rather than counted as a pass. Diagnosis confirmed both ONNX
models passed hash and metadata validation; OpenCvSharp's legacy native lookup
failed in the package-free diagnostic runner (`DllNotFound`, `0x8007007E`).
The copied managed/native binaries matched build09. The runner now resolves only
the exact codec import to its staged absolute path, with no machine PATH,
product-code, host or security-gate change.

**Fresh final strict preflight and provider-health check: PASS.**

```json
{
  "Issues": [],
  "DisabledSections": [],
  "LocalArtifacts": "loaded",
  "Passed": true,
  "ProviderConnection": "reachable_with_adapter_tls_validation",
  "RealPersonAcceptance": "not_run"
}
```

The final diagnostic runner compiled with zero warnings/errors. This validates
local models/metadata/codec, private configuration/storage inputs and the
adapter-protected health connection. The health request sends no API keys or
images: it does **not** independently validate credentials, recognition accuracy,
spoof resistance, successful enrollment, or device capture. No probability,
approval, expiry or retention guard was relaxed.
No real person's photo was uploaded, no real employee was created or approved,
and no production activation is certified by this report.

Fresh build09 face-security/service regressions passed **232/232**:
capture protection/JSON 33, enrollment journal 19, capture service 180.
Logs:
`D:/CosmixVerification/part-c-parent-20261013/managed-f04113c6f6fb428b97f59cf26108e87d/`.
These use synthetic collaborators and do not establish real matching/liveness,
authenticated SQL, or mobile-device acceptance.

## What still prevents an all-HR production sign-off

1. Authenticated SQL lifecycle/storage/concurrency acceptance for benefit award,
   consumption, correction/resolution and actual recovery-work fulfilment.
2. Live historical trip/fuel/maintenance reconciliation and the additional
   outstation home-break, partial-day and policy-boundary cases. Offline
   coverage does not close these live cases.
3. Real mobile capture, enrollment, matching and spoof-rejection testing with
   a consenting test volunteer, plus device/session/replay and failure paths.
4. A working approved OTP delivery path for a new employee. The local web OTP
   helper is restricted to named synthetic test accounts and is not a generic
   mobile/new-employee authentication channel.
5. Deployment-specific operational acceptance: service identity/private storage,
   restart/recovery, backups and restoration, monitoring, TLS and provider
   availability. The current development process is not an IIS acceptance test.

Unverified legal payroll rules remain inactive. Legal review is not replaced
by an HR test or a face configuration flag.

## Client-team handoff

The application team can integrate against the existing backend contracts.
For a real-person pilot, use normal HR creation and independent Admin approval,
approved mobile authentication, device registration and the server-owned
enrollment/challenge/capture flow. Both server-side liveness and matching must
pass for Check-In; Check-Out remains geofence-only. Do not mark a person enrolled
from a UI flag, manually insert accepted evidence, or bypass OTP.

## Files changed in this continuation

- `src/Cosmix.Application/HR/VehicleFuel/VehicleConsumedMaintenance.cs`
- `src/Cosmix.Application/HR/VehicleFuel/VehicleFuelService.cs`
- `docs/hr-srs-remediation/VerifyVehicleRemainingBoundaries.cs`
- `docs/hr-srs-remediation/Benefit-Correction-Contract.md`
- `docs/attendance-capture/Prepare-FacePilotConfiguration.ps1`
- `docs/attendance-capture/VerifyFacePilotPreparation.ps1`
- `src/Cosmix.Infrastructure/FaceVerification/Test-FaceConfiguration.ps1`
- This report and linked latest-status documentation.

No schema changes, commits, synchronization or migrations are part of this work.
