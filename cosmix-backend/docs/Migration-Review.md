# HR and Attendance migration review — 6 October 2026

## Employee Lifecycle Addendum - 7 October 2026

Reviewed range: `20261007151945_CompleteEmployeeHiringApproval` through
`20261007164427_LinkEmployeeStatusApprovals`. Up adds nullable `EmployeeStatusChanges.ApprovalRequestId`,
a filtered unique index and a restrictive FK to the existing workflow approval table. No table additions,
data backfills, history resets or cascading deletes. Down refuses to discard any recorded approval link.
Forward script: `docs/sql/20261007164427_LinkEmployeeStatusApprovals.sql`.

Applied to the same original LocalDB `Cosmix_Development` after COPY_ONLY/CHECKSUM and RESTORE VERIFYONLY.
Backup: `C:\Users\Huma.Khan\Cosmix_Development_before_employee_lifecycle_20261007_be9a4053d8614c0fa3cadb0e4f9aa808.bak`.
Verified eleven migration entries, model/snapshot consistency, nullable reference, unique index, trusted
restrictive FKs, unchanged 125 tables including migration history, and zero accounts. No previous migration
was changed. Behavioral SQL verification is recorded separately in Employee-Hiring-Verification.md.

## Employee Approval Addendum - 7 October 2026

Reviewed range: `20261007141249_AddEmployeeHiringDraftsAndDocumentPrivacy` through
`20261007151945_CompleteEmployeeHiringApproval`. Up only makes EmployeeAssignments.BranchId nullable
for Site Staff without an office. There are no new tables, data backfills, drops or cascading deletes.
Down first refuses null-office assignments; it cannot fabricate a zero-Guid office to force a downgrade.
The generated forward script is `docs/sql/20261007151945_CompleteEmployeeHiringApproval.sql`.

Applied to the original LocalDB `Cosmix_Development` after COPY_ONLY/CHECKSUM and RESTORE VERIFYONLY:
`C:\Users\Huma.Khan\Cosmix_Development_before_hiring_approval_20261007_a00b670f728c4883ad7cdcc68247894c.bak`.
Verified ten migration-history entries, nullable office, trusted restrictive FKs, unchanged 125 tables
including history, and zero accounts. Earlier applied migrations remain unchanged. Model/snapshot validation passed.

## Employee Hiring Addendum - 7 October 2026

The same verified `Cosmix_Development` database now has nine migrations through
`20261007141249_AddEmployeeHiringDraftsAndDocumentPrivacy`, with 124 business tables plus EF history.
The reviewed range begins at `20261007122140_AddOrganizationMastersAndHolidayRevisions`.
One nullable hiring-proposal table, restrictive foreign keys, rowversion, and document privacy metadata are added.
The upgrade refuses pre-existing employee documents until an approved privacy-classification conversion exists.
No older migration was edited. Forward operations do not drop existing data; Down drops the new table/column
and is not an approved production recovery procedure.

A COPY_ONLY/CHECKSUM backup passed RESTORE VERIFYONLY before applying the sole pending migration:
`C:\Users\Huma.Khan\Cosmix_Development_before_employee_hiring_20261007_8222ed1acf9d44178f693ae64374cc1d.bak`.
All eight new foreign keys and both check constraints are trusted/enabled; rowversion, no pending migrations,
model/snapshot consistency and no cascading deletes were checked. No owner or account was created.
See Employee-Hiring-Verification.md for actual workflow checks and provider limitations.
The older sections below retain their historical verification boundaries.

## Organization Masters Addendum - 7 October 2026

The original development database is now verified through the eighth migration,
`20261007122140_AddOrganizationMastersAndHolidayRevisions`: 123 business tables plus EF history.
The starting migration was `20261007090713_AddAccountRecoveryAndOwnerInitialization`.
A new COPY_ONLY/CHECKSUM backup passed RESTORE VERIFYONLY before applying the single pending migration.
No database was created, attached, reset or renamed. The seven earlier migrations are unchanged.

The new migration adds append-only organization change snapshots linked to existing AuditEvents,
and revision/active/predecessor metadata on HolidayCalendar. It replaces one unique index with the
code/start/revision identity and unique successor indexes; every FK remains restrictive. Existing
calendars default to active revision 1 without changing their holiday dates. Up has no table/column
drops, user seeds or automatic access grants. Down rejects populated organization history/revisions.
See the [incremental script](sql/20261007122140_AddOrganizationMastersAndHolidayRevisions.sql),
[module guide](Organization-Masters.md) and [verification record](Organization-Masters-Verification.md).
Earlier "latest" states below remain dated historical handovers.

The subsequent Site master API continuation reuses existing Site/City/Project/Employee and audit
tables. It adds no migration or seed data. SQL verification confirmed the same eight applied migrations,
no pending model changes and unchanged row counts after bounded synthetic verification cleanup.

## Shared Authentication Addendum

**Latest applied state, 7 October 2026:** the same original `Cosmix_Development` database is now
verified through `20261007090713_AddAccountRecoveryAndOwnerInitialization`: seven migrations,
122 business tables. Both the sixth web migration and seventh recovery migration were applied
after separate COPY_ONLY CHECKSUM backups and successful RESTORE VERIFYONLY. No database was
created, attached, reset or renamed. Earlier "prepared/unapplied" paragraphs below are historical.

The seventh [incremental SQL](sql/20261007090713_AddAccountRecoveryAndOwnerInitialization.sql)
starts at `20261007080926_AddWebSmsAuthenticationAndProtectedRoles`. Up adds one phone-recovery
history table, nullable recovery provenance on phone bindings, two additional OTP purposes and
explicit external-owner provenance on employee submissions/decisions. Normal HR still requires
nonempty actor IDs through domain and exhaustive CHECK constraints. Existing rows default to HR
provenance, never owner authorization. The submission/decision FK includes matching provenance;
all new FKs are restrictive and same-account where relevant. There are no business data seeds,
forward table/column drops or access grants. Three actor columns become nullable solely for
truthfully recorded external-owner authorization; constraints require actor IDs for normal HR.

Down is guarded: owner or recovery evidence causes an explicit refusal before any operation.
That evidence cannot be represented by the old schema. Use reviewed forward repair or a verified
backup recovery plan, never remove the guard to force a downgrade. No earlier migration was edited.

Actual SQL verification: 4,241 read-only schema assertions passed. A separate rollback-only test
executed the real first-owner service/repository graph, normal-HR target graph, replacement phone,
OTP completion, password reset and web-SMS session persistence. All 122 business tables remained
empty after the forced rollback; sequence/rowversion counters may advance. Private snapshot storage
was simulated in that SQL test and is checked separately; no SMS was sent and no account was committed.

**Latest prepared change, 7 October:** `20261007080926_AddWebSmsAuthenticationAndProtectedRoles`
follows `20261006144249_AddSharedMobileAuthentication`. It has not been applied and this web
implementation made no database changes. The generated [incremental SQL](sql/20261007080926_AddWebSmsAuthenticationAndProtectedRoles.sql)
adds `Roles.SystemRole`, nullable `UserSessions.SmsOtpChallengeId`, a same-account restrictive
proof FK/alternate key, three indexes (two filtered unique), and purpose/channel CHECK changes.
It allows an approved phone on a web-only employee without enabling mobile. It creates no tables,
seeds, role promotions, verified-phone evidence, forward column drops or data deletion.
All five previous migrations and designers are preserved. Initial Admin credential provisioning
uses existing columns and adds no schema. Down removes the new evidence and may reject existing
WebLogin/web-only-phone rows when restoring old checks; do not treat it as a safe operational rollback.
Use a reviewed roll-forward or verified backup recovery plan. The applied status below remains
the last verified database state, not confirmation that this sixth migration has run.

**7 October 2026 follow-up:** The existing SSMS development database `Cosmix_Development`
was backed up and upgraded by applying only `20261006144249_AddSharedMobileAuthentication`.
The five-entry history and 121-table schema are verified. The original migrations remain
unchanged; no initial migration, reset, new database or attachment was used. See the
[live verification record](Shared-Authentication-Verification.md) for the resolved LocalDB
instance mismatch, actual SQL checks and remaining limits. The paragraphs below describe
the preceding offline handover, not the latest database application status.

The 6 October shared-authentication implementation prepares the new, unapplied migration
`20261006144249_AddSharedMobileAuthentication`, starting at
`20261006105213_CorrectReviewedModelLifecycles`. The four earlier migrations remain unchanged.
See [Shared Authentication Verification](Shared-Authentication-Verification.md) for the reviewed
operations, SQL-script location, offline evidence, destructive rollback warning and application gates.
This authentication task did not connect to or modify a database. The review below is retained
as the historical corrective-model handover, not the current authentication feature status.

## Corrective review of commit `30aada1` — historical result

Checkout: `C:\Users\Affan.Ahmed\Desktop\cosmix-backend`, branch **develop**, HEAD
`30aada1`. There were no newer local commits or tracked edits at the start of this
correction; unrelated `UpgradeLog.htm` is preserved. The architecture, complete SRS
and existing coverage/review were used; the approved SRS hash is unchanged.
No database, commit or push operation was performed during the correction review.
The user subsequently authorized a reviewed commit and push to `develop`; the
database remains outside that authorization.

All five requested issues are **fixed**. The user explicitly confirmed in this chat
that Returned allowance claims mean correction and resubmission.

Decision source: in reply to the question about whether Returned means correction
and resubmission on the same claim/eligibility cycle, with previous decisions
preserved and duplicate settlement prevented, the user answered:
**"Haan — correction aur resubmission"**. That chat reply authorized the implemented
behavior. No separate PM approval document or ticket was supplied or independently
verified; this report records the user's confirmation, not a separate PM sign-off.

| Issue | Reproduction and fix | Changed production files |
|---|---|---|
| 1. Stale employee-document scans | Before: Rejected 08:10 then Clean 08:05 replaced the rejection and allowed verification. After: stale results do not mutate evidence; exact duplicates are idempotent; same-time disagreements persist a fail-closed conflict flag. Verification is bound to the exact scan timestamp; newer rescans are allowed but require new verification. | `HR/EmployeeDocuments/EmployeeDocument.cs`; `Configurations/HR/EmployeeDocumentConfiguration.cs` |
| 2. Scheduled membership termination | Before: both December 31 memberships rejected October 6 EndOn. After: original dates remain; one audited termination records effective date, reason, actor, timestamp and trusted business date. Backdating, extensions, completed periods, rewriting and deletion are rejected. | `Authentication/Scopes/TeamMember.cs`, `ProjectMember.cs`; `Configurations/Authentication/ScopeConfigurations.cs`; context guards |
| 3. Invitation/access recovery | Before: an Invited account with a revoked initial assignment could neither configure replacement access nor activate. After: `ReplaceInitialAccess` validates replacement access, revokes the current invitation and returns to ReadyForInvitation; old tokens remain unusable. Approval/account/scope bindings, expiry and reuse checks remain. | `Authentication/UserAccount.cs`; context transaction guard |
| 4. Group shift withdrawal | Before: append-only assignment had no supported end record. After: `ShiftPopulationAssignmentEnd` preserves original assignment, exclusive end, reason, actor/time and approval reference; unique FK permits one withdrawal. | New `Attendance/Rosters/ShiftPopulationAssignmentEnd.cs` and matching Attendance EF configuration; context DbSet |
| 5. Returned allowances | Before: the second Submit threw and the cycle index blocked a replacement claim. After: same-claim `ResubmitReturned` retains every immutable submission and approval, with correction reason/actor and corrected values. Cycle uniqueness remains. Settlement binds the exact approved revision and permits only one handoff and one payment record per claim. | `HR/Allowances/AllowanceClaim.cs`, new `AllowanceClaimSubmission.cs`, `AllowanceSettlementRecord.cs`, their three HR mappings and context guards |

Domain paths above are relative to `src/Cosmix.Domain`; configuration paths are
relative to `src/Cosmix.Infrastructure/Database`. Shared guards live in
`src/Cosmix.Infrastructure/Database/CosmixDbContext.cs`.

Independent review caught an approval-persistence gap and snapshot-reference reuse
across older allowance revisions. Both were fixed before scaffolding: tracked
approvals and newly changed immutable decisions must participate in the save, and
a binary-collated per-claim snapshot-reference unique index covers unloaded history.

The additional user request adds **Swagger UI at `/swagger` in Development only**.
`Cosmix.Api.csproj`, `packages.lock.json`, `Program.cs` and `launchSettings.json`
changed. The existing OpenAPI generator, `/openapi/v1.json`, bearer descriptions
and protected API authorization remain. UI assets are local to the pinned
Swashbuckle.AspNetCore.SwaggerUI 10.2.3 package; no login or business API was added.
This follows [Microsoft's Swagger UI integration guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/using-openapi-documents?view=aspnetcore-10.0).

### Corrective migration and SQL

New migration: [20261006105213_CorrectReviewedModelLifecycles.cs](../src/Cosmix.Infrastructure/Database/Migrations/20261006105213_CorrectReviewedModelLifecycles.cs),
with its generated Designer and EF-generated snapshot in the same folder.
All **six shared migration/Designer files**, including
`CompleteReviewedHrAttendanceDataModel`, retained their original hashes during
the correction review and are excluded from the corrective commit.
The earlier SQL scripts remain unchanged.

| Artifact | Starting migration | Ending migration |
|---|---|---|
| [Corrective incremental SQL](sql/20261006105213_CorrectReviewedModelLifecycles.incremental.sql) | `20261006074233_CompleteReviewedHrAttendanceDataModel` | `20261006105213_CorrectReviewedModelLifecycles` |
| [Updated full chain](sql/20261006105213_CorrectReviewedModelLifecycles.from-zero.sql) | `0` | `20261006105213_CorrectReviewedModelLifecycles` |

Up contains **two new tables, sixteen added columns, seven additional checks on
existing tables, seven indexes and one standalone FK**, plus the new-table keys,
checks and five FKs. There are 33 generated operations plus one preflight SQL operation.
No forward drop, column narrowing, cascade or business-data rewrite is generated.
The model now has **117 tables / 268 restrictive FKs**. Down has 28 operations and
removes only these added tables/columns/constraints; it loses new evidence and must
not be used as a recovery plan for populated data.

Preflight `THROW 51002` with `XACT_ABORT ON` stops if the previous schema contains
submitted allowance claims or settlement records. Their original history needs an
explicit conversion, not invented versions/approvals/payment metadata. Unsubmitted
draft claims are compatible. No database was queried to determine which records exist.

Existing employee-document verification actor/time/result is retained, but the new
`VerifiedScanAt` is deliberately null: existing documents require fresh manual
verification before availability. Existing membership schedules are unchanged and
new termination fields remain null. These behavioral consequences must be understood
before applying; the migration does not manufacture past scan/termination evidence.

### Corrective verification and boundaries

| Check | Result |
|---|---|
| Release solution build | Passed; zero warnings/errors. |
| Existing integrated model/guard checks | 965 passed; 117 entities, 268 restrictive FKs, no shadow properties; SQL constructed in memory. |
| Earlier focused regressions | 8 + 61 + 25 + 88 passed. |
| New document / membership / invitation checks | 68 / 87 / 61 passed; each original failure reproduced before its fix. |
| Shared SaveChanges guards | 107 passed; ten valid saves suppressed, zero DB connection attempts; sync/async paths checked. |
| Allowance and roster checks | Both original failing scenarios now pass; 65 additional checks cover invalid transitions, unchanged historical values, settlement duplicates, mappings and five safely suppressed saves. |
| Corrective migration metadata/scripts | 4,318 checks passed, zero connection attempts; exact EF SQL output, Up/Down scope, old migration hashes, FK types/collations, names and widths. Widest key/index: 632/1,016 bytes. |
| Independent migration/SQL review | No material blocker; reviewed 325 checks and 514 key/index definitions. Down dependency simulation restores 115 tables and 262 FKs. New full script equals the previous full chain plus the corrective incremental script. |
| EF pending-model check | No changes since the corrective migration. |
| Swagger HTTP checks | Before: 6 pass / 1 expected failure. After: 22 pass across Development/Production; local assets, bearer metadata and API authorization checked. Hosts stopped after verification. |

Checks are ignored file-based programs under `artifacts/fix-review`; no test project
was added. Existing dependency cache and local NuGet feed were used. Swagger's
public network restore was unavailable, so its existing package in the user's normal
NuGet cache was restored offline and the package lock updated. HTTP-only host checks
did not verify HTTPS certificates or DataProtection key storage; sandbox warnings
for those facilities were observed. No readiness/database endpoint was invoked.

Remaining implementation boundaries:

- Services must authorize actors and load authoritative tracked records. EF guards
  do not protect direct SQL or a caller fabricating Unchanged entity state.
- Persist scan conflicts, use all `IsAvailable` predicates in authorized download
  queries, and reload/re-evaluate after rowversion conflicts. General `StoredFile`
  scanning was outside this employee-document fix.
- Membership access queries must use both scheduled and terminated dates; supply
  the business date from the server. Roster queries must incorporate withdrawal
  records. No historical attendance is automatically recalculated.
- Allowance corrections cover amount, policy/calculation/evidence and payment dates;
  original eligibility/currency/deployment/session identity remains fixed. Store
  complete immutable private snapshots; uniqueness alone cannot guarantee their
  contents. Commit approval decisions, submissions and settlement metadata atomically.
  External payment execution/idempotency remains future work.
- Section 19 policy values/providers still need management configuration; no
  schema-blocking business decision remains for these five fixes.

Before application, confirm the actual development target, its migration history,
allowance data/conversion requirements and document re-verification plan. Select
one script for its exact starting state; use a runner that supports `GO` and stops
on errors. Validate in a separately authorized disposable database, with a backup
for populated targets. **SQL Server execution, real concurrent transactions and
end-to-end workflow/security correctness have not been verified.**

### Publication review — 6 October 2026

The user authorized a commit and push of the completed fixes to `develop`.
A fresh solution Release build passed with zero warnings and zero errors; EF again
reported no pending model changes. An independent final diff review found no
material blocker. The selected 31 source/documentation/migration files contained
no credential-pattern findings or binaries. This is a targeted review, not proof
that all possible secrets or security defects are absent.
The five focused offline programs were rerun: document 68, membership 87,
invitation 61, SaveChanges guards 107 and allowance/roster 65 checks passed
(388 total). Database connection attempts remained zero.

Subsequent local formatting edits to `ICurrentUser.cs`, `IUnitOfWork.cs`,
`20261005075444_InitialFoundation.cs` and
`20261005113807_AddInitialHrAttendanceModels.cs` are unrelated and excluded from
the commit, along with the existing `UpgradeLog.htm`. They are left on disk.
Ignored offline checks, build outputs, caches and local configuration are not
part of the commit. The old shared migration content remains the original
committed version; the excluded local namespace-formatting changes do not alter
their migration operations.

No schema-blocking business decision remains for this fix list. Section 19 policy
values/providers and the application prerequisites above remain unresolved future
work. No migration was applied and no database was accessed during publication.

The automated publication attempt stopped at staging: Git could not create
`.git/index.lock` because this session has read-only access to `.git`.
The index remained empty and HEAD remained `30aada1`; no commit or push occurred.
An ignored local PowerShell handover script under
`artifacts/fix-review/commit-reviewed-fixes.ps1` stages only the reviewed allowlist,
checks the file hashes/branch/remote, and commits/pushes when run by the user in
their own terminal. It contains no database commands.

The following sections retain the earlier review at `30aada1` for historical context.

## Status and checkout

**Reviewed incremental migration and two SQL scripts prepared. Not applied to a database.**

Reviewed checkout: `C:\Users\Affan.Ahmed\Desktop\cosmix-backend`, branch `develop`,
starting commit `8c0d83d` (`Add HR attendance models, EF mappings and solution updates`).
The existing models, EF configurations and `Data-Model-Coverage.md` were present.
Only the unrelated Visual Studio `UpgradeLog.htm` was untracked when review began;
it has been preserved.

Sources read: `AGENTS.md`, `README.md`, `Architecture.md`, `SRS-Module-Map.md`,
`Data-Model-Coverage.md`, and the complete SRS DOCX including every table, workflow,
acceptance criterion and section 19 decision. The SRS SHA-256 is
`0d7216815c24a476d82f12c46cdd97d953a8a50217ecdf10d7cf52332c403e71`.
The confirmed HR → Admin review → access assignment → secure invitation flow was
also used. Coverage claims were compared with source entities and mappings.

## Findings addressed

| Finding | Fix and limit |
|---|---|
| Repayment reversals could omit the original entry. | Domain validation and a SQL check require `ReversesEntryId` for a reversal. Existing composite FK binds the original to the same agreement/currency. Full-versus-partial reversal policy is not invented. |
| An approved temporary shift change could gain employees in a later save. | `CosmixDbContext` requires affected-employee rows to accompany a new parent change, matching the existing immutable manifest guards. A changed population needs new approval evidence. |
| Open-ended policy assignments could not be withdrawn without rewriting history. | New append-only `PolicyAssignmentEnd` records the exclusive end, reason, approval reference and recording actor/time. One end record per assignment; original evidence stays unchanged. Services must validate authority and resolve the effective end. |
| Bank verification, SIM return/status and commute-distance histories had no explicit replacement identity. | Added revision/predecessor metadata, root/successor checks, same-employee restrictive predecessor FKs and filtered unique predecessor indexes. Independent root records remain allowed; no one-bank/SIM-per-employee rule was introduced. Services must validate predecessor revision order, effective-period selection and resource identity. |
| Adding employee `DraftRevision` would backfill zero, violating its positive-value check. | EF default is now `1`, matching the domain's initial revision. This does not reconstruct approval history for existing non-Draft employees. |
| SRS section 15 insurance expiry/reminders had no complete source model. | The user confirmed both employee and vehicle coverage. New append-only `InsurancePolicy` has exactly one owner, provider/policy reference, inclusive coverage dates, private file reference and predecessor history. `InsuranceExpiryReminder` stores an explicit recipient/channel/due time, cancellation evidence and optional queued Notification reference with rowversion. No automatic delivery is implemented. |
| Reusing the expanded migration against old processed records could fabricate or invalidate evidence. | The new migration starts with a transactional preflight: non-Draft employees or scanned/verified documents require a separately reviewed conversion. `SET XACT_ABORT ON` and `THROW` abort this migration's transaction. Compatible drafts keep their existing fields, with initial draft revision 1; there is no business-data backfill or seed. |

## Clarifications and remaining decisions

Both migration blockers were resolved by the user on 6 October 2026:

- Insurance expiry and reminders are required for **employees and vehicles**.
- No database work/data exists yet. This remains a Code First project; an empty
  future database can use the full migration chain. No database was queried to
  verify that statement, and the existing history has been retained.

No schema-blocking decision remains within this review's approved scope. Reminder
lead times, recipients, channels, renewal scheduling and delivery providers remain
configurable application decisions. The model accepts explicit values and does not
invent a fixed schedule or recipient. Each covered employee/vehicle has its own
policy record; policy numbers are not globally unique, allowing shared group references.

Section 19 rates, thresholds, approval matrices and providers remain open business
configuration/implementation decisions. Existing versioned rules and evidence
references allow their values to remain unseeded; their absence alone does not
require guessed schema. The onboarding chain is resolved by the user's confirmed
flow. The SRS's ambiguous file-import row remains excluded under the established
mobile-only scope. Business status/value lists and glossary should receive PM
sign-off before application; this review does not claim such sign-off occurred.

## Review observations and boundaries

- Incomplete drafts deliberately allow null profile fields. Full submission and
  activation validation belongs to the future orchestration; blanket NOT NULL
  changes would prevent the required draft workflow.
- Employee submissions and decisions preserve private versioned snapshots,
  reviewer evidence and reasons. Employee approval and account activation are
  separate. Dynamic roles, permissions and employee/team/department/site/project
  scopes are represented; authorization is not established by merely storing IDs.
- Punches remain append-only and permit several events per employee/day. Face
  verification is Check In only and bound to employee/device/request. Sessions,
  corrections, summary revisions, exact period-lock events and payroll sources
  preserve history. Zone events remain separate evidence.
- Bank, medical, face and other sensitive records use separate/protected references;
  fine-grained access, cryptography, storage and audit capture are still services
  to implement. Actor subjects may refer to a trusted bootstrap identity and are
  not universally foreign-keyed to employee accounts.
- Monetary/quantity/coordinate precision, bounded strings, nullable relationships,
  unique indexes, temporal types, mutable-row concurrency and restrictive deletion
  are reviewed in the EF model. UTC instants, business dates and local shift times
  are distinct. Cross-record effective intervals and calculation selection remain
  transactional application responsibilities.
- Optional further relational hardening remains: invitation approval/account
  correspondence, Self-scope employee correspondence and allowance settlement /
  payroll-line correspondence. Existing constructors/services must enforce those
  relationships; these were not presented as implemented endpoint security.
- Insurance predecessor ownership and chronology, and reminder/notification
  recipient/channel correspondence, are checked by domain methods. Their simple
  FKs do not enforce these cross-row rules independently. Future services must
  authorize recipients/private files, resolve timezone scheduling and suppress or
  cancel reminders for superseded policies. A queued notification is not proof
  of successful delivery. Expiry dates are inclusive business dates.

## Migration history, range and artifacts

Existing files remain under `src/Cosmix.Infrastructure/Database/Migrations`:

1. `20261005075444_InitialFoundation` creates the Department table.
2. `20261005113807_AddInitialHrAttendanceModels` adds sixteen tables and the employee
   number sequence. Its snapshot describes seventeen entities.

No equivalent expanded-model migration existed. EF Core generated:

- [20261006074233_CompleteReviewedHrAttendanceDataModel.cs](../src/Cosmix.Infrastructure/Database/Migrations/20261006074233_CompleteReviewedHrAttendanceDataModel.cs)
- Its generated Designer file and updated `CosmixDbContextModelSnapshot.cs` in the
  same folder. The snapshot was generated by EF, never manually rewritten.

The four earlier migration/Designer files retain their original SHA-256 hashes.
Only the new migration's `Up` was customized with the evidence preflight above.

| SQL artifact | Starting migration (already applied) | Ending migration |
|---|---|---|
| [Incremental SQL](sql/20261006074233_CompleteReviewedHrAttendanceDataModel.incremental.sql) | `20261005113807_AddInitialHrAttendanceModels` | `20261006074233_CompleteReviewedHrAttendanceDataModel` |
| [Full chain for a future empty database](sql/20261006074233_CompleteReviewedHrAttendanceDataModel.from-zero.sql) | `0` (no migrations applied) | `20261006074233_CompleteReviewedHrAttendanceDataModel` |

These are alternative, non-idempotent scripts: do not run both or rerun one on an
already migrated target. They do not create a database or select a server/database.
See [SQL usage notes](sql/README.md). Neither script was executed.

The forward migration contains **98 new tables, 19 new columns on existing tables,
four alternate keys, 312 standalone index operations, six checks on existing tables,
20 standalone FK operations and seven additional schemas** (466 generated operations
plus one preflight SQL operation). New-table operations include further keys/checks/FKs.
The full model now has **115 business tables and 262 restrictive foreign keys**.
The full SQL chain also creates EF's migration-history table and the existing employee
number sequence. It adds no business data. No forward drops, column narrowing,
renames or cascading deletes were generated.

`Down` has 144 operations. Static graph analysis confirmed that its ten explicit FK
removals and table ordering safely remove the same 98 new tables and 19 columns,
leaving the exact 17 original tables and 23 original FKs. This is structurally correct
but **destroys data stored in the new tables/columns**; it is not a backup/restore plan.

## Verification

Final integrated checks on 6 October 2026:

| Check | Result |
|---|---|
| `dotnet build Cosmix.Backend.sln --configuration Release --no-restore -p:EnableSourceControlManagerQueries=false --verbosity minimal` | Passed, zero warnings/errors. |
| Existing file-based `artifacts/model-completion/verify.cs` | 946 checks passed; 115 mapped entities, 262 restrictive FKs, bounded strings, precision, rowversions, no shadow columns, model guards and in-memory SQL construction. |
| `artifacts/migration-review/fix-checks.cs` | 8 checks passed: reversal source, SQL revision default/lineage, rejection of later shift membership and acceptance of a new approved population. |
| `artifacts/migration-review/hr-history-check.cs` | 61 checks passed: valid roots/successors, invalid revision/pair rejection, employee-bound FKs and predecessor uniqueness. |
| `artifacts/migration-review/policy-end-check.cs` | 25 checks passed: withdrawal dates/evidence, original unchanged, mapping and append-only writes. |
| `artifacts/migration-review/insurance-check.cs` | 88 checks passed: ownership, coverage dates, renewal, reminder scheduling/cancellation/queue matching, concurrency and mapping constraints. |
| `artifacts/migration-review/inspect-model.cs` before generation | 17-entity snapshot compared with the 115-entity model; additive 466-operation delta, no equivalent migration. |
| `artifacts/migration-review/migration-check.cs` | 4,213 offline metadata/script checks passed: model/snapshot agreement, operations, history hashes, exact EF script output, FK SQL types/collations, delete rules, constraint names and key/index widths. Widest key 632 bytes; widest standalone index 800 bytes. |
| `dotnet ef migrations has-pending-model-changes` (Release, no build) | No model changes since the new migration. |
| Independent static SQL and Down dependency review | All 115 tables, 262 FKs, 314 checks and 504 key/index definitions reviewed. No missing referenced columns, dependency-order defect or constraint-name collision. |
| SHA-256 comparison of migration history | Four earlier migration/Designer files unchanged; snapshot updated by EF with the new migration. |
| `git diff --check` | Passed; only line-ending normalization warnings. |

The 1,128 final model/regression checks and migration metadata checks are **not** a database integration or UAT
suite. Each regression was reproduced before its fix. The original temporary-shift
regression reached a connection interceptor, which threw before opening anything;
all final verification runs reported zero connection attempts.

No test project was added. File-based commands used the existing local dependency
cache/environment and `NuGetAudit=false`, `EnableSourceControlManagerQueries=false`,
and the local restore configuration. This avoids host Git metadata/network restore
limitations; it is not a dependency vulnerability audit. Logs are ignored under
`artifacts/migration-review`. No database creation, connection, query, update,
EnsureCreated or startup migration was performed.

Independent read-only reviews found no remaining important defect in the fixes or
generated migration. A suggested SQL transaction-abort improvement was incorporated
and the scripts regenerated. Microsoft's [XACT_ABORT reference](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-xact-abort-transact-sql)
documents how `THROW` rolls back the current transaction with this option enabled;
compile errors still require the script runner to stop and handle failure correctly.

No SQL Server execution, real database concurrency, provider behavior, endpoint
authorization, payroll calculations or end-to-end workflows are claimed verified.

## Before applying to a development database

- Confirm the exact SQL Server instance and personal development database. For the
  user's confirmed new database, select the **from-zero** script after database
  creation is separately authorized. Confirm it is empty before applying.
- If a database already exists by application time, confirm its applied migration
  history and data with its owner. The incremental script requires the exact starting
  migration shown above. Do not bypass the evidence preflight; review a conversion
  separately if it reports incompatible records.
- Confirm SQL Server compatibility, schema permissions and approved business status/
  glossary values. Use a script runner supporting `GO` and stopping on errors.
  A failure in the full script may leave earlier completed migrations applied;
  inspect history before deciding how to resume rather than rerunning from zero.
- Once authorized, validate application in a disposable development database,
  including constraints/concurrency and application read/write scenarios. For any
  populated target, first obtain a restorable backup. No SQL Server execution has
  been performed as part of this offline review.
- Confirm the approved status/value catalog and unresolved policies/providers before
  enabling their workflows. This data model is not a complete production HR system.

No API/UI/seed accounts/demo data, commit or push is part of this task.
