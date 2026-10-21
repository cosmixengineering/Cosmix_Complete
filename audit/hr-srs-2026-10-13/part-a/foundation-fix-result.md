# Part A targeted foundation fixes — 13 October 2026

Implemented the four verified findings from `foundation-review.md` in the latest dirty backend checkout. No schema, migration, DI, permission catalog, attendance, A05/A06, SQL database, provider, account, commit or push was changed by this work.

## Changes

- `cosmix-backend/src/Cosmix.Domain/Authentication/UserAccount.cs`: lifecycle release distinguishes legitimate stored activation proof from a never-activated account. PendingAccess, ReadyForInvitation and Invited accounts without activation proof return to PendingAccess and clear current initial-access/invitation pointers. Revoked objects remain history. Valid prior web/mobile activation stays Active with both channels disabled and its credential/activation evidence retained. Incomplete or contradictory evidence fails before mutation and requires manual review; independent blocks cannot be restored.
- `cosmix-backend/src/Cosmix.Application/HR/Employees/EmployeeApprovalService.cs`: Approve loads the exact frozen submission in `authorizeResource` and requires current financial read/manage, independent bank read/request, and medical read/manage permissions as implied by that submission before initial execution or receipt replay. Exact submission/employee identity is checked. Return/reject and existing fixed Admin, base permission and initial self-review guards remain.
- `cosmix-backend/src/Cosmix.Application/HR/Employees/EmployeeOperation.cs`: added the internal `IsActiveEmploymentAsync` forwarding helper only, preserving root's compact receipt changes.
- `cosmix-backend/src/Cosmix.Application/HR/Employees/EmployeeBankService.cs`: Verify uses the authoritative effective employment/history/reconciliation predicate in the pre-receipt callback. It keeps the existing `employee_bank_verification_requires_active_employment` 409 response. Revoke/history remain available for approved inactive employees.
- `cosmix-backend/src/Cosmix.Api/Controllers/HR/EmployeeBankController.cs`: the two Verify/Revoke bounded JSON routes accept 65536 bytes, accommodating 54097-byte escaped Unicode JSON at the existing 1000/8000 character bounds. Other route limits and DTO/service validation were unchanged.

Verification sources added outside the backend project: `VerifyFoundationFixes.cs` and `RunFoundationFixChecks.ps1` in this audit directory. The runner creates its project/output under TEMP, never in the repository.

## Fresh verification

- Original reviewer public Domain reproduction rerun before edits: exit 1, released dormant identity became Active and the unchanged `CK_UserAccounts_Active` predicate was false. After the fix, the same reproduction exited 0 with PendingAccess and predicate true.
- The original reviewer's real Application receipt reproduction established the sensitive permission bypass. The broader new fixture was run against the pre-fix source: 28 scenarios failed, including all three dormant states, contradictory proof, missing sensitive permissions on replay, bank effective employment, exact frozen ID guarding and escaped Unicode body compatibility. Initial permission checks in an already-approved synthetic fixture previously reached the current-submission conflict before the new pre-receipt callback; these now fail authorization first.
- The final new fixture passed **68 checks, 0 failed scenarios**, exit 0, with ordinary warning-as-error settings. It exercises actual Domain/EmployeeApprovalService/EmployeeOperation/EmployeeBankService code and the actual linked bank controller metadata. Cases include valid prior web/mobile activation; six sensitive rights independently removed for initial requests and replay; authorized/profile-only/return/reject replay; exact frozen submission IDs; cached Active with denied effective history, active verification, inactive revoke replay; and both Unicode request limits.
- Fresh `dotnet build Cosmix.Backend.sln -c Release --no-restore` succeeded with **0 warnings, 0 errors**.
- Targeted `git diff --check` exited 0.
- The saved `RunFoundationFixChecks.ps1` runner was itself executed freshly and passed the same 68 checks, exit 0, creating `%TEMP%/cosmix-foundation-fixes-b1929a25762f41e78e282f7bf1cc511e` only.

The first TEMP Domain rebuild accidentally included the reviewer's nested receipt harness/generated files; narrowing its Compile item to Program.cs resolved that harness configuration issue. During the initial broader harness setup, concurrent unrelated Attendance compiler diagnostics temporarily prevented an ordinary Application build. A diagnostic run relaxed warning-as-error handling; the final fixture and full Release build above used ordinary settings and passed after the shared source stabilized. No Attendance code was edited here.

These checks use synthetic authorization/repository/private-file ports. They verify domain rules, service ordering, exact target/time forwarding and body-limit compatibility, not real SQL commits/races, authentication providers, a running HTTP host, SMS/scanner deployment or physical biometric cleanup. The real repository's effective history/reconciliation implementation belongs to root's coordinated lifecycle integration evidence.

## Reproduce

Run `./audit/hr-srs-2026-10-13/part-a/RunFoundationFixChecks.ps1` from the parent workspace. Existing development harness: `%TEMP%/cosmix-foundation-fixes-20261013/Checks.csproj`, isolated `Artifacts`. Original reviewer Domain harness: path recorded in `%TEMP%/cosmix-foundation-review-path.txt`.
