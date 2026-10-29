# October attendance test-data repair plan

Status update: the user subsequently explicitly authorized execution. The exact
778-row repair **committed on 28 October 2026**, after verified backup and retained
data checks. See [execution evidence and remaining setup gap](October-Test-Data-Repair-Execution-20261028.md).
The original proposed scope and execution conditions below remain as the review
record; references to pending execution describe the earlier planning stage.

## Why the screen is unavailable

The October period was locked through a legacy fixture shortcut, without saved
attendance summaries, a valid completed approval or a protected lock manifest.
The backend prevention fix and 254 standalone checks pass. A schema migration
cannot restore attendance that was never recorded. This repair is explicitly a
one-time removal of a disposable invalid fixture, not a production unlock API.

There is a second setup gap: the employee from the reported screen,
`D2BF1B9C-1F85-44B7-8231-C81318722C49`, exists but has **zero employment assignments**.
Removing the bad lock alone will not supply an effective site/office/shift or
verified attendance for that employee. Do not claim the whole screen repaired
solely because the lock is gone.

## Exact target and identity

- Existing server at inspection: `Software-PC1\LOCALDB#36E7B148`.
- Existing database: `Cosmix_Testing`, ONLINE; Windows user `COSMIXRND\Huma.Khan`.
- Database family: `D9C34627-18A2-41CF-A804-821E39516303`.
- Database GUID: `631714B1-1CB2-43CA-9A04-4B2C04E3D1A7`.
- Existing files: `C:\Users\Huma.Khan\Cosmix_Testing.mdf` and
  `C:\Users\Huma.Khan\Cosmix_Testing_log.ldf`.
- Applied migrations: 31, last `20261028114154_ProtectPayrollPaymentSettlementAndBankExports`.
- Invalid period: `4EB6E47C-91C6-482F-914E-D5060BD63D97`, October 1 to November 1 exclusive.
- Its event: `6E56463D-2802-478D-B917-2E8FD615F635`.

Revalidate identity and schema immediately before execution. A changed LocalDB
pipe is not permission to select another database. No database creation,
attach/detach, reset, migration, FK disabling or automatic restore is planned.

## Proposed deletion manifest: 778 rows, eight tables

Only IDs reachable from this exact lock event under the selectors in
`Inspect-October-Test-Repair-20261028.sql` enter the plan. Do not select records
merely by month, employee or a broad `SYNTH` prefix. All 70 selected batches were
independently checked to have the `SYNTH-BATCH` label and no lines from another lock.

| Table | Rows | SHA256 of ordered selected IDs |
|---|---:|---|
| attendance.AttendancePeriodLockEvents | 1 | 0C1714D26873455CCCF65F58A55DEDFFA71C1B31B2D16EFE02A7D0123DDBF1CC |
| attendance.AttendancePeriodLocks | 1 | 1389E69B900393D5F03C552983F3FF0F65D0672623BC9C06DCFE59F01FB814A6 |
| hr.PayrollHandoffBatches | 70 | 98276677808FF399BD0E0B32A824CC51D548101D071C7DEC378B65002EC4D39B |
| hr.PayrollHandoffLines | 190 | 789B9A4574DE013A887E22279B998FE43E3B0CB5BEA68C60605E8315FF1F0E7E |
| hr.PayrollJournalEntries | 327 | EC0CC4B466702F73A27BD86D5E54291EF21D505150122C5CF57CB5DFBC5F60A2 |
| hr.PayrollObligations | 64 | A9F730362260556CBC59BBE48DD4A0E707EA903E1577A61FEBC96BB2302A37DA |
| hr.SalaryPayments | 123 | 669A87B3A0BFFDE08841978F91A15D8741F85F49ED3B53012031D9B06EABEA41 |
| hr.StatutoryContributions | 2 | D6490469126D7D3E354B6DA86838C66BDAE3B83A26831C9C2A68D04E5E13B0D8 |

The two statutory rows reference selected journal entries even though their
ObligationId is null. They have zero child entries and zero outside journals
pointing to them. Their IDs are `A155E45F-1445-439A-A795-0FEB6266D4B4` and
`BD708AC1-D91E-4EFE-AC1D-6C4D71CCC871`. No statutory calculation or legal activation
is part of the repair.

Verified zero affected descendants: bank exports, bank-export renewals, batch
processing records, allowance settlements, period-summary memberships,
carry-forwards and corrections referring to this lock. No outside payment
references a selected journal/export. These zero conditions must be rechecked.

## Records and files preserved

All employees, accounts, credentials, roles, permissions, employment assignments,
sites, shifts, policies, raw punches and unrelated financial/statutory records.
All applied migrations, private evidence, keys, prior acceptance results,
approvals, approval decisions and audit history remain intact. Specifically,
the malformed pending legacy approval is retained as historical fixture evidence;
it must never be treated as completed authority or converted to a valid approval.

No payroll/salary/month-lock idempotency receipts were found by the operation-name
inspection. Receipts and outbox rows are not included in the deletion manifest.
There is one pending in-app notification and one completed report-export message.
Their encrypted payloads were not opened during this read-only review. Before
execution, inspect their owning producer/subject metadata: if any active message
or differently named receipt can replay/recreate a selected record, stop and
revise the scope; do not silently add its deletion to this plan.

## Execution sequence, after a reviewed executor and explicit approval

1. Pause application/acceptance writers and delivery workers; verify no competing
   writer. Recheck server, database family/GUID/files, migrations, foreign-key
   graph, all ID-set hashes, row versions/content fingerprints and zero conditions.
   Freeze selected row contents in a private manifest. ID hashes above bind
   membership, not complete contents; changed contents must be reviewed too.
2. Use a new, non-overwriting private directory:
   `C:\Users\Huma.Khan\AppData\Local\Cosmix\TestDataRepairs\OctoberAttendance20261028A01\`.
   It was absent at inspection. Keep manifest/backup/logs only there. Existing
   private storage and evidence files are never removed. Recheck space first
   (about 4.85 GB free at the final filesystem inspection).
3. Create a full `COPY_ONLY` database backup with `CHECKSUM`, using a fresh
   `Cosmix_Testing_before_October_fixture_repair.bak` filename; no `INIT` or overwrite.
   Run `RESTORE VERIFYONLY WITH CHECKSUM` and inspect backup header/file list to
   confirm database identity and backup completion. Save a backup SHA256.
   This verifies the backup media, not a test restore. No restore is authorized.
4. Execute a separately reviewed ID-bound script with `XACT_ABORT ON` in one
   serializable transaction. Keep all constraints enabled. Revalidate selected
   rows/dependencies under locking; require exact row counts after each statement.
   Delete in dependency order: 123 payments; two statutory contributions;
   327 journal entries; 64 obligations; 190 handoff lines; 70 batches;
   one invalid lock event; one invalid period. Any mismatch/error rolls back the
   entire transaction, with no automatic retry or expanded scope.
5. Before commit, verify all 778 selected IDs are absent, every preserved table's
   captured content/row fingerprint is unchanged, and migration history/schema
   are unchanged. Keep a private repair ledger containing actor, timestamp,
   approval reference, target identity, deleted IDs, backup hash and results.
6. Restart only the intended API after the repair commits. Use fresh request keys
   and recheck the reported calendar read. Do not reuse historical test receipts.
   Confirm the erroneous October lock no longer controls the calendar; verify
   remaining setup/evidence gaps remain honest rather than becoming Present/Absent.

The deletion executor has not been created or run. Do not use this document as
permission to hand-type unguarded deletes or disable the application's ordinary
immutability protections.

## Making attendance usable after fixture recovery

1. Through existing authorized HR/Admin workflows, give the chosen test employee
   the correct effective-dated employment/site-or-office assignment and roster.
   Use an explicitly selected test shift/policy; do not invent a historical join
   date, salary, office schedule, entitlement or attendance status for all staff.
2. For an agreed test date, use accepted mobile punches once the device/provider
   is ready, or the existing documented HR correction workflow with independent
   review. A correction is not biometric verification. Never turn the 94
   unassessed raw rows into approved attendance automatically.
3. Recalculate and persist the real daily summary through its authorized API;
   verify calendar/day display, frozen input evidence, retry and revision handling.
4. Test month readiness on an ended month with complete source coverage. Missing
   facts/policy/legal/financial prerequisites must continue to block finalization.
   Do not re-lock current October merely to satisfy a UI test.

Those employee setup and authenticated fixture writes require their own concrete
IDs/dates/inputs in the execution packet. This plan authorizes neither guessed
business values nor a claim of full Attendance acceptance.
