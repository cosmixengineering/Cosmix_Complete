# Part B implementation ledger - 13 October 2026

Baseline: `HR_Backend_Completion_Plan_A_to_G_2026-10-13.docx`, Part B.
Authoritative checkout: `C:/Users/Huma.Khan/Downloads/Cosmix_Erp_software/cosmix-backend`.
This is backend/source verification, not full Attendance acceptance.

Latest authoritative delivery/checklist and remaining approval/access gates are in
[Delivery-Status-20261013.md](Delivery-Status-20261013.md). The sections below retain
earlier checkpoints; later dated sections supersede pending source/schema statements.

## Ownership and boundaries

- The user authorized coordination with the Part A chat. Part A owns shared
  authentication/access, permission catalogue, DI, DbContext, migrations and snapshot.
- Part A confirmed ownership through `docs/integration/Part-A-B-Ownership-20261013.md`.
  No SQL write window is open. This Part B continuation does not write databases,
  seed accounts, start an API/provider, edit connection settings or change migrations.
- Existing dirty changes remain intact. This package adds no Git synchronization,
  commits, branches, worktrees or permanent test projects.
- Builds/checks use separate temporary artifact directories to avoid shared `obj` locks.

## Requirement-to-code checklist

| Task | Current evidence and remaining gap |
| --- | --- |
| B01 | Office rosters and `OfficeLeaveScheduleSource` exist; do not recreate them. Office boundary/version, office punch/session/summary/calculation/correction and payroll provenance remain missing. This package strengthens SITE Check-Out source revalidation; it does not implement office punches. Coordinate new office schema with Part A before edits. |
| B02 | Same-key replay and immutable evidence already exist. This package adds contradictory device-clock review reasons, keeps offline records Unverified, preserves legitimate second duty sessions and checks consumed/expired proof replay. Approved time-tolerance deduplication, anomaly limits, offline integrity acceptance and complete subsequent review remain outstanding. No tolerance, travel speed or maximum duty was invented. |
| B03 | Existing self/supervisor requests, HR decisions, protected evidence and factual carry-forward remain. Office manual duty depends on B01; out-of-boundary/accessibility/medical exceptions need explicit authority/evidence. Monetary locked-source changes belong to Part F. |
| B04 | Existing Development-only CompreFace/ONNX adapter and server-bound face workflow are retained. Explicit component-result/completion/enrollment consumption guards are strengthened in this package. Reviewed model/deployment, calibrated thresholds, approved production authority and real devices/captures are unavailable. No production/live face acceptance or fake success is claimed. |
| B05 | Existing protected storage, use-expiry and revocation guards are retained. Automatic durable retirement/offboarding, physical destruction and backup/key treatment need approved retention/hold/lifecycle authority and implementation. No deletion was performed. |
| B06 | Existing Operational/Red geometry/history, visible notice, own instructions and acknowledge/dispute/review services exist. Green/Amber/custom scope and offline sync acceptance remain decisions/extensions; no extra type or payroll effect was introduced. |
| B07 | Submit, own history/detail, per-site queues, protected files and Accept/Return/Reject/respond already exist. This package implements bounded Site/All-grant queue discovery and a consumer contract/request pack. It does not complete authenticated SQL/mobile/scanner acceptance, expired-receipt recovery or real notification delivery. |
| B08 | Closed by the user's explicit 13 October decision: "Mobile punches + HR corrections only; file import scope remove karo". Attendance file import is superseded for this release. No import module/route/permission/seed is added; mobile face/location rules and approved HR corrections remain mandatory. This is a scope closure, not a delivered import workflow. |

## Implemented capture changes

`AttendanceCaptureService` retains the original client timestamp and server receipt time.
When a claimed timestamp contradicts receipt or registered/verified device history,
an otherwise acceptable online event becomes QueuedForReview; an offline event remains
Unverified. Codes:

- `CAPTURE_AFTER_RECEIPT`
- `CAPTURE_PRECEDES_DEVICE_REGISTRATION`
- `CAPTURE_PRECEDES_DEVICE_VERIFICATION`

These are contradictions requiring review, not an invented clock-skew allowance or
salary rule. Earlier rejection/review gates retain their primary reason. A new event,
assessment and sequence exception are staged through the existing transactional
audit/receipt/notification boundary. No accepted session or proof consumption occurs.
Exact retries replay one immutable result; altered requests still conflict.
New assessments retain `capture-factual-v2`. Existing v1 records and saved receipts
are not rewritten or retroactively recalculated.

Accepted SITE Check-Out now also requires the opening punch's business date, site,
membership, site-roster, temporary-change, calendar and boundary references to agree
with its active session and resolved source. Missing legacy membership/roster evidence
or changed provenance yields `SESSION_EVIDENCE_CONFLICT` instead of silently closing
duty. Original raw evidence and the open revision remain unchanged. This is not an
office boundary implementation or a new approval stage; Check-Out remains face-free.

Face consumption and accepted Check-In assessments now require an explicit true
liveness result AND true identity-match result, and a completed verification time.
The service also requires the exact retained enrollment ID. A materialized `Passed`
status cannot stand in for missing/false component evidence. This extends existing
fail-closed contracts; it neither activates a provider nor claims real spoof testing.

B05/A04 coordination: Part A may logically revoke existing active face enrollments
inside its authorized offboarding transaction using the existing Domain method and
status-change audit identity. No automatic ciphertext destruction is authorized.
Part A owns execution/verification of that consumer, not this package.

## Red/green evidence

- New future-clock regression failed against the previous service: accepted attendance
  instead of retained review. After the patch, the extended existing service runner passed.
- New same-site/same-shift membership-change regression failed before provenance changes.
  After the patch, membership/roster/temporary-change/calendar cases passed.
- `VerifyCaptureService.cs`: 124 checks passed, including valid IN/OUT, replay, changed
  payloads, raw retention, chronology, source changes, legitimate second sessions and
  offline consumed/expired proof, unchanged-provenance overnight closure, mismatched or
  missing enrollment and incomplete component results. Synthetic repositories, sessions and preverified face
  fixtures only; no SQL, real authentication, HTTP or biometric success acceptance.
- `VerifyCaptureRules.cs`: 15 checks passed.
- `VerifyCaptureModels.cs`: 37 passed. A new model test first failed because a
  materialized Passed result without liveness evidence could be consumed. The strengthened
  consumption/assessment guards passed the extended suite.
- `VerifyCorrectionService.cs`: 99 passed after review caught an old synthetic
  accepted-proof fixture without completion time. The failure was reproduced; the fixture
  now supplies the required completion evidence. Production guards were not weakened.
- `VerifyCapturePersistenceModel.cs`: 30 passed at the earlier coordinated checkpoint;
  EF metadata and tracked guards only, no connection/command or database migration.
- `VerifySiteReports.cs`: 147 passed, including new queue discovery allowed/denied,
  wrong permission/scope/session/MFA, scoped totals/paging and repository leakage refusal.
- B07 implementation owner also ran `VerifySiteReportQueries.cs`: 24 passed with a
  connection-blocking interceptor, and `B07VerifyQueueContract.cs`: 8 compiled
  controller/DTO checks. These are SQL translation/metadata, not registered runtime
  OpenAPI, genuine login, persistence or client acceptance.
- An isolated Release solution build succeeded with zero warnings/errors at the
  earlier source checkpoint. The final combined `B07Verify.ps1` rerun stopped during
  build with seven CS1061 errors in concurrent Part A `EmployeeRecordRepository` references
  to `EmployeeAssetCustody.SupersedesEmployeeAssetCustodyId` / `EffectiveAt`. Part A was
  notified. Latest whole-solution verification is therefore pending coherent A05
  source integration; the earlier successful build is not claimed as the final build.
  No failed check or unfinished build was labelled accepted.
- Focused independent review found no evidenced production defect in the bounded B
  patches; its fixture finding and three missing coverage cases were addressed above.

## Package files and replayable verification

Capture changes:

- `src/Cosmix.Application/Attendance/Punches/AttendanceCaptureService.cs`
- `src/Cosmix.Domain/Attendance/FaceVerification/FaceVerificationAttempt.cs`
- `src/Cosmix.Domain/Attendance/Punches/PunchAssessment.cs`
- `docs/attendance-capture/VerifyCaptureService.cs`
- `docs/attendance-capture/VerifyCaptureModels.cs`
- `docs/attendance-corrections/VerifyCorrectionService.cs` (fixture completion only)

B07 changes:

- `src/Cosmix.Application/Attendance/SiteReports/SiteReportContracts.cs`
- `src/Cosmix.Application/Attendance/SiteReports/SiteReportRules.cs`
- `src/Cosmix.Application/Attendance/SiteReports/SiteReportService.cs`
- `src/Cosmix.Infrastructure/Repositories/Attendance/SiteReportRepository.cs`
- `src/Cosmix.Api/Controllers/Attendance/SiteReportsController.cs`
- `docs/attendance-completion/VerifySiteReports.cs`
- `docs/attendance-completion/VerifySiteReportQueries.cs`
- `docs/attendance-gap-b/B07ConsumerContract.md`
- `docs/attendance-gap-b/B07Contracts.http` (unexecuted requests with placeholders)
- `docs/attendance-gap-b/B07VerifyQueueContract.cs`
- `docs/attendance-gap-b/B07Verify.ps1`

After the shared source is coherent, run `pwsh -NoProfile -File
docs/attendance-gap-b/B07Verify.ps1` for Release and the three report checks.
For each existing capture/correction runner above, run `dotnet run --file <file>
--configuration Release --artifacts-path <new-owned-TEMP-directory> --no-launch-profile`.
These are offline checks; they do not provision providers, secrets, accounts or SQL.
Do not remove or reset another chat's build/fixture/database artifacts.

## Next dependency contract

Before office schema work, agree one migration owner and these changes with Part A:
effective-dated boundary tied to the real Branch ID (not a fake Site), exact boundary
version/radius/accuracy, retained office membership/roster/calendar sources in punches
and session/summary/correction/calculation snapshots, historical transfer/overnight
checks and schema-version compatibility for retained evidence. Route company location
authority and operational boundary permissions through existing approved access rules;
do not invent an office default, infer historical jurisdiction from Head Office or
silently make an Admin setting into biometric/legal approval.

Acceptance still needs an agreed current SQL/schema window, fresh bounded synthetic
employment/device fixtures and authenticated persistence/concurrency/storage checks.
Source-only or SQL-translation results do not replace those tests.

## Continuation checkpoint: integrated B01/B02 and B05 wiring

This section supersedes the earlier A05 compilation blocker and B01 model-handoff
dependency, not historical executed records. Part A explicitly froze and handed
the A23 model/composition window to this parent. A23 cs/designer/incremental SQL
and the pre-B snapshot hashes were rechecked unchanged. One later B migration is
still pending the remaining B06 model checkpoint; no schema has been applied.

- Real Branch office boundary publication/read, immutable geometry/jurisdiction,
  required internal historical source reader and exact raw/session office pins
  are implemented. Company-level authority remains existing SuperAdmin
  `org.company.read/manage`, All scope, web MFA/CSRF; shift authority is unchanged.
- Office capture is now integrated in the existing service, not a fake Site.
  Checkout pins opening branch/roster/membership/boundary/calendar, checks current
  receipt membership, and remains face-free. Missing geometry remains reviewable
  evidence, not a successful duty. New capture marker is `capture-factual-v3`;
  prior versions and receipt JSON remain readable through optional null-omitted fields.
- Required capture validation is integrated before proof consumption. The final
  clock/face permission/proof gate occurs after asynchronous policy/history reads.
  Candidate and final evidence stay distinct; final evidence, raw, assessment,
  interpreted session, audit/receipt and notifications use the existing transaction.
  New own result/history responses include opaque validation provenance and safe
  rule codes, not device observation codes, thresholds or biometric evidence.
- Missing approved capture policy blocks usable state/acceptance; the standalone
  fixture's explicitly approved SYNTHETIC disabled groups are never runtime defaults.
- B05 processor/repository/use gate registrations are integrated. The real worker is
  registered with disabled-by-default options, not started by these checks. Physical
  destruction, retention/holds/backup/key authority and live storage/SQL remain pending.
- A root requested new disjoint taxonomy/foundation-policy shared registrations;
  these were wired by the agreed composition owner without grants or authority defaults.
  Their source tests/consumer changes belong to A. No taxonomy mapping is invented here.
- Human confirmation now assigns new medical/accessibility/out-of-boundary alternative
  duty approval to Admin. Ordinary HR missing/failed Check-In authority is unchanged.
  Dedicated alternative authorization/evidence implementation is in progress, not accepted.

Executed parent checks at this checkpoint:

| Runner | Passed | Limit |
| --- | ---: | --- |
| B01VerifyOfficeSource.cs | 16 | Actual source validation, no SQL |
| B01VerifyOfficeBoundary.cs | 14 | Real publication/auth/retry service with synthetic IO |
| B01VerifyOfficeModel.cs | 13 | Shape plus actual raw/duty constructor guards |
| B01VerifyOfficePersistence.cs | 18 | Actual EF/append-only metadata, zero connections |
| VerifyCaptureService.cs | 135 | Real capture/evaluator/auth/replay services, synthetic IO/proof |
| VerifyRegisteredContracts.cs | 15 | Real MVC discovery/OpenAPI, 337 paths; host never started |
| VerifyAttendanceComposition.cs | 14 | Actual Dev/Prod DI, synthetic no-write storage |

B02 owner independently reports 118 checks with mandatory catalog, strict Release
and zero SQL attempts. B05 owner reports 95 durability/restart checks plus 367
existing face regressions. B07 owner reports 196 service, 32 query and 14 contract
checks. Counts overlap and are not summed into a full acceptance claim. A final
integrated rerun is required after all owners freeze their source.

RED/GREEN: office service initially failed the new accepted office IN assertion;
required source wiring made it pass. Office domain initially accepted a different
raw business date; date/shift guards made both mismatches fail as intended. A new
response contains collection records, so replay checks now compare serialized
values rather than CLR collection-reference equality. This fixes the check's
comparison, not a claimed product replay defect. OpenAPI's standalone generator
needed its own interceptor namespace and unstarted Kestrel registration; no
production build rule was weakened.

No database/instance/key/credential writes, host/provider activation, Git sync,
commit, cleanup or production activation occurred in this continuation checkpoint.

## Later Checkpoint: B24 Source And Temporal Authority

- B24 `20261013145007_AddOfficeCaptureValidationAndOfflineZoneClaims` was generated
  with actual EF design services, a forbidden-connection interceptor and exact frozen
  A23 snapshot/chain guards. The generator initially placed the two new generated
  files under the checkout's `Database/Migrations`; only those two owned files were
  placed into the existing Infrastructure migration directory with byte/hash checks.
  No prior file was overwritten; the generator now uses the absolute output path.
- Actual model/migration review passed 15 checks: 44 forward operations, only three
  new Attendance tables and ten nullable columns plus retained keys/constraints/
  indexes, no seeds/backfill or dropped business columns/tables. New B24 Down refuses
  before destructive operations. A23 migration/designer/SQL hashes remain unchanged.
- The generated A23-to-B24 idempotent SQL is saved in `docs/sql`. Its SHA256 is
  `A0EEFC4D51E1BF1A0FA4BB970A8DCE8094A336EF6D9219BAD54A599136B525F2`.
  B24 has NOT been applied by this parent; the coordinated Part A root is sole SQL
  owner, with its own target/backup/restore/acceptance evidence. Independent B24 review
  remains required before application handoff.
- Independent source review demonstrated a temporary grant could expire during the
  final access query. Internal JSON-ignored timing metadata now retains the exact
  max(role start, permission grant) / min(role end, revocation) in the existing SQL
  projection. New capture validates it at the final refreshed clock and fails closed
  on missing timing. No new roles, grants, EF fields or wire JSON were introduced.
- The same expiry race was reproduced in capture receipt replay. Its scoped replay
  gate now revalidates current session/device and final authority before returning the
  protected result. This does not change other module receipt protocols.
- Missing office geometry JSON fields were independently shown to deserialize as
  zero. Required presence now distinguishes omission from explicitly approved zero.
- Office missing-checkout reminders no longer discard all Site-less office duty.
  They use the original shift's configured deadline, original office source predicates,
  and the existing durable notice/suppression flow, with no fake checkout or pay effect.
- Current executed parent counts: capture service 147, authority projection/interval
  14, office boundary 19, actual Dev/Prod composition 26, reminder service/domain 44,
  reminder SQL translation 28, B24 source/model review 15. These are not authenticated
  SQL, real face/device, live notification delivery or client acceptance.
- Independent P1/P2 source fix rereview found no remaining product defect in those
  two fixes. The SQL check's assumed column aliases were replaced with assertions
  against EF's actual max-start/min-end CASE expressions, not a product workaround.
- Part B03 owner reports normal office correction/carry-forward source and dedicated
  fixed `ADMIN` alternatives frozen: 136 correction, 79 factual carry-forward,
  21 office-source, 26 evidence-policy/timing checks. Normal Site3/Office4 remain;
  alternative schema5 calculator/controller integration is still in progress.
- Office calculation/payroll owner reports 178 checks and strict Release success
  before the separate schema5 follow-up. Root jurisdiction DI uses an explicit
  concrete Site reader / Office decorator factory to avoid a recursive interface.
- B06 model is frozen with exactly two claim reference/expiry scalars and matching
  nullable-safe CK/index changes. Actual Unverified EmployeeReport intake/storage/
  reconciliation/cleanup source is implemented; its final non-EF acceptance handoff
  and approved deployment/retention settings remain separate.

Current resource limit: C: reached zero free bytes; a new independent reviewer
could not start (OS112). No source/SQL/key/other-chat file was deleted. Only preserving
NTFS compression of completed parent-owned temporary build outputs was started.
The reviewed source artifacts and SQL remain in the existing checkout.

## Final Integrated Source Freeze - 13 October 2026

The previous pending B03/B06/controller/calculator/independent-review statements
are superseded. Dedicated Admin alternative decisions and schema5 authority consumers
are integrated with the selected real calculation repository through the same scoped
transaction-bound journal. Exact private permission intervals and decision/source/
actor/scope evidence are retained; missing audit does not manufacture staging.
All special beneficiary decisions and receipt replays are denied, including factual
locked-period adjustments. Normal HR and raw/lock/payroll business rules are unchanged.

Parent independently executed31 distinct standalone check files with green latest
results and matching current check-file hashes. Strict four-project Release passed
zero warnings/errors after source freeze. Capture147, correction196, calculation181,
carry-forward87, actual selected-reader/query/DI22 and registered OpenAPI155/15 checks
are included;349 registered paths are not349 completed workflows. Exact per-file
counts, logs and test boundaries are in Delivery-Status-20261013.md. An interrupted
concurrent-source build and a later correction-check hash drift were retained and
superseded by fresh affected runs, not silently counted as passing.

All four B24 artifact hashes remain frozen. B independently read SQL-owner evidence
that clone/original now have24 migrations with original rows/IDs/facts preserved;
clone idempotent repeat passed, original repeat was not executed. B performed no SQL
or fixture writes. The old exact-three-account/stale-pipe host profile still refuses
the preserved five-account target; it was not weakened, reseeded or bypassed.

New parent build outputs use D:/CosmixVerification/gap-b-parent-20261013, with temporary
process-only TEMP/TMP overrides restored afterward. No source, backup, database,
key, credential, manifest or other-chat file was deleted. The earlier completed
parent-owned C: build outputs were preserved using NTFS compression.

Source ownership is released to the coordinating owner. Applied B24/Office22/A23
must remain immutable; future model work needs a later migration. Current business
configuration, real authenticated SQL/concurrency/rollback, scanner/outbox/client
and provider/device acceptance, physical disposal/backup/key approval and Part F
financial/legal activation remain explicit uncompleted gates. Full Part B/Attendance
acceptance is NOT claimed.
