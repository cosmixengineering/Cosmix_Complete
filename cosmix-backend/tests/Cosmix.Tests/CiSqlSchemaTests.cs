using Cosmix.Application.Authentication;
using Cosmix.Domain.Authentication;
using Cosmix.Domain.HR.PayrollAccounting;
using Cosmix.Domain.HR.PayrollHandoff;
using Cosmix.Infrastructure.Authentication.Access;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

public sealed class CiSqlFactAttribute : FactAttribute
{
    public CiSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COSMIX_CI_SQL_CONNECTION")))
            Skip = "Requires an explicitly selected empty disposable CI SQL database.";
    }
}

[Trait("Category", "SqlIntegration")]
public sealed class CiSqlSchemaTests
{
    [CiSqlFact]
    public async Task Empty_database_upgrades_and_enforces_posting_and_authority_boundaries()
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_CI_SQL_CONNECTION")!;
        var parsed = new SqlConnectionStringBuilder(connection);
        if (!parsed.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            !System.Text.RegularExpressions.Regex.IsMatch(parsed.InitialCatalog, "^Cosmix_Payroll_CI_[A-Za-z0-9_]{1,70}$") || !parsed.IntegratedSecurity)
            throw new InvalidOperationException("CI schema checks require a named disposable LocalDB database.");
        await using var db = new CosmixDbContext(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options);
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow; var actor = new AuthenticatedAccount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, SessionClient.Web, true);
        var authority = new AccessManagementRepository(db, new() { MaximumOtpAttempts = 5 });
        var method = authority.GetType().GetMethod("CurrentAdministratorQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var query = (IQueryable<Guid>)method.Invoke(authority, [actor, now, DateOnly.FromDateTime(now.UtcDateTime)])!;
        Assert.False(await query.AnyAsync());
        var bank = "CI-BANK-" + Guid.NewGuid().ToString("N"); var month = new DateOnly(2099, 1, 1);
        var batch = new PayrollHandoffBatch("CI-" + Guid.NewGuid().ToString("N"), month, month.AddMonths(1), "PKR", "synthetic-ci", now);
        var obligation = new PayrollObligation(batch.Id, 100m, 100m, "PKR", "synthetic-ci", now);
        var statement = new Cosmix.Domain.Finance.BankStatementSnapshot(Guid.NewGuid(), bank, "PKR", month, 1, new('A', 64), "synthetic-ci-source", "maker", Guid.NewGuid(), now);
        var proposal = new Cosmix.Domain.Finance.BankReconciliationProposal(Guid.NewGuid(), statement.Id, 1, "synthetic-ci-proposal", "maker", statement.CreatedByEmployeeId, now);
        var closed = new Cosmix.Domain.Finance.BankReconciliationClose(Guid.NewGuid(), statement, proposal, "synthetic-ci-close", "reviewer", Guid.NewGuid(), now);
        db.AddRange(batch, obligation, statement, proposal, closed); await db.SaveChangesAsync();
        var entry = new PayrollJournalEntry(obligation.Id, JournalEntryKind.SalaryPayment, "CI-PAYABLE", bank, 100m, "PKR", "Synthetic CI payment", "synthetic-ci", now);
        entry.BindPosting(month, Guid.NewGuid(), null, "CI-PAYMENT-" + Guid.NewGuid().ToString("N")); db.Add(entry);
        var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(51016, Assert.IsType<SqlException>(rejected.InnerException).Number);
        await transaction.RollbackAsync();
    }
}
