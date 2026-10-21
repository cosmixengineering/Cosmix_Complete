# Reviewed migration SQL

## 19 October 2026: No Database Application

Compiled chain: 25 migrations, latest
`20261015144424_AddFinancialAdjustmentsAndOutstationDeployments`.
Offline inspection of the current model against that migration reports zero
operations. This is a source/model result, not inspection of any live database.
The continuation did not connect to SQL, apply migrations, create fixtures or
change connection settings. Existing migration files and the already-dirty
snapshot were preserved. The newest user instruction prohibits migration
application. Historical target names and permissions below do not authorize it.
See [current verification](../hr-srs-remediation/Continuation-20261019.md).

## Office roster history upgrade - 13 October 2026

Migration 22 `20261013101327_AddOfficeRosterHistory` follows migration 21 and adds only
`attendance.OfficeRosterAssignments` and `attendance.OfficeRosterAssignmentEnds` with four
indexes and restrictive references. Its [reviewed incremental SQL](20261013101327_AddOfficeRosterHistory.sql)
contains six additive operations and no fixture/business-data updates. Down refuses retained
office history before dropping tables. The offline harness passed 14 checks after observed
preservation-test RED/GREEN. All 42 applied source/designer files for 1-21 remained unchanged.

The exact existing approved synthetic target `Software-PC1\LOCALDB#842DD4FB/Cosmix_Testing`
was verified at 21 before execution. A new B05 COPY_ONLY/CHECKSUM backup passed header and
restore verification; only migration 22 was applied. Retained business row counts and
employee/account identities were preserved; both new tables were empty at post-upgrade check.
This fixes the earlier model/snapshot gap, not Office duty or full Attendance acceptance.
Read [latest executed results](../attendance-completion/Verification.md) before choosing any
future range. Old fresh/full-chain 21-migration packets remain obsolete and are not replay commands.

## Existing synthetic laptop upgrade - 13 October 2026

Under the user's later direction, exact server `Software-PC1\LOCALDB#842DD4FB`, database
`Cosmix_Testing`, owner `COSMIXRND\Huma.Khan` was upgraded from 12 to 21 after a verified
COPY_ONLY/CHECKSUM backup. Initial business row counts and employee/account identities were
preserved during migration. No other database was upgraded or reset. See
[executed verification](../attendance-completion/Verification.md) for actual scope and limits.

The first attempt applied 13-18; migration 19 rolled back on premature filtered-index column
binding. Only then-unapplied `20261012125614_AddDirectPoliciesAndAdminLeave` was fixed using
inner EXEC for the two new-column filtered indexes. Applied files 1-18 were preserved. Reviewed
`Huma-Attendance-Remaining-19-21-20261013.sql` then completed the pending range. The initial
13-21 script is retained as failure provenance, not a current replay command. Corrected full
chain: `20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence.fresh-isolated-index-fix-20261013.sql`.

Current local model changes for `OfficeRosterAssignments`/`OfficeRosterAssignmentEnds` are NOT
in the retained snapshot/21-migration chain. The final verification guard stopped before SQL
connection. Do not bypass the mismatch, edit applied migration 19 now, or apply obsolete pinned
fresh packets. Coordinate the new schema ownership and review the actual pending range first.
Older unapplied/baseline sections below describe their dated stages, not the latest laptop state.

## Huma laptop continuation - 12 October 2026

Local migration 21, `20261012151917_AddAttendanceFinancialAttestationsAndLegalReviewEvidence`,
adds two append-only evidence tables plus the policy/version composite key. Downgrade refuses
retained attestations or legal dossiers. No source migration 1-20 was rewritten. This migration
is prepared, not applied; actual `Cosmix_Testing` was read-only verified at migration **12**.
Do not infer this machine's database state from source-laptop acceptance sections below.
See [the local continuation](../attendance-completion/Laptop-Continuation-20261012.md).

## Payroll inputs and company configuration — 12 October 2026

Migration 20, `20261012135725_AddAttendancePayrollInputsAndCompanySettings`, follows migration 19.
Its [incremental SQL](20261012135725_AddAttendancePayrollInputsAndCompanySettings.sql) was generated
offline and **has not been applied**. It creates only `attendance.AttendancePayrollInputRevisions`,
`hr.CompanyProfiles` and `hr.CompanySiteJurisdictionVersions`, with restrictive references,
singleton/revision constraints, unique timeline/revision identities and protected-evidence headers.
No company data, salary, role grant or legal setting is seeded. Downgrade refuses any retained
rows before table removal. The offline model/script harness passed 22 checks and confirms exact
snapshot agreement; 38 source/designer hashes for migrations 1–19 remain unchanged.
Reproduce using `docs/attendance-completion/VerifyPayrollInputModel.cs --write-sql`.
Migrations 18–20 remain unapplied in this continuation; the recorded isolated baseline remains 17.

## Local policy and leave continuation — 12 October 2026

Migration 19, `20261012125614_AddDirectPoliciesAndAdminLeave`, follows migration 18
`20261012114635_AddSiteReportResponsesAndShiftReminders`. Both are prepared locally and remain
unapplied in this continuation; the previously verified isolated baseline is migration 17.
No database inventory or write is implied by script generation.

Migration 19 adds explicit direct-HR policy authority with constrained nullable approval references,
policy revision concurrency, immutable leave day/shift links, typed leave kinds and returned-request
and submission identities. Existing policy authority remains approval-backed. New-column checks
use inner SQL batches to avoid premature SQL Server column binding. Downgrade refuses retained
workflow evidence before any destructive operation; no fake approval/policy identifier is generated.
Use `docs/attendance-completion/VerifyPolicyLeaveModel.cs --write-sql` to reproduce the incremental
script through the standalone offline model harness. This is not database application approval.

The earliest artifacts were prepared offline on 6 October 2026 using EF Core 10.0.12.
Later reviewed ranges and their application records are retained below. Script generation does not
connect to or change a database. Read [Migration Review](../Migration-Review.md) and
[authentication verification](../Shared-Authentication-Verification.md) before application.

The latest recorded upgrade of the original `Cosmix_Development` database is migration 12,
`20261008074438_AddEmployeeChangeJournal`; see [Employee Updates verification](../Employee-Updates-Verification.md).
The isolated `Cosmix_Testing` database has a separate history. The
[current attendance verification record](../attendance-completion/Verification.md) records its
latest upgrade outcome, remaining migrations and acceptance limits. Inspect the actual target's
history before choosing a pending range, including after a partially completed upgrade.

The dated sections below preserve historical review and execution evidence. Their table counts,
pending statuses and from-zero scripts describe the named stage, not the current whole schema.
Retained acceptance scripts elsewhere under `docs` may bind a particular machine, private path,
run and artifact hashes; they are not reusable setup commands without reviewing those assumptions.

## Site report responses and shift reminders — 12 October 2026

[Migration 18 incremental SQL](20261012114635_AddSiteReportResponsesAndShiftReminders.sql) upgrades
exactly migration 17 (`20261012082833_RetainZoneWarningTransitions`). It was generated offline and
**has not been executed**. It adds immutable `attendance.SiteReportResponses`, restrictive composite
relationships and uniqueness, plus nullable `ShiftVersions.MissingCheckoutReminderAfterMinutes`.
Existing shifts remain unconfigured. No fixture data or grants are included.

The positive-delay CHECK uses an inner `EXEC` in the existing transaction, so SQL Server compiles
the expression after the new column exists. The forward script is additive. Source `Down` refuses
when responses or configured reminder versions exist; it is not a data-preserving rollback plan.
Sixteen offline model/script checks passed, including snapshot equality and both downgrade guards.
All 34 migration/designer files for applied migrations 1–17 retain their recorded hashes.
Database application and genuine SQL/private-storage acceptance require separate approved scope.

## Employee Hiring Range - 7 October 2026

`20261007141249_AddEmployeeHiringDraftsAndDocumentPrivacy.sql` upgrades exactly
`20261007122140_AddOrganizationMastersAndHolidayRevisions` to
`20261007141249_AddEmployeeHiringDraftsAndDocumentPrivacy`. It adds hiring proposal storage and medical document
classification. It refuses existing unclassified employee documents; do not bypass that preflight. Prepare an
approved classification conversion for a populated legacy document table instead.
The original local development target was backed up and upgraded through EF; do not reapply the raw script
to that already-upgraded database. This is not authorization to change production. No reset/from-zero is required.

## Organization Range - 7 October 2026

[Organization Masters incremental SQL](20261007122140_AddOrganizationMastersAndHolidayRevisions.sql)
starts at `20261007090713_AddAccountRecoveryAndOwnerInitialization` and ends at
`20261007122140_AddOrganizationMastersAndHolidayRevisions`. It is incremental, not idempotent and not
a from-zero script. A verified COPY_ONLY/CHECKSUM backup preceded applying this eighth migration to
the original `Cosmix_Development` database. Do not reapply the script there. At that stage, the schema had 123
business tables plus EF history; [organization verification](../Organization-Masters-Verification.md)
records actual SQL checks and cleanup. Earlier ranges below are historical.

It adds `hr.OrganizationChanges`, three holiday revision columns, restrictive relationships/checks
and new revision indexes. The forward change drops only the superseded unique index, not tables or
data columns. Down refuses if organization audit rows or calendar revisions exist; it is not a
data-preserving rollback plan. Existing seven migrations are unchanged.

Offline regeneration after a Release build:

```powershell
dotnet ef migrations script 20261007090713_AddAccountRecoveryAndOwnerInitialization 20261007122140_AddOrganizationMastersAndHolidayRevisions --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api --configuration Release --no-build --output docs/sql/20261007122140_AddOrganizationMastersAndHolidayRevisions.sql -- --environment Development
```

## Earlier Authentication Range

**Historical authentication-stage state, 7 October:** seven migrations, ending at
`20261007090713_AddAccountRecoveryAndOwnerInitialization`, and 122 business tables in the original
SSMS `Cosmix_Development`. The sixth and seventh upgrades followed separately verified backups.
[Recovery and first-owner incremental SQL](20261007090713_AddAccountRecoveryAndOwnerInitialization.sql)
starts at `20261007080926_AddWebSmsAuthenticationAndProtectedRoles` and ends at
`20261007090713_AddAccountRecoveryAndOwnerInitialization`. It is not a from-zero or idempotent script.
Do not reapply it to this database. The older "pending" status below records the previous handover.
Fresh empty development databases should apply the complete preserved EF migration chain in order,
not use the historical five-migration script as if it represented the current schema.

The recovery migration adds one history table and explicit external-owner approval provenance;
normal HR actor requirements remain enforced. Its Down refuses when recovery/owner evidence exists.
Actual 4,241 read-only schema checks and a rollback-only SQL workflow check passed. No real account
or synthetic business data remains after verification. See the authentication verification record.

**Historical pending range before the 7 October 2026 application:**
[Web SMS and protected roles](20261007080926_AddWebSmsAuthenticationAndProtectedRoles.sql)
runs from `20261006144249_AddSharedMobileAuthentication` to
`20261007080926_AddWebSmsAuthenticationAndProtectedRoles`. It was prepared offline and unapplied
at this earlier handover, then applied as recorded above. It is incremental and does not initialize
an empty database. The development database then had its starting migration; verify the actual
target/history before any application.
No previous migration should be rerun, rewritten or reset. Existing full-chain artifacts below
end at the fifth migration and do not include this sixth one.

| File | Required starting state | Ending migration |
|---|---|---|
| [Full chain including authentication](20261006144249_AddSharedMobileAuthentication.from-zero.sql) | `0`: an empty target with no applied migrations | `20261006144249_AddSharedMobileAuthentication` |
| [Authentication incremental](20261006144249_AddSharedMobileAuthentication-from-CorrectReviewedModelLifecycles.sql) | `20261006105213_CorrectReviewedModelLifecycles` already applied | `20261006144249_AddSharedMobileAuthentication` |

This historical full-chain artifact contains five migrations, 121 business tables, EF's history table,
and the employee-number sequence. It has no business-data seeds, forward table/column
drops or cascading foreign keys. The two existing data-conversion guards remain intact.
The final migration replaces the active-account constraint to support verified passwordless
mobile activation; it does not automatically verify phones or enable access channels.

**Applied development status, 7 October 2026:** the user's existing SSMS database
`Cosmix_Development` had the four earlier migrations and 117 empty business tables. After a
COPY_ONLY CHECKSUM backup and successful RESTORE VERIFYONLY, EF applied only
`20261006144249_AddSharedMobileAuthentication`. Five history entries and 121 tables are verified.
The full-chain script was not applied to this database; no prior migration was repeated.

The earlier null database-ID result came from a different running LocalDB instance resolving
under the same alias in the command environment. SSMS's actual pipe was independently verified
before application. An initial creation attempt against the other instance failed because
existing MDF files were protected; no replacement database was created and no files were
attached, overwritten, renamed or deleted. Do not infer missing user data from an alias alone.
Confirm actual server/database/history before each run; pipe IDs are ephemeral.

These scripts neither create a database nor select a server/database. They are alternatives,
not idempotent scripts; do not run both or blindly rerun one. The full chain has a transaction
per migration, not one all-or-nothing transaction. Inspect actual history after any failure.

## Earlier corrective artifacts

| File | Required starting state | Ending migration |
|---|---|---|
| [Corrective full chain](20261006105213_CorrectReviewedModelLifecycles.from-zero.sql) | `0`: an empty target with no applied migrations | `20261006105213_CorrectReviewedModelLifecycles` |
| [Corrective incremental](20261006105213_CorrectReviewedModelLifecycles.incremental.sql) | `20261006074233_CompleteReviewedHrAttendanceDataModel` already applied | `20261006105213_CorrectReviewedModelLifecycles` |

The earlier `20261006074233` full-chain and incremental files are retained unchanged
as historical reviewed artifacts. They do not include the subsequent corrections.

At the earlier corrective-model handover, the user had confirmed no database existed yet,
so the **full chain** was the relevant artifact for that then-empty target. The later application
records supersede that historical absence. These scripts are alternatives.
Neither creates a database nor selects a server/database. Neither is idempotent;
do not rerun one or apply both. Confirm the destination and migration history first.

The earlier corrective full chain creates 117 business tables, EF's history table and the
employee-number sequence. The corrective incremental script adds two tables and
sixteen columns to the 115-table schema. Both contain no business-data seeds,
forward drops or cascading deletes.

The earlier expanded-model migration rejects old non-Draft employees or documents already marked as
scanned/verified/rejected/failed without the new evidence history. Such records need a
separately reviewed conversion; bypassing that guard would invent or lose evidence.
Compatible drafts retain their existing data with initial draft revision 1. The
confirmed empty target does not require legacy conversion.

The corrective migration adds another guard: submitted allowance claims or any
settlement records in the previous schema require a reviewed history conversion.
Unsubmitted allowance drafts are compatible. Existing employee-document verification
metadata is preserved, but document availability now requires fresh manual verification
bound to its current scan; `VerifiedScanAt` is not guessed/backfilled. Membership
scheduled dates remain unchanged and new termination metadata starts null.

Use a runner supporting SQL Server `GO` batch separators and stopping on errors.
The new migration enables `XACT_ABORT` before its preflight `THROW` statements.
The full script has a transaction per migration; earlier completed migrations may
remain if a later migration fails. Inspect the resulting history before resuming.
Do not assume the full chain is one atomic transaction.

The generated Down operations were reviewed for dependency order but intentionally
remove the newly added tables/columns and their data. They are not a recovery plan
for a populated database. No rollback script is supplied for casual execution.

## Employee Approval Increment - 7 October 2026

`20261007151945_CompleteEmployeeHiringApproval.sql` starts after
`20261007141249_AddEmployeeHiringDraftsAndDocumentPrivacy` and ends at `CompleteEmployeeHiringApproval`.
It changes only the assignment office column to nullable. The original LocalDB development database is already
upgraded through this tenth migration after a verified backup; do not rerun this non-idempotent script there.
No previous migration or historical script was rewritten. The migration's Down path refuses null-office data.

## Employee Lifecycle Increment - 7 October 2026

`20261007164427_LinkEmployeeStatusApprovals.sql` starts after `CompleteEmployeeHiringApproval` and ends
at `LinkEmployeeStatusApprovals`. It adds only the optional status-history approval reference, filtered unique
index and restrictive FK; no new tables. Already applied to the original LocalDB after a verified backup.
Do not rerun the non-idempotent SQL there. The migration's Down refuses to discard recorded approval links.

## Employee Updates Increment - 8 October 2026

`20261008074438_AddEmployeeChangeJournal.sql` starts after `20261007164427_LinkEmployeeStatusApprovals`
and ends at `20261008074438_AddEmployeeChangeJournal`. It creates only `hr.EmployeeChanges`, with
two restrictive foreign keys, three check constraints and two non-primary unique indexes. There are
no existing-table drops/column changes, business-data updates or seeds. The Down path refuses to
discard a populated change journal. Prior migrations and their scripts remain unchanged.

Applied to the same verified original `Cosmix_Development` after a new COPY_ONLY/CHECKSUM backup and
RESTORE VERIFYONLY. The verified state on 8 October was 12 migrations, 126 tables including history,
zero business rows/accounts, and no pending migrations or model differences at that time. Later
migration additions do not change this historical result. Do not rerun this non-idempotent
script there; inspect each other developer's actual target/history before applying its pending chain.
See [Employee Updates verification](../Employee-Updates-Verification.md) for scope and test evidence.

Reproduce the incremental SQL without applying it:

```powershell
dotnet ef migrations script 20261007164427_LinkEmployeeStatusApprovals 20261008074438_AddEmployeeChangeJournal --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api --configuration Release --no-build --output docs/sql/20261008074438_AddEmployeeChangeJournal.sql -- --environment Development
```

## Earlier artifact reproduction

These commands reproduce the named historical artifacts, not a current full-schema handoff.
Run from the repository root after restoring the pinned tool/packages and building the solution in Release:

```powershell
dotnet ef migrations script 0 20261006144249_AddSharedMobileAuthentication --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api --configuration Release --no-build --output docs/sql/20261006144249_AddSharedMobileAuthentication.from-zero.sql -- --environment Development

dotnet ef migrations script 20261006074233_CompleteReviewedHrAttendanceDataModel 20261006105213_CorrectReviewedModelLifecycles --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api --configuration Release --no-build --output docs/sql/20261006105213_CorrectReviewedModelLifecycles.incremental.sql -- --environment Development

dotnet ef migrations script 0 20261006105213_CorrectReviewedModelLifecycles --project src/Cosmix.Infrastructure --startup-project src/Cosmix.Api --configuration Release --no-build --output docs/sql/20261006105213_CorrectReviewedModelLifecycles.from-zero.sql -- --environment Development
```

These commands generate text only; there is no `database update` step here.
For this review, tooling used a dummy `invalid.local` connection configuration;
offline model checks also used a connection interceptor that forbids opening one.
The API contains no automatic startup migrations.

Static checks verified model/snapshot agreement, exact generated script contents,
FK types/collations, constraint names, index widths and Up/Down dependencies.
SQL Server execution, actual constraint enforcement and concurrent writes still
require separately authorized database verification.
