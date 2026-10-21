# OT Finalization Continuation

Superseded implementation status: the later parent continuation is recorded in
`Ot-Retained-Finance-20261015.md`. Exact protected award membership and Finance
consumption are now implemented and standalone-verified; the two cross-month
cases pass. Legal readiness and real HTTP/SQL acceptance remain incomplete.
The remainder of this document retains the earlier worker evidence unchanged.

Date: 2026-10-15.

Status: safe OT dependency inspection implemented and source-verified; source is stable for
the parent's combined build. Finding 1 remains blocked at the retained-award/Finance consumer
boundary. Approved OT still prevents month finalization. No financial/legal readiness was
manufactured. This is the scoped worker evidence note, not shared project status.

## Scope and Changes

- Added `src/Cosmix.Application/Attendance/MonthLocks/MonthLockOvertimeReadiness.cs`.
  It checks persisted request/session/approval/decision headers and protected-reference shape,
  fingerprints their exact scalar dependencies, and reports explicit missing/mismatched evidence.
  It does not read protected award bytes or validate monetary provenance. Every approved OT
  source remains blocked by `overtime_locked_handoff_unavailable`.
- Updated `src/Cosmix.Infrastructure/Repositories/Attendance/MonthLockRepository.cs` to retain
  missing/mismatched dependencies with left joins, include the exact decision, and incorporate
  the helper's fingerprint and issues. A missing session has no trustworthy duty date and is
  conservatively retained as an explicit undated dependency for every month until repaired.
- Added `docs/hr-srs-remediation/VerifyOtFinalizationBoundary.cs` (19 checks).
- Extended `docs/attendance-completion/VerifyMonthLocks.cs` with eight checks covering pending
  OT, unresolved approved OT, and an OT dependency appearing during private manifest IO.
- Updated `docs/attendance-completion/VerifyMonthLockQueries.cs` for dependency retention and
  exact decision/reference projection (42 checks), and updated this note.
- The unchanged MonthLockService includes the repository fingerprint in readiness and rechecks
  after private manifest IO. No new manifest schema or public monetary payload was introduced.
- No PayrollHandoff, attestations, Leaves, DI, authentication, schema, migration, or shared
  status/documentation edits. The parent-owned `AttendanceMonthLockFinancialReader.cs` change
  is untouched. Existing modifications were preserved.
- No SQL execution/connection, API host, external provider, Git writes, test project, or messages
  to another chat. Verification outputs and the attempted isolated build are on D only.

## Concrete Blocker

The read-only consumer inspection found that the two modules select different OT membership:

- `src/Cosmix.Infrastructure/Repositories/Attendance/MonthLockRepository.cs` selects OT by
  `WorkSession.AttendanceDate`. The new helper preserves the approved-OT blocker.
- `src/Cosmix.Infrastructure/Repositories/HR/PayrollHandoffRepository.cs:32` selects approved
  OT by `ApprovalRequest.ClosedAt`, not duty business date or retained lock membership.
- `src/Cosmix.Application/HR/PayrollHandoff/PayrollHandoffService.cs:243` requires an existing
  lock, then obtains OT by a fresh period query at line 255. It does not read a protected lock
  OT manifest. Its protected submission/decision validation at lines 340-365 does not repair
  the different membership selected earlier.
- The baseline repository fingerprint omitted the decision identity and protected financial
  evidence reference. The new dependency fingerprint includes them; this is not a substitute
  for validating protected contents or retaining and consuming the exact award.

The actual repository methods, executing over synthetic query rows, produced:

| Duty / Approval | September lock selection | September Finance selection | October lock selection | October Finance selection |
| --- | ---: | ---: | ---: | ---: |
| September 30 / October 2 | 1 | 0 | 0 | 1 |

Changing the synthetic protected decision reference left baseline `SourcesAsync`'s fingerprint
unchanged; that regression now passes against the modified repository/helper sources.
The dedicated test deliberately asserts that Finance consumes the exact membership of the lock
being handed off. It does not authorize a new approval-date payment or carry-forward policy.

These are header/query boundary reproductions. No protected award contents were fabricated or
accepted as verified money. The test uses the in-memory EF query-root substitution pattern from
`VerifyHrReportQueries.cs`; production joins, predicates, projections and `SourcesAsync` logic run
as supplied to each run. A connection interceptor throws on any attempted connection; zero attempts
were observed. The tests do not claim SQL execution or verified monetary awards.

## Executed Verification

Baseline RED and service regression runs used the user-named frozen Release:
`D:\CosmixVerification\hr-remediation-c-20261015\baseline\bin\Cosmix.Api\release`.

```powershell
& .\docs\attendance-gap-c\VerifyManaged.ps1 -ReleaseDirectory 'D:\CosmixVerification\hr-remediation-c-20261015\baseline\bin\Cosmix.Api\release' -Sources @('docs/hr-srs-remediation/VerifyOtFinalizationBoundary.cs')
```

Initial standalone compilation succeeded. Runtime exited 1: 11/14 checks passed, three expected RED failures:

1. September Finance omitted the September OT member approved in October.
2. October Finance rediscovered OT absent from October lock membership.
3. Replacing the decision/financial-evidence reference did not invalidate readiness fingerprinting.

Initial RED output, compiler response, frozen source and `inputs.json`:
`D:\CosmixVerification\part-c-parent-20261013\managed-59c89fea443846c3aadc2bdf42ad7d12`.

Before production edits, the expanded dedicated test was run through the same `VerifyManaged.ps1`
command against the frozen baseline. It compiled and exited 1: 11/19 passed, eight failures.
Besides the two membership failures, it reproduced unchanged decision-reference fingerprint,
unchanged fingerprint after decision removal, absent explicit missing-decision issue, hidden
session-owner mismatch, hidden missing approval, and hidden undatable missing session.

Expanded RED output and frozen inputs:
`D:\CosmixVerification\part-c-parent-20261013\managed-86543994eba9454caf826125d54b4210`.

```powershell
& .\docs\attendance-gap-c\VerifyManaged.ps1 -ReleaseDirectory 'D:\CosmixVerification\hr-remediation-c-20261015\baseline\bin\Cosmix.Api\release' -Sources @('docs/attendance-completion/VerifyMonthLocks.cs','docs/attendance-completion/VerifyMonthLockQueries.cs')
```

Exit 0: 66 month-lock service checks and 41 membership/offline query-translation checks passed.
This confirms existing guards and source reinspection remain effective; it is not a GREEN result
for the requested OT implementation. These counts describe that run's test versions, before the
query test was extended to 42 checks and the synthetic approved-OT issue name was updated.

Passing output, compiler logs, frozen sources and `inputs.json`:
`D:\CosmixVerification\part-c-parent-20261013\managed-fe0a7b6715914431aeab1358360101cd`.

The current `VerifyMonthLocks.cs` was subsequently rerun alone through `VerifyManaged.ps1`
against the same frozen Release: exit 0, all 66 checks passed, including the updated synthetic
approved-OT blocker and stale-preview rejection. Output:
`D:\CosmixVerification\part-c-parent-20261013\managed-d6a33ddd2fd34623891e8565a8d36c2a`.
This is service regression evidence against baseline binaries, not the combined build.

### Isolated Product Build Attempt

An Infrastructure Release build with `--no-restore`, `--artifacts-path`, disabled shared compilation,
and one MSBuild node wrote only to `D:\CosmixVerification\ot-readiness-20261015-3f01`.
Domain built; Application failed with four CS1061 errors in concurrently edited, parent-owned
`LeaveEncashmentService.cs` (lines 43, 48, 51, 57): the repository interface lacked
`GetRemainingEncashableMinutesAsync`, `GetEffectiveLeavePolicyVersionIdAsync`,
`GetLatestKpiPercentAsync`, and `GetMonthlyBaseSalaryAsync` at that time. No Leaves files were
modified to bypass this. Infrastructure was not reached. This is not a successful product build.
Log: `D:\CosmixVerification\ot-readiness-20261015-3f01\infrastructure-build.log`.
The parent now owns the combined build; no further product build is planned here.

### Source-Only Verification

To verify the scoped changes independently of unfinished Leaves changes, the D-only script
`D:\CosmixVerification\ot-readiness-20261015-3f01\VerifyOtSources.ps1` compiled the exact helper
and modified repository sources alongside each standalone verifier using frozen baseline dependencies.
The baseline Infrastructure assembly was referenced through an explicit alias for its real DbContext
and unchanged Finance repository; no modified repository/helper behavior was stubbed. Copied source
imports were mechanically aliased on D only. Original and compiled-source hashes and baseline input
hashes are retained, and the script checked original source and baseline hashes after execution.

- `VerifyMonthLockQueries`: 42/42 passed, including offline EF SQL translation.
- `VerifyOtFinalizationBoundary`: 17/19 passed. The six dependency regressions now pass.
- The two Finance membership assertions remain RED: September omits the September duty approved
  in October; October rediscovers that duty despite no October lock membership. Overall exit 1.

Output, response files, compiler/runtime logs, and input manifests:
`D:\CosmixVerification\ot-readiness-20261015-3f01\source-verification-7ab4d048f4f648f0bbcfad9ba2ba4205`.
This verifies scoped source behavior against baseline dependencies, not a combined product build
or a successful end-to-end OT finalization flow.

## Required Coordinated Continuation

An Attendance-only removal of the approved-OT blocker is unsafe with the current Finance consumer.
The safe dependency portion is ready for the parent's combined build. The remaining Finance
contract must be coordinated with the parent; it is not permission for this worker to edit Finance
or enable finalization.

The next coordinated change needs one exact protected OT membership contract: request, work-session
and source identity, approval and decision IDs, protected submission/decision digests, and retained
compensation/policy/formula/amount provenance. Attendance must validate that evidence under the
existing transaction, retain it in a compatible lock manifest, and recheck after private IO.
Finance must consume and validate those exact retained members and reject missing, substituted or
additional awards, including cross-month approvals. Requerying by approval month is insufficient.

The minimal consumer contract must identify the lock/event, employee/duty date, request/session,
approval/decision, protected source digests and validated award provenance. Finance must consume
only validated retained members, not an untrusted public list or a fallback live period query.
Legacy locks without the required evidence remain explicitly unavailable. The existing unpinned
`ledger.LatestAsync` path is another parent-owned financial-binding concern: a consumer must use
the exact retained payroll input identity, not silently replace it with a newer ledger. Preserve
the parent's preexisting loan changes and transaction boundaries when designing that bridge.

Pending OT, missing/corrupt awards, unavailable source authorization and absent legal activation
must continue to block. Posting/settlement remains downstream. Full award-validation, exact-member
replay/race checks and a GREEN end-to-end synthetic flow remain unimplemented and unverified here.
