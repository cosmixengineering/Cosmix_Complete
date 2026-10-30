using Cosmix.Infrastructure.Database;
using Cosmix.Infrastructure.Repositories.HR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace Cosmix.Tests;

[Trait("Category", "SqlIntegration")]
public sealed class PayrollPaymentConcurrencySqlTests
{
    [PayrollSqlFact]
    public async Task Different_employees_share_obligation_payment_lock_until_transaction_finishes()
    {
        var connection = Environment.GetEnvironmentVariable("COSMIX_PAYROLL_VERIFICATION_CONNECTION")!;
        var target = new SqlConnectionStringBuilder(connection);
        if ((target.DataSource != "(localdb)\\CosmixUat_d893653346" && !target.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase)) || target.InitialCatalog != "Cosmix_Payroll_20261028" || !target.IntegratedSecurity)
            throw new InvalidOperationException("Only the disposable payroll verification database is allowed.");
        var options = new DbContextOptionsBuilder<CosmixDbContext>().UseSqlServer(connection).Options;
        await using var first = new CosmixDbContext(options);
        await using var second = new CosmixDbContext(options);
        await using var one = await first.Database.BeginTransactionAsync();
        await using var two = await second.Database.BeginTransactionAsync();
        var obligation = Guid.NewGuid();
        await new PayrollAccountingRepository(first).AcquirePaymentLockAsync(obligation, Guid.NewGuid(), default);
        await using var sessionCommand = second.Database.GetDbConnection().CreateCommand();
        sessionCommand.Transaction = two.GetDbTransaction();
        sessionCommand.CommandText = "SELECT @@SPID";
        var session = Convert.ToInt32(await sessionCommand.ExecuteScalarAsync());
        var pending = new PayrollAccountingRepository(second).AcquirePaymentLockAsync(obligation, Guid.NewGuid(), default);
        await using var observer = new SqlConnection(connection);
        await observer.OpenAsync();
        await using var command = observer.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.dm_tran_locks WHERE request_session_id=@session AND resource_type='APPLICATION' AND request_status='WAIT'";
        command.Parameters.AddWithValue("@session", session);
        var waiting = false;
        try
        {
            for (var attempt = 0; attempt < 40 && !waiting; attempt++)
            {
                waiting = Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
                if (!waiting) await Task.Delay(25);
            }
            Assert.True(waiting, "A second employee's payment must wait for the same obligation lock.");
            Assert.False(pending.IsCompleted);
        }
        finally { await one.RollbackAsync(); }
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        await two.RollbackAsync();
    }
}
