# F3 financial carry-forward contract

Bounded service, source reader, persistence mappings, controller and standalone regression source are implemented. No worker build, SQL, provider, host, private-storage acceptance or migration execution is claimed. Parent owns wiring, schema 4 and Finance consumption, migration/snapshot and compilation.

## Parent integration contract

Application namespace: `Cosmix.Application.Attendance.PayrollInputs`.

```csharp
Task<IReadOnlyList<AttendancePayrollFinancialMonthEntry>>
    IAttendancePayrollFinancialMonthReader.ReadAsync(DateOnly targetMonth,
        IReadOnlyList<Guid> employeeIds, AttendanceActor actor, CancellationToken ct);
Task<bool> IAttendancePayrollFinancialTargetLockReader.ContainsAsync(
    AttendancePayrollFinancialMonthEntry entry, AttendanceActor actor, CancellationToken ct);
```

`AttendancePayrollFinancialMonthEntry` fields in order:

```text
AdjustmentId, EmployeeId, SourceMonth, TargetMonth, Revision, SourceRevisionId,
ConfirmedPostingId, TargetRevisionId, TargetInputFingerprint, PreviewFingerprint,
EvidenceFingerprint, EvidenceReference, SignedAmount, Currency, Status,
PostingResolutionId, PostingEvidenceFingerprint, FactualAdjustmentIds
```

Positive SignedAmount means additional recovery; negative means employee credit.
The HR OtherApprovedAdjustment projection negates this sign. All values are PKR.

- `approved`: exact approved unposted financial input. May be frozen in schema 4 and handed off once by adjustment ID/revision. Exact zero is verified no-change coverage; it needs no posting receipt and creates no handoff amount.
- `posted`: separately confirmed external posting. Freeze evidence, but do not create another debit/credit. Re-read current resolution before Finance lock/export.
- `cancelled`: retained by the history API and excluded from the month reader, monetary projection and factual financial coverage.

The lock pins immutable approval fields, including private reference/hash and factual adjustment IDs. Later `approved` to `posted` resolution must not invalidate that approval membership. Current resolution fields are append-only evidence, not immutable lock-input identity. Cancellation after target lock is denied. Posting after lock requires the parent-provided exact membership verifier and fails closed without it. Parent must include factual carry-forward employees when inspecting month financial readiness even if absent from the ordinary employment population.

The finalized status spelling is `approved`, replacing the earlier draft's `approved_input`/`no_change` labels. Entries are sorted by EmployeeId, SourceMonth, Revision. The repository reads all target rows without an employee filter; omitted noncancelled employees produce `financial_carry_forward_target_population_incomplete`, never an empty amount. Cancelled rows are validated before exclusion. Locked target reads validate retained evidence and current scopes without calling the live financial source reader. Open target reads revalue only the latest unresolved approval; historical zero approvals remain immutable after successor valuations. Earlier approvals cannot be cancelled after a successor.

FactualAdjustmentIds includes the latest interpretations and their exact retained predecessor IDs, so cumulative valuation covers earlier factual revisions as well as the latest one. The existing contract bound is 31 total IDs; longer histories remain a specific dependency. Approval/posting/cancellation retries require the corresponding persisted business row, terminal resolution where applicable, and exact protected response; an orphan idempotency receipt cannot return success. Self review is denied before replay too.

Domain namespace: `Cosmix.Domain.Attendance.PayrollInputs`.
New types: `AttendancePayrollFinancialAdjustment`, `AttendancePayrollFinancialResolution`.
Configurations: `AttendancePayrollFinancialAdjustmentConfiguration`, `AttendancePayrollFinancialResolutionConfiguration`.
Repository uses `db.Set<T>()`; no typed DbSet is required. Parent owns migration/snapshot generation and inspection.

Required scoped registrations:

```csharp
services.AddScoped<IAttendancePayrollFinancialRepository, AttendancePayrollFinancialRepository>();
services.AddScoped<IAttendancePayrollFinancialSourceReader, AttendancePayrollFinancialSourceReader>();
services.AddScoped<AttendancePayrollFinancialService>();
services.AddScoped<IAttendancePayrollFinancialMonthReader>(sp =>
    sp.GetRequiredService<AttendancePayrollFinancialService>());
// Parent supplies IAttendancePayrollFinancialTargetLockReader implementation.
```

Use existing payroll-input read and Accounts/Finance payroll-attestation read/attest permissions. No new grant, role, legal authority or payment authority is introduced. Enable controller/service wiring together with the real MonthLock and PayrollHandoff consumers, not before them.

## Bounded behavior

The source must have a validated exact posted receipt; previews, handoffs and absent money never fund credits. Monthly recalculation uses retained source policies, salary, leave, roster and OT evidence with validated approved factual duty replacements. Same-month ordinary/late overlap is recomputed as a whole. Cross-month late reset dependencies and special reviewed-work interpretations fail closed.

An authoritative saved current target-month input revision is required. Former employees without that target evidence remain `financial_carry_forward_target_inputs_unavailable`; this pass does not invent post-employment target salary/assignment evidence. They must remain visible as a readiness dependency.

Only confirmed prior adjustment postings affect the net financial balance. Pending approval blocks a later valuation until posted or cancelled; exact no-change valuations do not block subsequent corrections. Credit PKR 100 follows an actual PKR 200 deduction corrected to PKR 100. No salary release or legal activation occurs.

## API and verification

Route base: `/api/v1/attendance/employees/{employeeId}/payroll-inputs/{year}/{month}/financial-adjustments` where year/month identify the original deducted source month.

- GET `preview`: exact valuation and first unlocked target, before approval.
- GET base: bounded immutable approval/resolution history.
- POST base: approve the expected preview hash, target and predecessor.
- POST `{adjustmentId}/posting`: separately attest exact signed amount and PKR currency with dated external receipt/evidence.
- POST `{adjustmentId}/cancel`: cancel only the latest unresolved approval while its target remains unlocked.

Every mutation requires the existing Idempotency-Key contract. Request JSON rejects duplicate/unmapped/case-mismatched fields and implicit numeric values. Amounts, policy, salary, original source and corrected factual evidence remain in protected storage. Headers retain only bounded identities and fingerprints. Existing audit/idempotency and serializable authorization transaction are reused; a second key cannot repeat an unresolved approval, resolved posting or already valued correction set. Private-file failure may leave an unreferenced protected object, consistent with the existing storage lifecycle, but cannot commit a financial record without its journal/receipt.

New tables are `attendance.AttendancePayrollFinancialAdjustments` and `attendance.AttendancePayrollFinancialResolutions`. Unique employee/source-month/revision and predecessor indexes prevent branching; unique resolution per adjustment and non-null receipt-key index prevent repeat resolutions. Composite restricted FKs bind exact employee/source/target revisions and original posting. Both entities implement the existing append-only guard. No context, permission catalog, dependency injection, model snapshot, migration or package file was edited by this worker.

Standalone source: `docs/hr-srs-remediation/VerifyFinancialCarryForward.cs`. It exercises the real financial service, AttendanceOperation and configured time/payroll rules with explicitly synthetic source-reader/authorization/private-storage/target-lock adapters. It includes actual 200-to-100 arithmetic, approval/posting separation, later corrections, no-change succession, historical zero read/cancel regressions, retry/receipt duplication, target/source change during private IO, locked posting, scope/session/MFA denials, omitted target population, corrupted evidence and synthetic transaction rollback. It does not claim verification of the real SQL source-reader/factual evidence graph or Finance Preview/Prepare; parent consumer suites cover their own boundary.

Exact parent-owned commands, after shared wiring/migration integration and acquiring the sole build slot:

```powershell
dotnet build Cosmix.Backend.sln -c Release --no-restore
dotnet run --file docs/hr-srs-remediation/VerifyFinancialCarryForward.cs
```

The worker has not executed either command. Do not infer passing checks from the source or from older frozen builds. A02 source hashes are now historical; its packet was not regenerated or executed. Live HTTP/SQL, independent connections, encrypted storage failures/restart and owner acceptance remain unexecuted.
