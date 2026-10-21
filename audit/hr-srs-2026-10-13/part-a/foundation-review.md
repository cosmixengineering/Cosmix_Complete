# Independent Part A foundation review — 13 October 2026

Scope: latest dirty `cosmix-backend` and the existing `cosmix_frotned` consumers, limited to A01–A04 integration. Read the backend/web AGENTS, the approved A–G plan (`document-build/plan-content.json`, Part A), extracted SRS and A01/A02/A03/A04 execution notes. A05/A06 and the separate Part B attendance work were not independently reviewed here. No production source was edited, SQL connection or migration application performed, key/provider configuration changed, real identity operated on, or private employee file read.

The following findings were sent to root as they were identified. A separate implementer corrected them, and the scoped independent rereview below confirms all four are resolved. The original pre-fix triggers and evidence are retained for traceability. Root owns the coordinated final model and overall integration checks.

## Actionable findings

### FND-01 — P2: Suspension release creates an invalid Active identity for an employee who never activated

Location: `cosmix-backend/src/Cosmix.Domain/Authentication/UserAccount.cs:196` / assignment at line 201; caller `EmployeeLifecycleProcessor.cs:100`; existing constraint `src/Cosmix.Infrastructure/Database/Configurations/Authentication/AccountConfigurations.cs:17`.

Trigger: approve hiring and create its dormant PendingAccess identity, suspend the approved employee before first login, then approve suspension release under an enabled approved policy. The processor calls `PrepareForEmploymentReactivation`, which unconditionally sets the lifecycle-suspended identity to Active. Such an identity has no password/verified phone, `ActivatedAt`, or initial role assignment. The unchanged `CK_UserAccounts_Active` requires those activation facts. Release therefore cannot persist: immediate approval rolls back, or a scheduled reconciliation fails. ReadyForInvitation/Invited identities without activation have the same problem.

Evidence: fresh real Domain assembly, synthetic public-domain onboarding/suspend/release flow in TEMP exited 1 with:

```text
Created approved identity: PendingAccess; ActivatedAt=; PasswordHash=; PhoneVerifiedAt=; InitialRoleAssignmentId=
Released identity: Active; CK_UserAccounts_Active predicate=False
FAIL dormant release violates mapped existing active-account SQL constraint
```

The harness evaluates the actual existing mapped constraint's predicate; no SQL was contacted. Source: `C:/Users/Huma.Khan/AppData/Local/Temp/cosmix-foundation-review-2d9ec32073864a42ba69423adb61394d/Program.cs`, project `Review.csproj`, isolated `Artifacts` directory. An initial fixture run lacked required synthetic identity/contact fields and stopped at the domain submission guard; the evidence above is the corrected, completed reproduction.

Minimal fix: distinguish already activated identities from dormant identities during release and select a persistable dormant setup state for the latter. Retain revoked grants and disabled channels; handle retained invitation/initial-access pointers explicitly. Do not relax the existing Active-account constraint or fabricate activation evidence. Verify PendingAccess, ReadyForInvitation and Invited cases in addition to the previously activated case.

### FND-02 — P2: Hiring approval retries skip the sensitive rights required by the original approval

Location: `cosmix-backend/src/Cosmix.Application/HR/Employees/EmployeeApprovalService.cs:30`, conditional sensitive checks at lines 52–57, call terminator at line 92; receipt shortcut in `EmployeeOperation.cs:48`.

Trigger: a successful bank-containing hiring approval leaves a receipt, then the same reviewer loses bank-read/bank-request or financial/medical permissions but retains current Web MFA, fixed Admin review and the base employee/document permissions. The same key/body returns the original approval result because the receipt branch precedes the action. Conditional financial/bank/medical checks live only inside that action. `SaveFinancialAsync` already protects its corresponding retry through `authorizeResource`; `DecideAsync` does not.

Evidence: fresh real Application assembly, real EmployeeApprovalService/EmployeeOperation with synthetic authorization/repository ports and a valid saved approval receipt, exited 1:

```text
Replay succeeded: same DecisionId=True; BankRead=False; BankRequest=False; FinancialRead=False; MedicalRead=False
```

The receipt is serialized with the current omit-null format. This demonstrates the skipped authorization branch, not SQL transaction behavior. Harness source/project: the same TEMP directory above, `ApprovalReplay/Program.cs` and `ApprovalReplay/Review.csproj`; output under `ApprovalReplay/Artifacts`.

Minimal fix: before receipt lookup, load the authoritative submitted snapshot for approval and require the current sensitive permissions implied by that exact snapshot. Apply the same checks to initial execution and retry. Keep return/reject behavior and the immutable original receipt result intact.

### FND-03 — P2: New bank verification ignores due nonactive employment until the worker updates the cached employee

Location: `cosmix-backend/src/Cosmix.Application/HR/Employees/EmployeeBankService.cs:48`.

Trigger: an approved suspension, resignation or termination becomes effective while reconciliation is delayed. The target employee's cached `Employee.Status` is still Active. Bank VerifyAsync only reads that cached status and succeeds with otherwise valid bank/version/proof input, although this operation explicitly requires active employment. It has no effective status-history/marker check. Actor authorization is current, but does not establish the target employee's effective state.

Evidence: source trace through TransitionAsync, EmployeeAsync and the repository; no due history is queried in this path. The documented A03 contract says verification requires active employment. Existing access/update paths already resolve due status history independently of worker timing.

Minimal fix: use the current effective employment and reconciliation predicate for verification within the existing transaction, preserving independent bank-read/verify checks. Preserve authorized revocation/history for ended employment. Add the delayed-worker and unreconciled return-to-Active cases to synthetic verification.

### FND-04 — P2: Valid Unicode bank proof exceeds the new endpoint's body limit

Location: `cosmix-backend/src/Cosmix.Api/Controllers/HR/EmployeeBankController.cs:19` and line 22; accepted bounds `EmployeeBankContracts.cs:18`–19.

Trigger: send a valid 8000-character Urdu/Arabic text proof and 1000-character printable reason. Both satisfy the DTO bounds, but the request is larger than the 16384-byte verify/revoke limit and is rejected before service validation. JSON escaping increases the discrepancy.

Evidence: UTF-8 measurement of a compact valid body containing 8000 Arabic alif proof characters plus 1000 reason characters and valid versions was 18097 bytes. Worst escaped JSON for these bounded strings is approximately 54 KiB.

Minimal fix: set the two bounded bank JSON routes to a limit that accommodates the contract's UTF-8/escaped maximum, e.g. 65536 bytes, while retaining the DTO character bounds.

## Corrected during this review

The rescan endpoint initially retained `RequestSizeLimit(1024)` although its new contract permits a 1000-character reason plus two version fields. A compact valid ASCII body measured 1087 bytes. Root corrected document rescan/verification/expire/revoke/archive JSON limits to 16384; a subsequent source read confirmed the change. This is not an outstanding finding.

Root also confirmed that the pending A07 legacy marker backfill excludes Active releases, addressing the caution that cached Active status alone cannot prove an old suspension/release cycle revoked grants/devices. This review does not treat the not-yet-final migration/model as a completed-schema bug.

## Reviewed invariants and limits

- A01 receipt writes bind actor/action/key and exact payload, retain original versions, and check current fixed administrator authority before replay. Assignment retries also recheck target employment, role/permissions, scopes and protected system-account authority. Existing web consumers supply Reason and the new assignment key and retain uncertain intents in component memory.
- A03 normalizes supplied international personal/company/login/emergency contacts before draft/snapshot storage and approved changes; domain contact storage is canonical. Other non-rejected employee selections and live authentication bindings participate in login-phone uniqueness checks. Empty field policy retains baseline requirements; rule-bearing partial/unapproved/not-effective configuration fails closed. No pending taxonomy/age/probation values were invented.
- Bank status/history DTOs omit proof/full account data; private evidence binds exact employee/bank/version/actor/time. Journals are append-only with restrictive same-bank/employee relationships and successor/version uniqueness. Bank account changes start an unverified revision.
- A02 owns upload/scanner buffers across awaits, treats unavailable/unknown scanners as failure, commits rescan unavailability before scanner I/O, rejects stale attempt completion and requires renewed manual verification. Downloads perform a fresh authorization/evidence/date check after private I/O and clear denied buffers. Metadata journals omit object references and immutable upload metadata cannot be rewritten. Pending hiring approval rechecks exact live document versions and current dated availability.
- A04 reconciles transitions chronologically, revokes roles/devices/sessions/challenges/logical face use, retains evidence, and leaves channels disabled. Independent account blocks stay in force. Archive affects only ended-employment directory state; auth/access guards deny an unreconciled effective Active release until the exact marker is applied. Rehire's explicit 503 remains an intentional pending-policy gate.

These source checks do not prove real ClamAV detection, secure deployment/key backup/restore, SQL races/restarts/rollback, physical biometric disposal, genuine SMS/MFA/device acceptance, or authenticated browser behavior. Existing task evidence was read, not represented as a new execution here. The initial review freshly executed only the isolated reproductions and byte measurements above. No permanent test project/file was added by this reviewer. The fix implementer added an audit verification source and a runner that creates its project only in TEMP.

## Scoped independent rereview after the foundation fixes

Reviewed the actual current source in UserAccount, EmployeeApprovalService, EmployeeOperation, EmployeeBankService and EmployeeBankController, the implementer's `foundation-fix-result.md`, and its actual verification source/runner. This was a bounded check of the four findings; A05/A06 and Part B were not rescanned.

| Finding | Final status | Current behavior verified |
| --- | --- | --- |
| FND-01 | Resolved | Release recognizes consistent stored activation evidence. Never-activated PendingAccess/ReadyForInvitation/Invited identities return to PendingAccess, with stale current invitation/initial-access pointers cleared. Existing objects remain history. Previously activated web/mobile identities retain their activation evidence with both channels disabled. Incomplete/contradictory evidence rejects before mutation. Independent Disabled/Suspended accounts are not restored. The existing Active constraint is unchanged. |
| FND-02 | Resolved | Approve's pre-receipt `authorizeResource` callback loads the exact frozen submission/reference, binds submission/employee IDs, and requires each financial, bank and medical right implied by that snapshot before initial execution or retry. The current fixed Admin/base/self-review checks remain. Return/reject skip sensitive-payload reads and retain their prior base authority. Authorized retries retain original result IDs. |
| FND-03 | Resolved | Verify's pre-receipt callback forwards the exact target employee and actor instant to the existing effective history/reconciliation predicate. Cached Active with due nonactive history or an unapplied Active release cannot authorize verification or its replay. The established 409 code remains. Revoke/history retain approved inactive-employee availability and separate bank rights. |
| FND-04 | Resolved | Both bank JSON routes now allow 65536 bytes. DTO/service character bounds remain 1000 reason/8000 proof. Actual linked-controller metadata accepts the 54097-byte escaped Unicode fixture. Document JSON routes retain root's 16384-byte correction. |

Fresh independent executions:

- Ran `RunFoundationFixChecks.ps1` against current source with its ordinary Release settings: **PASS 68 foundation checks, 0 scenarios failed**, exit 0. It exercised actual domain/services and the actual linked bank-controller metadata, using synthetic ports. Its newly created project/output are under `C:/Users/HUMA~1.KHA/AppData/Local/Temp/cosmix-foundation-fixes-859496c4336448f293b2d86bdcb68ef4`.
- Reran the original public Domain reproduction from this review's TEMP project: exit 0; released identity is **PendingAccess**, `CK_UserAccounts_Active predicate=True`.
- Source tracing confirmed `IsEmployeeActiveForAccessAsync` still requires cached Active, unambiguous due Active history and exact reconciliation marker. The new internal helper only forwards target/time/cancellation; it does not add authority.
- Source tracing confirmed root calls `ChangeTracker.DetectChanges()` before the write guards, and the lifecycle processor still preserves independently Disabled/Suspended account restrictions and revokes roles/devices/sessions/challenges/logical face use.

No remaining actionable issue was found in these four fixes, and their changes do not widen account/channel authority or relax sensitive permissions. The implementer's full Release build result was read as supplied evidence; this reviewer did not repeat the full solution/model build because root is already running the coordinated final checks. No production edits, SQL/provider/real-account activity occurred during rereview. The deployment/SQL/host acceptance limits stated above remain.
