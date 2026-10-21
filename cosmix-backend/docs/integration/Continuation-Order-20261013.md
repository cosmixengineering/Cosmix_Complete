# Cosmix Attendance And Integration Continuation Order

Prepared 13 October 2026, Asia/Karachi. The integration document's source-baseline
step has started. Attendance acceptance remains incomplete. Real face-provider
testing is deferred, not waived; it does not prevent source review and design for
the first CRM trial. This sequence does not activate financial rules or promise
the full SRS within five months.

## Current Scope

Keep the original `Cosmix_Erp_software\cosmix-backend` authoritative. Preserve all
local changes, authentication, protected storage, applied migrations and unrelated
HR features. No Git/GitHub synchronization, branches, worktrees or automation.
The separate backend outside this parent folder is not a target.

User decisions on 13 October: preserve the old `Cosmix_Testing`; verify and propose
a fresh setup before SQL writes; defer real CompreFace/ONNX/device acceptance;
configure KPI/waiting/extra-leave/encashment/recovery policies without invented
defaults and block dependent effects until approved applicable rules exist.

## Delivery Order

| Stage | Work | Exit evidence |
| --- | --- | --- |
| Now: integration baseline | Pin actual target/donor identities, selected source hashes, dependencies, defects and target ownership | [CRM source baseline](CRM-Contact-Source-Baseline-20261013.md); no code import claimed |
| Now: isolated setup review | Check absent target/paths, migration/seed boundaries, file placement and exact fresh adapter design | [SQL proposal](../attendance-completion/Fresh-Isolated-SQL-Proposal-20261013.md); explicit user approval still required |
| Attendance policy design | Extend existing immutable policy/version/applicability records with typed extension contracts; separate definition, business decision and financial activation | Reviewed authority/schema/lifecycle design, no assumed values or default grants |
| Approved isolated acceptance | Fresh synthetic authentication, then bounded attendance fixtures; real MFA/CSRF/SQL/storage/race checks | Executed case results, actual target/history and storage receipts; not synthetic adapter counts |
| First Contact trial | Review identity/scope/DTO/frontend ownership; implement list/create/detail/edit using target services and selected donor UI | Authorized and denied scope, persistence, expected-version/retry/audit checks and regression of existing HR authentication |
| Inventory | Items/units/stores, append-only physical ledger, receipts, issues/returns, transfers, then valuation/reports | Atomic multi-line posting, no duplicate retry or stock creation; approved unit/valuation/negative-stock rules |
| Projects | Project/task/member/board/comment flows mapped to existing employees | Membership and cross-module contracts, auditable transitions and affected verification |
| Later linked packages | Selected Procurement, Accounts/Finance and FSM/customer/mobile/channel capabilities | Their own reviewed SRS gaps and accepted workflows, not wholesale donor imports |

CRM's first trial is a five-working-day time budget from the original review plan,
not a completion guarantee. Re-estimate after measuring the accepted trial. With
three developers, each usable slice needs review and acceptance capacity; do not
start every ERP module simultaneously. Shared composition, permissions, context
and migration snapshot require one named owner before implementation.

## Policy Extension Boundaries For Review

Reuse `Policy`, `PolicyVersion`, `PolicyAssignment`, immutable assignment endings,
existing approvals/evidence and module-specific repositories. Schema 3 currently
handles arrival, paid time, ordinary pay and late deductions, not a KPI engine.
Keep historical schema 1/2/3 interpretation intact while designing explicitly typed
KPI, waiting-attribution, benefits, encashment and recovery contracts.

Policy management must validate all required values, applicability, effective
intervals, actor permissions, concurrency and audit history. A saved draft, role
name, province selection or SuperAdmin toggle is not approved legal applicability.
Missing/unapproved/conflicting rules must explain why a calculation is unavailable;
an empty table must not be interpreted as zero impact or exemption.

Benefit awards/elections, reservation/consumption, finalized KPI evidence,
waiting verification and recovery fulfillment need actual lifecycles. Existing
models alone do not provide them. Current ordinary leave balance reads only
Utilization/Release; arbitrary Entitlement/Encashment rows would not extend it.
Extend that interpretation deliberately, preserving historical day denominators.
Reuse financial input revisions and exact Finance receipts instead of a new payroll
module. Existing deduction receipts cannot settle new benefit credits, partial
claims or reversals without a supported contract.

Remaining policy decisions can be supplied as configured approved definitions,
not hardcoded answers: extension verifier/approver authority; exact KPI period and
precision; eligible benefit pools/elections/expiry/valuation; waiting attribution;
and consumption, recovery-credit allocation and settlement/reversal rules. For
example, eight unused days or 45 minutes of waiting do not establish an amount.
An OT-awarded interval cannot silently become recovery credit as well.

These boundaries are a proposal for review, not an approved implementation design.
The ordinary direct-publication, Admin leave, single-HR correction/OT and other
already-confirmed Attendance workflows remain unchanged.

## What Still Prevents Full Attendance Acceptance

Read [the latest verification](../attendance-completion/Verification.md), not its
superseded older sections. Outstanding work includes authenticated SQL/private
storage/concurrency acceptance; confirmed financial credit/recovery/carry-forward;
no-charge and OT financial handoff; formal jurisdiction-aware legal activation;
positive month finalization with all verified dependencies; extension workflows;
and biometric lifecycle/device/spoof acceptance. Face provider testing is deferred
by the user, and fail-closed Check-In gates remain intact. Check-Out stays geofence
only. No salary payment is released and locked months never reopen.

## Fresh Checks In This Continuation

| Executed 13 October | Result and limit |
| --- | --- |
| Release solution build, `--no-restore` | 0 warnings, 0 errors |
| `VerifyPolicyPublication.cs` | 64 actual-service checks, synthetic repositories/private IO; no SQL |
| `VerifyAttendanceComposition.cs` | 14 actual registration/composition checks, synthetic storage; no SQL/network/private keys |
| `VerifyEvidenceMigration.cs` | 16 model/snapshot/incremental SQL checks; connection interceptor records zero attempts |
| Migration syntax/operation inventory | 21 Up methods, 800 builder calls, 11 raw SQL calls and zero seed-data calls; source review only |
| LocalDB/path inventory | Proposed instance/private/MDF/LDF/artifact paths absent; no database connection or instance start/create |
| Existing client HTTP file | SHA256 remains `704604E7A6F2A4723D4CE1FA0AA8082B5BAEBBFB4BD0B116F95ED827731234B7` |

The first policy harness command used `--no-restore` before its run-file assets
existed and failed with NETSDK1004. Restoring from the local NuGet cache allowed the
64-check run; no runtime guard was changed. The initial Roslyn SDK assembly-load
attempt conflicted with PowerShell's loaded parser; the clean repeat used its
version 5 parser and reported zero migration syntax errors. Neither is SQL acceptance.

The initial source-review stage added only planning notes. After review, the user
approved local code-only testing preparation: the fresh target guards, explicit
seed handoff and isolated host configuration were added without SQL/instance/key
writes. See [the exact later patch and tests](../attendance-completion/Fresh-Preparation-Handoff-20261013.md).
No permanent connection configuration, source migrations or client code was changed.
The executable provisioning/configuration/secret-generation adapter and final
manifest remain incomplete; old laptop/old-target runners are not substitutes.
