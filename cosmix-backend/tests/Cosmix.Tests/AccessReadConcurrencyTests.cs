using Cosmix.Application.Authentication;
using Cosmix.Application.Authentication.Access;
using Cosmix.Infrastructure.Authentication.Access;
using Cosmix.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cosmix.Tests;

[CollectionDefinition("Access SQL concurrency", DisableParallelization = true)]
public sealed class AccessSqlConcurrencyCollection;

// These tests intentionally hold database-wide authorization locks. Running unrelated
// SQL writers beside them tests fixture contention instead of the promised lock behavior.
[Collection("Access SQL concurrency")]
[Trait("Category", "SqlIntegration")]
public sealed class AccessReadConcurrencyTests
{
    [PayrollSqlFact]
    public async Task Audit_range_reports_serialize_while_ordinary_readers_can_enter()
    {
        await using var first = Context();
        await using var second = Context();
        await using var ordinary = Context();
        IAccessManagementRepository Repository(CosmixDbContext db) => new AccessManagementRepository(db, new());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var employee = await first.Employees.Select(x => x.Id).FirstAsync(deadline.Token);
        async Task<bool> Report(CosmixDbContext db, bool hold)
        {
            await new Cosmix.Infrastructure.Repositories.Attendance.WorkTreatmentRepository(db, null!, null!, null!)
                .FactsAsync(employee, "synthetic.audit-range", deadline.Token);
            if (hold) { entered.SetResult(); await release.Task.WaitAsync(deadline.Token); }
            else secondEntered.SetResult();
            db.Add(new Cosmix.Domain.Common.Audit.AuditEvent("synthetic.audit-range", "synthetic", "synthetic",
                "Concurrency", "test", DateTimeOffset.UtcNow, Guid.NewGuid().ToString("N"), employee));
            await db.SaveChangesAsync(deadline.Token);
            // Exercise the real audit save, retaining no test audit in this database.
            throw new InvalidOperationException("rollback synthetic audit");
        }
        var held = Assert.ThrowsAsync<InvalidOperationException>(() => Repository(first)
            .InReadTransactionAsync(_ => Report(first, true), deadline.Token, serializeReaders: true));
        await entered.Task.WaitAsync(deadline.Token);
        try
        {
            Assert.True(await Repository(ordinary).InReadTransactionAsync(_ => Task.FromResult(true), deadline.Token));
            var queued = Assert.ThrowsAsync<InvalidOperationException>(() => Repository(second)
                .InReadTransactionAsync(_ => Report(second, false), deadline.Token, serializeReaders: true));
            await Task.Delay(200, deadline.Token);
            Assert.False(secondEntered.Task.IsCompleted);
            release.TrySetResult();
            Assert.Equal("rollback synthetic audit", (await held).Message);
            Assert.Equal("rollback synthetic audit", (await queued).Message);
            Assert.True(secondEntered.Task.IsCompleted);
        }
        finally { release.TrySetResult(); await held; }
    }

    [PayrollSqlFact]
    public async Task Shared_read_rejects_unsaved_business_changes_before_commit()
    {
        await using var db = Context();
        IAccessManagementRepository repository = new AccessManagementRepository(db, new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InReadTransactionAsync(async ct =>
        {
            var employee = await db.Employees.FirstAsync(ct);
            db.Entry(employee).Property("IsArchived").CurrentValue = !employee.IsArchived;
            return true;
        }, default));
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [PayrollSqlFact]
    public async Task Shared_read_rejects_business_save_and_releases_transaction_for_the_next_writer()
    {
        await using var db = Context();
        IAccessManagementRepository repository = new AccessManagementRepository(db, new MobileAuthenticationOptions());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InReadTransactionAsync(async ct =>
        {
            var employee = await db.Employees.FirstAsync(ct);
            db.Entry(employee).Property("IsArchived").CurrentValue = !employee.IsArchived;
            await db.SaveChangesAsync(ct);
            return true;
        }, default));
        Assert.Contains("cannot change business", error.Message);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.True(await repository.InTransactionAsync(_ => Task.FromResult(true), default));
    }

    [PayrollSqlFact]
    public async Task Shared_read_audit_rolls_back_when_the_operation_fails()
    {
        await using var db = Context();
        var audit = new Cosmix.Domain.Common.Audit.AuditEvent("test.shared-read", "synthetic", "synthetic",
            "Concurrency", "test", DateTimeOffset.UtcNow, Guid.NewGuid().ToString("N"));
        IAccessManagementRepository repository = new AccessManagementRepository(db, new MobileAuthenticationOptions());
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.InReadTransactionAsync<bool>(async ct =>
        {
            db.Add(audit);
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("synthetic failure after audit save");
        }, default));
        Assert.False(await db.AuditEvents.AnyAsync(x => x.Id == audit.Id));
    }

    private static CosmixDbContext Context()
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_PAYROLL_VERIFICATION_CONNECTION")!;
        var parsed = new SqlConnectionStringBuilder(connection);
        if (!parsed.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase) ||
            !parsed.InitialCatalog.StartsWith("Cosmix_Payroll_", StringComparison.Ordinal) || !parsed.IntegratedSecurity)
            throw new InvalidOperationException("Disposable SQL database required.");
        return new(new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options);
    }

    [PayrollSqlFact]
    public async Task Audited_reads_overlap_but_revocation_writer_waits_until_they_finish()
    {
        await using var first = Context();
        await using var second = Context();
        await using var writer = Context();
        IAccessManagementRepository Read(CosmixDbContext db) => new AccessManagementRepository(db, new MobileAuthenticationOptions());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var held = Read(first).InReadTransactionAsync(async ct =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(ct);
            return true;
        }, timeout.Token);
        await entered.Task.WaitAsync(timeout.Token);
        try
        {
            using var readDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            Assert.True(await Read(second).InReadTransactionAsync(_ => Task.FromResult(true), readDeadline.Token));
            var writeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var write = Read(writer).InTransactionAsync(_ => { writeEntered.SetResult(); return Task.FromResult(true); }, timeout.Token);
            await Task.Delay(200, timeout.Token);
            Assert.False(writeEntered.Task.IsCompleted);
            release.TrySetResult();
            Assert.True(await held);
            Assert.True(await write);
        }
        finally { release.TrySetResult(); await held; }
    }
}
