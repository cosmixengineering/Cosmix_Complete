using System.Text;
using System.Text.Json;
using Cosmix.Application.Authentication;
using Cosmix.Application.Common.Security;
using Cosmix.Application.Finance.Costing;
using Cosmix.Application.Finance.Ledger;
using Cosmix.Application.HR.PayrollAccounting;
using Cosmix.Application.HR.PayrollSalary;
using Cosmix.Domain.Attendance.Sites;
using Cosmix.Domain.Authentication.Scopes;
using Cosmix.Domain.Common.Approvals;
using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Finance;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

public sealed class FinanceSqlIntegrationFactAttribute : FactAttribute
{
    public FinanceSqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMIX_FINANCE_SQL_CONNECTION")))
            Skip = "Select a LocalDB master connection; this test creates and deletes only its own unique synthetic database.";
    }
}

/// <summary>Real SQL/repositories/protected files; synthetic upstream employee, attendance and session authority.
/// Never reads a business database or makes a real payment/company approval.</summary>
[Trait("Category", "SqlIntegration")]
public sealed class PayrollFinanceSqlIntegrationTests
{
    [FinanceSqlIntegrationFact]
    public async Task Fresh_month_postings_reconcile_through_shared_SQL_ledger_and_approved_project_site_costs()
    {
        var selected = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COSMIX_FINANCE_SQL_CONNECTION")!);
        Assert.StartsWith("(localdb)\\", selected.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("master", selected.InitialCatalog, ignoreCase: true);
        Assert.True(selected.IntegratedSecurity);
        Assert.Empty(selected.AttachDBFilename);
        var run = Guid.NewGuid().ToString("N");
        var database = "Cosmix_Payroll_FinanceSql_" + run;
        var evidence = Path.GetFullPath(Path.Combine(Environment.GetEnvironmentVariable("COSMIX_FINANCE_SQL_EVIDENCE") ?? Path.GetTempPath(), "finance-sql-" + run));
        Directory.CreateDirectory(evidence);
        var target = new SqlConnectionStringBuilder(selected.ConnectionString) { InitialCatalog = database, Pooling = false };
        var created = false;
        try
        {
            await using (var master = new SqlConnection(selected.ConnectionString))
            {
                await master.OpenAsync();
                var dataFile = Path.Combine(evidence, "scenario.mdf").Replace("'", "''", StringComparison.Ordinal);
                var logFile = Path.Combine(evidence, "scenario_log.ldf").Replace("'", "''", StringComparison.Ordinal);
                await using var command = new SqlCommand($"CREATE DATABASE [{database}] ON PRIMARY (NAME=N'{database}', FILENAME=N'{dataFile}') LOG ON (NAME=N'{database}_log', FILENAME=N'{logFile}')", master) { CommandTimeout = 120 };
                await command.ExecuteNonQueryAsync(); created = true;
            }
            await using (var schema = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>()
                .UseSqlServer(target.ConnectionString, sql => sql.CommandTimeout(180)).Options))
            {
                await schema.Database.MigrateAsync();
                Assert.Empty(await schema.Database.GetPendingMigrationsAsync());
            }
            await File.WriteAllTextAsync(Path.Combine(evidence, "source-import.csv"), """
                rowType,employeeNumber,effectiveFrom,currency,baseAmount,grossAmount,wageBasis,paymentMethod,componentCode,componentKind,componentAmount,effectiveToExclusive,prorateWithBase
                Compensation,1,2026-09-01,PKR,30000,30000,Monthly,Bank,,,,,
                Component,1,2026-09-01,PKR,,,,,TRANSPORT,FixedEarning,3000,,true
                Component,1,2026-09-01,PKR,,,,,RECOVERY,FixedDeduction,500,,false
                """, new UTF8Encoding(false));

            await using var f = new PayrollMonthAcceptanceFixture(target.ConnectionString, evidence);
            await f.SeedEmployeeAsync();
            await f.ImportAndApproveAsync();
            var handoff = await f.RetainSyntheticAttendanceAsync();
            var finance = await PayrollMonthFinanceAcceptance.ConfigureFinanceAsync(f);
            var context = PayrollMonthAcceptanceFixture.Context;
            var preview = await handoff.PreviewAsync(2026, 9, context, default);
            Assert.True(preview.SalaryComplete);
            var batch = await handoff.PrepareAsync(new(2026, 9, preview.PreviewFingerprint, "Synthetic SQL integration"), Key(), context, default);
            f.ActorId = Guid.NewGuid();
            batch = await handoff.HrReviewAsync(batch.Batch.Id, new(batch.Batch.Version, ApprovalState.Approved, "Synthetic independent HR review"), Key(), context, default);
            f.ActorId = Guid.NewGuid();
            batch = await handoff.LockAsync(batch.Batch.Id, new(batch.Batch.Version, "Synthetic independent management lock"), Key(), context, default);
            f.ActorId = Guid.NewGuid();
            await handoff.ProcessingAsync(batch.Batch.Id, new("SYNTHETIC-SQL-FINANCE", "Synthetic handoff only"), Key(), context, default);
            var posted = await finance.FinalizeAndReconcileAsync(f, batch.Batch.Id, closePeriod: false);
            Assert.Equal(4, posted.JournalCount);

            f.AddPermissions([FinanceLedgerService.ReadPermission, PayrollCostService.Read, PayrollCostService.Manage, PayrollCostService.Approve]);
            var sensitive = new SensitiveComplianceOperation(f.Current, f.Access, f.Authorization, f.Journal, f.Clock);
            var writes = new PayrollFinanceOperation(sensitive, f.Journal, f.Files, f.Clock);
            var ledgerRepository = new FinanceLedgerRepository(f.Db);
            var ledger = new FinanceLedgerService(sensitive, ledgerRepository);
            var month = PayrollMonthAcceptanceFixture.Month;
            var balance = await ledger.TrialBalanceAsync(month, month.AddMonths(1), context, default);
            Assert.Equal(34500m, balance.Accounts.Sum(x => x.Debit));
            Assert.Equal(34500m, balance.Accounts.Sum(x => x.Credit));
            Assert.Equal(0m, balance.Accounts.Sum(x => x.ClosingBalance));
            Assert.Equal(-32200m, Assert.Single(balance.Accounts, x => x.AccountCode == "TEST-PAY").ClosingBalance);
            Assert.Equal(-1800m, Assert.Single(balance.Accounts, x => x.AccountCode == "TEST-STAT").ClosingBalance);
            Assert.Equal(new[] { "Payroll" }, balance.IncludedSources);
            var first = await ledger.LedgerAsync(month, month.AddMonths(1), null, "PKR", 1, 3, context, default);
            var second = await ledger.LedgerAsync(month, month.AddMonths(1), null, "PKR", 2, 3, context, default);
            var third = await ledger.LedgerAsync(month, month.AddMonths(1), null, "PKR", 3, 3, context, default);
            Assert.True(first.HasMore); Assert.True(second.HasMore); Assert.False(third.HasMore);
            var legs = first.Items.Concat(second.Items).Concat(third.Items).ToArray();
            Assert.Equal(8, legs.Length);
            Assert.Equal(8, legs.Select(x => (x.JournalId, x.AccountCode)).Distinct().Count());
            Assert.Equal(4, legs.Select(x => x.JournalId).Distinct().Count());
            Assert.All(legs, x => Assert.Equal(posted.ObligationId, x.ObligationId));
            Assert.Equal(34500m, legs.Sum(x => x.Debit)); Assert.Equal(34500m, legs.Sum(x => x.Credit));
            var payable = await ledger.LedgerAsync(month, month.AddMonths(1), "TEST-PAY", "PKR", 1, 20, context, default);
            Assert.Equal(32200m, Assert.Single(payable.Items).Credit);
            var nextMonth = await ledger.TrialBalanceAsync(month.AddMonths(1), month.AddMonths(2), context, default);
            Assert.All(nextMonth.Accounts, x => { Assert.Equal(0m, x.Debit); Assert.Equal(0m, x.Credit); Assert.Equal(x.OpeningBalance, x.ClosingBalance); });

            var project = new Project("SQL-PROJECT", "Synthetic HVAC Project");
            var site = new Site("SQL-SITE", "Synthetic Project Site", projectId: project.Id);
            f.Db.AddRange(project, site); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
            var handoffRepository = new PayrollHandoffRepository(f.Db);
            var accountingRepository = new PayrollAccountingRepository(f.Db);
            var financeRepository = new PayrollFinanceRepository(f.Db);
            var sourceReader = new PayrollCostSourceReader(new PayrollFrozenBatchReader(handoffRepository, f.Files), accountingRepository, financeRepository, handoffRepository);
            var costRepository = new PayrollCostRepository(f.Db);
            var costs = new PayrollCostService(sensitive, writes, costRepository, sourceReader,
                new PayrollFinanceSetupService(financeRepository), f.Files, f.Clock);
            var source = await sourceReader.ReadAsync(batch.Batch.Id, default);
            Assert.Equal(34500m, Assert.Single(source.Employees).Amount);
            var unallocated = await costs.MonthReportAsync(2026, 9, context, default);
            Assert.Equal(batch.Batch.Id, Assert.Single(unallocated.UnallocatedBatchIds));
            var request = new PayrollCostRequest(source.Fingerprint, null,
                [new(f.EmployeeId, project.Id, site.Id, 6000), new(f.EmployeeId, null, null, 4000)], "Synthetic project/site expense allocation");
            var key = Key();
            var plan = await costs.CreateAsync(batch.Batch.Id, request, key, context, default);
            Assert.Equal(plan.Id, (await costs.CreateAsync(batch.Batch.Id, request, key, context, default)).Id);
            var self = await Assert.ThrowsAsync<AuthenticationFailure>(() => costs.DecideAsync(plan.Id,
                new(plan.Version, true, "Synthetic prohibited self review"), Key(), context, default));
            Assert.Equal(403, self.StatusCode);
            f.ActorId = Guid.NewGuid(); f.ActorEmployeeId = Guid.NewGuid();
            plan = await costs.DecideAsync(plan.Id, new(plan.Version, true, "Synthetic independent cost review"), Key(), context, default);
            Assert.Equal("Approved", plan.State);
            f.Db.ChangeTracker.Clear();
            var persisted = Assert.Single(await costRepository.PlansAsync(batch.Batch.Id, default));
            Assert.Equal("Approved", persisted.State);
            Assert.NotEqual(persisted.CreatedBy, persisted.DecidedBy);
            Assert.NotEqual(persisted.CreatedByEmployeeId, persisted.DecidedByEmployeeId);
            var workspace = await costs.WorkspaceAsync(batch.Batch.Id, context, default);
            Assert.Equal(plan.Id, workspace.EffectivePlanId);
            Assert.Equal(34500m, Assert.Single(workspace.Plans).Costs.Sum(x => x.Amount));
            var report = await costs.MonthReportAsync(2026, 9, context, default);
            Assert.Empty(report.UnallocatedBatchIds); Assert.Equal(1, report.ApprovedBatchCount);
            Assert.Equal(20700m, Assert.Single(report.Rows, x => x.ProjectId == project.Id && x.SiteId == site.Id).Amount);
            Assert.Equal(13800m, Assert.Single(report.Rows, x => x.ProjectId == null && x.SiteId == null).Amount);
            Assert.Equal(balance.Accounts.Where(x => x.AccountCode is "TEST-SAL" or "TEST-ER").Sum(x => x.Debit), report.Rows.Sum(x => x.Amount));
            Assert.Equal(4, await f.Db.PayrollJournalEntries.CountAsync());
            Assert.Empty(await f.Db.SalaryPayments.AsNoTracking().ToArrayAsync());

            await f.SaveScenarioEvidenceAsync("finance-ledger-cost-result.json", new
            {
                Scenario = "Fresh isolated synthetic SQL integration; not company acceptance", Database = database,
                PersistedJournals = posted.JournalCount, SharedViewLegs = legs.Length, TrialBalance = balance,
                ProjectSiteCosts = report, PersistedPlanId = plan.Id, NoBankPayments = true,
                Boundary = "Real service/repository/SQL/protected-file integration; test-only identity, upstream hiring and attendance fixtures."
            });
            Console.WriteLine("Finance SQL integration evidence: " + evidence);
        }
        finally
        {
            if (created)
            {
                await using var master = new SqlConnection(selected.ConnectionString);
                await master.OpenAsync();
                await using var drop = new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];", master) { CommandTimeout = 120 };
                await drop.ExecuteNonQueryAsync();
                await File.WriteAllTextAsync(Path.Combine(evidence, "owned-database-cleanup.json"), JsonSerializer.Serialize(new { Database = database, Dropped = true }));
            }
        }
    }
    private static string Key() => Guid.NewGuid().ToString("N");
}
