# B02 integration handoff

Status: **SCOPED READY for the parent's single B24 scaffold**. Owned Application/Domain/controller/
configuration/repository source is stable. Parent granted the A23 handoff at
`20261013121357_CompleteHrFoundationPartA` before any B02 Domain/EF edits. Model names are stable.
The desktop messaging tool refuses messages to the native ancestor and this session has no native
collaboration messaging tool. This file carries the required proposed hooks for the parent.

Fresh `B02Verify.ps1 -RequireCatalog` passed strict Release (zero warnings/errors) and 118 checks:
31 rules + 27 evaluator/finalization + 7 protected reader + 22 registered management + 31 persistence.
Persistence checks attempted zero connections and executed zero commands. Final logs:
`C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-b02-33ff33c52e294e859500830191796c77`.
No further owned model/configuration/repository edit is pending before B24.

Parent owns AttendanceCaptureService, existing RawPunchEvent/WorkSession/PunchAssessment,
existing checks, shared catalog/DI and the later migration. B02 will not edit those files.

Catalog definitions supplied in `CaptureValidationPermissions.Definitions`; parent has now wired them:

- `CaptureValidationPermissions.Read`: `attendance.capture.validation.read`, global All scope, web MFA.
- `CaptureValidationPermissions.Manage`: `attendance.capture.validation.manage`, global All scope,
  web MFA, `requireSystemRole: true` on the same fixed Admin/SuperAdmin permission assignment.
- No seeded permissions, grants, thresholds, provider codes or company policy.

Required runtime constructor: add `ICaptureValidationEvaluator captureValidation`.
After chronology/session provenance checks and before consuming face proof or creating a session,
call `EvaluateAsync(raw, identity.Device, active.Count == 1 ? active[0] : null, decision, token)`.
The result is a `CaptureValidationEvaluation`, an unpersisted, raw-bound candidate. Use its `Decision`
as the candidate decision. **After every await, refresh the server clock and recheck face proof expiry
before consuming proof.** Then call the mandatory synchronous finalization method exactly once:

```csharp
var validation = await captureValidation.EvaluateAsync(raw, identity.Device,
    active.Count == 1 ? active[0] : null, decision, token);
decision = validation.Decision;
assessedAt = clock.GetUtcNow();
// Parent rechecks IsValidProof at assessedAt and sets the final decision if expired.
var validationEvidenceId = captureValidation.RecordFinal(raw, validation, decision, assessedAt);
// No further await: Consume and construct PunchAssessment using this same decision/assessedAt.
```

`RecordFinal(RawPunchEvent, CaptureValidationEvaluation, CaptureDecision, DateTimeOffset)` returns
the staged evidence ID. Only this method stages final evidence in the same authorization transaction
as raw/assessment/session writes. It requires the exact candidate issued by the same scoped evaluator,
matches employee/action/device/raw/request hash/receipt time, forbids backdating before evaluation,
rejects future timestamps, prevents unsafe acceptance upgrades and rejects duplicate finalization.
Its evidence names `CandidateDecision` and `FinalAssessment` explicitly. An Accepted candidate may
therefore finalize Rejected after face expiry without recording a contradictory Accepted final artifact.
Keep existing blocking gate reasons; offline stays Unverified. Never use the HR `PolicyVersionId` FK.

Results expose codes, evidence IDs and capture-policy version/revision/source fingerprint.
Expose these separately in capture/audit DTOs when integrating. Existing raw evidence stays immutable.
New capture policy cannot change HR financial policy hashes or late counters.

Parent-owned integration locations:

- `src/Cosmix.Application/Authentication/Access/AccessPermissionCatalog.cs`: import the new namespace
  and include `.. CaptureValidationPermissions.Definitions`. Both supplied definitions are sensitive,
  management, non-self permissions; no role grants are seeded.
- `src/Cosmix.Application/DependencyInjection.cs`: call `services.AddCaptureValidationApplication()`.
  This registers `CaptureValidationPolicyService`, its `ICaptureValidationPolicyReader` alias and
  `ICaptureValidationEvaluator` with the existing scoped operation and server TimeProvider.
- `src/Cosmix.Infrastructure/DependencyInjection.cs`: call `services.AddCaptureValidationInfrastructure()`.
  It registers `CaptureValidationPolicyRepository` and `CaptureValidationEvidenceRepository` for their
  interfaces with the same scoped context as the existing authorization transaction. Parent has wired it.
- `AttendanceCaptureService`: required constructor parameter and the above awaited hook. There must
  be no optional evaluator/default-disabled fallback. Keep existing chronology/provenance gate checks
  before the async hook. Refresh clock/recheck proof AFTER all awaitable evidence IO; call RecordFinal
  and consume/create raw assessment/session synchronously using the same final decision and timestamp.
- Existing capture/audit DTOs: attach `Codes`, `PolicyVersionId`, `PolicyRevision`, `SourceFingerprint`
  and `RelatedRawPunchIds` from this separate evaluation, without reusing the HR policy FK.
- Existing capture fixtures: explicitly configure an approved **synthetic disabled** policy/reader.
  Missing production policy must never be replaced with that fixture at runtime.

Ready HTTP source: `CaptureValidationPolicyController` has preview/history/publication routes under
`/api/v1/attendance/capture-validation/policy`. Publication requires a future UTC effective time,
reason, approval reference, expected revision/timeline and explicit groups. It uses the existing
authorized fixed Admin/SuperAdmin pattern; it does not introduce salary or SuperAdmin-only authority.
Parent has integrated catalog/DI and the capture hook (`capture-factual-v3`). Schema application and
authenticated deployment acceptance still remain outside this source handoff.

Stable owned model/configuration names:

| Entity | Configuration | Table |
|---|---|---|
| `Cosmix.Domain.Attendance.Punches.CaptureValidationPolicyVersion` | `CaptureValidationPolicyVersionConfiguration` | `attendance.CaptureValidationPolicyVersions` |
| `Cosmix.Domain.Attendance.Punches.CaptureValidationFinalEvidence` | `CaptureValidationFinalEvidenceConfiguration` | `attendance.CaptureValidationFinalEvidence` |

Parent added DbSet properties `CaptureValidationPolicyVersions` and `CaptureValidationFinalEvidences`.
Repositories use `Set<T>()`, and the existing configuration assembly discovery picks up both mappings.
No edit to Core DI/catalog/DbContext, old migration/A23/designer/SQL/snapshot or existing capture/session
models/checks was made by the B02 owner. Parent owns the later B24 schema scaffold and migration.

Implemented post-freeze persistence shape:

- Global append-only publication: revision, prior version, effective/published UTC times, publishing
  actor, approval reference, protected proof reference, settings and exact source SHA-256. Unique global
  revision and supersession/effective ordering; no HR PolicyVersion relation and no seeded policy.
- Append-only per-raw final evaluation: raw/employee/action/registered device/request hash/receipt,
  evaluated/assessed UTC times, explicit candidate decision and final assessment decision/codes, related
  immutable raw IDs, policy version/revision/hash and observed device code. Add only in RecordFinal.
- Recent query: employee predicate BEFORE projection, inclusive server receipt range, deterministic
  time/ID ordering, Take(513) so overflow over the operational 512-row cap becomes pending evidence;
  all actions must be retained for accepted intervening break cycles. Latest assessment must match
  the raw AND employee. Opening lookup must pin employee and exact raw ID; reject malformed scopes
  before any query. Never delete or rewrite raw punches or sessions.
- `ICaptureValidationEvidenceRepository.FinalAsync(employeeId, punchId, ct)` supplies a bounded,
  employee/raw-scoped final result for the parent's existing protected audit/history readers.
- `B02VerifyPersistence.cs` uses ToQueryString plus a DbConnectionInterceptor that forbids both sync
  and async connections. It checks predicates, bounds, deterministic ordering, assessment references,
  exact policy FK, unique/append-only protections and wrong employee/action/reference refusal.

Executed checks and complete changed-file manifest: `B02Verification.md`.
Run `B02Verify.ps1 -RequireCatalog`: it deliberately fails if permission
definitions are absent, instead of treating unregistered management checks as a completed workflow.

Acceptance remains conditional on approved company limits/disabled groups, approval reference,
approved device/mock code mappings and real authenticated SQL/device checks. Synthetic fixtures
are explicitly identified and are not company rules.
