# Reviewed model migration implementation plan

> **For agentic workers:** Use the executing-plans workflow for implementation, with independent reviews of HR, Attendance and access/onboarding. The user has authorized this review and migration preparation; no commit, push or database access is authorized.

**Goal:** Review the existing SRS models and prepare one reviewed incremental EF Core migration and SQL script if no schema-blocking decision remains.

**Architecture:** Retain the four application projects, single SQL Server context and Infrastructure migration history. Main agent owns shared files, migrations and documentation; domain reviewers initially perform read-only inspection.

**Tech Stack:** .NET 10, C#, EF Core 10, SQL Server.

**Spec:** `docs/requirements/HR_Attendance_Employee_Mobile_SRS_v1.2.docx`, the confirmed HR → Admin approval → access assignment → account invitation flow, and the user's migration-review instructions of 6 October 2026.

## Global constraints

- Work in the confirmed Desktop checkout on develop and preserve unrelated work.
- No controllers, services, UI, seed data, new test projects, commit or push.
- No database connection, creation, update, EnsureCreated or startup migrations.
- Preserve existing migration files; let EF generate the incremental migration and snapshot.
- Ask about genuine unresolved schema decisions before changing affected structures.

## Review focus

- Incomplete employee drafts and immutable submitted profile versions must coexist.
- Role permissions and effective data scopes must retain approval/account boundaries.
- Punches, corrections, calculation versions and lock/payroll sources must retain evidence.
- Required fields, relationships and check constraints must agree with domain constructors.
- Existing rows must survive forward migration; new required values cannot fabricate evidence.

## Tasks and evidence

- [x] Confirm checkout, branch, model/configuration/coverage presence and capture existing migration hashes.
- [x] Read the complete SRS and compare HR, Attendance, access/onboarding and shared records with actual code.
- [x] Record findings and business decisions; reproduce clear technical defects using ignored file-based checks, then fix within scope.
- [x] Run Release build and EF model/guard validation without database access.
- [x] Inspect the two existing migrations and snapshot; check for an equivalent migration and examine the model delta before generation.
- [x] If no schema blocker remains, scaffold an incremental migration in `src/Cosmix.Infrastructure/Database/Migrations` and review all Up/Down operations.
- [x] Generate and review an offline SQL script for an explicit starting/ending migration range; verify history hashes and model/snapshot agreement.
- [x] Update `docs/Data-Model-Coverage.md` and write `docs/Migration-Review.md` with findings, evidence, risks, remaining decisions and application prerequisites.

## Execution notes

- Checkout confirmed at `C:\Users\Affan.Ahmed\Desktop\cosmix-backend`, branch `develop`; only pre-existing `UpgradeLog.htm` was untracked at task start.
- SRS SHA-256: `0d7216815c24a476d82f12c46cdd97d953a8a50217ecdf10d7cf52332c403e71`.
- Review scratch data and no-database verification belong in ignored `artifacts/migration-review`; no additional project is added to the solution.
- Earlier model work deliberately left the snapshot at 17 entities. This task explicitly authorizes preparing the later migration, subject to the review gate.
- Ruling: preserve the confirmed develop checkout and do not create an isolated branch or commit; the user requested review of this checkout and explicitly prohibited commit/push.
- Ruling: revision links, reversal-source validation, immutable shift membership and policy-ending evidence are technical history fixes within the existing SRS; no policy rates/authorities or cardinality defaults are introduced.
- Tasks through model verification complete: Release build zero warnings/errors; 931 integrated + 94 focused checks passed, 113 entities, 256 restrictive FKs; no database connection or SQL execution.
- Initial review stopped at the migration gate until insurance ownership and legacy data were clarified. Existing history remained unchanged during that review.
- Final focused independent review: no important defect in the fixes; application-level revision sequencing, authorization and effective-period selection remain explicit responsibilities.
- User clarification on 6 October: no database work/data exists yet; insurance expiry and reminders cover both employees and vehicles. Those blockers are resolved.
- Bounded extension: immutable `InsurancePolicy` with exactly one Employee/Vehicle FK, provider/reference, inclusive coverage dates, private evidence and predecessor history; `InsuranceExpiryReminder` with caller-configured due time/recipient/channel and a link to the existing queued Notification. No reminder lead days, recipients, provider, policy categories or delivery worker are invented.
- Ruling: preserve both earlier Code First migrations even for the new database. Generate an incremental migration after `AddInitialHrAttendanceModels`, plus SQL for both that incremental range and `0` through the new migration for a future empty database. Do not apply either script.
- Completed insurance extension and independent review: 115 mapped entities, 262 restrictive foreign keys; no remaining schema blocker. Reminder delivery remains future Application/provider work.
- EF generated `20261006074233_CompleteReviewedHrAttendanceDataModel`, its Designer and snapshot. New Up adds a preflight with `SET XACT_ABORT ON` to reject incompatible legacy evidence if reused against populated tables; no data is invented.
- Reviewed 467 Up operations (466 generated + preflight) and 144 Down operations. Forward delta: 98 tables, 19 columns, no drops/narrowing; Down dependency simulation preserves the original 17 tables and 23 FKs but removes new data.
- Release build: zero warnings/errors. Final model checks: 946 + 8 + 61 + 25 + 88 passed. Final migration metadata checks: 4,213 passed, zero connections. EF reports no pending model changes; four earlier migration/Designer hashes preserved.
- SQL scripts generated and reviewed for `20261005113807_AddInitialHrAttendanceModels` -> new ID and `0` -> new ID under `docs/sql`. No SQL executed, database touched, test project created, commit or push performed; `UpgradeLog.htm` preserved.
