# Salary and Finance tasks 12–21 — 28 October 2026

Later verification: [Synthetic payroll month acceptance](Payroll-Synthetic-Month-Acceptance-20261028.md) records the subsequent one-employee persisted September scenario, additional discovered fixes, 484 passing regression checks, one passing complete SQL month test, and normal Android login/history/native PDF evidence. The counts below describe the earlier implementation snapshot. Real company sign-off remains separate from the authorized synthetic acceptance.

## Scope and implementation

This continuation implements and verifies the requested salary calculation and Finance integration. Monthly, daily and hourly wages are supported. Company rates, salary rules, applicability, account mappings and approvers remain explicit authorized configuration; synthetic test values are not company policy.

| Task | Implemented behavior | Principal verification |
|---|---|---|
| 12 — Salary master | Effective compensation plus dated, independently approved earning/deduction components; immutable revision and protected source evidence. | Salary calculation/projection and import workflow tests. |
| 13 — Salary-sheet import | Bounded CSV/XLSX uploads, column mapping, validation preview, duplicate source/row checks, source hash/history and canonical pending requests. Excel 1904 dates are interpreted correctly. | Parser and real inspect→preview→submit service tests, SQL source/submission persistence, client contracts. |
| 14 — Full payroll | `salary-v2` generates base salary and approved earnings/deductions, employee gross/net and protected frozen line/batch evidence. Legacy adjustment-only runs remain distinct. | Real projection and preview→prepare tests for Monthly/Daily/Hourly; SQL first insert and frozen append rejection. |
| 15 — Special cases | Joining/leaving coverage, effective revisions, paid/unpaid leave, explicit proration/rounding, zero payable time and duplicate component/loan prevention. Ordinary daily/hourly wage reduction is not deducted a second time as a late penalty. | Calculation, retained-attendance projection, frozen evidence and loan-posting tests. |
| 16 — Approval lifecycle | Separate maker, HR review, Management lock and Finance handoff; current permissions/versions, self-approval rejection, cancellation/rejection history and immutable prepared lines. | Lifecycle, source integrity and HTTP contract tests. |
| 17 — Payslips | Own employee scope and authorized management PDF/XLSX documents from approved frozen salary, actual payment status, SHA-256 envelopes and private mobile file handling. | Document/own-scope tests, actual SQL query translation, mobile adapter tests with synthetic native ports. |
| 18 — Statutory entries | Actual employee/employer amounts, employee registrations and protected independently approved coverage/entry evidence; no equal division of a header total. | Statutory domain/service tests and actual SQL entry/approval/rowversion round trips. |
| 19 — Contribution journals/reports | Actual employee schedules and approved employer payable postings; employee deductions already accrued in payroll are not posted again. | Contribution export, separation, amount/proof and posting tests. |
| 20 — Payroll–Finance binding | Finance requires the approved frozen `salary-v2` run and explicit Finance handoff. Optional caller totals are exact assertions, never accounting inputs. | Source-derived finalization, one-cent mismatch and changed-source replay rejection tests. |
| 21 — Journal controls | Configured salary expense/net payable/deduction/statutory accounts, typed bank accounts, approved profiles/open periods, independent approvals, source identity and duplicate controls. | Balanced posting, account-kind, period, installments, overpayment, idempotency and error contract tests. |

## Corrections found during verification

- Complete salary modules existed in source but were not fully registered in composition; the services, repositories, permission definitions and statutory source reader are now wired.
- Finance configuration and approval permissions were absent from the central catalog, making legitimate setup requests fail authorization. Both definitions are registered and exercised through production API policy registration.
- Salary metadata was not persisted. The new migration maps the salary version, evidence and HR approval fields.
- The initial SQL insert of a freshly frozen salary was incorrectly treated as an attempt to append to an existing frozen batch. New aggregate insertion is allowed; subsequent appends still fail. This has a failing-first SQL regression log.
- Frozen source validation rejects duplicate base/component/loan sources, foreign batch lines, modified lines and whitespace actor bypasses.
- Finance finalization requires Finance handoff, uses frozen totals, and cannot infer a bank account from its name or silently choose the first posting profile.
- Expected Finance validation/conflict exceptions receive scoped 400/409 responses. Unexpected implementation/storage failures remain 500 and do not expose internal details.
- Client numeric enums, permissions, import source recovery and nullable legacy fingerprints were aligned with the API.
- The payroll-related client verification also exposed unrelated HR views with fabricated leave/bank data. Recovery copies of overwritten files are outside Git. Repeated overwrites of the leave draft are isolated by routing both HR leave aliases through `ServerLeaveManagementPage`, which renders the actual backend-backed `LeaveAdminPage`; the draft is neither exported nor imported into active routes. The controlled suite checks the actual route/export binding as well as denied permission and backend-failure behavior. Concurrent source changes are tracked separately from verified snapshots.

## Database execution

Target: `(localdb)\CosmixUat_d893653346`, database `Cosmix_Testing`.

1. Created a database backup and completed SQL Server backup verification.
2. Restored a disposable `Cosmix_Payroll_20261028` clone.
3. Reviewed SQL and applied four pending migrations to the clone, then to the authorized testing database.
4. Confirmed migration history **26 → 30**, with latest `20261028071931_CompleteSalaryRunAndStatutoryEntries`.
5. Confirmed all **160 existing business-table row counts unchanged**, including 14 employees and zero payroll batches. Eight tables were added; migration history is the expected changed system table.
6. `DBCC CHECKDB` and `DBCC CHECKCONSTRAINTS` completed without reported integrity violations.

The four migrations are `20261026193000`, `20261027120000`, `20261027133000` and `20261028071931`. Existing migration Up/Down implementations were preserved. A misplaced snapshot index was repaired, and the new migration preserves existing SQL-generated rowversions rather than attempting unsupported updates/alterations of them. Pending-model warnings are no longer suppressed.

SQL verification inserts synthetic records only in the guarded disposable clone and rolls each test transaction back. No company payroll policy, salary payment or contribution payment was manufactured in the target database.

## Evidence

Durable local evidence: `D:/CosmixPayrollVerification20261028`.

- `red-tests.log`, `red-results/red.trx`: failing-first source/approval tests.
- `sql-red-frozen-insert.log`: actual SQL first-insert failure before the correction.
- `final-regression.log`, `results/final-regression.trx`: final combined backend, controller/DI and SQL regression run; earlier `regression`/`integration` logs retain intermediate results.
- `build-final.log`: final Release API build.
- `migration-testing.log`, `migration-clone.log`, `migrations.sql`: reviewed and applied migrations.
- `backup.log`, `Cosmix_Testing_before_salary.bak`: verified recovery backup; not in Git.
- `database-before.txt`, `database-after.txt`, `database-preservation.json`, `database-integrity.txt`: target verification and count comparison.
- `client-evidence/report.md`, scripts, logs and source hashes: web/mobile verification and bounded client checks.

### Final backend results

- Release API build: **passed, zero warnings and zero errors**.
- Combined regression: **463 passed, zero failed, zero skipped**. This includes 15 HTTP/DI contracts, 4 SQL persistence tests, 13 salary projection/prepare cases, 5 import workflows, 16 parser cases, 7 payslip cases and the existing regression suites.
- SQL tests confirm no pending migrations/model differences, real query translation, duplicate-import protection, complete salary persistence, frozen append rejection and actual statutory approval/amount persistence.
- Mobile `npm run verify`: passed lint, formatting and TypeScript. Bounded client suite: **19 checks passed**, including the actual active leave-route binding.
- Final web checks passed: lint (the lint phase in `web-final-current-verify.log`), then whole-repository formatting (`final-format-check.log`) and TypeScript/production build (`final-build-current.log`). The verify command first stopped on formatting in three concurrently edited HR files; formatting-only repairs were followed by the remaining checks. Active server-backed leave routes are included in this build. The existing nonfatal large-bundle warning remains.

Reproduction: set `COSMIX_PAYROLL_VERIFICATION_CONNECTION` to the disposable LocalDB clone, then run `dotnet test tests/Cosmix.Tests/Cosmix.Tests.csproj -c Release --filter 'FullyQualifiedName!~AcceptanceExecutionSuite'`. SQL tests explicitly reject a business database. Omit that environment variable only when intentionally excluding SQL acceptance; those tests then report skipped, which is not equivalent to the final run above. The original execution used `--artifacts-path D:/CosmixPayrollVerification20261028/build`, single-worker compilation and `UseSharedCompilation=false`.

## Acceptance boundaries

Passing code, HTTP contracts and SQL tests do not constitute a real employee payroll-month acceptance. These checks use synthetic sources/transport/native ports where stated. The broad test command deliberately excludes the pre-existing `AcceptanceExecutionSuite`, whose hard-coded business database and write behavior are inappropriate for this isolated verification.

An authorized company user must configure/approve actual compensation, salary policy, statutory coverage and Finance mappings, then compare a real historical month and run the first live month in parallel. No real bank transfer, tax/legal-rate certification, SMS provider verification, physical-phone/face acceptance or native mobile rebuild was performed in this continuation. Earlier tasks 1–11 have their own evidence and outstanding operational acceptance; this report does not relabel them all complete.
