# Employee Salary Change Verification

Verification date: 7 October 2026. Scope and API contracts: [Employee Compensation](Employee-Compensation.md).
No frontend, real employee, account initialization, SMS configuration, commit or push is part of this continuation.

## Recorded checks

- Baseline and feature Release builds: zero warnings/errors.
- Before implementation, the external runtime check failed because the public compensation workflow was absent.
  The hosted API check also reproduced zero compensation paths before the new API was built.
- Parent rerun: **251/251** temporary service/operation/domain checks passed. These cover first/subsequent salary,
  current/future dates, precise money validation, explicit salary grants, web MFA/current-session/All scope,
  fixed-review assignment, requester/subject separation, stale versions, exact retries, return/reject/cancel,
  immutable before/after evidence, expired/future cutoffs, private-data boundaries and simulated rollback.
- Parent rerun: six repository checks passed and captured 13 translated SQL commands with database connections
  suppressed. They cover timeline validation, current/next/latest selection, full-history end resolution across
  pagination, employee/request-type filtering and original SQL rowversion expectations.
- Actual hosted `Program`: OpenAPI 200, **8 compensation operations across 7 paths**, required web cookie/CSRF/retry
  metadata and strict request schemas. All **43 employee operations across 37 paths** denied anonymous requests.
  The helper stopped only its own temporary API process; the user's Visual Studio process was left untouched.
- `dotnet ef migrations has-pending-model-changes` reported no model changes since the last migration.
  No migration was generated or applied for this feature.
- Regression reruns: 115 lifecycle, 55 hiring-service, 45 approval-materializer and 37 protected-role checks passed.
- Parent independent SQL rerun: **27/27 checks, 270 assertions, zero failures**, on the original development
  database. All 125 table counts returned to baseline, accounts remained zero, schema fingerprint stayed unchanged,
  and the same 11 migrations/model remained consistent. Details and boundaries are below.
- Independent read-only review found no actionable correctness/authorization/privacy defect in the scoped implementation.
- `git diff --check` passed; Git reported only the pre-existing snapshot CRLF normalization warning.

One intermediate test expected salary read and salary review on the same assignment. That expectation was corrected:
as in the existing approved operation, **review must belong to the active fixed-role assignment**, while salary read
can independently come from another valid All-scope grant. Custom-role review borrowing an unrelated Admin assignment
still fails. The earlier 250/251 run is retained; the corrected full parent rerun is 251/251. No product authorization
guard was weakened to satisfy the test. Initial fixture timezone/purpose setup errors are also retained in temporary evidence.

## Evidence and limits

Temporary harnesses, not repository test projects:

- `%TEMP%/Cosmix.EmployeeCompensation-20261007/parent-results.json`
- `%TEMP%/cosmix-compensation-repository-check`
- `%TEMP%/Cosmix.CompensationHttp-20261007/results.json`
- `%TEMP%/Cosmix.CompensationSql-20261007/sql-results.json`

## Original database verification

The initial test target guard refused a configured named-pipe transport rather than guessing a database.
Read-only process inventory tied that existing pipe to the original per-user MSSQLLocalDB master file;
the harness then required both the exact transport and SQL-side master-file identity before proceeding.
No new instance/database was started, created, attached, restored or selected as a replacement.
All subsequent checks targeted the original `Cosmix_Development` with its existing schema.

The rollback-only SQL harness exercised actual EF/service/repository behavior: frozen proposals, first and revised
salary approval, exact retries, request and employee SQL rowversions, return/reject/cancel without salary effects,
future and business-midnight boundaries, expired-version non-resurrection, pagination and employee/type isolation.
It also tested ambiguous predecessor/revision/date/approval evidence, actual FK/unique/check constraints,
append-only guards, stale tracked SQL writers and rollback after a saved decision when later audit storage failed.

Normal scenarios use the prior savepoint adapter inside an outer serializable transaction that is always rolled
back. A separate probe exercised the real administration transaction and exclusive application lock, approved a
synthetic salary, then deliberately threw before commit; the transaction and lock ended with no retained rows.
The final independent parent run confirmed all table counts, migrations, schema and account count remained unchanged.
SQL sequences and rowversion allocation advance normally even on rollback; they were not reset or reseeded.

SQL actor identity, permission/fixed-role checks and private files are explicitly substituted fixture boundaries;
those service authorization cases are covered separately above. This does not prove a real user's SMS login or
real storage deployment. Stale tracked writers were checked sequentially, not as a distributed concurrent load
or successful-commit race. No real employee data or external provider call was used.

Service checks execute real application/domain/operation code with explicit repository, authorization, clock and
private-storage substitutes. Simulated rollback and file hash/purpose checks do not prove SQL atomicity or encryption.
The repository translation check does not execute SQL. Anonymous HTTP checks do not constitute authenticated
browser/mobile acceptance. No real SMS/scanner/owner setup, production load or simultaneous successful-commit race
test is claimed. Scope excludes payroll calculation, bank updates, general profile changes and multistage approval.
