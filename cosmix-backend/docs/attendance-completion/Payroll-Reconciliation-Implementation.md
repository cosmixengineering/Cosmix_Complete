# Bounded ordinary/late payroll reconciliation

## Implemented local calculation path

The prior cross-month rule recomputed ordinary candidates for earlier dates, subtracted them,
and emitted `prior_deduction_reconciliation_required`. Revisions and handoff receipts do not prove
actual deductions. The new path uses the exact protected revision pinned by a retained receipt
chain and explicitly confirmed manual `posted` or `settled` evidence. Posting already confirms
the actual deduction under the existing contract; an extra settlement event is not required.

`AttendancePayrollInputRules.Calculate` accepts confirmed ordinary source evidence. Each prior
source date must match retained attendance, policy and compensation identities/fingerprints,
the original ordinary line, source revision, and confirmed receipt identities/fingerprints.

`new late charge = max(0, late candidate - current-month ordinary candidates - prior confirmed ordinary amounts)`

The prior ordinary line retains its original salary, divisor, rounding, exact fraction and date.
The late threshold retains the completion-date salary and current month divisor. Missing or
mismatched evidence leaves the result unavailable and omits the unresolved late line rather than
exposing a false provisional amount. Current ordinary candidates may remain visible; unavailable
preview totals stay null through the unchanged input service. A larger ordinary charge never
creates a credit, reversal, salary change, reopening, activation, payment instruction or readiness.

The new source reader selects the chain's pinned revision through receipt headers, never the
latest replacement. Root's static `ReadValidatedEvidenceAsync` and `ReadValidatedHistoryAsync`
validate protected source/header bindings and all retained receipts. Corruption and denied access
throw rather than becoming missing evidence or zero money. Saved-only/handoff-only sources stay
unavailable. Confirmed `posted` and `settled` receipts are accepted.

The reader checks current authentication, web MFA, current session and both financial read
permissions against complete current/original source scopes. Current scopes come directly from
`IAttendanceSummaryRepository`, including the retained lookback. It runs within the caller's
existing authorization transaction and never invokes a nested operation, public history/detail
service, or producer. It writes no financial/business records.

Prior late lines overlapping requested dates remain an explicit unsupported reconciliation case;
they cannot become ordinary comparison amounts.

Prior zero ordinary charge is accepted only when a complete retained nonempty source with
validated confirmed posting proves the exact day and contains no ordinary line for it. An empty
prior revision cannot obtain posting proof through the existing bounded attestation workflow and
remains unavailable. Absence of a receipt is never treated as a zero deduction.

The producer requests only prior dates of completed unresolved thresholds, retains confirmed
source evidence in nullable `PriorConfirmedOrdinary`, and unions retained current/original scopes.
The new manifest property is omitted when null using `JsonIgnore(WhenWritingNull)`, preserving
historical schema-1 serialization/fingerprints. Existing calculation version and line keys remain.
New non-null provenance changes the target fingerprint, including progression from posted to
settled evidence. Stored historical revisions are never rewritten.

The two-argument domain API and producer constructor remain source compatible. A producer without
the optional reader continues to fail closed. No source service, attestation service, month-lock,
carry-forward, table or migration change is made by this worker.

## Root integration

Root owns the existing static validators and this shared registration:

```csharp
services.AddScoped<IAttendancePayrollReconciliationSourceReader, AttendancePayrollReconciliationSourceReader>();
```

The reader depends on ledger, attestation repository, summary repository, AttendanceOperation,
current authentication, access reader, scheduling journal and TimeProvider. It has no dependency
on either payroll service or the producer, preventing a circular DI graph. No permission/catalog,
default grant, DbContext, snapshot or migration change is needed.

## Verification and limits

`VerifyPayrollReconciliation.cs` was written before production edits, then expanded to exercise
real rules, source reader, existing InputService saves, existing AttestationService receipts,
root static protected validators and unchanged AttendanceOperation. Synthetic adapters supply
repositories, purpose-bound protected storage, authentication/session/grants and the transaction.
The transaction adapter rejects nesting. No SQL or network connection is opened.

Coverage includes original divisor/completion salary, two prior months, larger-only suppression,
duplicate/changed provenance, explicit zero versus missing evidence, posted/settled receipts,
saved-only/handoff, pinned source despite a later replacement, original/current scope loss,
session/MFA/mobile denial, source/receipt tampering and historical manifest byte compatibility.
Synthetic source calculation does not verify real employment coverage, protected compensation
approvals, daily attendance production, real private storage or SQL.

Executed on 12 October 2026 after root released the shared build slot:

```powershell
dotnet run --file docs/attendance-completion/VerifyPayrollReconciliation.cs --no-restore --no-cache -c Release
```

The first attempt reported `NETSDK1004` because the new file-based app had no temporary assets.
An offline restore/run restricted `RestoreSources` to the existing local NuGet cache and disabled
NuGet audit, without accessing external package sources:

```powershell
dotnet run --file docs/attendance-completion/VerifyPayrollReconciliation.cs --no-cache -c Release --property:RestoreSources=C:\Users\Huma.Khan\.nuget\packages --property:NuGetAudit=false
```

It passed **48** rules/actual-reader/source/receipt assertions. No compiler warning/error appeared.
The same 48-check `--no-restore --no-cache -c Release` run then passed after the regression proof:

1. Temporarily permit an unresolved cross-month threshold through the owned domain's
   `if (!priorAvailable) continue` guard. This restores the original unknown-prior numeric-charge
   defect, without changing authentication, evidence validation or any other developer's files.
2. Run the full harness and observe exit 1 at the first provisional-charge assertion:
   `Missing settlement must not expose a provisional late amount as a charge`.
3. Immediately restore the exact guard and rerun the whole harness: exit 0,
   `PASS: 48 reconciliation rules/actual reader/source/receipt assertions; synthetic IO, no SQL/network writes.`

This is **post-implementation regression RED -> GREEN proof**, not a fabricated test-first
execution history. The test source preceded implementation, but the user instructed preparing
patches while another worker owned the build slot, so the first runtime run followed implementation.
The temporary failing behavior was removed and the restored guard was independently re-read.

The existing `docs/attendance-policies/VerifyPayrollInputs.cs` was also run with
`--no-restore --no-cache -c Release`. It failed at line 120:
`Known legacy-to-schema3 late-rule change resets prior historical uncertainty`.
That cross-month case expects ready without confirmed prior evidence. The new conservative path
also verifies prior no-ordinary dates against retained posted/settled source evidence rather than
assuming actual zero. The root-owned existing harness was not edited. Root must review this
expectation; the affected suite is not reported green. Root owns final solution build and broader
model/HTTP regressions. The worker released the shared build slot after these runs.

This is bounded input reconciliation only. Manual receipts remain authorized assertions, not an
independently verified external payroll adapter. Financial carry-forward, reversals, credits,
legal activation, general readiness and payment release remain separate.

No test project, database writes, migration application, network synchronization, git mutation,
DI/catalog edit or shared build-file change occurred in this worker's scope.
