# Carry-forward verification

The coordinated initial build passed with zero warnings/errors. Local migration
`20261009104434_AddAttendanceCarryForwardAndDirectMonthLocks` was generated, not applied. There is no
test project, SQL connection, production-provider call, real-account
permission change or database fixture insertion in these harnesses.

| Runner | Meaningful coverage | Actual result |
|---|---|---|
| `VerifyCarryForwardRules.cs` | Next calendar month, partial/overlapping locks, year/leap boundaries, exact tick and displayed-minute deltas | 13 passed |
| `VerifyCarryForwardService.cs` | Actual service/operation/evidence/overlap code with in-memory auth, storage, repository and outbox; frozen source, proposal/approval/reject/cancel/replay, stale source/target/rowversion, different reviewer, successive and negative deltas, manual duty, source scopes, rollback simulation, moved virtual windows, exact source-policy binding and bounded per-operation month caching | 55 passed |
| `VerifyCarryForwardQueries.cs` | Actual SQL Server LINQ translation for composed request projections, all queue population scopes, bounded cursor scans, duplicate requests, all-revision monthly postings and cross-date raw/virtual overlap | 23 passed; actual SQL confirmed parameterized Pending predicate, and the overly literal harness assertion was fixed |
| Root-owned model runner | Exact new entity mapping, immutable write guard, composite/restricted FK and migration downgrade guards | 35 passed |

Affected ordinary correction service checks: 99 passed, including required virtual-overlap denial.
Affected factual summary checks: 61 passed. These results use synthetic IO or query translation;
they do not constitute authenticated SQL acceptance.

The service harness uses a synthetic approved policy result solely to construct a retained lock input.
It does not configure the real policy adapter or prove any financial formula. Its fake transaction
models rollback and rejects nested transactions, but is not a substitute for actual SQL isolation,
concurrent writers or private-storage durability.

Required separately authorized acceptance: apply the migration to an isolated database with suitable
past-month synthetic approved employment, source sessions, calculated policy summaries and direct lock
manifest; exercise authenticated self/HR cookies/MFA/CSRF, real grants and revocation, same-key retries,
concurrent target locks/prior approvals, storage replay/integrity, failure rollback and posted-month
reads. Current isolated fixture has no such past-month lock dataset. No SQL acceptance is claimed.

Production policy calculation remains unavailable; real final locks require complete approved policy
results. Legacy locks without an exact protected manifest return explicit unavailable provenance.
Financial/time-pay integration remains pending; there is no reopening endpoint or source rewrite.
