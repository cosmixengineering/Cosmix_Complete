# Part A migration independent review

Reviewed `20261013121357_CompleteHrFoundationPartA` against retained Office22, the generated idempotent upgrade SQL, current lifecycle reconciliation consumers and downgrade guard. This was source-only review; no SQL connection, database modification, model/source edit or provider action occurred.

## Findings sent to root

1. **P1 — Wrap backfill in dynamic SQL before freezing the deployment script.** Upgrade SQL lines 247–253 statically reference newly added `EffectiveOn` and `LastAppliedStatusChangeId` columns, while the generated script contains only one final `GO`. SQL Server can compile those references before preceding `ALTER TABLE ADD` statements have executed. Wrap the entire custom backfill `SqlOperation` in `EXEC(N'...')`, escape literal quotes and regenerate the reviewed script. EF already uses this approach for its new-column constraints. [Official EF migration guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing) documents this parser issue in idempotent scripts. No live SQL reproduction was attempted.

2. **P2 — Legacy inactive marker does not itself prove the added revocations happened.** Backfill lines 248–273 assign the marker from matching cached inactive status plus approval evidence. Pre-Part-A `EmployeeLifecycleProcessor.ApplyAsync` revoked sessions/challenges but did not revoke devices, role assignments or active face enrollments. Current `DueEmployeesAsync` and `UnappliedChangesAsync` skip a marked latest row, so a previously suspended/ended employee can retain those old active records indefinitely. This remains an upgrade reconciliation gap even though inactive authentication is denied and a later approved suspension release revokes them. Require explicit legacy cleanup verification/reconciliation before claiming offboarding migration complete; do not treat the cached-status match as proof of all new reconciliation side effects.

## Other checks

- Forward changes add exactly the four HR history tables; source does not recreate the Office22 tables or drop existing tables/columns.
- Certification initial `Revision` and `Status` use `1`. Derived dates initialize before the new check constraints, preserving original issue/return dates.
- Legacy marker intentionally excludes Active releases; approval actor/time checks and same-employee marker FK are present.
- Original unique asset-reference index filtered by `ReturnedAt IS NULL` is retained in Office22/current target model.
- New history relationships use restrictive deletes and employee/document composite keys where ownership must be preserved.
- The downgrade guard is the first operation and refuses ordinary retained Part A histories before destructive operations.

## Resolution review

**P1 resolved.** The current migration wraps the complete custom projection text in `EXEC(N'...')` using quote doubling. The regenerated upgrade SQL was independently inspected: both newly added date projections and the marker update are inside that dynamic batch, and the request-type literal is correctly doubled. Compilation of those column references is deferred until the preceding `ALTER TABLE ADD` operations execute.

**P2 resolved by bounded migration safety.** Both current migration source and regenerated SQL now mark only an independently approved inactive row whose offboarding projections are already clean. All other markers remain null, preserving original evidence and requiring explicit operator review. The root-owned upgrade runbook must state that these unresolved legacy employees are not automatically reconciled; an operator must not set a marker merely to bypass the review gate or authorize suspension release from unresolved history. No claim of live legacy cleanup or rollout acceptance is made.

Confirmed Office22 schema/columns for that predicate:

| Source | Clean condition |
| --- | --- |
| `attendance.FaceEnrollments` | No `EmployeeId = employee.Id AND IsActive = 1` row |
| `auth.UserAccounts` | Each linked `EmployeeId` account has `Status IN (4,5)`, `MobileAccessEnabled = 0`, `WebAccessEnabled = 0` |
| `auth.UserRoleAssignments` | No linked `UserAccountId` row with `RevokedAt IS NULL`, including scheduled/expired assignments |
| `auth.RegisteredDevices` | No linked `UserAccountId` row with `RevokedAt IS NULL` |
| `auth.UserSessions` | No linked `UserAccountId` row with `RevokedAt IS NULL`, including expired sessions |
| `auth.MobileOtpChallenges` | No linked `UserAccountId` row with `ConsumedAt IS NULL AND RevokedAt IS NULL`, including expired challenges |

`AccountStatus.Suspended = 4`, `Disabled = 5`; the employee status enum uses different values. `GetOpenChallengesAsync` refers specifically to `MobileOtpChallenges`, not `MfaChallenges` (which has no `RevokedAt` column). A no-account employee also requires no active face enrollment. Equality-correlated `NOT EXISTS` queries can use existing EmployeeId/UserAccountId-leading keys; no new index/model change is necessary for this bounded upgrade check.

The current SQL also joins `hr.EmployeeSubmissions` and verifies its employee, version and provenance against the employee's submitted version and approved onboarding decision, matching the existing runtime employment-proof requirements.

Status: **both source/script findings resolved; no remaining material issue found in this scoped migration review.** Root retains ownership of the upgrade runbook, model/script freeze and eventual real-target acceptance. The runbook was not yet present when this scoped rereview read it; its required unresolved-legacy gate was explicitly confirmed by root.

Reviewed SHA256 values:

- Migration source: `377D2ED05117EF50B2A6F37F2E0FAFF64E15B00E42FDDAA0066D5689AAD04AA9`
- Regenerated upgrade SQL: `787DB2C00217F6F9026CAA3AC56D9E5053CE71AA8925AE79304308133F8A1EF8`
