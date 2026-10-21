# B03 and B01 OFFICE correction consumers

Scope: Application Corrections/CarryForward, their module repositories/registration, standalone checks, and subsequently authorized B03 API controllers/client contracts. No Domain, capture, calculation/payroll/OT, office-source, core DI/catalog/context/migration/snapshot/Program files edited here. No SQL, host, provider, credentials, secrets, Git synchronization or database configuration writes. The latest controller/verification checkpoint below supersedes the earlier pending controller/build/audit-fixture status.

## Parent integration checkpoint

- New permission IDs: `attendance.corrections.alternatives.decide` and `attendance.corrections.alternatives.configure`.
- Definitions are appended to `CorrectionPermissions.Definitions`. Both are sensitive, web-only, non-self. No grants are automatically created.
- Fixed `ADMIN` authority and permission must belong to the same current role assignment and cover every original/current scope. Web MFA and live session are required, including receipt replay. A custom HR role, an unrelated Admin assignment, or a SuperAdmin role alone does not supply this confirmed Admin authority.
- **No new EF model fields/entities or migration are needed.** Configured evidence-policy versions reuse protected append-only `AuditEvent` records and existing operation receipts. Supporting documents reuse existing `EmployeeDocument` records, not copied public files.
- Module registration extensions bind `CorrectionAlternativePolicyService`, `ICorrectionAlternativeEvidenceReader`, and `ICorrectionAlternativePolicyRepository`. Core registration need not be duplicated.
- `AttendanceCorrectionService` now requires `IOfficeAttendanceSourceReader` and `ICorrectionAlternativeEvidenceReader`.
- `CorrectionReviewService` additionally requires those readers and `IAccessManagementRepository`.
- `CarryForwardEvidence` requires `IOfficeAttendanceSourceReader`, `IIndividualRosterResolver`, and `IAttendanceCalculationSnapshotProducer`.
- `CarryForwardService` additionally requires the alternative evidence reader and access-management repository.
- `CorrectionAlternativePolicyService`, `CorrectionReviewService`, and `CarryForwardService` also require the existing platform `TimeProvider`. No Options binding is needed. New special authority uses `PermissionGrant.IsEffectiveAt` at a fresh time AFTER the access query and again after role queries; unknown/future/expired timing fails closed. Policy selection and document-query completion also recheck effective dates after IO.

## Calculator schema contract

Normal Site correction schemas 1/2/3 remain readable with original omission/hash contracts. Only schema3 remains applicable for ordinary Site duty. Normal OFFICE correction evidence uses schema4 and `correction-source-v4-office` fingerprints. OFFICE raw/session source fields are optional and omitted on Site contracts.

Admin alternative corrections use **schema5**. `AttendanceCorrectionService.Matches` validates the immutable original correction fields, exact `AlternativeKind`, expected configured policy ID/fingerprint, document IDs/digests/versions/scan-verification evidence and retained factual source. Its underlying factual source remains Site-v3 or OFFICE-v4; do not treat schema5 as ordinary HR authority.

Parent/calculation owner must accept schema5 ONLY for `ManualSession`, after those source checks AND verification of the actual matching approved decision: fixed `ADMIN` actor role, different requester, exact correction/approval/evidence identity, and retained dedicated alternative-decision authority/audit. The decision operation is `attendance.correction.decide`, with permission `attendance.corrections.alternatives.decide` and explicit matching `DecideCorrectionRequest.AlternativeKind`. Do not merely widen the accepted schema number. Do not re-resolve a later alternative policy/document state to rewrite already approved/finalized history.

The initial summary calculation runs after the approved decision/session flush but before the outer operation stages its final audit/receipt. Avoid an audit/summary dependency cycle: validate the exact staged approved decision and protected schema5 source inside that existing authorized transaction; the dedicated permission/same-assignment fixed Admin guard is enforced before the write and receipt replay. For committed historical sources, verify the retained matching Admin decision/audit. An ordinary HR decision must not qualify. Parent owns this calculator/repository integration.

Factual carry-forward keeps legacy Site schema1, OFFICE schema2, and uses schema3 for alternatives. It validates the saved configured alternative evidence and exact approved `ADMIN` decision during immutable ledger reconstruction. All variants retain the original lock, frozen summary/session membership, prior virtual interpretation, first eligible target month and signed factual delta. No money is generated.

## Completed B03 controller/client integration

The user subsequently assigned these B03 controllers/contracts to this work. Existing submit/decide routes inherit optional alternative fields unchanged; dedicated controllers now expose these real service methods with no-store/authentication/CSRF/Idempotency-Key conventions:

- `CorrectionAlternativePolicyService.MineAsync(kind, ...)`: safe self requirements, including explicit `not_configured` state.
- `HistoryAsync(...)` and `ConfigureAsync(...)`: fixed Admin evidence-policy management. `RequiresSupportingDocuments` and `AllowedDocumentTypes` are explicit required fields; no certificate type is assumed.
- `CorrectionReviewService.AlternativeQueueAsync(...)` and `AlternativeDetailAsync(...)`.
- `CarryForwardService.AlternativeQueueAsync(...)` and `AlternativeDetailAsync(...)`.

Nine actual registered actions cover policy history/configuration, safe self requirements, dedicated Admin correction queue/detail/decision, and dedicated Admin factual carry-forward queue/detail/decision. Dedicated decision routes reject absent AlternativeKind before service invocation; the underlying services enforce exact saved kind, scope, same-assignment fixed ADMIN authority, current MFA/session/grants, retry and no-self-review. No duplicate decision workflow, model or permission catalog is added.

Actual routes, JSON semantics, source schemas, Office identity retention, scope rules, error/retry behavior and explicitly unexecuted client examples are in `B03ClientContract.md` and `B03Contracts.http`. Own `B03VerifyRegisteredContracts.cs` generates actual MVC/OpenAPI with existing transformers without running Program or starting a listener/worker. Parent/calculation owner retains calculator schema5/special-authority integration; this work does not claim actual HTTP or SQL acceptance.

## Implemented behavior

- OFFICE intake/review binds actual historical Branch, office roster, employee assignment, shift, office boundary and holiday-calendar identities. It authoritatively re-reads and rejects changed/missing sources.
- Approved office manual duty has no raw Check-In/Check-Out or successful face assessment. Existing failed evidence is unchanged. Verified-duty revisions retain the exact opening office identities, including overnight business date and transfer checks.
- Normal missing/failed Check-In remains one authorized HR reviewer, no self-review. Explicit medical/accessibility/out-of-boundary alternatives require configured evidence rules and one dedicated Admin decision. A known reliable outside-boundary source cannot pass the ordinary HR manual path.
- Missing configuration blocks alternatives with `alternative_evidence_policy_required`. An explicit written-only configuration can be approved; no hidden medical-certificate requirement exists. If supporting documents are required, only selected employee-owned, available, verified/scanned document types qualify; changed/revoked evidence fails revalidation.
- Evidence policy versions have exact expected version, prospective effective dates and approval reference. New requests bind the policy in force at submission; a changed effective policy requires refreshed submission, not silent inheritance.
- New alternatives remain corrections only. They never bypass live face/geofence punch verification, enable production biometrics, activate legal penalties or release salary.
- Locked OFFICE factual carry-forward validates frozen evidence and exact current historical office resolution. Empty frozen office duty is usable ONLY if the successfully calculated daily provenance hash can be authoritatively rebuilt byte-identically, including the exact office source. A missing legacy proof is not zero and remains unavailable.
- Subsequent corrections compare with the last approved virtual interpretation, retaining original evidence and avoiding duplicate factual deltas. Locked months never reopen; a changed first eligible target is shown before resubmission.
- Normal HR review queues omit Admin alternatives. Protected alternative detail requires the dedicated Admin authority. Own requests remain readable by their requester.

## Verification boundaries

Standalone harnesses use actual application/authorization/evidence/domain code and explicitly synthetic IO adapters. Release build and SQL translation are not authenticated SQL or device acceptance. Real authenticated SQL/concurrency/rollback, company policy approval, document scanning, both client implementations, face/device/provider acceptance and production legal activation remain parent/external acceptance work.

RED observations: OFFICE preview lacked office source; frozen OFFICE duty was rejected as Site-less; mixed Site roster/OFFICE session was not rejected; ordinary HR could approve an explicitly marked Admin alternative. Each was reproduced before its corresponding behavioral fix. Intermediate compile failures from concurrently edited A/capture files were not counted as behavioral RED.

Executed results are appended after the final strict regression pass below.

## Exact changed files

Application Corrections:

- `src/Cosmix.Application/Attendance/Corrections/CorrectionContracts.cs`
- `src/Cosmix.Application/Attendance/Corrections/CorrectionSourceEvidence.cs`
- `src/Cosmix.Application/Attendance/Corrections/CorrectionOfficeEvidence.cs` (new)
- `src/Cosmix.Application/Attendance/Corrections/AttendanceCorrectionService.cs`
- `src/Cosmix.Application/Attendance/Corrections/CorrectionReviewService.cs`
- `src/Cosmix.Application/Attendance/Corrections/AttendanceCorrectionRegistration.cs`
- `src/Cosmix.Application/Attendance/Corrections/CorrectionAlternativeContracts.cs` (new)
- `src/Cosmix.Application/Attendance/Corrections/CorrectionAlternativePolicyService.cs` (new)

Application CarryForward:

- `src/Cosmix.Application/Attendance/CarryForward/CarryForwardContracts.cs`
- `src/Cosmix.Application/Attendance/CarryForward/CarryForwardEvidence.cs`
- `src/Cosmix.Application/Attendance/CarryForward/CarryForwardRules.cs`
- `src/Cosmix.Application/Attendance/CarryForward/CarryForwardService.cs`
- `src/Cosmix.Application/Attendance/CarryForward/CarryForwardQueries.cs`

Infrastructure:

- `src/Cosmix.Infrastructure/Repositories/Attendance/CorrectionAlternativePolicyRepository.cs` (new)
- `src/Cosmix.Infrastructure/Repositories/Attendance/AttendanceCorrectionRegistration.cs`

Standalone checks/documentation:

- `docs/attendance-corrections/VerifyCorrectionService.cs`
- `docs/attendance-corrections/VerifyCarryForwardService.cs`
- `docs/attendance-gap-b/B03VerifyOfficeEvidence.cs` (new)
- `docs/attendance-gap-b/B03VerifyAlternativePolicies.cs` (new)
- `docs/attendance-gap-b/B03Verify.ps1` (new)
- `docs/attendance-gap-b/B03-Office-Corrections-Handoff-20261013.md` (new)

Production source was re-frozen after the parent-requested P1 grant-timing integration. Configuration Kind/EffectiveFrom have explicit `JsonRequired` annotations. Normal proposal `AlternativeKind` remains optional/omitted on null, preserving legacy Site payload hashes.

## Prior source-checkpoint results

Final scoped Application Release build: **0 warnings, 0 errors**, artifact root `%TEMP%/cosmix-b03-frozen-app`. Final current source service runs passed after the TimeProvider/P1 metadata update:

| Check | Executed result | Boundary |
|---|---:|---|
| `VerifyCorrectionService.cs` | 136 passed | Real intake/review/operation code, synthetic auth/storage/repository; OFFICE, normal HR, dedicated Admin alternatives, scopes, MFA, stale sources, retry, rollback simulation |
| `VerifyCarryForwardService.cs` | 79 passed | Actual frozen/virtual evidence and ledger services with synthetic IO; Office absence proof, successive deltas, target races and Admin authority |
| `B03VerifyOfficeEvidence.cs` | 21 passed | Exact legacy Site-v2/v3 and carry-forward JSON bytes, OFFICE bindings and malformed-context rejection |
| `B03VerifyAlternativePolicies.cs` | 26 passed | Real protected policy/audit/receipt services, configured document requirements, availability/revocation/version guards; unknown/future/expired after-query timing |
| `VerifyCarryForwardRules.cs` | 13 passed | Pure calendar/tick rules |
| `VerifyExceptions.cs` | 54 passed | Actual exception services with synthetic collaborators |
| `VerifyCorrectionQueries.cs` | 33 passed | SQL translation, no connection |
| `VerifyCorrectionEligibility.cs` | 25 passed | Historical eligibility and SQL translation, no connection |
| `VerifyCarryForwardQueries.cs` | 23 passed | SQL translation, no connection |
| `VerifyExceptionQueries.cs` | 35 passed | SQL translation, no connection |

`git diff --check` on the scoped files passed. Two earlier full Release checkpoints passed 0 warnings/0 errors, but they predate the final P1 timing change. The final full global build remains parent-owned after the A source freeze.

The combined `B03Verify.ps1` strict run was interrupted by concurrently edited, externally owned source: first `StaffTaxonomyResolver.cs` CS0136, then `EmployeeHiringService.cs` CS0128/CS0165. Owners were notified; no warning suppression or source rollback was used. Later Infrastructure-only runs saw a briefly incomplete `SelfHostedFaceConfigurationReadiness.cs` (`AddJsonStream` / `IConfigurationRoot` disposal); successful query reruns above used the repaired current source.

`VerifyAuditReads.cs` remains **not green**: the current B02 `AttendanceAuditService` constructor additionally requires `ICaptureValidationEvidenceRepository`, `ISchedulingJournalRepository`, and `TimeProvider`, while its existing fixture still uses the old constructor (CS7036 at line229). This capture/audit fixture integration is parent/B02-owned and was not bypassed or silently counted as passing.

No SQL target was connected or migrated; no hosted authenticated HTTP acceptance, actual SQL concurrency/rollback, real document scan, physical evidence deletion, CompreFace/ONNX inference, mobile device, or React client was tested. New policy/alternative controller registration and calculator schema5/special-authority consumption remain parent-owned acceptance work. Full Attendance acceptance is **not achieved** by these source checks.

Final narrow source checkpoint hashes (SHA256):

- `CorrectionAlternativeContracts.cs`: `D45317FFB074E1B7A9F0B72058854AE805F96B2A22B96A04580DB1DAA87904E8`
- `CorrectionAlternativePolicyService.cs`: `AD41BF149D6CB996AA6500FF2AAC4E432BF2B5CA995619E5CC690EA05DA6ED83`
- `CorrectionReviewService.cs`: `74A976E7365C737B63DE3F1AA03D01C51DF36AC48AEBFD22747443A78D968BAC`
- `CarryForwardService.cs`: `F36A78583461C4566F5D6065FBC29016AB2B3AFA8F19923E9A2F6DD2089C5FF5`

## Latest B03 controller source freeze - 13 October 2026

The prior pending controller/build/audit-fixture statuses above are superseded by this checkpoint. No production Application/Infrastructure/model/core/EF source changed during this controller continuation. The four narrow Application hashes above were rechecked and remain identical. Parent-owned `VerifyCaptureService` and `VerifyRegisteredContracts` were not edited.

Fresh integrated `Cosmix.Backend.sln` Release build passed **0 warnings / 0 errors** using `--no-restore -p:RestoreLockedMode=true` and the already-created `D:/CosmixVerification/b03-contract-red-20261013/artifacts` output. Neither source bin/obj nor parent build output was targeted.

| Fresh executed check | Result | Actual boundary |
|---|---:|---|
| `B03VerifyRegisteredContracts.cs` | 155 passed | Real registered MVC/OpenAPI, typed security/response/request metadata, optional legacy JSON, strict unknown-member/required policy JSON, missing-case decision boundary, all 15 shipped HTTP examples inspected against registered paths/JSON; no requests issued |
| `VerifyCorrectionService.cs` | 136 passed | Actual intake/review/authorization/evidence services, synthetic IO |
| `VerifyCarryForwardService.cs` | 79 passed | Actual frozen/virtual factual ledger and Admin authority services, synthetic IO |
| `B03VerifyOfficeEvidence.cs` | 21 passed | Real Office source binding and exact legacy bytes |
| `B03VerifyAlternativePolicies.cs` | 26 passed | Actual protected configurable policy/evidence/authority services, synthetic IO |
| `VerifyAuditReads.cs` | 47 service/authorization + 12 translation passed | Narrow B02 fixture constructor adaptation only; legacy rows explicitly have no capture-validation record, never fabricated success; connection interceptor prohibits SQL |

The controller test first reproduced the genuinely missing registered GET alternative-policy route before controllers were added. A later example-pack test wildcard/static-route ambiguity was an offline runner bug, not an MVC defect; the runner now checks unique highest-precedence registered method/path. C: full initially prevented NuGet and source writes. Only two reviewed, previously generated B03 TEMP bin/obj folders (no DB/key/credential/reparse point) were preserved by moving them to D:, not deleted; the parent separately recovered disk space. Existing D: metadata/build outputs and existing lightweight Application caches were reused thereafter. No existing database, configuration or source code was removed.

Exact additional changed files in this controller continuation:

- `src/Cosmix.Api/Controllers/Attendance/AttendanceCorrectionAlternativesControllers.cs` (new; 6 actions)
- `src/Cosmix.Api/Controllers/Attendance/AttendanceCarryForwardAlternativeControllers.cs` (new; 3 actions)
- `src/Cosmix.Api/Controllers/Attendance/MyAttendanceCorrectionsController.cs` (B03 descriptions only)
- `src/Cosmix.Api/Controllers/Attendance/AttendanceCorrectionReviewControllers.cs` (B03 descriptions only)
- `src/Cosmix.Api/Controllers/Attendance/AttendanceCarryForwardControllers.cs` (B03 descriptions only)
- `docs/attendance-corrections/VerifyAuditReads.cs` (current required validation-evidence/journal/clock fixture; no source change)
- `docs/attendance-gap-b/B03VerifyRegisteredContracts.cs` (new, own runner)
- `docs/attendance-gap-b/B03ClientContract.md` (new)
- `docs/attendance-gap-b/B03Contracts.http` (new, examples not executed)
- `docs/attendance-gap-b/B03Verify.ps1` (adds own registered-contract runner; combined suite was not rerun in this continuation)
- `docs/attendance-gap-b/B03-Office-Corrections-Handoff-20261013.md` (this record)

New controller freeze SHA256:

- `AttendanceCorrectionAlternativesControllers.cs`: `A839D7A66247CBA3B0F0F4A950D68CE482C7635D57A71839B735DAF68CEB78B7`
- `AttendanceCarryForwardAlternativeControllers.cs`: `1EA54E212DDDCE8B3DA9C94BACAE9A46CB6A44A92ECFB22A63312592F1D5736E`

No additional core DI, catalog, EF/model or migration requirement: parent registrations already supplied the required services. Calculator interpretation/financial readiness remains Herschel/parent-owned. A source build and registered OpenAPI do not validate parent host guards, real login/CSRF, SQL storage/concurrency/rollback, actual document scan, face/provider/device or React/React Native clients. Those were not run or claimed. Full Attendance acceptance remains **not achieved** by this scoped checkpoint.
