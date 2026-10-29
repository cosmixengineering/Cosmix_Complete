# Locked attendance summaries: 28 October 2026

## Finding and scope

Read-only inspection of the existing `Cosmix_Testing` database on
`Software-PC1\LOCALDB#36E7B148` found 31 applied migrations. Database family:
`D9C34627-18A2-41CF-A804-821E39516303`. No database writes, migration application,
unlocking, deletion, reseeding or provider calls were performed for this investigation.

October 2026 has one locked attendance period, but no saved daily summaries or
period-summary memberships. Its event has no protected manifest. Its linked
approval is pending, has the noncanonical `AttendanceLock` request type, and
references a different subject. There are no approval decisions for it.
The event actor/reason match the legacy payroll acceptance fixture's direct
`RecordDecision` pattern. That is evidence of an invalid fixture pattern, not
proof of the identity/process that originally wrote the row.

There are 94 raw punch rows, all without a BusinessDate, no punch assessments,
no work sessions, and no attendance payroll-input revisions. Those raw rows do
not establish approved duty or a verified historical attendance calculation.
An unavailable locked day must not become Present, Absent or zero salary input.

The invalid event is referenced by 190 payroll handoff lines in 70 synthetic
batches. Related records include 64 obligations, 327 journal entries and 123
salary payments. These are database records, not evidence of real bank payments.
Further dependency inspection found two statutory records pointing to the selected
journals. The user confirmed disposable test history; the resulting
[repair plan](October-Test-Data-Repair-Plan-20261028.md) identifies 778 proposed
rows across eight tables. The read-only manifest was executed; the repair was not.
Removing or excluding the lock without addressing those dependencies would
silently change historical payroll provenance. No such operation was executed.

## Implemented prevention

`AttendancePeriodWriteGuard` now rejects newly written legacy approval-backed
locks, missing protected manifest references, empty membership, duplicate
employee/day or summary references, out-of-period dates and invalid tracked
summary identities. Existing composite foreign keys enforce untracked summary
identity. The application finalization service remains responsible for actual
protected evidence, coverage, policy, financial readiness and source-race checks.

Existing legacy reads remain supported. Previously locked periods still cannot
be reopened, deleted, moved or revised through ordinary SaveChanges. This fix
does not repair the existing October fixture or remove its N/A cells.

No schema change is needed for this fix. A migration adding fabricated summaries
or bypassing the locked-period rule would not constitute recovery.

## Verification executed

- Release API build: passed, zero warnings/errors.
- Existing `VerifyCarryForwardModel.cs`: 57 guard/model checks passed; new negative
  regression failed before the fix.
- Existing `VerifyMonthLockQueries.cs`: 42 membership/query checks passed.
- Existing `VerifyMonthLocks.cs`: 85 service checks passed.
- Existing `VerifySummaries.cs`: 70 service/authorization/snapshot checks passed.

These 254 checks use standalone/offline verification; they are not authenticated
SQL acceptance. The real SQL operations in this investigation were read-only.

## Remaining recovery boundary

The existing invalid fixture cannot be reconstructed from authoritative saved
attendance: none exists. Do not automatically unlock the month, insert guessed
summaries, migrate the lock away, or delete linked payroll records. A separate
reviewed synthetic-data recovery decision is required: preserve this dependent
history, or explicitly authorize its bounded backup/repair after enumerating all
foreign-key dependencies and confirming the records are disposable test data.
No cleanup script is approved or supplied by this document.

New valid attendance requires the normal effective-dated employee/site/shift and
policy setup, genuine accepted punches or approved HR correction evidence, saved
daily calculations, and successful month readiness/finalization. The historical
October test month must not be presented as repaired until that recovery is done.

Legacy fixture writers in `AcceptanceExecutionSuite`, `DashboardSummarySqlTests`
and `PayrollSqlPersistenceTests` still use the now-rejected shortcut. They were
not executed or modified here; adapt them to valid isolated workflow fixtures
before using those suites, rather than weakening the production guard.
