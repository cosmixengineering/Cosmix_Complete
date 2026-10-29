# October disposable test-data repair — executed 28 October 2026

The user confirmed the linked history was disposable test history, reviewed the
repair plan, and then explicitly requested execution in this chat. The bounded
repair committed at **2026-10-28 13:43:10 UTC (18:43:10 Pakistan time)**.

## Target and backup

- Existing `Software-PC1\LOCALDB#36E7B148 / Cosmix_Testing`, ONLINE.
- Windows actor/owner `COSMIXRND\Huma.Khan`.
- Database GUID `631714B1-1CB2-43CA-9A04-4B2C04E3D1A7`;
  family `D9C34627-18A2-41CF-A804-821E39516303`.
- Existing MDF/LDF identities matched the plan. No attach, reset, restore,
  replacement database, FK disabling, schema change or migration was performed.
- Full COPY_ONLY backup: **50,450,432 bytes**, checksum verification passed,
  header and file list matched. SHA256:
  `E296ECC33B9D931854A4E761F59638B360FB862D42E9812CF27CC93B7F3C55A4`.
- `RESTORE VERIFYONLY WITH CHECKSUM` verifies media; no restore test was run.

Private manifests, exact selected rows, backup and commit ledger are under the
planned `C:\Users\Huma.Khan\AppData\Local\Cosmix\TestDataRepairs\OctoberAttendance20261028A01\`
directory. Windows packaged-app virtualization maps that directory to:

`C:\Users\Huma.Khan\AppData\Local\Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Local\Cosmix\TestDataRepairs\OctoberAttendance20261028A01\`

`GetFinalPathNameByHandle` established the actual backing path. SQL Server could
not see the virtual path, so the same directory's verified physical path was
used for backup. SQL directory visibility was checked before backup. Directory
ACL permits only the current Windows user and SYSTEM. Original review files
were retained; the reviewed V2 manifest is a separate file.

## Committed scope

| Table | Deleted disposable rows |
|---|---:|
| hr.SalaryPayments | 123 |
| hr.StatutoryContributions | 2 |
| hr.PayrollJournalEntries | 327 |
| hr.PayrollObligations | 64 |
| hr.PayrollHandoffLines | 190 |
| hr.PayrollHandoffBatches | 70 |
| attendance.AttendancePeriodLockEvents | 1 |
| attendance.AttendancePeriodLocks | 1 |
| **Total** | **778** |

Every ID set matched the original plan. A separate static review identified
non-FK loan-ledger references and statutory receipt replay checks; both were
added and returned zero before deletion and again under transaction locks.
Both retained outbox records were completed report/notification work; no active
financial replay work was present. Receipt, approval, audit and private evidence
records were preserved.

Executor reviewed SHA256:
`7F0E78CB7A0CCD5E84926C92E5D3B7BF1A1239C8C3A599709A24F59D9DEB4111`.
The script froze all 173 tables under a serializable transaction, checked full
source contents, schema and external dependencies, deleted in dependency order,
checked each affected count, and compared the entire remaining database against
the pre-captured preserved-row fingerprints before committing. All comparisons
passed. The private `committed.json` records successful commit acknowledgment.

## Post-commit evidence and practical limit

An independent SELECT-only query confirmed:

- Zero October locks remain.
- All **32 employees** and **94 raw punch events** remain.
- Applied migrations remain **31**.
- No attendance summaries were invented (saved summaries remain zero).
- The originally reported employee
  `D2BF1B9C-1F85-44B7-8231-C81318722C49` still has **zero employment assignments**.

`dotnet run --file docs/attendance-completion/VerifyOctoberRepairReadOnly.cs -c Release`
then passed **105 real SQL/repository checks**. This exercised the actual EF
`AttendanceSummaryRepository.LookupAsync` and strict `LatestAsync` for all 31
October dates: every date was unlocked, no saved evidence was fabricated, and
no locked-summary exception occurred. Target identity, employee/raw-punch
preservation and migration count were checked too. The verifier contains only
reads and does not start an API, write fixtures or bypass HTTP authentication.

The invalid-lock cause is removed. This does not reconstruct attendance that
was never recorded. That employee still needs an authorized effective-dated
employment/site-or-office assignment, applicable schedule/policy, and accepted
punches or an approved HR correction. No guessed historical assignment, salary,
attendance or biometric evidence was inserted.

This repair is a test-only administrative recovery, not a new production unlock
workflow. Ordinary finalized-history protections remain unchanged. The API on
port 7240 was not running during execution and was not automatically started;
no live authenticated browser/calendar acceptance is claimed by the repair.
