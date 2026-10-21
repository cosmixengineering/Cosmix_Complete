# G1 Recovery Commitment Evidence Continuation

15 October 2026. Implementation and synthetic standalone verification. The parent
subsequently authorized this agent to execute the checks against its frozen
Release: recovery 51/51, encashment workflow 30/30, valuation 80/80 passed.
This does not complete G1 benefits, leave credits or recovery fulfilment.

## Changed Paths

- `src/Cosmix.Application/HR/Leaves/LeaveEncashmentService.cs`: only the class
  partial declaration and recovery methods changed in this continuation.
- `src/Cosmix.Application/HR/Leaves/LeaveRecoveryEvidence.cs`: new recovery-only
  approval, cancellation, retained audit and receipt validation.
- `src/Cosmix.Application/HR/Leaves/LeaveEncashmentContracts.cs`: two read methods
  added to the existing module repository interface.
- `src/Cosmix.Infrastructure/Repositories/HR/LeaveEncashmentRepository.cs`: exact
  recovery audit and actor/operation/key receipt lookups using existing tables.
- `docs/hr-srs-remediation/VerifyLeaveRecoveryReplay.cs`: expanded standalone
  service checks and synthetic transactional rollback/concurrency collaborator.
- `docs/hr-srs-remediation/VerifyEncashmentValuation.cs`: the parent subsequently
  assigned this suite for the two recovery repository test-double handlers.
- This document.

Existing dirty encashment code was preserved. No other agents' paths, shared
composition files, permissions, DbContext, mappings, migrations, snapshot or
shared status documents were edited. The source freeze was respected: no edits
were made until the parent explicitly released it. Later source changes make
historical A02 source hashes stale; no A02 packet was regenerated.

## Implemented Behavior

Creation and receipt replay both require the exact approved leave submission,
unambiguous cancellation history, original protected decision proof and applicable
configured recovery policy. The policy must cover the leave dates and have been
published by the approval time. Frozen approval usage is used to verify the
original paid/unpaid treatment; today's balance is not charged again. Superseded,
pending-cancellation or cancelled parents cannot create/replay a commitment.

A completed, live receipt must resolve to exactly one persisted commitment for
that leave, with the same full response, recorder and recording time. That row
must also have one original recovery audit, the matching immutable before/after
evidence, original leave/decision/policy references and valid original date and
minute limits. A surviving receipt cannot certify a deleted or modified row.
Existing expired/incomplete/changed-payload receipt behavior remains owned by
AttendanceOperation; the new guard does not extend receipt expiry.

Self history still checks ownership before fetching commitments. Each returned
commitment now verifies its audit and protected evidence, independently of retry
receipt retention. Valid history remains readable after a leave cancellation;
the row is a historical promise, not proof that work was completed. New promises
also verify prior commitments on the same leave before counting their capacity.
Cross-leave same-date reservations continue to count conservatively as before.

Recovery writes now select the permission-bearing fixed system role for the
operation audit. A custom All-scope grant encountered first can no longer label
the original/replay audit as the custom role while a separate fixed Admin grant
supplies the authorization. Previously retained audits with a non-fixed actor
role fail closed; no historical audit is rewritten or retrospectively certified.

The existing serializable authorization transaction and exclusive authentication
application lock continue to serialize parent checks, capacity reads, insertion,
audit and receipt commit. No nested transaction or external provider was added.
The existing private store is not transactionally rolled back with SQL; failed
operations may leave unreachable protected objects, as with the existing write
pattern. No cleanup or retention policy was invented here.

## Parent Integration

No schema, migration, permission, DI registration or controller signature changes
are needed. The existing repository registration supplies the two new methods.
The existing recovery create/list endpoints execute the strengthened behavior.

The parent subsequently authorized this agent to edit `VerifyEncashmentValuation.cs`.
Its `ILeaveEncashmentRepository` handler now includes these two switch cases, so
there is no outstanding parent test-double integration change:

```csharp
"RecoveryReceiptAsync" => Task.FromResult(Receipts.SingleOrDefault(x =>
    x.ActorUserId == ((Guid)a[0]!).ToString() && x.Key == (string)a[1]! &&
    x.Operation == LeaveEncashmentService.RecoveryOperation)),
"RecoveryAuditAsync" => Task.FromResult(Audits.SingleOrDefault(x =>
    x.EventType == LeaveEncashmentService.RecoveryOperation &&
    x.TargetType == "Attendance" && x.TargetId == ((Guid)a[0]!).ToString())),
```

Executed from the backend directory against the parent's frozen Release, without
rebuilding any product assembly:

```powershell
& .\docs\attendance-gap-c\VerifyManaged.ps1 `
  -ReleaseDirectory 'D:\CosmixVerification\hr-backend-parallel-20261015\build\bin\Cosmix.Api\release' `
  -Sources @('docs/hr-srs-remediation/VerifyLeaveRecoveryReplay.cs',
    'docs/hr-srs-remediation/VerifyEncashmentWorkflow.cs',
    'docs/hr-srs-remediation/VerifyEncashmentValuation.cs')
```

The runner compiles only the standalone checks in a new verification directory;
it strips project directives and uses copied, hashed frozen product assemblies.
It confirmed that retained Release inputs and check sources remained unchanged.
Exit code: 0. Output directory:
`D:\CosmixVerification\part-c-parent-20261013\managed-a3526ee855b94d5199b0d4f2e136657c`.
Per-suite outputs are `<suite>-compile.log` and `<suite>.log`; `inputs.json`
retains the assembly/dependency input hashes. No product compile or behavior
failure was found by these three suites; no further fix was required.

## Verification and Remaining Boundaries

Performed by this agent: source inspection of the actual SRS DOCX, policy and
approval contracts, EF repository queries, transaction implementation and private
audit serialization; scoped whitespace checks (zero findings), and in-memory
Roslyn syntax parsing of all five changed C# files (zero syntax errors). Loading
the SDK's Roslyn assembly first encountered the shell's existing assembly-version
binding; using the already loaded Roslyn 5.0 parser succeeded without any output
files or application execution. Syntax parsing is not compilation or type checking.
That initial syntax-only stage was followed by the authorized frozen-assembly
run above: all three check sources compiled and executed successfully. The
expanded recovery source retains the earlier sixteen checks and adds approval
proof/policy replay, missing/modified row, missing/duplicate/damaged audit,
invalid cancellation, supersession, history after receipt removal/cancellation,
mixed-role audit selection, synthetic serialized competition, publication/date
boundaries and synthetic rollback/retry after storage or journal failures.

The synthetic transaction is an in-memory collaborator; its passing run does not prove
SQL isolation, independent-connection races, actual encrypted storage or restart
behavior. Authenticated HTTP/SQL, rollback, source changes during protected IO and
independent-connection cancellation/replay acceptance remain unexecuted. No SQL,
instance, fixture, key, host, real provider or Git write was performed.

SRS LEAVE-011 permits management to grant extra leave or equivalent pay, while
MD-011 explicitly leaves additional six-day handling and the approval chain for
confirmation. Existing KPI settings define score calculation, and recovery
settings define commitment eligibility/caps/windows. Neither defines qualifying
recovery work, partial fulfilment/correction rules, conversion into leave/money,
benefit election/exclusivity or the missing benefit decision authority. These
rules were not invented. Actual fulfilment also requires competing OT/make-up/
early-credit consumers to recognize the same exclusive work allocations in both
creation orders. That is a separate cross-module implementation, not completed
by a commitment audit repair.
