# Attendance completion second pass - 12 October 2026

## Scope and ownership

Continue only this checkout. No synchronization, commits, branches, worktrees, automation or
database writes. Existing client HTTP changes and all migration history remain preserved.
The user's detailed completion specification and confirmed decisions are the binding brief.
Root owns shared registrations, month locks, existing payroll input/attestation services and
migration ownership. Independent workers own disjoint reconciliation, face lifecycle and
acceptance-tooling files. Builds are serialized through the root-owned slot.

## Requirement-to-code checklist

| Requirement | Actual baseline | Work in this pass |
|---|---|---|
| Core scheduling/capture/corrections/leave/OT | Implemented existing workflows | Preserve; affected regression checks |
| Cross-month ordinary/late | Explicit reconciliation issue | Protected retained-source and posting provenance integration |
| Month finalization | Daily readiness flag, exact daily membership only | Inspect saved monthly dependencies; retain exact financial revisions; reject source races |
| Biometric lifecycle | Encrypted images, no destruction primitive | Approved use-boundary/retirement controls; no invented deletion policy |
| SQL/authenticated acceptance | Existing DB migration 12, ambiguous extra drafts | Prepared guarded tooling; await synthetic provenance/setup approval |
| Legal activation | Evidence recording only; authority proposed | Keep unverified penalties unavailable; no invented legal clearance |
| KPI/waiting/benefits/recovery | Missing approved formulas and authority | SRS evidence trace; request exact unresolved decisions only |

## Implementation and verification ledger

- Month-lock regression first failed: a daily `PayrollReady=true` with no monthly financial
  reader permitted readiness. New writes now require transaction-local monthly source inspection.
- Added schema-2 protected lock manifests with exact employee/month/source revision, source
  and handoff evidence hashes and legal activation binding. Schema-1 reads remain supported.
- Finalization reinspects sources after private-manifest IO before staging the business lock.
- `VerifyMonthLocks.cs`: **58 passed**, actual services with synthetic financial/policy/storage IO.
  Includes missing reader, financial revision race, storage-time source race and exact retained
  financial membership. This is not genuine SQL finalization acceptance.
- `VerifyPayrollInputService.cs`: **64 passed**, actual saved-input and dependency services,
  synthetic collaborators. Missing/stale revision, missing handoff/legal proof and denied financial
  read permission are covered. No nested operation or salary/payment action is introduced.
- A fixture initially used an object initializer to disable a reader after constructor wiring;
  the fixture was corrected to pass the setting to its constructor. Production guards were not
  relaxed. The future-month fixture now returns its requested month rather than September.
- Independent review found a missing-assignment financial-source exception that aborted preview,
  and a schema-2 financial authorization gap on exact lock receipt replay. Each was reproduced
  failing at runtime, then fixed. The reader catches only the exact known 409 missing assignment;
  authorization/corrupt history failures still propagate. Month locking uses an optional
  transaction-local `beforeReplay` hook on the existing operation to validate frozen membership
  before replay. Other module calls have unchanged default behavior. Schema-1 compatibility,
  missing handoff/legal proof, source races and revoked financial retry are covered above.
- The selected Development face PoC now wires the retention-controlled template-store decorator
  and encrypted lifecycle journal. Missing approved options/journal deny biometric use; no default
  durations, automatic post-SQL retirement or destructive cleanup were introduced. Final combined
  lifecycle run passed **66**; actual dependency composition passed **14**. Both use synthetic IO.
- The acceptance sidecar supplied 15 explicit fixture/case contracts, not an executable acceptance
  runner. Five PowerShell files parsed without errors. No SQL/HTTP acceptance execution occurred.
- Cross-month reconciliation passed **48** actual-service/rules checks with synthetic IO. Confirmed
  manual posted or settled sources are pinned despite later revisions. Saved/handoff-only and
  changed/missing evidence remain unavailable. Historical null provenance serialization is preserved.
- Existing payroll producer passed **50** after splitting the old no-posting cross-month expectation
  into a missing-evidence denial and a positive explicit-evidence late-reset case. Existing capture,
  crypto, provider, attestation, legal, factual carry-forward and model checks passed again.
- Final solution Release build: **0 warnings / 0 errors**. Actual anonymous HTTP/OpenAPI sweep:
  **1014** checks, owned host stopped. It is not authenticated SQL or client/provider acceptance.
- Final review also reproduced missing-manifest receipt replay and omitted biometric/ordinary
  keyring path isolation. New lock replays always validate locked evidence; all keyrings' active
  and retained key paths are checked against all storage/index roots. Closure was independently
  confirmed by static inspection. No remaining finding in that bounded closure review.

## Changed module and file groups

- Domain PayrollInputs: extended `AttendancePayrollInputRules.cs`; new `AttendancePayrollConfirmedOrdinaryDay.cs`.
- Application PayrollInputs: producer/contracts, transaction-local source/attestation validation,
  new `AttendancePayrollReconciliationContracts.cs`, `AttendancePayrollReconciliationSourceReader.cs`
  and `AttendanceMonthLockFinancialReader.cs`.
- Application MonthLocks: service/contracts/evidence; optional `beforeReplay` hook in the existing
  `AttendanceOperation.cs`. Shared Application DI registers actual financial readers.
- Application FaceVerification: `FaceStorageRetentionContracts.cs`, `RetentionControlledFaceTemplateStore.cs`;
  existing enrollment failure classification includes retention storage failures.
- Infrastructure FaceVerification: `FaceStorageRetentionJournalContracts.cs`,
  `EncryptedFaceStorageRetentionJournal.cs`; root capture registration wires the selected PoC gate.
- Verification/docs: new reconciliation/lifecycle/composition harnesses and acceptance case contract;
  extended payroll/month-lock regressions; current verification, UAT, module map, architecture,
  face client/PoC docs, legal retrieval note and this handoff. No new schema migration in this pass.

## Explicit incomplete boundaries

The new monthly inspector checks authoritative saved calculations and handoff records, but
formal legal activation is still absent and blocks readiness. Empty-source handoff and exact OT
payroll handoff remain separate dependencies. Production successful finalization is not claimed.
Physical biometric deletion, independent payroll posting, financial correction carry-forward,
real provider/model/device acceptance and unresolved SRS extensions remain incomplete unless a
later executed section explicitly establishes them. No unresolved amount becomes zero.

The existing `Cosmix_Testing` target must not be written until the user confirms the additional
eight drafts are synthetic and approves a verified backup/pending upgrade, or a new exact isolated
target is approved. The previous source laptop's migration 17 baseline is not this laptop's state.
