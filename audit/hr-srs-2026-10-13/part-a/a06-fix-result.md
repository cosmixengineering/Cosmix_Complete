# Part A06 review fixes — 13 October 2026

Both open findings in `a06-review.md` are addressed in the approved local backend. The root's explicit EF change-detection guard fix remains unchanged. This result is ready for the root's independent review.

## Changes

- `ApprovalRoutingContracts.cs` registers twenty distinct review permissions: `approvals.stages.1.review` through `approvals.stages.20.review`. `StageReview` is bounded to stages 1–20; `Review` aliases stage 1 for existing source callers. `IsReviewPermission` permits only that controlled set for delegation. The old generic `approvals.requests.review` string is unregistered and confers no authority.
- `ApprovalRoutingService.cs` requires a published permission to exactly match its stage and requires the publisher's explicit All-scope grant for that selected permission. Delegation accepts only registered stage permissions and retains the existing exact permission/scope ownership checks. Each real approver role must receive each required stage grant explicitly; no role codes, routes, amounts or authorities are seeded.
- `ApprovalRouteResolver.cs` rejects unsupported or mismatched permissions in matching rules with `approval_route_invalid_authority`, including malformed rules supplied outside publication. The selected permission was already retained in immutable rule versions, route fingerprints and protected publication evidence. `ApprovalAuthorityService` already uses the selected permission exactly in direct and delegated paths and needed no production change.
- Publication rejects negative bounds, bounds above `9999999999999999.99`, decimal scale above two (including trailing zeros), and reversed bounds before entering `Write`. Invalid amounts therefore stage no rule, receipt, audit or private evidence. The value is never rounded. This matches the existing SQL `decimal(18,2)` contract without mapping or migration changes.
- `docs/Approval-Routing.md` documents stage grants, delegation matching, the unsupported old string and the exact amount contract. The root standalone resolver/service harnesses contain the regression checks; no permanent test project was created.

## Red/green evidence

Before production edits, the actual service harness reproduced same-permission stage publication and precision/range acceptance with retained records/private evidence. The expanded run reported **21 failing regression checks** (including the unregistered stage-permission configuration/publication paths). The resolver regression failed because a stage-1 rule using the stage-2 permission was accepted instead of returning `approval_route_invalid_authority`.

Final normal Release runs, with no warning suppression, passed:

- `VerifyApprovalRouting.cs`: **24 routing checks**.
- `VerifyApprovalServices.cs`: **59 actual service/authority checks**. Cases include separate stage-two publication authority, stage-one-only direct denial at stage two, an explicit separate stage-two direct grant with its retained assignment ID, wrong-stage delegation denial, correct stage-two delegation with retained source grant/delegation IDs, exact supported amount bounds, both bounds' scale/range/negative rejection and no records/files after each rejection.
- Reviewer's `%TEMP%/cosmix-a06-review-7b4124c03b214de5a9f1352688279cbd/VerifyDelegatedAuthority.cs`: **34 extended actual service/authority checks**, retaining requester/beneficiary exclusions, current underlying source grants/account/employment, narrowed scope, expiry and revocation checks.
- `dotnet build Cosmix.Backend.sln -c Release --artifacts-path %TEMP%/cosmix-a06-fix-solution-artifacts`: **succeeded, 0 warnings, 0 errors**.

Fresh standalone outputs used `%TEMP%/cosmix-a06-fix-service-final-artifacts`, `cosmix-a06-fix-resolver-final-artifacts` and `cosmix-a06-fix-extended-final-artifacts`. Commands used `dotnet run --file <path> -c Release --artifacts-path <temporary-path>`.

Earlier verification encountered concurrent unfinished face-retirement/capture-policy compilation warnings. A temporary `NoWarn=CS9113` switch allowed the initial red/green probes to execute; the final normal runs and complete solution build above require no suppression. An intermediate delegated regression revealed that the original root harness returned source grants for every account. Its fixture now keys grants by account, matching the independent review harness and ensuring the delegate has no unintended direct grants. No production authority change was needed for that fixture issue.

## Permission configuration and limits

The actual existing private `RequireRoleConfiguration` guard was invoked for stage 1, 2 and 20 using a synthetic fixed administrator with only the All-scope `access.roles.permissions.manage` grant. All passed after registration without personal ownership of those review permissions. This verifies the same guard used by role permission grants/assignment and avoids a new business-permission ownership bootstrap dead end. Existing system/assigned-role immutability and self-assignment restrictions remain; no initial owner receives approval permissions automatically.

No SQL connection, host/provider/account operation, database write, mapping/migration edit, Git operation or permanent test project was performed. The tests use actual approval services with synthetic repositories and private storage; they are not authenticated SQL concurrency, rollback/restart, real-storage failure or complete D/E lifecycle acceptance. Those deployment boundaries remain as recorded in the independent review.
