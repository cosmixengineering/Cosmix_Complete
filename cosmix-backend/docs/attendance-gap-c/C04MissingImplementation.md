# C04 Independently Reviewed Missing-Evidence Events

This is a new C04 sidecar. It does not redo C01 and does not edit its files,
parent KPI source/core, shared catalog/DI, EF models/configurations, migrations,
ordinary leave, salary records or policy publications. All production changes
are new KpiMissingEvidence-prefixed files in the assigned three layers.

## Parent Hooks

- IReviewedKpiMissingEvidenceReader.ReadAsync(employeeId, from, toExclusive, ct)
  returns ReviewedKpiMissingEvidenceSource: State, active verified Events, SourceIds,
  retained/current Scopes, Fingerprint, Issues and typed Revisions.
- State is verified only when all applicable source/review cases are resolved.
  Unresolved source, pending review, malformed authority or stale original leave
  yields pending with concrete reasons. A pending result is not verified zero.
- Each event has Count 1 and exact employee/date/leave, allocation, summary,
  shift, policy, assignment, approval and decision IDs; source/evidence/event
  fingerprints; independent timed review authority; and original scopes.
- Revisions retain the original approved proof plus every validated protected
  review revision: actual audit ID, original approval/decision IDs, prior revision
  ID, before/after sources, count, reason, snapshot/proof fingerprints, authority
  and cumulative retained scopes. Active marks the reviewed chain tip, not an
  assertion that a pending result is usable. Only a verified result is aggregable.
- Parent aggregates actual counts by day and retains the whole proof beside
  Waiting. This producer applies no KPI score, composition, payroll or salary
  consequence and never finalizes an annual score.
- AddKpiMissingEvidenceApplication() and
  AddKpiMissingEvidenceInfrastructure() are new feature entrypoints. Parent owns
  wiring them to core DI and the existing KPI supplemental reader.
- Existing KpiPermissions.Read, Prepare and Decide are reused. No permission
  definitions, grants, fixed organizational role or seeded configuration added.

## Required Eligibility Setting

The live Domain KpiRules.MissingEvidenceEligibility optional init property has
type KpiMissingEligibilityRules. Its codec requires explicit settings when
MissingEvidenceEffectEnabled is true, and requires null when disabled.
The sidecar accesses that property directly. Constructors are unchanged and
Domain has no Application dependency. The explicit camelCase publication shape:

| Field | Required explicit choices |
| --- | --- |
| cases | MedicalSickLeave=1 and/or EmergencyHalfDay=2; nonempty distinct list. |
| assessmentBasis | AtIndependentReview=1, explicitly selected by the approved company policy. |
| durationBasis | DatedRosterWindow=1 or PublishedLeaveStandardDay=2. |
| allowedLeaveStates | Explicit existing ApprovalState values Pending=0, Approved=1, Returned=2 and/or Rejected=3. Cancelled is never eligible. |

These are schema choices, not company defaults or seeds. Domain enum names are
KpiMissingCaseSetting, KpiMissingAssessmentSetting and KpiMissingDurationSetting.
There is no serialized-property adapter, JsonElement bag, duplicate Application
settings type or client eligibility override.
Missing or unsupported settings yield
kpi_missing_evidence_eligibility_configuration_required. An unknown deadline or
grace interpretation remains unavailable; it does not fall back to independent
review timing. A future deadline-based interpretation needs another explicitly
approved, source-backed setting and reader before support can be claimed.

No 1-percent default is introduced. MissingEvidencePoints must be explicit,
enabled and positive in the effective configured KPI family. The sidecar emits
counts; parent calculations consume their actual published rules.

## Actual Leave Boundaries

The actual LeaveKind is used, never a display name/code inference. Other remains
leave_typed_current_eligibility_required. Current LeaveRules.Evidence rejects a
new Sick submission without Medical evidence. The sidecar cannot fabricate such
a LeaveRequest or bypass submission validation. A genuine retained typed request
still needs its actual protected submission, allocation and approval provenance.

Emergency submissions can have an empty evidence envelope when that actual leave
type does not require attachments. No medical/emergency status is inferred from
HasEvidence alone. LeaveEvidence.ReadAsync verifies the protected envelope's
request/employee binding and supplies actual stored evidence kinds. Required
Medical or Emergency absence is assessed from that real source. Every attachment
is also downloaded through the existing purpose-bound evidence path, verifying
its exact stored bytes/hash and attachment format. Corrupt or unreadable private
evidence fails closed; a client cannot assert document success.

LeaveProvenance.VerifyAsync pins original submission and exact days. Original
leave approval/decision state, rowversion, protected decision proofs and
cancellations are retained and checked. Original reviewer/requester account
ownership is resolved from actual UserAccounts; historical beneficiary self
review or unavailable ownership remains pending. Cancelled, superseded,
untyped, ambiguous or stale originals do not become approved missing events.

The actual saved factual day must still match its source fingerprint. Its dated
roster must contain the actual leave allocation and match the allocation/saved
shift version. Full or half duration is exact, without rounding into eligibility.
DatedRosterWindow uses the actual start/end interval. PublishedLeaveStandardDay
requires the LeaveRequest's actual selected, applicable, proven leave-policy
version and its explicit StandardDayMinutes. It does not guess eight hours or
select a policy for an unpaid/pending request. Unspecified partial medical-leave
interpretations remain kpi_missing_partial_medical_leave_eligibility_required.
No approved or paid leave is assumed from a preview or attendance summary.

## Review And Persistence

Prepare reads actual sources, compares the expected source fingerprint, checks
for same employee/date/leave pending or approved reviews, writes a protected
snapshot, rechecks source/authority and creates one pending ApprovalRequest.
Returned/rejected proposals can be proposed again; an existing approved event
cannot be counted twice. No evidence/rate/count/deadline flag comes from a client.

Decide requires the actual existing attendance.kpi.decide permission, web MFA,
a live session and timed current grants. It denies the proposal requester,
original leave requester and beneficiary, including before receipt replay.
Original snapshot scopes and current scopes must both be authorized. New approval
rechecks eligibility/fingerprint and duplicate targets; it uses rowversion and
the actual outer serializable AttendanceOperation transaction.

The immutable decision links its original protected proposal snapshot. A new
typed protected audit proof binds the exact actual approval/decision IDs, verified
source, snapshot digest and timed independent authority. The consumer checks
the audit target/actor/date and private purpose, exact decision relationship and
scope/permission proof before exposing a count-one event. Generic approval
success or an ordinary private audit is insufficient. Current original-source
changes make retained success pending; replay cannot hide the change.

## Explicit Reviewed Revisions

POST /api/v1/attendance/employees/{employeeId}/kpi/missing-evidence/{id}/revision
calls the actual ReviseAsync method. The body requires ExpectedVersion (the
original approval's rowversion), ExpectedActiveRevision (the active audit ID),
ExpectedSourceFingerprint and Reason. It accepts no count, rate, missing flag,
document assertion, replacement ID or payroll/refund instruction. GET {id}
exposes ActiveRevisionId, ActiveCount and ActiveSourceFingerprint alongside the
unchanged original approval/version fields.

One currently authorized independent KpiPermissions.Decide reviewer reinspects
the actual source and appends an attendance.kpi.missing-evidence.revised.v1 audit
with a typed protected proof. It is not a fabricated request from another user.
No new approval decision is impersonated or attached to a completed request.
The original approval, decision, audit, private snapshot and rowversion remain
unchanged. The proof binds the original decision and snapshot, exact prior audit
and proof hash, before/after sources, rowversion, server-derived count, reason
and timed review authority.

Known eligible_for_review produces one. Known evidence_present, ineligible,
disabled or withdrawal_required produces zero only after this explicit review.
withdrawal_required is backed by actual protected cancellation or authoritative
returned-request-to-corrected-request provenance, not a placeholder response.
Unlinked corrections, unavailable historical authority, stale factual/roster
sources, untyped evidence or unknown settings remain pending and cannot be
reviewed as zero. The current ordinary LeaveRequest cannot have evidence added
after submission; later-document support follows its real linked correction,
without inventing an attachment mutation API.

Every revision authorizes all original and retained revision scopes plus current
source/date scopes, with web MFA, live session and timed current grants. Original
proposal/leave submitter and beneficiary are denied before replay. The service
rechecks source bytes/provenance fingerprint, active tip, rowversion, scopes and
live authority after private IO. A changed payload cannot reuse a receipt;
only the exact still-active revision can replay. Stale tips and unchanged-source
duplicate reviews conflict. Competing writes use the actual outer Serializable
transaction and exact bounded audit-history query; SQL races are not accepted
by the standalone memory checks.

The reader validates the single linked revision chain, not time/ID ordering as
precedence. Missing, duplicate, branched, forged or stale proofs yield pending.
It retains all valid prior proofs in Revisions, exposes only the tip's count-one
event when its actual current source still matches, and proves zero through an
active reviewed count-zero revision. A later source change returns pending until
another independent review. The new fingerprint/identities support parent
source refresh/rescoring, but this sidecar performs no score finalization,
financial refund, charge, salary mutation or payment.

Only existing ApprovalRequest, ApprovalDecision, AuditEvent and private file
storage are written. Reads use employee/date/controlled request family before
bounds: target history 33/32, revision audits 33/32, range 513/512, decision/proof rows 2, leave candidates
4097/4096. Actual serializable transaction presence is checked in the repository;
SQL race acceptance remains untested. Existing authorization serialization and
approval rowversion participate, without adding EF indexes or tables.

## Owned Files

1. src/Cosmix.Application/Attendance/Kpi/KpiMissingEvidenceContracts.cs
2. src/Cosmix.Application/Attendance/Kpi/KpiMissingEvidenceRules.cs
3. src/Cosmix.Application/Attendance/Kpi/KpiMissingEvidenceSourceReader.cs
4. src/Cosmix.Application/Attendance/Kpi/KpiMissingEvidenceAuthority.cs
5. src/Cosmix.Application/Attendance/Kpi/KpiMissingEvidenceEvidenceReader.cs
6. src/Cosmix.Application/Attendance/Kpi/KpiMissingEvidenceService.cs
7. src/Cosmix.Application/Attendance/Kpi/KpiMissingEvidenceReader.cs
8. src/Cosmix.Application/Attendance/Kpi/KpiMissingEvidenceRegistration.cs
9. src/Cosmix.Infrastructure/Repositories/Attendance/KpiMissingEvidenceRepository.cs
10. src/Cosmix.Infrastructure/Repositories/Attendance/KpiMissingEvidenceInfrastructureRegistration.cs
11. src/Cosmix.Api/Controllers/Attendance/KpiMissingEvidenceController.cs
12. docs/attendance-gap-c/C04MissingVerifyRules.cs
13. docs/attendance-gap-c/C04MissingVerifyService.cs
14. docs/attendance-gap-c/C04MissingVerifyPersistence.cs
15. docs/attendance-gap-c/C04MissingVerifyRegisteredJson.cs
16. docs/attendance-gap-c/C04MissingVerify.ps1
17. docs/attendance-gap-c/C04MissingImplementation.md
18. docs/attendance-gap-c/C04MissingDomainProposal.md
19. docs/attendance-gap-c/C04MissingVerifySources.cs

## Verification Limit

These sidecar files and five standalone suites are prepared. **Zero sidecar
behavioral assertions executed; no observed RED/GREEN cycle or compile success
is claimed.** Rule and meaningful service tests were authored before their
implementation, but the explicit serial-build hold prevents execution. They
cover self/MFA/timed permission and original/current scope denial, duplicate
pending/approved events, stale original/active revision, same-key replay and
changed payloads, explicit withdrawal/replacement, immutable original records,
post-IO role/grant expiry, source/scope races, branched history and
pending-versus-verified output. The actual-source suite exercises the concrete
LeaveEvidence, LeaveProvenance, AttendanceSummaryService and SourceReader with
memory repository/file/scanner boundaries, including protected actual typed
documents and corrections. This is not SQL or real employee acceptance. EF tests have an
interceptor that forbids SQL connections; registered JSON tests build MVC/DI
metadata without starting a host. All prepared suites and SQL concurrency
acceptance remain unexecuted verification work for the released slot.

C03 reported a missing AccessScopeKind namespace in the owned contracts. The
Cosmix.Domain.Authentication import was corrected after a nonempty CreateNew
D: backup and SHA256 equality check. No build was started to verify that repair.
Every edit of an existing owned file has the same backup/precheck treatment.

After the parent releases this sidecar's serial slot:

```powershell
./docs/attendance-gap-c/C04MissingVerify.ps1 -BuildSlotReleased
```

Prefer the parent-owned VerifyManaged.ps1 with the new frozen ReleaseDirectory
and Sources containing C04MissingVerifyRules.cs, C04MissingVerifyService.cs,
C04MissingVerifySources.cs, C04MissingVerifyPersistence.cs and
C04MissingVerifyRegisteredJson.cs (all under docs/attendance-gap-c). That path
compiles only standalone checks against the same fresh DLLs and performs no
product rebuild/NuGet restore. No managed check has been run in this source-only
window. The earlier 0-warning/0-error parent Release was before these revision
bodies; it is not evidence for the current source epoch.

The runner refuses compilation by default, checks at least 350 MB C: reserve,
uses unique owned D: artifacts/TEMP/cache/runfile identities, and restores process
environment afterward. Optional PackageCache must be a D: path for package
reuse. It does not issue a parallel full-solution build. No SQL, host, provider,
Git/autosync, physical fixtures, secrets, cache deletion or salary/policy mutation
has been performed for this sidecar. Earlier C01's 76 runtime assertions are
separate evidence, not sidecar test results.
