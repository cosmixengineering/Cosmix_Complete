# C05 Paired Waiting Source Handoff

Source scope: exact completion-plan DOCX paragraphs454-462 (C05.1-.3, C05.T1-.2),
OFF-005/SITE-005/MD-010. Source readiness is not authenticated acceptance.

## Parent Wiring

Parent adds `AddAttendanceWaitingApplication()` and
`AddAttendanceWaitingInfrastructure()` to the existing composition root and binds
`WaitingPermissions.Definitions` exactly once in the existing catalog. No grants,
new roles, EF entities/migrations or shared composition files are changed by C05.
The dedicated `attendance.waiting.decide` must be cataloged once, including when
the parent's advanced policy catalog already defines it. Existing scoped readers,
AttendanceOperation, private storage and notification outbox are reused.

Parent reported the actual seven permission definitions and both DI hooks bound,
and the C04 supplemental adapter consuming the full Waiting proof/IDs/scopes.
These are parent integration changes; C05 did not edit shared files.
Parent also reports its current DTO/source manifest retains verified source IDs,
all worker scopes and the configured rate, with model money null. Final score
clamping remains C04-owned; waiting points are not model percentages or pay.

Uses the frozen parent `IAdvancedAttendancePolicyReader` and schema1 WaitingRules.
The accepted resolver state is exactly `configured`. No missing-family default.
No parent typed contract extension is needed.

`IWaitingPerformanceEffectReader.ReadAsync(Guid causingEmployee, DateOnly from,
DateOnly to, CancellationToken ct=default)` lives in
`Cosmix.Application.Attendance.Waiting`. Dates are inclusive/exclusive, max366days.
Caller must already own authorization and the existing transaction; no nested
AttendanceOperation. Parent C04 may adapt this interface into its own supplemental
source reader; C05 makes no Kpi namespace edits.

`WaitingPerformanceEffects` returns State (`verified`/`unavailable`), ProofFingerprint,
Points, RetainedScopes and Reasons. Each `WaitingPerformancePoint` returns Date,
ExactPoints, request/evidence/assessment/pair IDs, factual and effect approvals and
decisions, policy version, underlying SourceIds and RetainedScopes. Parent must
authorize every retained scope before exposing/scoring it and bind the fingerprint.
Points are one current capped checkpoint per causer/date/location causal group,
not a sum of per-worker waiting claims. Revisions replace a checkpoint in a fresh
KPI calculation while preserving prior KPI/waiting evidence and decision histories.
Pending, unverified, disputed, duplicate, changed source/policy/document, absent
effect approval and missing family policy return unavailable with a concrete reason.
An explicitly configured FactualOnly/FinancialProposal family contributes a verified
empty performance source. Financial proposals are never interpreted as KPI points.
An actual bounded query with no claims is verified empty when policy is configured.

## Durable Workflow

New immutable pair proposals retain effective dates (max31days), two workers,
task dependency and written instruction, source snapshots and approval. An independent
review checks both schedules, sites/branches, staff taxonomy, historical employment,
policy and scopes. Revisions retain an original root and prior revision; no branches.

Waiting intake is an unverified protected proposal linked to a pending ApprovalRequest.
It creates no WaitingTimeEvidence. Verified facts require a real different actor,
the current dedicated configured permission, both workers' historical duty, approved
schedule/policy, current and original scopes, and source proof. Requester, beneficiary
and causer cannot self-review, including another account for the same employee.
Return/Reject can retain unverified duty without fabricating verified evidence.

Approval constructs WaitingTimeEvidence with display minutes only; protected payloads
retain exact ticks and intervals. 10:00-10:45 claimed loss with productive work from
10:20 yields at most10:00-10:20 proven waiting. Approved leave/excused intervals and
current schedule changes are rechecked. Overlapping A/C facts are unioned by delaying
worker/date/location and capped once with explicitly configured WaitingRules.
Task reference remains retained, but changing task text cannot create another loss key.
Changed approved facts are new retained revisions with before/after group totals and
signed tick delta. Existing evidence, sources, decisions and assessments are append-only.

The group sequence is explicit and transaction-serialized, so equal decision times
cannot choose a wrong checkpoint. A returned historical revision stops blocking the
consumer after a newer revision is approved. Policy-changed pending intake can be
returned/rejected and amended; approval still requires the retained/current policy.
Live scoring requires fresh active proof. Scoped request/effect history stays
readable after locks/corrections and retains original proof; no year lock is rewritten.

Disputes are immutable protected audit records. New independently approved revisions
resolve an earlier dispute; a dispute never overwrites approval. Consumers stay
unavailable while an active member is disputed or stale. Multiple affected workers may
need their own revisions after a shared source changes.

Only separately proposed and independently reviewed configured performance/financial
interpretations create PolicyImpactAssessment. Exact waiting points remain private;
points are not silently converted into the model's percentage field. Financial
interpretation/reference and exact factual source survive, with money/currency null
and `amount_unavailable`, never zero/charge. PartF valuation, settlement, posting,
consumption and forward financial adjustments are not implemented by C05.

## Query And Atomicity Boundaries

Pairs, loss membership, causing-worker business-date indexes, disputes and effects
persist through controlled protected AuditEvents and real ApprovalRequests/Decisions.
Existing audit target indexes are reused. Every query has a bound (pairs/effects/
disputes128, loss/causer sources512) and over-limit conflicts instead of truncation.
The causer range is filtered in LINQ before materialization. Private envelopes bind
schema/kind/record identity/content digest to a stable server-selected purpose and
reject oversized or mismatched payloads. Protected references never enter HTTP DTOs.

AttendanceOperation owns live session/MFA/scope checks, serializable authorization
transaction, protected audit and retry receipts. Business, approval, decision, receipt
and staged notification outbox commit together. No external delivery occurs here.
Early SaveChanges obtains rowversion within the outer transaction; it never commits
separately. Replays recheck current plus retained authorization via existing guards.
Source fingerprints and rowversion reject stale previews; duplicate intake, effects
and branching revisions conflict. Database/private-storage/restart atomic acceptance
remains a separate permitted verification window.

Pair/factual/effect decisions stage `WaitingDecisionNotifications.StageAsync`, which
derives the actual ApprovalDecision.Id, ApprovalRequest requester account and subject
case ID and uses only the existing generic Decision template. No mail/SMS/send,
arbitrary notification text, score or private reference is supplied. A notification
stage failure propagates to roll back the producer transaction. A synthetic outbox
adapter check covers identity/dedup/conflict/failure; it is not durable delivery or
SQL transaction acceptance. Reviewer queue recipient lookup is not configured by
C05 and remains an explicit G notification-routing dependency.

## Latest Verification - 2026-10-13

Verified at 2026-10-13T18:32:27.7901853+00:00: all five standalone suites passed,
159 checks total, against the parent's fresh joint Release DLLs at
`D:\CosmixVerification\part-c-parent-20261013\c-current\bin\Cosmix.Api\release`.
C05 did not rebuild production, restore packages, start a host, execute SQL,
deliver provider notifications or create persisted employee fixtures.
All 20 frozen owned file hashes and Release input hashes remained stable during
the run; zero owned files were empty. This handoff update changes documentation
only, and the new dated freeze records every current product/document hash.

| Suite | Passed | Exact Runtime Log |
| --- | ---: | --- |
| Attribution and authority, including positive/signed precision | 17 | [Attribution.log](D:/CodexTemp/C05-managed-checks-470870a19dec4559808037a0d4e0e5d7/Attribution.log) |
| Actual C01 Waiting policy boundary | 7 | [PolicyBoundary.log](D:/CodexTemp/C05-managed-checks-470870a19dec4559808037a0d4e0e5d7/PolicyBoundary.log) |
| Connection-intercepted LINQ translation and EF metadata | 21 | [Persistence.log](D:/CodexTemp/C05-managed-checks-470870a19dec4559808037a0d4e0e5d7/Persistence.log) |
| Generic decision/outbox contract, synthetic adapter | 39 | [Notifications.log](D:/CodexTemp/C05-managed-checks-470870a19dec4559808037a0d4e0e5d7/Notifications.log) |
| Actual workflow services, synthetic IO boundaries | 75 | [Workflow.log](D:/CodexTemp/C05-managed-checks-470870a19dec4559808037a0d4e0e5d7/Workflow.log) |

[C05-Acceptance-Results.json](D:/CodexTemp/C05-managed-checks-470870a19dec4559808037a0d4e0e5d7/C05-Acceptance-Results.json)
contains counts, exact runtime/compile log paths, test source hashes and remaining
acceptance limits. [Release-inputs.json](D:/CodexTemp/C05-managed-checks-470870a19dec4559808037a0d4e0e5d7/Release-inputs.json)
binds the exact tested dependencies; [Results.json](D:/CodexTemp/C05-managed-checks-470870a19dec4559808037a0d4e0e5d7/Results.json)
records all five passed states. The managed process exited successfully and the
serial slot was returned. Source boundary guards and PowerShell syntax checks pass.

The 75 workflow checks exercise real WaitingWorkflowService, WaitingSources,
AdvancedPolicyReader, AttendanceOperation, WaitingPerformanceEffectReader and
NotificationOutbox with synthetic in-memory boundary adapters. Cases cover
current session/MFA and every worker scope, self/requester/causer review denial,
pending intake, replay/key reuse/duplicate/stale source, Return/resubmission,
disputes/amendments/original source retention, A/C union and equal-timestamp sequence,
separate effect approval/exact points/financial exclusion, receipt/outbox rollback
faults, same-key and different-key races, competing same-case and A/C reviews.
The synthetic transaction serializes and restores staged in-memory state on errors;
these are real service checks, not actual SQL isolation or encrypted-storage tests.

## Precision Regression

The earlier workflow run returned verified `ExactPoints` of
`0.9999999999999999999999999999` for 20 minutes at the synthetic rate 3/hour,
instead of the representable exact result `1m`. Dividing into repeating fractional
hours before multiplying caused premature decimal rounding. One owned product
line changed in [WaitingAttribution.cs:91](C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend/src/Cosmix.Application/Attendance/Waiting/WaitingAttribution.cs:91):

```diff
- return signedTicks / (decimal)TimeSpan.TicksPerHour * rules.PerformancePointsPerHour.Value;
+ return signedTicks * rules.PerformancePointsPerHour.Value / TimeSpan.TicksPerHour;
```

Source SHA256: `1D664EA9DFE146A19AD7E77FB64578FC970A9F46AC5494BA1BD5167153B6ACC6`.
The fresh binary passed the positive `20 minutes * 3/hour == 1m` and signed
`-5 minutes * 3/hour == -.25m` regressions, plus the full 75-check workflow suite.
These synthetic rates are test inputs, not invented organizational rules.
Exact ticks, source retention, configured union/cap and financial exclusion are unchanged.

## Managed Runner

C05Verify.ps1 requires a granted parent managed-check slot and the joint Release
directory on D: containing Domain/Application/Infrastructure and dependency DLLs.
It compiles only the five standalone check sources using installed .NET10 Roslyn
and reference packs, then runs against hash-verified copies of that Release,
selecting Windows managed assets from the actual dependency manifest.
It never invokes MSBuild, product compilation, package restore or a new test project.
TEMP/CLI/check artifacts go to a unique D:/CodexTemp/C05-managed-checks-* directory;
Release inputs and prior outputs are preserved. Invoke only after the parent releases:

```powershell
& 'C:\Users\Huma.Khan\Downloads\Cosmix_Erp_software\cosmix-backend\docs\attendance-gap-c\C05Verify.ps1' -BuildSlotGranted -ReleaseDirectory 'D:\<parent-joint-Release-directory>'
```

The supplied directory must contain all runtime dependencies (normally the joint
API Release output, not an isolated Application directory). Results.json is written
only after all five checks pass and the original Release input hashes still match.
The runner includes Workflow after Attribution/PolicyBoundary/Persistence/Notifications
and routes TEMP/TMP/TMPDIR, CLI home, copied sources/dependencies, compiler responses,
runtime configuration and logs to D:. No new test project, production rebuild,
restore, Git operation or deletion is performed.

## Unexecuted Acceptance

The 159 passed managed checks do not claim authenticated HTTP/SQL acceptance,
actual database transaction/isolation/restart behavior, encrypted private-storage
acceptance, year-lock operational acceptance or durable external provider delivery.
Those remain separate, unexecuted verification work requiring an authorized window.
Persistence checks use ToQueryString/metadata and connection interception only;
workflow/notification checks use synthetic IO boundaries, not SQL or real accounts.
G reviewer queue recipient lookup remains explicitly unconfigured. C05 has no F
financial valuation, charge or settlement; financial model money remains null.

## Historical Results - Superseded

The initial handoff's prepared/unexecuted suite status is historical, not current.
Its source-presence guard first failed on missing WaitingContracts.cs, then passed.
An intermediate run passed 82 checks (Attribution15, Policy7, Persistence21,
Notifications39) before Workflow exposed the precision defect. Its
[Workflow.log](D:/CodexTemp/C05-managed-checks-bcec9ed2a597431f90f9327f21cdfc06/Workflow.log)
and [WorkflowDiagnostic.log](D:/CodexTemp/C05-managed-checks-bcec9ed2a597431f90f9327f21cdfc06/WorkflowDiagnostic.log)
are retained as regression reproduction evidence only. The later dated 159-check
run above supersedes the old waiting-for-slot and unexecuted managed-suite claims.

All edits require C: >=350MiB reserve. Before editing an existing owned nonempty file,
CreateNew backups with verified SHA256 are retained under D:/CodexTemp/C05-owned-backup-*.
The initial implementation plan is D:/CodexTemp/C05-20261013-Implementation-Plan.md.
