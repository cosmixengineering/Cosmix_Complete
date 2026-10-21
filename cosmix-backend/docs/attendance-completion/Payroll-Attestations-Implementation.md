# Bounded Manual Payroll Attestations

## Approved no-deduction handoff - 13 October 2026

This dated section supersedes the empty-source rejection described in the initial slice below.
An authoritative protected saved calculation must be `ready`, validated, complete and issue-free.
Its exact `Lines=[]` now permits an authorized `handoff` only. The existing endpoint, explicit
Accounts/Finance permission assignment, MFA, CSRF, original/current scopes, audit, source
fingerprint and idempotency contracts remain unchanged. There are no default grants or new roles.
The record is reviewable evidence of delivery, never Finance approval or proof of deduction.

The request must explicitly contain `lines: []` and `postedAtUtc: null`, retain the exact saved
revision/fingerprint and identify genuine handoff evidence using the existing evidence fields.
Do not invent an external receipt, timestamp, hash, evidence locator or zero-valued line.
Missing arrays and fabricated zero lines are invalid. Missing, incomplete, failed or unavailable
calculations cannot be saved as no-charge evidence. `posted` and `settled` against an empty
source remain `payroll_attestation_no_deduction_lines` (409). The confirmed-posting reader returns
null for this handoff, so it cannot generate refunds, recoveries or payment instructions.

Month dependency inspection retains the exact handoff ID and protected fingerprint and removes
only `payroll_no_charge_handoff_required`. Legal activation and every other readiness/finalization
guard remain. `PayrollReady`, `PaymentInstruction` and `ConfirmsPosting` remain false.
No table, migration, public request shape, endpoint or historical nonempty line fingerprint changed.

Executed: `VerifyPayrollAttestations.cs` **186** real-service/authorization/evidence/replay and
offline model checks with synthetic IO; `VerifyMonthLocks.cs` **58**; saved inputs **64**;
reconciliation **48**; legal evidence **46**. This is not authenticated SQL/HTTP acceptance.
The empty-source behavioral regression failed before the implementation and passed afterward.

Legal activation authority remains explicitly proposed, not approved. Admin policy configuration
and SuperAdmin company/location management do not confer salary-penalty legal approval.
No new approval responsibility or production activation was assigned.

## Second-pass consumers

The bounded cross-month input adapter now reuses static protected source/chain validation inside
the existing transaction and accepts explicit confirmed posted or settled receipts, not handoff.
Month dependency inspection also validates exact handoff against the saved revision. The original
confirmed-reader HTTP wrapper remains available, but is not nested into a producer transaction.
These consumers do not create credits, reversals, financial carry-forward or legal/payment authority.
See [reconciliation](Payroll-Reconciliation-Implementation.md) and [latest verification](Verification.md).
Earlier no-consumer statements below describe the initial attestation slice.

## Scope and Meaning

This slice records explicit manual evidence against an existing, protected, deduction-bearing Attendance payroll-input revision. It does not cover every employee's monthly payroll handoff. It does not connect to payroll, verify an external receipt, reconcile financial balances, certify legal policy, release payments, change compensation or make payroll ready. Saved previews/revisions are never posting proof. A configured jurisdiction and proposed legal authority remain insufficient for legal approval.

A complete verified `ready` calculation with `Lines=[]` is valid payroll-input evidence, distinct from missing/unavailable input and from a line whose explicit amount is zero. It can be previewed/saved by the existing input service. This bounded attestation slice rejects all handoff/posting/settlement and confirmed-read attempts for that valid empty revision with `payroll_attestation_no_deduction_lines` (409), after protected source validation. JSON permits an explicit empty line array so this source classification can be reported; it still requires the field and forbids missing amounts. No zero line is fabricated, no empty posting proof is generated, and no-charge/monthly handoff is claimed. A general no-charge payroll handoff requires a separately approved workflow. An unavailable source remains unknown with a null financial total and cannot be saved; an absent revision is not found. Empty request lines against a nonempty saved source remain an invalid receipt.

One immutable chain per employee/month is bounded to three events:

| Event | Required Predecessor | Meaning |
| --- | --- | --- |
| `handoff` | None | Explicit documented delivery of the complete saved inputs; no posting timestamp or posting claim. |
| `posted` | Exact handoff ID | Authorized manual assertion that the exact lines were posted, with actual posting timestamp and external evidence. |
| `settled` | Exact posted ID | Authorized manual assertion of settlement, retaining the same posting timestamp and external posting reference. |

The first handoff must choose the latest saved revision. Later events stay bound to that revision even if Attendance later saves a replacement. A replacement cannot inherit or open a second chain for that employee/month. This bounded slice has no cancel, reversal, replacement, partial posting, partial settlement or editing route. Any correction/reversal requires separately designed, explicitly authorized append-only workflow; operators must not create another reference to bypass a conflict. Confirm actual source/reference/amounts before handoff. Settlement here describes the attested deduction posting, not proof that an employee's entire net salary was paid.

All responses have `VerificationMode=manual_authorized_attestation`, `PayrollReady=false` and `PaymentInstruction=false`. `ConfirmsPosting` is false for handoff and true only for retained posted/settled receipts. It means authorized manual assertion, not independently verified external-system status. No financial reconciliation consumer is implemented.

`IAttendanceConfirmedPayrollPostingReader` is a read-only internal consumer interface implemented by the same service. `ReadConfirmedPostingAsync` delegates to the exact retained-history path: existing protected input detail read first, bound-source/chain/evidence validation inside the next authorized transaction, original/current scopes and both source/attestation read permissions, current web MFA, and the same history audit. It returns null for a deduction-bearing saved source or handoff-only chain; otherwise it returns the last exact validated manual posted/settled receipt. Valid empty revisions return the specific no-deduction conflict above. Corrupt, inaccessible or mismatched evidence throws rather than becoming null/zero. There is no extra HTTP endpoint, fallback or bypass. This adapter alone does not resolve `prior_deduction`, source-to-target line mapping, reconciliation or credit production. The whole consumer's source and target provenance must be designed and implemented before any such financial action.

## Authority and Evidence

Only explicitly authorized Accounts & Finance personnel may receive `attendance.payroll-attestations.attest`. The business owner must approve the individuals and scopes before assignment through the existing permission system. There is no hardcoded role, default grant, seeded role, Admin/SuperAdmin bypass or department-name check. Runtime authority is the dedicated current assigned permission, current eligible session, web MFA and all required scopes. Provisioning must enforce the owner's Accounts & Finance restriction; the permission evaluator does not independently infer someone's job function.

Restricted reads separately require `attendance.payroll-attestations.read`; all routes also require existing `attendance.payroll-inputs.read`. No permission implies another. Existing cookie/CSRF middleware applies to POST. Original protected source scopes and server-resolved current scopes are both enforced. Audit author is the authenticated account, never a body field.

Every POST explicitly provides source revision/fingerprint, expected previous ID (including explicit `null` for initial handoff), status, UTC event time, nullable UTC posting time, complete exact line keys/amounts/currency, external system/reference, evidence locator, uppercase SHA-256 of the external evidence artifact, statement and reason. Missing amount, duplicate key, duplicate JSON field, unknown JSON field, negative amount, sub-cent amount, wrong currency, missing/extra line or one-cent mismatch fails. An existing explicit zero line must be included; a missing amount/source is never converted to zero. Valid empty sources have the distinct no-deduction response documented above; unavailable sources cannot be attested. Receipt author and creation time are set by the server.

The evidence locator is an explicit human-supplied external document identifier, not a trusted private-store reference. Its checksum and statement record the author's assertion; the application neither downloads it nor independently checks it. Source references and all line/evidence content are retained in protected storage under the existing private-file store. Headers retain SHA-256 integrity fingerprints and bounded identifiers, not financial amounts or external receipt bodies. Existing audit/idempotency snapshots are also protected.

## API

`POST` and `GET`:

`/api/v1/attendance/employees/{employeeId}/payroll-inputs/{year}/{month}/revisions/{sourceRevisionId}/attestations`

POST requires `Idempotency-Key`. GET returns zero to three validated events for that exact source. A chain bound to another source returns conflict, not the other source's evidence. POST JSON is case-sensitive, rejects duplicate/unmapped fields, requires explicit numbers/nulls and is bounded to 256 KiB. Protected attestation evidence is bounded to 1 MiB. A long payload may hit the byte limit before the maximum 732 lines; shorten explanatory text/locators without omitting source lines.

Request fields: `expectedSourceRevision`, `expectedSourceFingerprint`, `expectedPreviousAttestationId`, `status`, `occurredAtUtc`, `postedAtUtc`, `lines` (each `key`, `amount`, `currency`), `externalSystem`, `externalReference`, `evidenceReference`, `evidenceSha256`, `statement`, `reason`. No sample financial receipt is fabricated by this implementation.

Handoff posting time must be null. Posted event time equals actual posting time. Settlement event time is at or after posting/handoff and retains the prior posting timestamp/reference. Future events and events predating the bound source's creation are rejected. External system is a bounded ASCII code, normalized uppercase; reference is trimmed and remains case-sensitive. References must uniquely identify this employee/month's receipt. A batch reference shared by several employees needs a stable employee/month component in its explicit external reference. Same normalized system/reference and stage cannot be reused across chains.

## Transaction and Integration

The existing `AttendancePayrollInputService.DetailAsync` runs first in its own audited transaction and verifies authoritative protected source evidence. The subsequent `AttendanceOperation` transaction reloads the header/protected source, rechecks identity/fingerprint/complete lines/scopes, verifies all retained receipt evidence and validates the expected predecessor before staging a new row. It never calls DetailAsync from inside another AttendanceOperation. This avoids the existing nested-transaction prohibition.

The shared serializable authorization transaction/application lock serializes Attendance saves and attestations. Expected predecessor plus SQL unique employee/month/stage and external receipt/stage indexes prevent concurrent duplicate stages. Composite restricted FKs bind the employee/month/source and predecessor. `IAppendOnlyEntity` uses the existing DbContext save guard to reject updates/deletes. Existing idempotency returns the same protected response only after current authority and scopes pass; a different payload under the same key conflicts. Expired receipts/second keys do not append duplicate events. Private writes that precede a failed database commit may leave protected unreferenced files; follow the existing storage lifecycle, never fabricate a committed receipt.

### Exact Root-Owned Changes

In `src/Cosmix.Application/DependencyInjection.cs`, alongside Attendance registrations:

```csharp
services.AddScoped<AttendancePayrollAttestationService>();
```

For internal consumers that request the confirmed-posting interface, add an alias to that same scoped service:

```csharp
services.AddScoped<IAttendanceConfirmedPayrollPostingReader>(provider =>
    provider.GetRequiredService<AttendancePayrollAttestationService>());
```

This alias adds no table, permission, endpoint or migration change.

In `src/Cosmix.Infrastructure/DependencyInjection.cs`, alongside the payroll ledger registration:

```csharp
services.AddScoped<IAttendancePayrollAttestationRepository, AttendancePayrollAttestationRepository>();
```

The existing files already import the payroll-input Application namespace and matching Infrastructure repository namespace. In `src/Cosmix.Application/Authentication/Access/AccessPermissionCatalog.cs`, add to `All`:

```csharp
.. AttendancePayrollAttestationPermissions.Definitions,
```

Do not add default permission assignments. Before operational use, the owner must approve the actual Accounts & Finance recipients/scopes, then authorized administrators provision both dedicated permissions and existing source-read permission through the current auditable access workflow.

No existing service/ledger interface method is needed: source lookup uses `IAttendancePayrollInputLedger.FindAsync/LatestAsync`; protected reads use `AttendanceOperation.ReadPrivateAsync`; source detail verification uses the existing service. No DbContext edit is required: the repository uses `db.Set<AttendancePayrollAttestation>()`; `ApplyConfigurationsFromAssembly` discovers the new mapping and the existing append-only guard applies. A typed `DbSet<AttendancePayrollAttestation>` would be optional convenience only, owned by root. Controller discovery needs no Program edit. No package or project reference change is needed.

Root alone must generate/review the migration and snapshot after acquiring the build slot. New mapping creates `attendance.AttendancePayrollAttestations` with primary `Id`, source and predecessor restricted composite FKs, Employee/UserAccount restricted FKs, unique `(EmployeeId, PeriodStart, Step)` and `(ExternalReceiptKey, Step)`, binary-collated status/fingerprints, period/source revision/stage/time/hash/UTC checks, and the predecessor's alternate key `(EmployeeId, PeriodStart, SourceRevisionId, Id)`. The source principal `(EmployeeId, PeriodStart, Id)` already exists. Reject destructive downgrade when retained attestations exist. Do not seed receipts, financial grants or business data. Pending model changes are expected until root owns the updated snapshot.

### Verification Status

`docs/attendance-completion/VerifyPayrollAttestations.cs` is standalone file-based regression source, written before production files, then expanded before the confirmed-reader implementation. It executes the real `AttendancePayrollInputService` to preview/save protected source revisions, the real `AttendancePayrollAttestationService`, and the unchanged `AttendanceOperation` authorization/audit/idempotency path. Only IO is synthetic: purpose-bound in-memory protected storage, module repositories, grants/current-session adapters, and a transaction adapter that rejects nesting and rolls back staged rows/audits/receipts on failure. The calculation producer is a synthetic financial-input contract, not verification of attendance calculation or payroll integration. Reflection is used only to dispatch the large existing authorization interface's generic transaction method, not to inject entities or alter production authority.

Service checks cover exact saved-source amounts despite changed current calculation output, explicit zero-line retention, missing/null/wrong amount denials, valid empty versus unavailable/absent source, handoff versus manually posted/settled semantics, confirmed-reader provenance, source hash corruption before and between transactions, retained posting evidence corruption, same-key changed payload/replay, stale source/predecessor/latest source, a later replacement that cannot change a bound chain, removed source between transactions, revoked original/current source and attestation scopes, revocation between detail/write, current session/MFA/mobile denials, protected audit/result receipts and synthetic commit rollback. Offline append-only checks act on the actual service-produced final settled entity. Pure rules and offline EF model checks remain, with an interceptor forbidding SQL connections. All service checks require root's real catalog integration; the harness never changes catalog definitions or grants defaults. After root confirms the single build slot:

```powershell
dotnet build Cosmix.Backend.sln -c Release --no-restore
dotnet run --file docs/attendance-completion/VerifyPayrollAttestations.cs
```

No test project is created. The worker did not compile or execute this regression source because the shared build slot was not authorized. Red/green TDD execution is therefore unverified. No SQL, database fixture, migration application, real account, live permission/CSRF test, real storage fault test, authenticated SQL race/replay test or external posting verification was performed. Root's earlier successful build predates these new files and is not evidence for this slice. Observed test database inventory is migration 12 and contains unexpected employee/draft data; it must not be upgraded or used for writes without the pending confirmation. Earlier migration-17 expectations must not be used as proof of its current state.

Before deployment, root must verify denied HR/unassigned personnel, approved Accounts & Finance scope/MFA/CSRF, scope loss between preliminary detail and write, source revision race, same predecessor race, cross-employee receipt reuse, protected evidence tampering/failure, replay after revoke and finalized-chain rejection. Full financial reconciliation/readiness remains separate future work and must require actual posted receipt evidence.
