# Part A06 independent review — 13 October 2026

Scope: the new shared approval contracts/resolver/authority/configuration service, delegation domain extension, EF repository/guard/mappings, controller, and shared DI/permission wiring. Read-only production review against `AGENTS.md`, `docs/Approval-Routing.md`, approved `document-build/plan-content.json` A06, and SRS APR-001–005/API-003–004. No production source edits, permanent test project, SQL connection, provider/account operation, migration execution, commit, or push was performed by this reviewer.

## Open findings

1. **P2 — The configured stages cannot select distinct approval authorities.**
   - Location: `src/Cosmix.Application/Common/Approvals/ApprovalRoutingService.cs:35`; corresponding consumption at `ApprovalAuthorityService.cs:24`.
   - Every published rule must use the single `approvals.requests.review` permission. No approver role, group, account, or stage-specific permission is represented. Therefore the stage field orders work but cannot express an authority matrix such as HR → Accounts → management: the same scoped grant authorizes every stage regardless of the intended authority for that stage.
   - Actual service reproduction published a stage-1 rule and stage-2 rule, resolved the two-stage route, and called `RequireAsync` for both stages using one ordinary Review grant. Both calls succeeded and returned the same RoleAssignmentId (`SAME REVIEW GRANT CAN AUTHORIZE STAGES 1+2: True`). This is not a claim that different humans are universally required; it demonstrates that distinct required authorities cannot be configured at all.
   - Minimal fix: add an explicit immutable per-rule approver role/group binding, or a controlled set of registered stage/type review permissions. Validate the configured authority at publication, enforce it in direct and delegated paths, include its identity/version in the route fingerprint and retained decision evidence, and retain the existing fixed Admin/HR workflow exclusions. Pending management decisions can remain unseeded.

2. **P2 — Published amount thresholds can exceed the precision retained by SQL.**
   - Location: `src/Cosmix.Application/Common/Approvals/ApprovalRoutingService.cs:54`; SQL contract at `src/Cosmix.Infrastructure/Database/Configurations/Common/ApprovalsConfiguration.cs:63` (both amount properties use `HasPrecision(18, 2)`).
   - The service/domain validate nonnegative/ordered bounds but accept additional fractional places and values beyond decimal(18,2). Actual publication accepted MinimumAmount=100.005 PKR, returned 100.005, and retained 100.005 in protected result/audit evidence. The mapped SQL representation cannot retain that exact value; a later SQL read/resolution can use a changed boundary while the replay receipt still reports the original one. Oversized values can also fail at persistence instead of returning a validation error.
   - Minimal fix: reject bounds that cannot be represented exactly in decimal(18,2) before mutation (including range), or adopt an explicitly agreed canonical amount representation in both persistence and protected responses. Preserve the validated exact threshold in the evidence. Add a service case for fractional precision and supported range.
   - Evidence is actual service behavior plus EF mapping; SQL rounding/overflow was not executed or claimed as live acceptance.

## Resolved during review

**P2 — Delegation grant immutability guard ran before explicit EF change detection.** Originally `CosmixDbContext.ValidateTrackedWrites` called `ApprovalDelegationWriteGuard.Validate` before `ChangeTracker.DetectChanges`. With an attached delegation and AutoDetectChangesEnabled=false, rewriting StartsAt was accepted and became EntityState.Modified after the later detection. With automatic detection enabled the same change was rejected. The root implementer moved explicit detection before the new guards (`CosmixDbContext.cs:218–222`). A fresh real-EF-model check now rejects the rewrite with both automatic detection settings. No SQL connection or SaveChanges occurred in this probe.

## Verification and limits

- Fresh supplied resolver harness: `PASS 20 routing checks`.
- Fresh supplied actual service/authority harness: `PASS 22 actual routing service/authority checks`.
- Temporary extended actual service harness: `PASS 34 extended actual routing service/authority checks`. Extra cases exercised a live narrowed delegation and retained source RoleAssignmentId/DelegationId; delegator requester/beneficiary and delegate beneficiary exclusions; source grant removal, disabled account, removed web access, lockout and inactive employment; site exclusion; expiry and revocation. Original grant retry after the start time elapsed also passed.
- Temporary stage/precision reproduction produced both outputs described above and completed the same 34 assertions.
- Real EF guard probe demonstrated the initial bypass and the root fix, without any database operation.
- Temporary file-based harnesses and independent build artifacts are under `%TEMP%/cosmix-a06-review-7b4124c03b214de5a9f1352688279cbd`; all commands used Release and isolated `--artifacts-path` directories.

The resolver selects effective versions before filters, does not revive an older version after successor expiration, rejects ambiguous/missing/noncontiguous chains, and computes stable ordered rule identity/version fingerprints. Current web MFA/session checks reuse the existing authentication repository; delegation decision checks reread the source account, employment and underlying grants. Private result and audit payloads use server-selected purposes and failures propagate through the existing serializable transaction. The protected `{payload,result}` shape, immutable old route versions and one-time revocation preserve reconstructable prior state; no independent finding is raised solely for the absence of fields literally named `before`/`after`.

No complete D/E request lifecycle exists in A06 and none is claimed. Later consumers must materialize persisted subject/scope values, freeze the full subject/ordered route, validate retained prior-stage decisions, call the authority inside their business/authorization transaction including replay, persist authority/route/decision evidence and materialize the business effect only after the final approved stage. Real authenticated SQL concurrency, rollback/restart, private-storage failure and provider/end-to-end acceptance remain required.

## Scoped independent re-review after the A06 fixes

**Current disposition: both open findings above are resolved in the reviewed local source. No remaining finding was identified in this scoped re-review.** The initial review and its evidence are retained above as history.

Reviewed only the two corrected paths, the fix result (`a06-fix-result.md`), associated contracts/resolver/authority permission use and documentation. No broad rescan, model/migration change, production edit, SQL connection, host/provider/account operation or Git operation was performed.

- Stage authority: twenty distinct registered stage permissions replace the generic permission; publication requires the permission matching its exact stage and the publisher's explicit All-scope grant. Resolver validation refuses malformed matching authority configuration. Delegation requires a registered exact permission and a current underlying source grant; the authority service consumes the selected permission unchanged. Protected rule evidence and route fingerprints retain that exact permission. The old generic review string is unsupported.
- Amounts: nonnegative range, supported decimal(18,2) maximum and stored scale of at most two are checked before `Write`, including supplied trailing zeros. No rounding is introduced. Documentation describes this contract.

Fresh independent normal Release execution (no warning suppression), all exit 0:

1. Supplied `VerifyApprovalRouting.cs`: **PASS 24 routing checks**.
2. Supplied `VerifyApprovalServices.cs`: **PASS 59 actual routing service/authority checks**.
3. Independently extended `%TEMP%/cosmix-a06-review-7b4124c03b214de5a9f1352688279cbd/VerifyScopedFix.cs`: **PASS 52 extended actual routing service/authority checks**. This retained the earlier 34 delegated authorization assertions and added targeted fix reproductions.

The independent extension directly reran the original cases: stage-2 publication with the stage-1 permission and MinimumAmount=100.005 both return `invalid_approval_route`. It also rejects an oversized bound and a trailing-zero scale-3 bound. For each rejection, counts of route rules, receipts, audits and private objects stay unchanged. A publisher without stage-2 permission is denied before evidence. A stage-1-only review holder can authorize stage 1 but cannot authorize stage 2; a separate explicit stage-2 grant succeeds and returns its exact RoleAssignmentId. Delegation without that underlying stage-2 grant fails; a stage-1 delegation cannot authorize stage 2. A correct stage-2 delegation succeeds with its exact delegation/source grant IDs, and source grant removal denies it immediately. The exact maximum `9999999999999999.99` remains unchanged in returned/protected evidence.

Independent output included: `SCOPED FIX REPRODUCTIONS PASS: original stage/precision cases blocked before evidence; distinct direct/delegated stage grants enforced.` Build artifacts were isolated under the same temporary directory (`fix-resolver-artifacts`, `fix-service-artifacts`, `scoped-fix-artifacts`). This re-review does not claim authenticated SQL concurrency, rollback/restart, provider acceptance or completed D/E request-stage consumer integration; the deployment/consumer limits above remain.
