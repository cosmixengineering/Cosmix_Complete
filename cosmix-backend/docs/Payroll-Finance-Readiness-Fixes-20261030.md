# Payroll configuration and Finance verification — 30 October 2026

The company setup report is advisory. It does not approve company policies, approve a payroll batch, publish salary data, authorize bank payments or replace historical-month acceptance.

## Corrections

- The readiness HTTP endpoint now uses `attendance.payroll-salary.read` through the same audited, current All-scope web/MFA operation as salary administration. Anonymous sessions, ordinary signed-in accounts without permission, mobile sessions and sessions without MFA cannot read the company employee configuration report. It uses the guarded read transaction instead of taking the exclusive authentication writer lock.
- Base salary coverage comes from actual `CompensationRecords`, with the existing compensation history/approval integrity checks. Fixed earning allowances no longer count as a base salary. Every employed day in the selected month must be covered; mid-month joining, monthly/daily/hourly wages and salary revisions are supported. Expired successors never reactivate an older open-ended salary.
- Shift coverage resolves the actual dated site/office roster and staff taxonomy for every employed day, including rest days. Historical individual shift assignments are no longer treated as an override of the effective roster. Missing configuration is reported as not ready; integrity and authorization failures propagate.
- An approved salary calculation policy must be effective for the selected month. The company must approve its own proration, rounding and earnings rules; the report does not insert guessed rules.
- Posting profiles count as ready only if they cover the **whole month in PKR**, retain independent approval and pass the **same account validation used by actual Finance posting**. Disabled/missing/wrong-kind salary, deduction or statutory accounts cannot be replaced by an unrelated active account. A profile expiring mid-month does not make the month ready.
- Bank coverage only counts verified, effective bank records whose revision has not already been superseded.
- Trial-balance `IncludedSources` now comes from the actual selected journals, including an empty list for an empty ledger. It no longer unconditionally declares Payroll as the only included source.

## Existing Finance coverage and its boundary

The shared `finance.GeneralLedgerEntries` SQL view currently projects the committed payroll journals, including payroll-origin payment/contribution/reversal postings. The trial balance checks journal pairs, posting dates, duplicates, currency separation and balancing. Payroll costing has independently approved plans, source fingerprints, project/site validation, missing-allocation reporting and exact cent-preserving allocation.

No separate sales/purchasing/general Finance posting source currently feeds that view. This change does not invent AP/AR transactions or claim that all future company Finance modules have been integrated. Actual company account mappings and allocation plans remain company configuration.

The company readiness employee population is currently active, approved employees who joined before the selected month ends. Historical leavers, finalized attendance, payable salary calculation and operational/company acceptance are validated in their dedicated month-close/payroll workflows. A ready configuration report is not a company sign-off.

## Evidence

- Two regression tests first failed on the actual defects: an authenticated caller without salary permission reached the readiness repository; the trial balance hardcoded the source list.
- Follow-up review added six failing regressions for posting-profile/account readiness, followed by **74/74 passing focused tests** (`posting-red.trx`, then `posting-green.trx`). The shared applicability predicate and existing Finance rule validator now govern both posting and configuration readiness.
- **47/47 focused checks passed**, including salary/readiness, Finance ledger, project/site cost allocation and HTTP permission/audit checks. They ran from isolated build output under `D:/CosmixReleases/finance-readiness-work`; the final result is `verified-fresh.trx`.
- **1/1 SQL readiness query check passed** against the existing authorized local `Cosmix_Testing` database without business writes (`readiness-sql-final.trx`). The SQL query test supplies a test-only unconfigured roster; canonical roster behavior is covered separately by the focused coverage tests and existing attendance tests.
- Exact result files are under `D:/CosmixReleases/finance-readiness-evidence`. `red.trx` retains the initial failing evidence; the final passing runs are separate files.

## Fresh SQL Finance integration

`PayrollFinanceSqlIntegrationTests.Fresh_month_postings_reconcile_through_shared_SQL_ledger_and_approved_project_site_costs` passed **1/1** in a newly generated, isolated LocalDB database. It applied all real migrations, imported the synthetic salary source through the production import services, ran the salary approval/handoff and Finance posting services, and queried the actual shared SQL ledger through the production repositories.

- Four persisted salary/contribution journals produced eight shared ledger legs. Debits and credits were both PKR 34,500: salary expense 33,000 plus employer contribution expense 1,500; net payable 32,200, other deductions 500 and statutory payable 1,800.
- Ledger pagination and account filtering returned the expected persisted entries. The following month's opening balances retained the prior balances with no new movements.
- The unallocated batch initially appeared in the cost report. A protected plan allocated 60% to a linked project/site and 40% to general costs. Idempotent replay returned the same plan; self-approval was rejected; an independent account/employee approved it. Reloading the persisted plan and protected proof returned project/site cost 20,700 plus general cost 13,800, matching the 34,500 expense total without additional journals.
- The test made no payments and checked that salary payment records remained empty. Authentication authority and upstream employee/attendance facts are explicitly synthetic fixture boundaries. This verifies implemented payroll Finance integration, not unrelated AP/AR modules, company approvals or real bank acceptance.
- The unique database was dropped in cleanup. Final evidence is `finance-sql-integration-r4.trx` and `integration/finance-sql-5a5ecea53ef74aacb955df537afd270f/finance-ledger-cost-result.json` under `D:/CosmixReleases/finance-readiness-evidence`; the adjacent `owned-database-cleanup.json` records successful deletion. Earlier attempts retained separate failures from test file-path/ACL provisioning; production private-file permission validation remained enabled.

To repeat on Windows, set `COSMIX_FINANCE_SQL_CONNECTION` to an integrated-security **LocalDB master** connection and optionally set `COSMIX_FINANCE_SQL_EVIDENCE` to an evidence directory. Run the test with filter `FullyQualifiedName~PayrollFinanceSqlIntegrationTests`. It creates its own unique `Cosmix_Payroll_FinanceSql_<guid>` database and drops only that generated database. Without the explicit connection, the integration test is skipped. Evidence and synthetic private files remain outside source control.

No business-database migration, company policy approval, actual salary publication or bank submission was performed by these checks.
