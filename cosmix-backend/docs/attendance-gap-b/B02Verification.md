# B02 scoped source verification

Owned Application/Domain/controller/configuration/repository source is implemented. Parent explicitly
granted the A23 handoff at `20261013121357_CompleteHrFoundationPartA` before B02 Domain/EF edits.
Parent then wired catalog, both registration extensions, DbSets, capture finalization and policy-gated
state. This is source readiness for the parent's single B24 scaffold, not completed SQL/device acceptance.
No shared DbContext/snapshot/migration/catalog/Core DI, existing capture/model/check file, HR financial
policy, host, SQL, provider or Git operation was changed by the B02 owner. All old migrations and the
A23 migration/designer/SQL were preserved by this owner.

## Files Created

All paths below are relative to this backend root:
`C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend`.

| New file | Purpose |
|---|---|
| `src/Cosmix.Application/Attendance/Punches/CaptureValidation/CaptureValidationContracts.cs` | Explicit policy groups/publication/response/proof/evidence records and repository/reader/evaluator interfaces |
| `src/Cosmix.Application/Attendance/Punches/CaptureValidation/CaptureValidationPolicyRules.cs` | Strict explicit-group/limit/code-set validation, canonical source hash, contiguous immutable timeline and effective selection |
| `src/Cosmix.Application/Attendance/Punches/CaptureValidation/CaptureValidationRules.cs` | Pure duplicate/duration/travel/approved device-map evaluation, offline and existing gate priority |
| `src/Cosmix.Application/Attendance/Punches/CaptureValidation/CaptureValidationEvaluator.cs` | Read-only async candidate evaluation and mandatory synchronous, raw-bound RecordFinal after final clock/face recheck |
| `src/Cosmix.Application/Attendance/Punches/CaptureValidation/CaptureValidationPolicyService.cs` | Protected evidence reader and audited, future-effective publication/preview/history using AttendanceOperation |
| `src/Cosmix.Application/Attendance/Punches/CaptureValidation/CaptureValidationPermissions.cs` | Read/manage definitions, now catalog-registered by parent |
| `src/Cosmix.Application/Attendance/Punches/CaptureValidation/CaptureValidationRegistration.cs` | Scoped application registration extension, now called by parent |
| `src/Cosmix.Domain/Attendance/Punches/CaptureValidationPolicyVersion.cs` | Append-only approved effective-dated global policy with private proof and source hash |
| `src/Cosmix.Domain/Attendance/Punches/CaptureValidationFinalEvidence.cs` | Immutable explicit candidate/final outcome and raw-bound clock/device/policy evidence |
| `src/Cosmix.Infrastructure/Database/Configurations/Attendance/CaptureValidationPolicyVersionConfiguration.cs` | Unique global revision timeline and policy source tuple mapping |
| `src/Cosmix.Infrastructure/Database/Configurations/Attendance/CaptureValidationFinalEvidenceConfiguration.cs` | Unique per-raw final record, employee/raw and exact policy tuple FKs, UTC/offline/final-chronology checks |
| `src/Cosmix.Infrastructure/Repositories/Attendance/CaptureValidationPolicyRepository.cs` | Bounded ordered global timeline and validated, staged publication writes |
| `src/Cosmix.Infrastructure/Repositories/Attendance/CaptureValidationEvidenceRepository.cs` | Bounded employee-scoped recent/opening/final queries, latest matched assessments and immediate materialized-source refusal |
| `src/Cosmix.Infrastructure/Repositories/Attendance/CaptureValidationRegistration.cs` | Scoped infrastructure extension, now called by parent |
| `src/Cosmix.Api/Controllers/Attendance/CaptureValidationPolicyController.cs` | Compiled protected preview/history/publication controller; schema/real HTTP acceptance pending |
| `docs/attendance-gap-b/B02VerifyRules.cs` | Pure behavior checks with explicitly synthetic approved disabled/active policy fixtures |
| `docs/attendance-gap-b/B02VerifyEvaluator.cs` | Actual evaluator checks with synthetic awaited IO; no evaluation-time writes, exact final outcome/code/time binding and duplicate/unsafe finalization refusal |
| `docs/attendance-gap-b/B02VerifyPolicyReader.cs` | Actual protected policy reader and historical source validation checks |
| `docs/attendance-gap-b/B02VerifyManagement.cs` | Compiled controller/DTO and actual authorized publication/replay/audit/All-scope/fixed-role/MFA/session checks; unregistered refusal remains explicit |
| `docs/attendance-gap-b/B02VerifyPersistence.cs` | SQL translation/model/domain/source/append-only checks with forbidden sync/async connection interceptor |
| `docs/attendance-gap-b/B02Verify.ps1` | Strict Release and independent standalone runners, isolated TEMP artifacts, optional required catalog gate |
| `docs/attendance-gap-b/B02Integration-Handoff.md` | Parent-owned constructor/runtime/catalog/DI/EF hooks and explicit freeze boundary |
| `docs/attendance-gap-b/B02Verification.md` | This manifest, executed evidence and outstanding work |

## Executed Verification

Before the parent's final clock steering, `B02Verify.ps1` completed with exit 0 using artifacts:
`C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-b02-212d211d071448b6aca378c12e3ca4a2`.
It used locked Release solution restore/build with **zero warnings and zero errors**.

- 31 pure rules checks passed.
- 13 actual runtime evaluator checks passed (superseded by the finalization split below).
- 7 actual protected policy reader checks passed.
- 10 compiled controller/DTO, malformed input and actual unregistered-access refusal checks passed.
- Management printed explicit HOLD for authorized success/replay/scope/audit acceptance. That branch
  is executable when the parent registers the exact definitions; it was not counted as passed.

The runners compile actual Application/API source. All policy values, approval references, device
codes, passed prior assessment fixtures and IO are synthetic. No biometric verification, authenticated
HTTP acceptance, database connection/command, app host or provider was executed.

Observed RED -> GREEN progression:

1. Missing policy assertion failed while the evaluator returned the prior Accepted decision;
   now it returns a QueuedForReview candidate with CAPTURE_POLICY_NOT_CONFIGURED.
2. Extended rules first failed because offline was still accepted; the 31-case rules run now passes.
3. Actual evaluator first failed because missing policy did not retain pending evidence. After the
   parent's clock steering, recording now requires explicit RecordFinal on the pending candidate.
4. Actual protected reader first failed to resolve a configured protected version; reader checks now pass.
5. Protected storage outage initially threw without retaining pending evidence; actual evaluator now
   returns CAPTURE_POLICY_EVIDENCE_UNAVAILABLE and preserves existing blocked gate reasons.

Parent's final clock steering was then implemented with a separate RED -> GREEN cycle. The assertion
that async evaluation persists no final artifact failed against the earlier staging evaluator. After
that split, a stub RecordFinal failed the explicit recording/returned-ID assertion. The actual evaluator
then passed 25 checks, including awaited IO advancing the clock past a synthetic initial face gate,
refusal of the old timestamp, exact raw binding, final Rejected outcome/code/time matching an actual
Check In PunchAssessment, explicitly separate Accepted CandidateDecision, no unsafe acceptance
upgrade, duplicate finalization refusal and explicit offline Unverified finalization. No genuine face
proof/provider success is claimed. The previous artifacts above do not verify the final split and
post-freeze persistence. The latest combined result is recorded below.

Post-freeze RED -> GREEN: model discovery first failed because the approved policy/final-evidence
entities/mappings were absent. After those mappings were implemented, query discovery first failed
because the bounded repositories were absent. Real SQL translation/model/source/append-only checks
then passed with zero attempted connections. No table or migration was applied.

A combined run at `cosmix-b02-92417e9e797c41dc95ae6fea01ccb0e4` passed strict Release (zero warnings/errors),
31 rules, 27 evaluator, 7 reader and 10 unregistered-management checks. Its subsequent persistence step
was interrupted by the parent's transient CS9113 unread `capturePolicies` constructor parameter in
AttendanceCaptureService while shared integration was in progress. This historical partial run is not
a combined final pass and the B02 owner did not edit the shared file.

Final scoped run after parent registration:
`B02Verify.ps1 -RequireCatalog` completed with exit 0. Fresh artifacts and all logs are at
`C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-b02-33ff33c52e294e859500830191796c77`.

- Strict locked Release solution build: zero warnings and zero errors, no diagnostic downgrade.
- 31 pure rules checks passed.
- 27 actual evaluator/finalization checks passed.
- 7 actual protected policy reader checks passed.
- 22 actual management authorization/publication/audit/replay and compiled controller checks passed;
  the catalog gate was mandatory and there was no skipped/unregistered success branch.
- 31 actual bounded query/model/domain/source/append-only checks passed, with zero attempted sync or
  async database connections and zero executed commands.
- Total: **118 executed B02 checks**. This excludes the parent's separately reported 131 capture checks.

The 23 files in this manifest are the complete B02-owned source/check/handoff set. Domain/configuration/
repository/registration names are stable and ready for the parent's single B24 scaffold. No further
B02 model change is pending or authorized by this handoff.

The first RED run could not reach assertions due to concurrent B05 scaffold CS9113 diagnostics.
An invocation-only WarningsNotAsErrors=CS9113 reached the intended missing-policy RED/GREEN.
No build configuration was weakened. Final strict verification used no diagnostic override.
A concurrent temporary CS8794 pattern diagnostic in the B02 policy guard was corrected before
the final strict run; other owners' historical partial-run records remain unchanged.

## Behavior and Limits

No duplicate tolerance, speed, duty duration, location quality ceiling or provider observation code
is seeded or defaulted. Enabled groups require explicit positive numeric limits or a nonempty
approved device/mock mapping. Disabled groups apply no such rule and reject leftover numeric limits.
The 512-row processing limits and the Earth radius are operational/physical constants, not company
attendance rules. Evidence overflow remains pending rather than silently validating a truncated list.

Time-tolerance duplicate checks retain each immutable raw and its related source IDs. An accepted
intervening opposite action permits a distinct legitimate break/field cycle. Travel uses only accepted,
online, reliable inside-boundary prior points, server receipt elapsed time and conservative accuracy
radii. Active duration uses the accepted opening raw and exact session reference. Device observations
come from the persisted registered device; unknown codes remain observations, not invented risks.
Offline is always Unverified. Existing blocked gate decisions keep priority. Evaluation persists no
final evidence. The caller must refresh time/recheck face expiry after all awaits, then call RecordFinal
once before synchronous proof consumption and actual raw assessment/session creation. FinalAssessment
and CandidateDecision have distinct names; an earlier accepted candidate cannot be mistaken for a
final Accepted record. No client verification
result is an evaluator/controller input. Capture policy provenance is independent of HR financial
policy, so this feature does not reset late counters or change pay authority.

## Remaining Work

Parent owns the single B24 scaffold/snapshot and any later schema application, broader audit/history
consumer integration, existing fixture updates and integrated capture acceptance. Parent reports its
real evaluator/explicit synthetic disabled-policy capture fixture passed 131 checks; that report is not
counted as an independently executed B02 check here. Required-catalog management source checks can
verify publication/replay/All-scope/fixed-role/MFA/session behavior using synthetic IO. Actual atomic
SQL rollback and authenticated HTTP/device acceptance are still unverified.

Company must supply approved enabled/disabled choices, limits, approval references and any device/mock
code set. Real device observation quality/provider integration and authenticated isolated SQL acceptance
remain unavailable. Code compilation and synthetic passed assessment fixtures do not prove them.
